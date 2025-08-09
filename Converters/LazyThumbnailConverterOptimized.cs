using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Mangaanya.Models;
using Mangaanya.Services;

namespace Mangaanya.Converters
{
    /// <summary>
    /// 最適化された遅延読み込みサムネイルコンバーター
    /// </summary>
    public class LazyThumbnailConverterOptimized : IValueConverter
    {
        private static readonly Lazy<IThumbnailService> _thumbnailService = new(() => 
            App.ServiceProvider.GetRequiredService<IThumbnailService>());
        private static readonly Lazy<IConfigurationManager> _configManager = new(() => 
            App.ServiceProvider.GetRequiredService<IConfigurationManager>());
        
        // 軽量キャッシュ（200件、約40MB）
        private static readonly ThumbnailMemoryCache _memoryCache = new(200);
        private static readonly ConcurrentDictionary<string, Task<BitmapImage>> _loadingTasks = new();
        private static readonly ConcurrentDictionary<string, bool> _existsCache = new();
        
        private static readonly Lazy<BitmapImage> _defaultThumbnail = new(CreateDefaultThumbnail);
        private static readonly Lazy<BitmapImage> _loadingThumbnail = new(CreateLoadingThumbnail);
        
        // UI更新の制限用
        private static readonly System.Threading.Timer _uiUpdateTimer;
        private static readonly ConcurrentQueue<MangaFile> _pendingUpdates = new();
        
        static LazyThumbnailConverterOptimized()
        {
            // UI更新を500ms間隔でバッチ処理
            _uiUpdateTimer = new System.Threading.Timer(ProcessPendingUpdates, null, 
                TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not MangaFile mangaFile)
                return _defaultThumbnail.Value;

            try
            {
                // サムネイル表示設定をチェック
                var showThumbnails = _configManager.Value.GetSetting<bool>("ShowThumbnails", true);
                if (!showThumbnails)
                {
                    return _defaultThumbnail.Value;
                }

                var cacheKey = GenerateCacheKey(mangaFile);

                // メモリキャッシュから高速取得
                if (_memoryCache.TryGet(cacheKey, out var cachedImage) && cachedImage != null)
                {
                    return cachedImage;
                }

                // 非同期でディスクキャッシュをチェック（UIスレッドをブロックしない）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // ファイル存在チェックをキャッシュ
                        var exists = await CheckThumbnailExistsAsync(mangaFile.ThumbnailPath, cacheKey);
                        
                        if (exists)
                        {
                            // 軽量画像読み込み
                            var diskImage = await LoadImageFromDiskLightAsync(mangaFile.ThumbnailPath!);
                            if (diskImage != null)
                            {
                                _memoryCache.Add(cacheKey, diskImage);
                                
                                // UI更新をキューに追加
                                _pendingUpdates.Enqueue(mangaFile);
                                return;
                            }
                        }

                        // サムネイル生成が必要
                        await StartAsyncGenerationOptimized(mangaFile, cacheKey);
                    }
                    catch
                    {
                        // エラー時は何もしない（デフォルト画像のまま）
                    }
                });

