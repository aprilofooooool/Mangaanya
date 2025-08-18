using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Mangaanya.ViewModels;

namespace Mangaanya.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;
        private bool _isPasswordVisible = false;
        private string _apiKeyCache = string.Empty;

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            
            // 初期値の設定
            InitializeComboBoxes();
            
            // APIキーの設定
            _apiKeyCache = _viewModel.GeminiApiKey;
            ApiKeyPasswordBox.Password = _apiKeyCache;
            
            // APIキーの変更イベント
            ApiKeyPasswordBox.PasswordChanged += ApiKeyPasswordBox_PasswordChanged;
            
            // APIキーが空でない場合は、マスク表示
            if (!string.IsNullOrEmpty(_apiKeyCache))
            {
                ApiKeyPasswordBox.Password = new string('•', 12); // マスク表示
                _isPasswordVisible = false;
            }
            
            // ウィンドウが閉じられる前のイベント
            Closing += SettingsWindow_Closing;
        }

        private void InitializeComboBoxes()
        {
            // メモリ使用上限の初期選択
            long maxMemory = _viewModel.MaxMemoryUsage;
            double memoryGB = maxMemory / (1024.0 * 1024 * 1024);
            MemorySlider.Value = Math.Max(1, Math.Min(16, Math.Round(memoryGB)));
            
            // キャッシュサイズの初期選択
            long cacheSize = _viewModel.CacheSize;
            double cacheGB = cacheSize / (1024.0 * 1024 * 1024);
            CacheSlider.Value = Math.Max(0.25, Math.Min(4, Math.Round(cacheGB * 4) / 4));
            
            // 同時AI処理数の初期選択
            int maxRequests = _viewModel.MaxConcurrentAIRequests;
            AIRequestsSlider.Value = Math.Max(10, Math.Min(100, maxRequests));
        }

        private void MemorySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_viewModel != null)
            {
                // GB単位の値をバイト単位に変換
                long bytes = (long)(e.NewValue * 1024 * 1024 * 1024);
                _viewModel.MaxMemoryUsage = bytes;
            }
        }

        private void CacheSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_viewModel != null)
            {
                // GB単位の値をバイト単位に変換
                long bytes = (long)(e.NewValue * 1024 * 1024 * 1024);
                _viewModel.CacheSize = bytes;
            }
        }

        private void AIRequestsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_viewModel != null)
            {
                _viewModel.MaxConcurrentAIRequests = (int)e.NewValue;
            }
        }

        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // マスク表示の場合は値を更新しない
            if (!_isPasswordVisible && ApiKeyPasswordBox.Password.All(c => c == '•'))
            {
                return;
            }
            
            _apiKeyCache = ApiKeyPasswordBox.Password;
            _viewModel.GeminiApiKey = _apiKeyCache;
        }

        private void ShowApiKey_Click(object sender, RoutedEventArgs e)
        {
            if (_isPasswordVisible)
            {
                // パスワードを隠す
                ApiKeyPasswordBox.Password = new string('•', 12); // マスク表示
                ((System.Windows.Controls.Button)sender).Content = "表示";
            }
            else
            {
                // パスワードを表示
                ApiKeyPasswordBox.Password = _apiKeyCache;
                ((System.Windows.Controls.Button)sender).Content = "隠す";
            }
            
            _isPasswordVisible = !_isPasswordVisible;
        }

        private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 確認ダイアログは CancelCommand で処理するため、ここでは何もしない
        }
    }
}
