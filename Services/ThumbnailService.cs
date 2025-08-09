using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace Mangaanya.Services
{
    public class ThumbnailService : IThumbnailService
    {
        private readonly ILogger<ThumbnailService> _logger;
        private readonly IConfigurationManager _config;
        private readonly IMangaRepository _repository;
        private readonly string _thumbnailDirectory;
        private readonly string _defaultThumbnailPath;

        public ThumbnailService(
            ILogger<ThumbnailService> logger,
            IConfigurationManager config,
            IMangaRepository repository)
        {
            _logger = logger;
            _config = config;
            _repository = repository;

            // サムネイル保存ディレクトリを設定（アプリフォルダ優先、権限エラー時はAPPDATAにフォールバック）
            _thumbnailDirectory = GetThumbnailDirectory();
            _defaultThumbnailPath = Path.Combine(_thumbnailDirectory, "default_thumbnail.jpg");
            CreateDefaultThumbnail();
        }

        public async Task<ThumbnailGenerationResult> GenerateThumbnailAsync(MangaFile mangaFile)
        {
            try
            {
                if (mangaFile == null || string.IsNullOrEmpty(mangaFile.FilePath))
                {
                    return new ThumbnailGenerationResult
                    {
                        Success = false,
                        ErrorMessage = "ファイル情報が無効です",
                        MangaFile = mangaFile
                    };
                }

                if (!File.Exists(mangaFile.FilePath))
                {
                    return new ThumbnailGenerationResult
                    {
                        Success = false,
                        ErrorMessage = "ファイルが存在しません",
                        MangaFile = mangaFile
                    };
                }

                _logger.LogInformation("サムネイル生成開始: {FilePath}", mangaFile.FilePath);

                // IDが無効な場合はエラー
                if (mangaFile.Id <= 0)
                {
                    _logger.LogWarning("無効なIDを持つファイルのため、サムネイルを生成できません: {FilePath}", mangaFile.FilePath);
                    return new ThumbnailGenerationResult
                    {
                        Success = false,
                        ErrorMessage = "データベースに未登録のファイルです。",
                        MangaFile = mangaFile
                    };
                }

                // サムネイルファイル名を生成（IDを使用）
                var thumbnailFileName = $"{mangaFile.Id}.jpg";
                var thumbnailPath = Path.Combine(_thumbnailDirectory, thumbnailFileName);

                // 既にサムネイルが存在する場合はそれを返す
                if (File.Exists(thumbnailPath))
                {
                    // パスがDBと異なっていれば更新する
                    if (mangaFile.ThumbnailPath != thumbnailPath)
                    {
                        mangaFile.ThumbnailPath = thumbnailPath;
                        mangaFile.ThumbnailCreated = File.GetLastWriteTime(thumbnailPath);
                        await _repository.UpdateAsync(mangaFile).ConfigureAwait(false);
                    }

                    return new ThumbnailGenerationResult
                    {
                        Success = true,
                        ThumbnailPath = thumbnailPath,
                        MangaFile = mangaFile
                    };
                }

                // アーカイブファイルから最初の画像を抽出してサムネイル生成
                var success = await ExtractAndCreateThumbnailAsync(mangaFile.FilePath, thumbnailPath).ConfigureAwait(false);

                if (success)
                {
                    // データベースを更新
                    mangaFile.ThumbnailPath = thumbnailPath;
                    mangaFile.ThumbnailCreated = DateTime.Now;
                    await _repository.UpdateAsync(mangaFile).ConfigureAwait(false);

                    _logger.LogInformation("サムネイル生成成功: {FilePath} -> {ThumbnailPath}", mangaFile.FilePath, thumbnailPath);
                    return new ThumbnailGenerationResult
                    {
                        Success = true,
                        ThumbnailPath = thumbnailPath,
                        MangaFile = mangaFile
                    };
                }
                else
                {
                    _logger.LogWarning("サムネイル生成失敗: {FilePath}", mangaFile.FilePath);
                    return new ThumbnailGenerationResult
                    {
                        Success = false,
                        ErrorMessage = "サムネイル生成に失敗しました",
                        MangaFile = mangaFile
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル生成中にエラーが発生しました: {FilePath}", mangaFile?.FilePath);
                return new ThumbnailGenerationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    MangaFile = mangaFile
                };
            }
        }

        public async Task<List<ThumbnailGenerationResult>> GenerateThumbnailsBatchAsync(
            IEnumerable<MangaFile> mangaFiles,
            IProgress<ThumbnailProgress>? progress = null,
            CancellationToken cancellationToken = default,
            bool skipExisting = true)
        {
            var results = new List<ThumbnailGenerationResult>();
            var fileList = mangaFiles.ToList();
            var totalFiles = fileList.Count;
            var processedCount = 0;
            var skippedCount = 0;

            for (int i = 0; i < totalFiles; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var file = fileList[i];

                // 既存のサムネイルをスキップするかチェック
                if (skipExisting && ThumbnailExists(file.ThumbnailPath))
                {
                    skippedCount++;


                    // スキップした場合も成功として扱う
                    results.Add(new ThumbnailGenerationResult
                    {
                        Success = true,
                        ThumbnailPath = file.ThumbnailPath,
                        MangaFile = file
                    });

                    // 進捗報告（スキップ）
                    progress?.Report(new ThumbnailProgress
                    {
                        CurrentFile = i + 1,
                        TotalFiles = totalFiles,
                        CurrentFileName = file.FileName,
                        Status = "スキップ"
                    });

                    continue;
                }

                processedCount++;

                // 進捗報告
                progress?.Report(new ThumbnailProgress
                {
                    CurrentFile = i + 1,
                    TotalFiles = totalFiles,
                    CurrentFileName = file.FileName,
                    Status = "サムネイル生成中"
                });

                var result = await GenerateThumbnailAsync(file);
                results.Add(result);

                // UIスレッドに制御を戻してスピナーの応答性を保つ
                await Task.Yield();
            }

            _logger.LogInformation("サムネイル一括生成完了: 処理={Processed}件, スキップ={Skipped}件", processedCount, skippedCount);
            return results;
        }

        public bool ThumbnailExists(string? thumbnailPath)
        {
            return !string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath);
        }

        public async Task<bool> DeleteThumbnailAsync(string? thumbnailPath)
        {
            try
            {
                if (string.IsNullOrEmpty(thumbnailPath) || !File.Exists(thumbnailPath))
                    return false;

                await Task.Run(() => File.Delete(thumbnailPath));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル削除中にエラーが発生しました: {ThumbnailPath}", thumbnailPath);
                return false;
            }
        }

        public string GetDefaultThumbnailPath()
        {
            return _defaultThumbnailPath;
        }

        public async Task<BitmapImage> GetThumbnailImageAsync(MangaFile mangaFile)
        {
            try
            {
                // 既存のサムネイルがあるかチェック
                if (!string.IsNullOrEmpty(mangaFile.ThumbnailPath) && File.Exists(mangaFile.ThumbnailPath))
                {
                    return LoadBitmapImageFromFile(mangaFile.ThumbnailPath);
                }

                // サムネイル生成
                var result = await GenerateThumbnailAsync(mangaFile);
                if (result.Success && !string.IsNullOrEmpty(result.ThumbnailPath))
                {
                    return LoadBitmapImageFromFile(result.ThumbnailPath);
                }

                // 失敗時はデフォルト画像
                return CreateDefaultBitmapImage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル画像取得中にエラー: {FilePath}", mangaFile.FilePath);
                return CreateDefaultBitmapImage();
            }
        }

        private BitmapImage LoadBitmapImageFromFile(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 120;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private BitmapImage CreateDefaultBitmapImage()
        {
            try
            {
                // デフォルトサムネイルファイルから読み込み
                if (File.Exists(_defaultThumbnailPath))
                {
                    return LoadBitmapImageFromFile(_defaultThumbnailPath);
                }

                // ファイルが存在しない場合は空のBitmapImageを作成
                var emptyBitmap = new BitmapImage();
                emptyBitmap.BeginInit();
                emptyBitmap.EndInit();
                emptyBitmap.Freeze();
                return emptyBitmap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "デフォルトサムネイル画像作成中にエラーが発生しました");

                // 最終的なフォールバック
                var fallbackBitmap = new BitmapImage();
                fallbackBitmap.BeginInit();
                fallbackBitmap.EndInit();
                fallbackBitmap.Freeze();
                return fallbackBitmap;
            }
        }

        private async Task<bool> ExtractAndCreateThumbnailAsync(string archivePath, string thumbnailPath)
        {
            try
            {


                using var archive = ArchiveFactory.Open(archivePath);

                // 画像ファイルを探す
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
                var imageEntry = archive.Entries
                    .Where(e => !e.IsDirectory)
                    .Where(e => !string.IsNullOrEmpty(e.Key) && !e.Key.Contains("__MACOSX")) // macOSの隠しファイルを除外
                    .Where(e => !string.IsNullOrEmpty(e.Key) && !e.Key.StartsWith(".")) // 隠しファイルを除外
                    .Where(e => !string.IsNullOrEmpty(e.Key) && imageExtensions.Any(ext => e.Key.ToLower().EndsWith(ext)))
                    .Where(e => e.Size > 1024) // 1KB以上のファイルのみ
                    .OrderBy(e => e.Key ?? string.Empty) // ファイル名順でソート
                    .FirstOrDefault();

                if (imageEntry == null)
                {
                    _logger.LogWarning("アーカイブ内に画像ファイルが見つかりませんでした: {ArchivePath}", archivePath);
                    // 画像が見つからない場合はダミー画像を使用
                    File.Copy(_defaultThumbnailPath, thumbnailPath, true);
                    return true;
                }

                _logger.LogDebug("サムネイル生成に使用する画像: {ImageKey} ({Size} bytes)", imageEntry.Key, imageEntry.Size);

                // 画像を抽出してサムネイル生成
                using var entryStream = imageEntry.OpenEntryStream();
                using var memoryStream = new MemoryStream();
                await entryStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                return await CreateThumbnailFromStreamAsync(memoryStream, thumbnailPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "アーカイブからの画像抽出中にエラーが発生しました: {ArchivePath}", archivePath);

                // エラーの場合はダミー画像を使用
                try
                {
                    File.Copy(_defaultThumbnailPath, thumbnailPath, true);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }



        private async Task<bool> CreateThumbnailFromStreamAsync(Stream imageStream, string thumbnailPath)
        {
            try
            {
                // ConfigureAwait(false)でUIスレッドへの復帰を避け、パフォーマンスを向上
                await Task.Run(() =>
                {
                    using var originalImage = Image.FromStream(imageStream);

                    // 固定サイズのキャンバス（600x400ピクセル）
                    const int canvasWidth = 600;
                    const int canvasHeight = 400;

                    // アスペクト比を維持して縮小サイズを計算
                    var scaledSize = CalculateThumbnailSize(originalImage.Width, originalImage.Height, canvasWidth, canvasHeight);

                    // 中央配置のための座標を計算
                    var x = (canvasWidth - scaledSize.Width) / 2;
                    var y = (canvasHeight - scaledSize.Height) / 2;

                    using var thumbnail = new Bitmap(canvasWidth, canvasHeight);
                    using var graphics = Graphics.FromImage(thumbnail);

                    // 高品質だが軽量な縮小設定に変更
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighSpeed;
                    graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;

                    // 背景を白にする（JPGは透明をサポートしないため）
                    graphics.Clear(Color.White);

                    // 画像を中央に描画
                    graphics.DrawImage(originalImage, x, y, scaledSize.Width, scaledSize.Height);

                    // JPG形式で品質設定付きで保存
                    var quality = _config.GetSetting<int>("ThumbnailJpegQuality", 85);
                    SaveAsJpeg(thumbnail, thumbnailPath, quality);
                }).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル画像生成中にエラーが発生しました: {ThumbnailPath}", thumbnailPath);
                return false;
            }
        }

        private (int Width, int Height) CalculateThumbnailSize(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
        {
            double ratioX = (double)maxWidth / originalWidth;
            double ratioY = (double)maxHeight / originalHeight;
            double ratio = Math.Min(ratioX, ratioY);

            int newWidth = (int)(originalWidth * ratio);
            int newHeight = (int)(originalHeight * ratio);

            return (newWidth, newHeight);
        }

        private void CreateDefaultThumbnail()
        {
            try
            {
                if (File.Exists(_defaultThumbnailPath))
                    return;

                // 600x400のダミー画像を作成
                using var bitmap = new Bitmap(600, 400);
                using var graphics = Graphics.FromImage(bitmap);

                // 背景を透明にする
                graphics.Clear(Color.Transparent);

                // 枠線を描画
                using var pen = new Pen(Color.Gray, 1);
                graphics.DrawRectangle(pen, 0, 0, 119, 79);

                // テキストを描画
                using var font = new Font("Arial", 9, FontStyle.Bold);
                using var brush = new SolidBrush(Color.DarkGray);
                var text = "No Image";
                var textSize = graphics.MeasureString(text, font);
                var x = (120 - textSize.Width) / 2;
                var y = (80 - textSize.Height) / 2;
                graphics.DrawString(text, font, brush, x, y);

                // JPG形式で保存
                var quality = _config.GetSetting<int>("ThumbnailJpegQuality", 85);
                SaveAsJpeg(bitmap, _defaultThumbnailPath, quality);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "デフォルトサムネイル作成中にエラーが発生しました");
            }
        }

        /// <summary>
        /// サムネイル保存ディレクトリを取得します（アプリフォルダ優先、フォールバック対応）
        /// </summary>
        /// <returns>サムネイル保存ディレクトリパス</returns>
        private string GetThumbnailDirectory()
        {
            try
            {
                // アプリケーションフォルダ内のthumbnailsディレクトリを試行
                var appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(appDirectory))
                {
                    var thumbnailDir = Path.Combine(appDirectory, "thumbnails");

                    // ディレクトリが存在しない場合は作成を試行
                    if (!Directory.Exists(thumbnailDir))
                    {
                        Directory.CreateDirectory(thumbnailDir);
                    }

                    // 書き込み権限をテスト
                    var testFile = Path.Combine(thumbnailDir, "write_test.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);

                    _logger.LogInformation("サムネイル保存先: アプリフォルダ ({Directory})", thumbnailDir);
                    return thumbnailDir;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "アプリフォルダへのアクセスに失敗しました。APPDATAにフォールバックします");
            }

            // フォールバック: APPDATAフォルダを使用
            try
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mangaanya");
                var thumbnailDir = Path.Combine(appDataPath, "thumbnails");

                if (!Directory.Exists(thumbnailDir))
                {
                    Directory.CreateDirectory(thumbnailDir);
                }

                _logger.LogInformation("サムネイル保存先: APPDATAフォルダ ({Directory})", thumbnailDir);
                return thumbnailDir;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "APPDATAフォルダへのアクセスにも失敗しました");
                throw new InvalidOperationException("サムネイル保存ディレクトリを作成できませんでした", ex);
            }
        }

        /// <summary>
        /// 指定した品質でJPEG形式で画像を保存します
        /// </summary>
        /// <param name="image">保存する画像</param>
        /// <param name="path">保存先パス</param>
        /// <param name="quality">品質（0-100）</param>
        private void SaveAsJpeg(Image image, string path, long quality)
        {
            // JPEGエンコーダーを取得
            var jpegCodec = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

            if (jpegCodec == null)
            {
                // JPEGエンコーダーが見つからない場合はデフォルト保存
                image.Save(path, ImageFormat.Jpeg);
                return;
            }

            // エンコーダーパラメータを設定
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            // 指定した品質でJPEG保存
            image.Save(path, jpegCodec, encoderParams);
        }
    }
}