                return _loadingThumbnail.Value;
            }
            catch (Exception)
            {
                return _defaultThumbnail.Value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 非同期ファイル存在チェック（キャッシュ付き）
        /// </summary>
        private async Task<bool> CheckThumbnailExistsAsync(string? thumbnailPath, string cacheKey)
        {
            if (string.IsNullOrEmpty(thumbnailPath))
                return false;

            // 存在チェックのキャッシュを確認
            if (_existsCache.TryGetValue(cacheKey, out var cachedExists))
                return cachedExists;

            // 非同期でファイル存在チェック
            var exists = await Task.Run(() => File.Exists(thumbnailPath));
            
            // 結果をキャッシュ（5分間）
            _existsCache.TryAdd(cacheKey, exists);
            _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ => 
                _existsCache.TryRemove(cacheKey, out var _));

            return exists;
        }

        /// <summary>
        /// 軽量画像読み込み
        /// </summary>
        private async Task<BitmapImage?> LoadImageFromDiskLightAsync(string path)
        {
            try
            {
                return await Task.Run(() =>
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    
                    // 解像度維持（480px）
                    bitmap.DecodePixelWidth = 480;
                    
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 最適化された非同期生成
        /// </summary>
        private async Task StartAsyncGenerationOptimized(MangaFile mangaFile, string cacheKey)
        {
            // 既に生成中の場合はスキップ
            if (_loadingTasks.ContainsKey(cacheKey))
                return;

            var task = _loadingTasks.GetOrAdd(cacheKey, _ => Task.Run(async () =>
            {
                try
                {
                    // 軽量サムネイル生成
                    var result = await _thumbnailService.Value.GenerateThumbnailAsync(mangaFile);
                    
                    if (result.Success && !string.IsNullOrEmpty(result.ThumbnailPath))
                    {
                        var image = await LoadImageFromDiskLightAsync(result.ThumbnailPath);
                        if (image != null)
                        {
                            _memoryCache.Add(cacheKey, image);
                            
                            // UI更新をキューに追加
                            _pendingUpdates.Enqueue(mangaFile);
                            
                            return image;
                        }
                    }
                    
                    return _defaultThumbnail.Value;
                }
                catch
                {
                    return _defaultThumbnail.Value;
                }
                finally
                {
                    _loadingTasks.TryRemove(cacheKey, out var _);
                }
            }));

            await task;
        }

        /// <summary>
        /// UI更新のバッチ処理
        /// </summary>
        private static void ProcessPendingUpdates(object? state)
        {
            if (_pendingUpdates.IsEmpty)
                return;

            try
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var mainWindow = System.Windows.Application.Current.MainWindow;
                        if (mainWindow != null)
                        {
                            var dataGrid = mainWindow.FindName("MangaFilesGrid") as System.Windows.Controls.DataGrid;
                            if (dataGrid != null)
                            {
                                // 一度だけリフレッシュ
                                dataGrid.Items.Refresh();
                                
                                // キューをクリア
                                while (_pendingUpdates.TryDequeue(out var _)) { }
                            }
                        }
                    }
                    catch
                    {
                        // UI更新エラーは無視
                    }
                });
            }
            catch
            {
                // エラーは無視
            }
        }

        private string GenerateCacheKey(MangaFile mangaFile)
        {
            return $"{mangaFile.FilePath}_{mangaFile.FileSize}_{mangaFile.ModifiedDate:yyyyMMddHHmmss}";
        }

        /// <summary>
        /// 軽量デフォルトサムネイル
        /// </summary>
        private static BitmapImage CreateDefaultThumbnail()
        {
            try
            {
                var visual = new System.Windows.Media.DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    var rect = new System.Windows.Rect(0, 0, 480, 320);
                    var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Gray, 1);
                    context.DrawRectangle(System.Windows.Media.Brushes.LightGray, pen, rect);
                    
                    var text = new System.Windows.Media.FormattedText(
                        "No Image",
                        CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Arial"),
                        48,
                        System.Windows.Media.Brushes.Gray,
                        96);
                    
                    var textX = (480 - text.Width) / 2;
                    var textY = (320 - text.Height) / 2;
                    context.DrawText(text, new System.Windows.Point(textX, textY));
                }

                var renderBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    480, 320, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderBitmap.Render(visual);
                renderBitmap.Freeze();

                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderBitmap));

                using var memory = new MemoryStream();
                encoder.Save(memory);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
            catch
            {
                var emptyBitmap = new BitmapImage();
                emptyBitmap.BeginInit();
                emptyBitmap.EndInit();
                emptyBitmap.Freeze();
                return emptyBitmap;
            }
        }

        /// <summary>
        /// ローディングサムネイル（解像度維持）
        /// </summary>
        private static BitmapImage CreateLoadingThumbnail()
        {
            try
            {
                var visual = new System.Windows.Media.DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    var rect = new System.Windows.Rect(0, 0, 480, 320);
                    var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.LightBlue, 1);
                    pen.DashStyle = System.Windows.Media.DashStyles.Dash;
                    context.DrawRectangle(System.Windows.Media.Brushes.White, pen, rect);
                    
                    var text = new System.Windows.Media.FormattedText(
                        "Loading...",
                        CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Arial"),
                        40,
                        System.Windows.Media.Brushes.LightBlue,
                        96);
                    
                    var textX = (480 - text.Width) / 2;
                    var textY = (320 - text.Height) / 2;
                    context.DrawText(text, new System.Windows.Point(textX, textY));
                }

                var renderBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    480, 320, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderBitmap.Render(visual);
                renderBitmap.Freeze();

                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderBitmap));

                using var memory = new MemoryStream();
                encoder.Save(memory);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
            catch
            {
                return CreateDefaultThumbnail();
            }
        }

        public static void ClearCache()
        {
            _memoryCache.Clear();
            _loadingTasks.Clear();
            _existsCache.Clear();
        }

        public static void OnSettingsChanged()
        {
            ClearCache();
        }
    }
}
