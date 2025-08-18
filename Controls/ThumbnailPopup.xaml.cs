using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Mangaanya.Models;
using Mangaanya.Services;

namespace Mangaanya.Controls
{
    /// <summary>
    /// サムネイルポップアップ用のユーザーコントロール
    /// </summary>
    public partial class ThumbnailPopup : System.Windows.Controls.UserControl
    {
        private DispatcherTimer _hideTimer;
        private MangaFile? _currentMangaFile;
        private IConfigurationManager? _configManager;

        public ThumbnailPopup()
        {
            InitializeComponent();
            
            // 設定マネージャーを取得
            try
            {
                var app = System.Windows.Application.Current as App;
                _configManager = app?.Services.GetService<IConfigurationManager>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定マネージャーの取得に失敗しました: {ex.Message}");
            }
            
            // 自動非表示タイマーを初期化
            _hideTimer = new DispatcherTimer();
            _hideTimer.Tick += HideTimer_Tick;
            UpdateHideTimerInterval();
        }

        /// <summary>
        /// サムネイルを表示します
        /// </summary>
        /// <param name="mangaFile">表示するマンガファイル</param>
        public void ShowThumbnail(MangaFile mangaFile)
        {
            if (mangaFile == null) return;

            _currentMangaFile = mangaFile;
            
            // ファイル情報を設定
            FileNameText.Text = mangaFile.FileName ?? "不明なファイル";
            FileSizeText.Text = mangaFile.FileSizeFormatted ?? "";

            // サムネイル画像を読み込み
            LoadThumbnailImage(mangaFile);

            // 表示状態にする
            Visibility = Visibility.Visible;
            
            // 設定から最新の自動非表示時間を取得
            UpdateHideTimerInterval();
            
            // 自動非表示タイマーを開始
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        /// <summary>
        /// ポップアップを非表示にします
        /// </summary>
        public void HidePopup()
        {
            Visibility = Visibility.Collapsed;
            _hideTimer.Stop();
            
            // 画像リソースをクリア
            ThumbnailImage.Source = null;
            _currentMangaFile = null;
        }

        /// <summary>
        /// マウスがポップアップ上にある間は非表示タイマーをリセット
        /// </summary>
        public void ResetHideTimer()
        {
            if (_hideTimer.IsEnabled)
            {
                _hideTimer.Stop();
                _hideTimer.Start();
            }
        }

        private async void LoadThumbnailImage(MangaFile mangaFile)
        {
            try
            {
                // まず読み込み中画像を表示
                ShowLoadingImage();

                // LazyThumbnailConverterOptimizedを使用してサムネイル画像を取得
                var converter = new Mangaanya.Converters.LazyThumbnailConverterOptimized();
                var result = converter.Convert(mangaFile, typeof(BitmapImage), null!, System.Globalization.CultureInfo.CurrentCulture);

                if (result is BitmapImage bitmapImage)
                {
                    ThumbnailImage.Source = bitmapImage;
                    
                    // サムネイルが"Loading..."画像の場合は、生成完了を待つ
                    if (IsLoadingImage(bitmapImage))
                    {
                        // 非同期でサムネイル生成完了を待機
                        await WaitForThumbnailGeneration(mangaFile);
                    }
                }
                else
                {
                    ShowDefaultImage();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"サムネイル読み込みエラー: {ex.Message}");
                ShowDefaultImage();
            }
        }

        private async Task WaitForThumbnailGeneration(MangaFile mangaFile)
        {
            try
            {
                // 最大10秒間、0.5秒間隔でサムネイル生成完了をチェック
                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(500);
                    
                    // 現在のMangaFileと異なる場合は処理を中断
                    if (_currentMangaFile != mangaFile)
                        return;

                    // サムネイルバイナリデータが生成されているかチェック
                    if (mangaFile.HasThumbnail)
                    {
                        // サムネイル画像をバイナリデータから再読み込み
                        var bitmap = CreateBitmapImageFromBytes(mangaFile.ThumbnailData!);

                        // UIスレッドで画像を更新
                        Dispatcher.Invoke(() =>
                        {
                            if (_currentMangaFile == mangaFile) // 再度チェック
                            {
                                ThumbnailImage.Source = bitmap;
                            }
                        });
                        
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"サムネイル生成待機エラー: {ex.Message}");
            }
        }

        private BitmapImage CreateBitmapImageFromBytes(byte[] imageData)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(imageData);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private bool IsLoadingImage(BitmapImage image)
        {
            try
            {
                // 画像サイズや特徴から"Loading..."画像かどうかを判定
                // 簡易的な判定として、600x400サイズで特定の特徴を持つ画像を"Loading..."とみなす
                return image.PixelWidth == 600 && image.PixelHeight == 400;
            }
            catch
            {
                return false;
            }
        }

        private void ShowLoadingImage()
        {
            try
            {
                // 読み込み中画像を作成
                var visual = new System.Windows.Media.DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    var rect = new Rect(0, 0, 400, 300);
                    var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.LightBlue, 2);
                    pen.DashStyle = System.Windows.Media.DashStyles.Dash;
                    context.DrawRectangle(System.Windows.Media.Brushes.AliceBlue, pen, rect);
                    
                    var text = new System.Windows.Media.FormattedText(
                        "読み込み中...",
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Arial"),
                        20,
                        System.Windows.Media.Brushes.DodgerBlue,
                        96);
                    
                    var textX = (400 - text.Width) / 2;
                    var textY = (300 - text.Height) / 2;
                    context.DrawText(text, new System.Windows.Point(textX, textY));
                }

                var renderBitmap = new RenderTargetBitmap(400, 300, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderBitmap.Render(visual);
                renderBitmap.Freeze();

                ThumbnailImage.Source = renderBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"読み込み中画像作成エラー: {ex.Message}");
                ShowDefaultImage();
            }
        }

