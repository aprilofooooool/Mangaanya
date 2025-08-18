using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using Mangaanya.Constants;
using Mangaanya.Common;
using SharpCompress.Archives;

namespace Mangaanya.Services
{
    /// <summary>
    /// 最適化されたサムネイルサービス（バイナリ保存版）
    /// </summary>
    public class ThumbnailServiceOptimized : IThumbnailService
    {
        private readonly ILogger<ThumbnailServiceOptimized> _logger;
        private readonly IMangaRepository _repository;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly ConcurrentQueue<MangaFile> _dbUpdateQueue = new();
        private readonly System.Threading.Timer _dbUpdateTimer;
        private readonly Lazy<BitmapImage> _defaultThumbnail;

        public ThumbnailServiceOptimized(
            ILogger<ThumbnailServiceOptimized> logger,
            IMangaRepository repository)
        {
            _logger = logger;
            _repository = repository;
            
            // 並列処理数を10に制限（CPUを使い切らない）
            var maxConcurrency = ThumbnailConstants.MaxConcurrency;
            _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            
            // DB更新をバッチ処理するタイマー（5秒間隔）
            _dbUpdateTimer = new System.Threading.Timer(ProcessDbUpdates, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            
            // デフォルトサムネイルの遅延初期化
            _defaultThumbnail = new Lazy<BitmapImage>(CreateDefaultThumbnailImage);
        }

        /// <summary>
        /// 複数のマンガファイルのサムネイルを一括生成します
        /// </summary>
        /// <param name="mangaFiles">サムネイル生成対象のマンガファイル</param>
        /// <param name="progress">進捗報告用のプログレス</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <param name="skipExisting">既存のサムネイルをスキップするかどうか</param>
        /// <returns>サムネイル生成結果のリスト</returns>
        public async Task<Result<List<ThumbnailGenerationResult>>> GenerateThumbnailsBatchAsync(
            IEnumerable<MangaFile> mangaFiles, 
            IProgress<ThumbnailProgress>? progress = null,
            CancellationToken cancellationToken = default,
            bool skipExisting = true)
        {
            var fileList = mangaFiles.ToList();
            var totalFiles = fileList.Count;
            var processedCount = 0;
            var results = new ConcurrentBag<ThumbnailGenerationResult>();

            _logger.LogInformation("高速バイナリサムネイル生成開始: {TotalFiles}件, 並列数: {Concurrency}", 
                totalFiles, _concurrencyLimiter.CurrentCount);

            // 並列処理でサムネイル生成（10並列に制限）
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = ThumbnailConstants.MaxConcurrency // 10並列に制限
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(fileList, parallelOptions, (file, state, index) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        state.Stop();
                        return;
                    }

                    try
                    {
                        // 既存チェック（バイナリデータベース）
                        if (skipExisting && file.HasThumbnail)
                        {
                            results.Add(new ThumbnailGenerationResult
                            {
                                MangaFile = file,
                                WasSkipped = true
                            });
                        }
                        else
                        {
                            // 同期版の高速バイナリサムネイル生成
                            var result = GenerateThumbnailFast(file);
                            results.Add(result);
                        }

                        var current = Interlocked.Increment(ref processedCount);
                        
                        // 進捗報告（100件ごと）
                        if (current % ThumbnailConstants.ProgressReportInterval == 0 || current == totalFiles)
                        {
                            progress?.Report(new ThumbnailProgress
                            {
                                CurrentFile = current,
                                TotalFiles = totalFiles,
                                CurrentFileName = file.FileName,
                                Status = "バイナリ処理中"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "バイナリサムネイル生成エラー: {FileName}", file.FileName);
                        results.Add(new ThumbnailGenerationResult
                        {
                            MangaFile = file
                        });
                    }
                });
            }, cancellationToken);

            // 最終DB更新を実行
            await ProcessDbUpdatesAsync();

            var resultList = results.ToList();
            _logger.LogInformation("高速バイナリサムネイル生成完了: 処理件数={Count}件", resultList.Count);

            return Result<List<ThumbnailGenerationResult>>.Success(resultList);
        }

