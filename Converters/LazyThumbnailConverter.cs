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
    /// 遅延読み込み対応のサムネイルコンバーター
    /// </summary>
    public class LazyThumbnailConverter : IValueConverter
    {
        private static readonly Lazy<IThumbnailService> _thumbnailService = new(() => 
            App.ServiceProvider.GetRequiredService<IThumbnailService>());
        private static readonly Lazy<IConfigurationManager> _configManager = new(() => 
            App.ServiceProvider.GetRequiredService<IConfigurationManager>());
        private static readonly ThumbnailMemoryCache _memoryCache = new(50);
        private static readonly ConcurrentDictionary<string, Task<BitmapImage>> _loadingTasks = new();
        private static readonly Lazy<BitmapImage> _defaultThumbnail = new(CreateDefaultThumbnail);
        private static readonly Lazy<BitmapImage> _loadingThumbnail = new(CreateLoadingThumbnail);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not MangaFile mangaFile)
                return _defaultThumbnail.Value;

            try
            {
                // サムネイル表示設定をチェック - 非表示の場合は処理をスキップ
                var showThumbnails = _configManager.Value.GetSetting<bool>("ShowThumbnails", true);
                if (!showThumbnails)
                {
                    return _defaultThumbnail.Value;
                }

                var cacheKey = GenerateCacheKey(mangaFile);

                // メモリキャッシュから取得を試行
                if (_memoryCache.TryGet(cacheKey, out var cachedImage) && cachedImage != null)
                {
                    return cachedImage;
                }

                // 既存のディスクキャッシュをチェック
                if (!string.IsNullOrEmpty(mangaFile.ThumbnailPath) && File.Exists(mangaFile.ThumbnailPath))
                {
                    try
                    {
                        var diskImage = LoadImageFromDisk(mangaFile.ThumbnailPath);
                        _memoryCache.Add(cacheKey, diskImage);
                        return diskImage;
                    }
                    catch
                    {
                        // ディスク読み込み失敗時は生成処理に進む
                    }
                }

                // 非同期生成を開始
                StartAsyncGeneration(mangaFile, cacheKey);
                
                return _loadingThumbnail.Value;
            }
            catch (Exception)
            {
                // エラー時はデフォルト画像を返す
                return _defaultThumbnail.Value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 非同期でサムネイル生成を開始します
        /// </summary>
        private void StartAsyncGeneration(MangaFile mangaFile, string cacheKey)
        {
            _loadingTasks.GetOrAdd(cacheKey, _ => GenerateThumbnailAsync(mangaFile, cacheKey));
        }

        private async Task<BitmapImage> GenerateThumbnailAsync(MangaFile mangaFile, string cacheKey)
        {
            try
            {
                // サムネイル生成（既にThumbnailService内で適切に非同期処理されている）
                var result = await _thumbnailService.Value.GenerateThumbnailAsync(mangaFile);
                
                if (result.Success && !string.IsNullOrEmpty(result.ThumbnailPath))
                {
                    // 画像読み込みを非同期で実行（I/O集約的処理）
                    var image = await Task.Run(() => LoadImageFromDisk(result.ThumbnailPath));
                    _memoryCache.Add(cacheKey, image);
                    
                    // UIスレッドで更新通知
                    _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        NotifyThumbnailUpdated(mangaFile);
                    });
                    
                    return image;
                }
                
                return _defaultThumbnail.Value;
            }
            catch (Exception)
            {
                // エラー時はデフォルト画像を返す
                return _defaultThumbnail.Value;
            }
            finally
            {
                _loadingTasks.TryRemove(cacheKey, out var _);
            }
        }

        /// <summary>
        /// キャッシュキーを生成します
        /// </summary>
        private string GenerateCacheKey(MangaFile mangaFile)
        {
            // ファイルパス + サイズ + 更新日時でキーを生成
            return $"{mangaFile.FilePath}_{mangaFile.FileSize}_{mangaFile.ModifiedDate:yyyyMMddHHmmss}";
        }

        /// <summary>
        /// ディスクから画像を読み込みます
        /// </summary>
        private BitmapImage LoadImageFromDisk(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 480; // ポップアップ表示(600px)に対応、DataGrid表示(180px)の2.67倍で高DPI対応
            bitmap.EndInit();
            bitmap.Freeze(); // UIスレッド以外からアクセス可能にする
            return bitmap;
        }

        /// <summary>
        /// サムネイル更新をUIに通知します
        /// </summary>
        private void NotifyThumbnailUpdated(MangaFile mangaFile)
        {
            try
            {
                // MainWindowのDataGridを取得してリフレッシュ
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var dataGrid = mainWindow.FindName("MangaFilesGrid") as System.Windows.Controls.DataGrid;
                    if (dataGrid != null)
                    {
                        // DataGridの表示を更新
                        dataGrid.Items.Refresh();
                    }
                }
            }
            catch
            {
                // エラーが発生した場合は無視（UI更新は必須ではない）
            }
        }

        /// <summary>
        /// デフォルトサムネイル画像を作成します
        /// </summary>
        private static BitmapImage CreateDefaultThumbnail()
        {
            try
            {
                // WPFのDrawingVisualを使用してダミー画像を作成
                var visual = new System.Windows.Media.DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    // 背景を透明にする
                    var rect = new System.Windows.Rect(0, 0, 480, 320);
                    
                    // 枠線を描画
                    var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Gray, 1);
                    context.DrawRectangle(null, pen, rect);
                    
                    // テキストを描画
                    var text = new System.Windows.Media.FormattedText(
                        "No Image",
                        CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Arial"),
                        48,
                        System.Windows.Media.Brushes.DarkGray,
                        96);
                    
                    var textX = (480 - text.Width) / 2;
                    var textY = (320 - text.Height) / 2;
                    context.DrawText(text, new System.Windows.Point(textX, textY));
                }

                // DrawingVisualをBitmapに変換（表示サイズに合わせて最適化）
                var renderBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    480, 320, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderBitmap.Render(visual);
                renderBitmap.Freeze();

                // RenderTargetBitmapをBitmapImageに変換
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderBitmap));

                using (var memory = new System.IO.MemoryStream())
                {
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
            }
            catch
            {
                // エラーの場合は空のBitmapImageを返す
                var emptyBitmap = new BitmapImage();
                emptyBitmap.BeginInit();
                emptyBitmap.EndInit();
                emptyBitmap.Freeze();
                return emptyBitmap;
            }
        }

        /// <summary>
        /// 読み込み中サムネイル画像を作成します
        /// </summary>
        private static BitmapImage CreateLoadingThumbnail()
        {
            try
            {
                // WPFのDrawingVisualを使用して読み込み中画像を作成
                var visual = new System.Windows.Media.DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    // 背景を透明にする
                    var rect = new System.Windows.Rect(0, 0, 480, 320);
                    
                    // 枠線を描画（点線）
                    var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.LightBlue, 1);
                    pen.DashStyle = System.Windows.Media.DashStyles.Dash;
                    context.DrawRectangle(null, pen, rect);
                    
                    // テキストを描画
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

                // DrawingVisualをBitmapに変換（表示サイズに合わせて最適化）
                var renderBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    480, 320, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderBitmap.Render(visual);
                renderBitmap.Freeze();

                // RenderTargetBitmapをBitmapImageに変換
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(renderBitmap));

                using (var memory = new System.IO.MemoryStream())
                {
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
            }
            catch
            {
                // エラーの場合はデフォルト画像を返す
                return CreateDefaultThumbnail();
            }
        }

        /// <summary>
        /// メモリキャッシュをクリアします（設定変更時などに使用）
        /// </summary>
        public static void ClearCache()
        {
            _memoryCache.Clear();
            _loadingTasks.Clear();
        }

        /// <summary>
        /// 設定変更時の処理を実行します
        /// </summary>
        public static void OnSettingsChanged()
        {
            // キャッシュをクリアして新しい設定を反映
            ClearCache();
            
            // 新しい設定でデフォルト・ローディング画像を再生成
            _defaultThumbnail.Value?.Freeze();
            _loadingThumbnail.Value?.Freeze();
        }
    }
}
