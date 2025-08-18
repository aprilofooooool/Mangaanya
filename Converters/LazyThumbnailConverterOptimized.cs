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
    /// 最適化された遅延読み込みサムネイルコンバーター（バイナリデータ対応）
    /// </summary>
    public class LazyThumbnailConverterOptimized : IValueConverter
    {
        private static readonly Lazy<IConfigurationManager> _configManager = new(() => 
            App.ServiceProvider.GetRequiredService<IConfigurationManager>());
        
        // IDベースキャッシュ（300件、パフォーマンス向上のため拡張）
        private static readonly ThumbnailMemoryCache _memoryCache = new(300);
        
        private static readonly Lazy<BitmapImage> _defaultThumbnail = new(CreateDefaultThumbnail);
        
        // UI更新の制限用
        private static readonly System.Threading.Timer _uiUpdateTimer;
        private static readonly ConcurrentQueue<MangaFile> _pendingUpdates = new();
        
        // 重複読み込み防止用（進行中の読み込み操作を追跡）
        private static readonly ConcurrentDictionary<int, Task> _loadingTasks = new();
        
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
                var thumbnailMode = _configManager.Value.GetSetting<ThumbnailDisplayMode>("ThumbnailDisplay", ThumbnailDisplayMode.Standard);
                if (thumbnailMode == ThumbnailDisplayMode.Hidden)
                {
                    return _defaultThumbnail.Value;
                }

                // IDベースのキャッシュキー生成（最適化されたメソッド使用）
                var cacheKey = ThumbnailMemoryCache.GenerateIdBasedKey(mangaFile.Id);

                // メモリキャッシュから高速取得
                if (_memoryCache.TryGet(cacheKey, out var cachedImage) && cachedImage != null)
                {
                    return cachedImage;
                }

                // 軽量読み込み対応: ThumbnailDataがnullの場合の遅延読み込み
                if (mangaFile.ThumbnailData == null)
                {
                    // ThumbnailGeneratedプロパティでサムネイル存在を判定
                    if (mangaFile.ThumbnailGenerated.HasValue)
                    {
                        // 非同期でDBからサムネイルデータを読み込み（UIをブロックしない）
                        _ = Task.Run(async () =>
                        {
                            await LoadThumbnailFromDatabaseAsync(mangaFile, cacheKey);
                        });
                    }
                    
                    // 読み込み中はデフォルトサムネイルを表示
                    return _defaultThumbnail.Value;
                }

                // バイナリデータから画像を読み込み（従来の処理）
                if (mangaFile.ThumbnailData.Length > 0)
                {
                    var image = CreateBitmapImageFromBytes(mangaFile.ThumbnailData);
                    if (image != null)
                    {
                        _memoryCache.Add(cacheKey, image);
                        return image;
                    }
                }

                return _defaultThumbnail.Value;
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
        /// データベースからサムネイルデータを非同期で読み込み、キャッシュに追加します
        /// 重複読み込み防止とエラーハンドリングを含む完全な実装
        /// </summary>
        private static async Task LoadThumbnailFromDatabaseAsync(MangaFile mangaFile, string cacheKey)
        {
            var fileId = mangaFile.Id;
            
            try
            {
                // 重複読み込み防止: 既にキャッシュに存在する場合はスキップ
                if (_memoryCache.TryGet(cacheKey, out var existingImage) && existingImage != null)
                {
                    return;
                }

                // 重複読み込み防止: 既に同じIDで読み込み中の場合は既存のタスクを待機
                if (_loadingTasks.TryGetValue(fileId, out var existingTask))
                {
                    await existingTask;
                    return;
                }

                // 新しい読み込みタスクを作成して登録
                var loadingTask = LoadThumbnailFromDatabaseInternalAsync(mangaFile, cacheKey);
                
                if (_loadingTasks.TryAdd(fileId, loadingTask))
                {
                    try
                    {
                        await loadingTask;
                    }
                    finally
                    {
                        // 完了後にタスクを削除
                        _loadingTasks.TryRemove(fileId, out _);
                    }
                }
                else
                {
                    // 他のスレッドが既に追加した場合は、そのタスクを待機
                    if (_loadingTasks.TryGetValue(fileId, out var concurrentTask))
                    {
                        await concurrentTask;
                    }
                }
            }
            catch (Exception ex)
            {
                // エラー時もタスクを削除
                _loadingTasks.TryRemove(fileId, out _);
                
                // エラーログを出力
                System.Diagnostics.Debug.WriteLine($"サムネイル読み込みエラー (ID: {fileId}): {ex.Message}");
            }
        }

        /// <summary>
        /// 実際のデータベース読み込み処理（内部メソッド）
        /// </summary>
        private static async Task LoadThumbnailFromDatabaseInternalAsync(MangaFile mangaFile, string cacheKey)
        {
            try
            {
                // 最終チェック: キャッシュに既に存在する場合はスキップ
                if (_memoryCache.TryGet(cacheKey, out var cachedImage) && cachedImage != null)
                {
                    return;
                }

                // MangaRepositoryサービスを取得
                var repository = App.ServiceProvider.GetRequiredService<IMangaRepository>();
                
                // データベースからサムネイルデータを取得
                var thumbnailData = await repository.GetThumbnailDataByIdAsync(mangaFile.Id);
                
                if (thumbnailData != null && thumbnailData.Length > 0)
                {
                    // バイナリデータから画像を作成
                    var image = CreateBitmapImageFromBytes(thumbnailData);
                    if (image != null)
                    {
                        // キャッシュに追加（重複チェック付き）
                        if (!_memoryCache.TryGet(cacheKey, out var finalCheck) || finalCheck == null)
                        {
                            _memoryCache.Add(cacheKey, image);
                            
                            // UI更新をキューに追加
                            _pendingUpdates.Enqueue(mangaFile);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 詳細なエラーログ
                System.Diagnostics.Debug.WriteLine($"データベースサムネイル読み込み失敗 (ID: {mangaFile.Id}, Path: {mangaFile.FilePath}): {ex.Message}");
                throw; // 上位レベルでハンドリング
            }
        }

        /// <summary>
        /// バイナリデータからBitmapImageを作成
        /// </summary>
        private static BitmapImage? CreateBitmapImageFromBytes(byte[] imageData)
        {
            try
            {
                using var stream = new MemoryStream(imageData);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 480; // 現在の表示サイズを維持
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
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



        public static void ClearCache()
        {
            _memoryCache.Clear();
            
            // 進行中の読み込みタスクもクリア
            _loadingTasks.Clear();
        }

        public static void OnSettingsChanged()
        {
            ClearCache();
        }

        /// <summary>
        /// メモリ使用量を最適化します（300件環境用に強化）
        /// </summary>
        public static void OptimizeMemoryUsage()
        {
            // 300件環境では、メモリ使用量が10MBを超えた場合に自動最適化
            _memoryCache.AutoOptimizeIfNeeded(10);
        }

        /// <summary>
        /// キャッシュ統計を取得します
        /// </summary>
        public static (int Count, int MaxSize, double HitRate) GetCacheStats()
        {
            return (_memoryCache.Count, _memoryCache.MaxSize, _memoryCache.HitRate);
        }

        /// <summary>
        /// 詳細なキャッシュ統計情報を取得します（300件環境用の拡張監視）
        /// </summary>
        public static CacheStatistics GetDetailedCacheStatistics()
        {
            return _memoryCache.GetDetailedStatistics();
        }

        /// <summary>
        /// メモリ使用量の自動最適化を実行します（300件環境用）
        /// </summary>
        /// <param name="maxMemoryMB">最大メモリ使用量（MB）、デフォルト10MB</param>
        /// <returns>最適化が実行された場合はtrue</returns>
        public static bool AutoOptimizeMemoryIfNeeded(int maxMemoryMB = 10)
        {
            return _memoryCache.AutoOptimizeIfNeeded(maxMemoryMB);
        }

        /// <summary>
        /// 推定メモリ使用量を取得します（300件環境用の監視）
        /// </summary>
        /// <returns>推定メモリ使用量（MB）</returns>
        public static double GetEstimatedMemoryUsageMB()
        {
            return _memoryCache.GetEstimatedMemoryUsage() / (1024.0 * 1024.0);
        }

        /// <summary>
        /// 現在進行中の読み込みタスク数を取得します（デバッグ用）
        /// </summary>
        /// <returns>進行中の読み込みタスク数</returns>
        public static int GetActiveLoadingTasksCount()
        {
            return _loadingTasks.Count;
        }

        /// <summary>
        /// DB読み込み統計情報を取得します
        /// </summary>
        /// <returns>統計情報（キャッシュ統計 + 読み込みタスク数）</returns>
        public static (int CacheCount, int MaxCacheSize, double HitRate, int ActiveLoadingTasks) GetLoadingStatistics()
        {
            var cacheStats = GetCacheStats();
            return (cacheStats.Count, cacheStats.MaxSize, cacheStats.HitRate, _loadingTasks.Count);
        }
    }
}