        /// <summary>
        /// 高速バイナリサムネイル生成（同期版）
        /// </summary>
        private ThumbnailGenerationResult GenerateThumbnailFast(MangaFile mangaFile)
        {
            try
            {
                if (mangaFile == null || string.IsNullOrEmpty(mangaFile.FilePath))
                {
                    throw new ArgumentException("ファイル情報が無効です");
                }

                // 高速バイナリデータ生成
                var thumbnailData = GenerateThumbnailBytesSync(mangaFile.FilePath);

                if (thumbnailData != null && thumbnailData.Length > 0)
                {
                    // バイナリデータをモデルに設定
                    mangaFile.ThumbnailData = thumbnailData;
                    mangaFile.ThumbnailGenerated = DateTime.Now;
                    
                    // DB更新をキューに追加（バッチ処理）
                    _dbUpdateQueue.Enqueue(mangaFile);

                    return new ThumbnailGenerationResult
                    {
                        MangaFile = mangaFile,
                        WasSkipped = false
                    };
                }

                throw new Mangaanya.Exceptions.ThumbnailGenerationException(mangaFile.FilePath, "バイナリサムネイル生成に失敗しました");
            }
            catch (Exception ex)
            {
                throw new Mangaanya.Exceptions.ThumbnailGenerationException(mangaFile.FilePath, "サムネイル生成中に予期しないエラーが発生しました", ex);
            }
        }

        /// <summary>
        /// 高速バイナリサムネイル生成（同期版）
        /// </summary>
        private byte[]? GenerateThumbnailBytesSync(string archivePath)
        {
            try
            {
                using var archive = ArchiveFactory.Open(archivePath);
                
                // 最初の画像を高速検索
                var imageEntry = archive.Entries
                    .Where(e => !e.IsDirectory && e.Size > 1024)
                    .FirstOrDefault(e => IsImageFile(e.Key));

                if (imageEntry == null)
                {
                    // デフォルトサムネイルのバイナリデータを返す
                    return CreateDefaultThumbnailBytes();
                }

                // 高速バイナリ画像処理
                using var entryStream = imageEntry.OpenEntryStream();
                return CreateThumbnailBytesFromStream(entryStream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "アーカイブ処理エラー、デフォルトサムネイルを使用: {ArchivePath}", archivePath);
                return CreateDefaultThumbnailBytes();
            }
        }

