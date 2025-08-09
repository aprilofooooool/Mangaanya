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
using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using SharpCompress.Archives;

namespace Mangaanya.Services
{
    /// <summary>
    /// 最適化されたサムネイルサービス
    /// </summary>
    public class ThumbnailServiceOptimized : IThumbnailService
    {
        private readonly ILogger<ThumbnailServiceOptimized> _logger;
        private readonly IConfigurationManager _config;
        private readonly IMangaRepository _repository;
        private readonly string _thumbnailDirectory;
        private readonly string _defaultThumbnailPath;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly ConcurrentQueue<MangaFile> _dbUpdateQueue = new();
        private readonly System.Threading.Timer _dbUpdateTimer;

        public ThumbnailServiceOptimized(
            ILogger<ThumbnailServiceOptimized> logger,
            IConfigurationManager config,
            IMangaRepository repository)
        {
            _logger = logger;
            _config = config;
            _repository = repository;

            _thumbnailDirectory = GetThumbnailDirectory();
            _defaultThumbnailPath = Path.Combine(_thumbnailDirectory, "default_thumbnail.jpg");
            
            // 並列処理数を10に制限（CPUを使い切らない）
            var maxConcurrency = 10;
            _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            
            // DB更新をバッチ処理するタイマー（5秒間隔）
            _dbUpdateTimer = new System.Threading.Timer(ProcessDbUpdates, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            
            CreateDefaultThumbnail();
        }

        public async Task<List<ThumbnailGenerationResult>> GenerateThumbnailsBatchAsync(
            IEnumerable<MangaFile> mangaFiles, 
            IProgress<ThumbnailProgress>? progress = null,
            CancellationToken cancellationToken = default,
            bool skipExisting = true)
        {
            var fileList = mangaFiles.ToList();
            var totalFiles = fileList.Count;
            var processedCount = 0;
            var results = new ConcurrentBag<ThumbnailGenerationResult>();

            _logger.LogInformation("高速サムネイル生成開始: {TotalFiles}件, 並列数: {Concurrency}", 
                totalFiles, _concurrencyLimiter.CurrentCount);

            // 並列処理でサムネイル生成（10並列に制限）
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = 10 // 10並列に制限
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
                        // 既存チェック（高速化）
                        if (skipExisting && ThumbnailExistsFast(file))
                        {
                            results.Add(new ThumbnailGenerationResult
                            {
                                Success = true,
                                ThumbnailPath = file.ThumbnailPath,
                                MangaFile = file
                            });
                        }
                        else
                        {
                            // 同期版の高速サムネイル生成
                            var result = GenerateThumbnailFast(file);
                            results.Add(result);
                        }

                        var current = Interlocked.Increment(ref processedCount);
                        
                        // 進捗報告（100件ごと）
                        if (current % 100 == 0 || current == totalFiles)
                        {
                            progress?.Report(new ThumbnailProgress
                            {
                                CurrentFile = current,
                                TotalFiles = totalFiles,
                                CurrentFileName = file.FileName,
                                Status = "高速処理中"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "サムネイル生成エラー: {FileName}", file.FileName);
                        results.Add(new ThumbnailGenerationResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message,
                            MangaFile = file
                        });
                    }
                });
            }, cancellationToken);

            // 最終DB更新を実行
            await ProcessDbUpdatesAsync();

            var resultList = results.ToList();
            _logger.LogInformation("高速サムネイル生成完了: 成功={Success}件, 失敗={Failed}件", 
                resultList.Count(r => r.Success), resultList.Count(r => !r.Success));

            return resultList;
        }

        /// <summary>
        /// 高速サムネイル生成（同期版）
        /// </summary>
        private ThumbnailGenerationResult GenerateThumbnailFast(MangaFile mangaFile)
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

                var fileHash = GetFileHashFast(mangaFile.FilePath);
                var thumbnailFileName = $"{fileHash}.jpg";
                var thumbnailPath = Path.Combine(_thumbnailDirectory, thumbnailFileName);

                // 既存チェック
                if (File.Exists(thumbnailPath))
                {
                    return new ThumbnailGenerationResult
                    {
                        Success = true,
                        ThumbnailPath = thumbnailPath,
                        MangaFile = mangaFile
                    };
                }

                // 高速画像抽出・生成
                var success = ExtractAndCreateThumbnailFast(mangaFile.FilePath, thumbnailPath);

                if (success)
                {
                    // DB更新をキューに追加（バッチ処理）
                    mangaFile.ThumbnailPath = thumbnailPath;
                    mangaFile.ThumbnailCreated = DateTime.Now;
                    _dbUpdateQueue.Enqueue(mangaFile);

                    return new ThumbnailGenerationResult
                    {
                        Success = true,
                        ThumbnailPath = thumbnailPath,
                        MangaFile = mangaFile
                    };
                }

                return new ThumbnailGenerationResult
                {
                    Success = false,
                    ErrorMessage = "サムネイル生成に失敗しました",
                    MangaFile = mangaFile
                };
            }
            catch (Exception ex)
            {
                return new ThumbnailGenerationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    MangaFile = mangaFile
                };
            }
        }

        /// <summary>
        /// 高速画像抽出・生成
        /// </summary>
        private bool ExtractAndCreateThumbnailFast(string archivePath, string thumbnailPath)
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
                    File.Copy(_defaultThumbnailPath, thumbnailPath, true);
                    return true;
                }

                // 高速画像処理
                using var entryStream = imageEntry.OpenEntryStream();
                return CreateThumbnailFast(entryStream, thumbnailPath);
            }
            catch
            {
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

        /// <summary>
        /// 最適化されたサムネイル作成（解像度維持）
        /// </summary>
        private bool CreateThumbnailFast(Stream imageStream, string thumbnailPath)
        {
            try
            {
                using var originalImage = Image.FromStream(imageStream);
                
                // 解像度維持（600x400）
                const int canvasWidth = 600;
                const int canvasHeight = 400;
                
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
                
                // 品質設定付きでJPG保存
                var quality = _config.GetSetting<int>("ThumbnailJpegQuality", 85);
                SaveAsJpegFast(thumbnail, thumbnailPath, quality);
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsImageFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;
            var ext = Path.GetExtension(fileName).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp";
        }

        private bool ThumbnailExistsFast(MangaFile file)
        {
            return !string.IsNullOrEmpty(file.ThumbnailPath) && File.Exists(file.ThumbnailPath);
        }

        private string GetFileHashFast(string filePath)
        {
            return Math.Abs(filePath.GetHashCode()).ToString("X8");
        }

        private void SaveAsJpegFast(Image image, string path, int quality)
        {
            var encoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            image.Save(path, encoder, encoderParams);
        }

        private (int Width, int Height) CalculateThumbnailSize(int originalWidth, int originalHeight, int maxWidth, int maxHeight)
        {
            double ratioX = (double)maxWidth / originalWidth;
            double ratioY = (double)maxHeight / originalHeight;
            double ratio = Math.Min(ratioX, ratioY);

            return ((int)(originalWidth * ratio), (int)(originalHeight * ratio));
        }

        /// <summary>
        /// DB更新のバッチ処理
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
                    // バッチでDB更新
                    await _repository.UpdateBatchAsync(updates);
                    
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "DB一括更新エラー: {Count}件", updates.Count);
                }
            }
        }

        // 既存メソッドの実装...
        public async Task<ThumbnailGenerationResult> GenerateThumbnailAsync(MangaFile mangaFile)
        {
            return await Task.FromResult(GenerateThumbnailFast(mangaFile));
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

        private string GetThumbnailDirectory()
        {
            // 実装は既存のThumbnailServiceと同じ
            var appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var thumbnailDir = Path.Combine(appDirectory!, "thumbnails");
            
            try
            {
                Directory.CreateDirectory(thumbnailDir);
                return thumbnailDir;
            }
            catch
            {
                var fallbackDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mangaanya", "thumbnails");
                Directory.CreateDirectory(fallbackDir);
                return fallbackDir;
            }
        }

        private void CreateDefaultThumbnail()
        {
            if (File.Exists(_defaultThumbnailPath)) return;

            try
            {
                // 解像度維持（600x400）
                using var bitmap = new Bitmap(600, 400);
                using var graphics = Graphics.FromImage(bitmap);
                
                // 背景を白にする
                graphics.Clear(Color.White);
                
                // 枠線を描画
                using var pen = new Pen(Color.Gray, 2);
                graphics.DrawRectangle(pen, 10, 10, 580, 380);
                
                // テキストを描画
                using var font = new Font("Arial", 48, FontStyle.Bold);
                using var brush = new SolidBrush(Color.DarkGray);
                var text = "No Image";
                var textSize = graphics.MeasureString(text, font);
                var x = (600 - textSize.Width) / 2;
                var y = (400 - textSize.Height) / 2;
                graphics.DrawString(text, font, brush, x, y);
                
                SaveAsJpegFast(bitmap, _defaultThumbnailPath, 85);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "デフォルトサムネイル作成エラー");
            }
        }

        public string GetDefaultThumbnailPath()
        {
            return _defaultThumbnailPath;
        }

        public async Task<System.Windows.Media.Imaging.BitmapImage> GetThumbnailImageAsync(MangaFile mangaFile)
        {
            try
            {
                // サムネイル生成または取得
                var result = await GenerateThumbnailAsync(mangaFile);
                
                if (result.Success && !string.IsNullOrEmpty(result.ThumbnailPath))
                {
                    // 画像を読み込んでBitmapImageとして返す
                    return await Task.Run(() =>
                    {
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(result.ThumbnailPath, UriKind.Absolute);
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 480; // 解像度維持
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    });
                }
                
                // 失敗時はデフォルト画像を返す
                return await Task.Run(() =>
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_defaultThumbnailPath, UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                });
            }
            catch
            {
                // エラー時はデフォルト画像を返す
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_defaultThumbnailPath, UriKind.Absolute);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }

        public void Dispose()
        {
            _dbUpdateTimer?.Dispose();
            _concurrencyLimiter?.Dispose();
        }
    }
}