        private void ShowDefaultImage()
        {
            try
            {
                // デフォルト画像を作成
                var visual = new System.Windows.Media.DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    var rect = new Rect(0, 0, 400, 300);
                    var pen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Gray, 2);
                    context.DrawRectangle(System.Windows.Media.Brushes.LightGray, pen, rect);
                    
                    var text = new System.Windows.Media.FormattedText(
                        "サムネイルなし",
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface("Arial"),
                        24,
                        System.Windows.Media.Brushes.DarkGray,
                        96);
                    
                    var textX = (400 - text.Width) / 2;
                    var textY = (300 - text.Height) / 2;
                    context.DrawText(text, new System.Windows.Point(textX, textY));
                }

                var renderBitmap = new RenderTargetBitmap(400, 300, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                renderBitmap.Render(visual);
                renderBitmap.Freeze();

                ThumbnailImage.Source = renderBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"デフォルト画像作成エラー: {ex.Message}");
            }
        }

        private void HideTimer_Tick(object? sender, EventArgs e)
        {
            HidePopup();
        }

        /// <summary>
        /// 設定から自動非表示時間を取得してタイマー間隔を更新します
        /// </summary>
        private void UpdateHideTimerInterval()
        {
            try
            {
                // 設定から自動非表示時間を取得（デフォルト3秒）
                var hideDelaySeconds = _configManager?.GetSetting<double>("ThumbnailPopupHideDelay", 3.0) ?? 3.0;
                _hideTimer.Interval = TimeSpan.FromSeconds(hideDelaySeconds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"自動非表示時間の設定取得に失敗しました: {ex.Message}");
                _hideTimer.Interval = TimeSpan.FromSeconds(3.0); // デフォルト値
            }
        }

        /// <summary>
        /// 設定変更時に呼び出されて、設定を再読み込みします
        /// </summary>
        public void OnSettingsChanged()
        {
            try
            {
                // タイマー間隔を更新
                UpdateHideTimerInterval();
                
                // 現在表示中の場合は一度非表示にして、次回表示時に新しい設定を適用
                if (Visibility == Visibility.Visible)
                {
                    HidePopup();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ポップアップ設定変更の適用に失敗しました: {ex.Message}");
            }
        }

        protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            ResetHideTimer();
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            // マウスが離れたら少し遅延して非表示
            _hideTimer.Stop();
            
            // 設定から離脱時の遅延時間を取得（デフォルト0.5秒）
            var leaveDelaySeconds = _configManager?.GetSetting<double>("ThumbnailPopupLeaveDelay", 0.5) ?? 0.5;
            _hideTimer.Interval = TimeSpan.FromSeconds(leaveDelaySeconds);
            _hideTimer.Start();
        }
    }
}