        /// <summary>
        /// 最適化されたバイナリサムネイル作成
        /// </summary>
        private byte[]? CreateThumbnailBytesFromStream(Stream imageStream)
        {
            try
            {
                using var originalImage = Image.FromStream(imageStream);
                
                // 固定解像度（480x320）- 表示最適化
                const int canvasWidth = ThumbnailConstants.Width;
                const int canvasHeight = ThumbnailConstants.Height;
                
                // アスペクト比を維持して縮小サイズを計算
                var scaledSize = CalculateThumbnailSize(originalImage.Width, originalImage.Height, canvasWidth, canvasHeight);
                
                // 中央配置のための座標を計算
                var x = (canvasWidth - scaledSize.Width) / 2;
                var y = (canvasHeight - scaledSize.Height) / 2;
                
                using var thumbnail = new Bitmap(canvasWidth, canvasHeight);
                using var graphics = Graphics.FromImage(thumbnail);
                
                // 品質と速度のバランス設定
                graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                
                // 背景を白にする
                graphics.Clear(Color.White);
                
                // 画像を中央に描画
                graphics.DrawImage(originalImage, x, y, scaledSize.Width, scaledSize.Height);
                
                // 85%品質固定でJPEGバイナリに変換
                return ConvertToJpegBytes(thumbnail, ThumbnailConstants.JpegQuality);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "バイナリサムネイル作成エラー");
                return null;
            }
        }

        /// <summary>
        /// 画像ファイルかどうかを判定
        /// </summary>
        private bool IsImageFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            var ext = Path.GetExtension(fileName).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp";
        }

        /// <summary>
        /// 画像をJPEGバイナリデータに変換（85%品質固定）
        /// </summary>
        private byte[] ConvertToJpegBytes(Image image, int quality)
        {
            using var stream = new MemoryStream();
            var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            
            image.Save(stream, encoder, encoderParams);
            return stream.ToArray();
        }

        /// <summary>
        /// デフォルトサムネイルのバイナリデータを作成
        /// </summary>
        private byte[] CreateDefaultThumbnailBytes()
        {
            try
            {
                // 固定解像度（480x320）
                using var bitmap = new Bitmap(480, 320);
                using var graphics = Graphics.FromImage(bitmap);
                
                // 背景を白にする
                graphics.Clear(Color.White);
                
                // 枠線を描画
                using var pen = new Pen(Color.Gray, 2);
                graphics.DrawRectangle(pen, 10, 10, 460, 300);
                
                // テキストを描画
                using var font = new Font("Arial", 24, FontStyle.Bold);
                using var brush = new SolidBrush(Color.DarkGray);
                var text = "No Image";
                var textSize = graphics.MeasureString(text, font);
                var x = (480 - textSize.Width) / 2;
                var y = (320 - textSize.Height) / 2;
                graphics.DrawString(text, font, brush, x, y);
                
                return ConvertToJpegBytes(bitmap, 85);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "デフォルトサムネイルバイナリ作成エラー");
                // 最小限のJPEGヘッダーを返す
                return new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9 };
            }
        }

        /// <summary>
        /// アスペクト比を維持してサムネイルサイズを計算
        /// </summary>
        private (int Width, int Height) CalculateThumbnailSize(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
        {
            double ratioX = (double)maxWidth / originalWidth;
            double ratioY = (double)maxHeight / originalHeight;
            double ratio = Math.Min(ratioX, ratioY);

            return ((int)(originalWidth * ratio), (int)(originalHeight * ratio));
        }

        /// <summary>
        /// バイナリデータ用DB更新のバッチ処理
        /// </summary>
        private async void ProcessDbUpdates(object? state)
        {
            await ProcessDbUpdatesAsync();
        }

        private async Task ProcessDbUpdatesAsync()
        {
            var updates = new List<MangaFile>();
            
            // キューから全て取得
            while (_dbUpdateQueue.TryDequeue(out var file))
            {
                updates.Add(file);
            }

            if (updates.Count > 0)
            {
                try
                {
                    // バイナリフィールド用バッチDB更新
                    await _repository.UpdateBatchAsync(updates);
                    _logger.LogDebug("バイナリサムネイルDB更新完了: {Count}件", updates.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "バイナリサムネイルDB一括更新エラー: {Count}件", updates.Count);
                    
                    // 失敗したアイテムを再キューに追加（最大3回まで）
                    foreach (var file in updates)
                    {
                        if (!file.HasThumbnail) continue; // データが無効な場合はスキップ
                        _dbUpdateQueue.Enqueue(file);
                    }
                }
            }
        }

        /// <summary>
        /// 単一ファイルのサムネイル生成（非同期版）
        /// </summary>
        public async Task<Result<ThumbnailGenerationResult>> GenerateThumbnailAsync(MangaFile mangaFile)
        {
            try
            {
                var result = GenerateThumbnailFast(mangaFile);
                return await Task.FromResult(Result<ThumbnailGenerationResult>.Success(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル生成中にエラーが発生しました: {FilePath}", mangaFile.FilePath);
                return Result<ThumbnailGenerationResult>.Failure($"サムネイル生成エラー: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// バイナリデータからサムネイル画像を取得
        /// </summary>
        public BitmapImage? GetThumbnailImage(MangaFile mangaFile)
        {
            try
            {
                if (mangaFile?.ThumbnailData == null || mangaFile.ThumbnailData.Length == 0)
                    return null;

                return CreateBitmapImageFromBytes(mangaFile.ThumbnailData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ファイル {FileId} のバイナリサムネイル読み込みに失敗しました", mangaFile?.Id);
                return null;
            }
        }

        /// <summary>
        /// デフォルトサムネイル画像を取得
        /// </summary>
        public BitmapImage GetDefaultThumbnail()
        {
            return _defaultThumbnail.Value;
        }

        /// <summary>
        /// バイナリデータからBitmapImageを作成
        /// </summary>
        private BitmapImage CreateBitmapImageFromBytes(byte[] imageData)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(imageData);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 480; // 表示サイズに最適化
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>
        /// デフォルトサムネイルのBitmapImageを作成
        /// </summary>
        private BitmapImage CreateDefaultThumbnailImage()
        {
            try
            {
                var defaultBytes = CreateDefaultThumbnailBytes();
                return CreateBitmapImageFromBytes(defaultBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "デフォルトサムネイル画像作成エラー");
                
                // フォールバック: 最小限の透明BitmapImageを作成
                try
                {
                    using var fallbackBitmap = new Bitmap(480, 320);
                    using var graphics = Graphics.FromImage(fallbackBitmap);
                    graphics.Clear(Color.LightGray);
                    
                    var fallbackBytes = ConvertToJpegBytes(fallbackBitmap, 85);
                    return CreateBitmapImageFromBytes(fallbackBytes);
                }
                catch
                {
                    // 最終フォールバック: 空のBitmapImage
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CreateOptions = BitmapCreateOptions.None;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
        }



        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            _dbUpdateTimer?.Dispose();
            _concurrencyLimiter?.Dispose();
        }
    }
}
