using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mangaanya.Services;
using Mangaanya.Models;
using Mangaanya.Configuration;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Collections.Generic;

namespace Mangaanya.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly IConfigurationManager _config;
        private readonly IDialogService _dialogService;
        private readonly ISettingsChangedNotifier _settingsChangedNotifier;
        private readonly ISystemSoundService _systemSoundService;
        private readonly IMangaRepository _repository;

        [ObservableProperty]
        private long _maxMemoryUsage;

        [ObservableProperty]
        private long _cacheSize;

        [ObservableProperty]
        private int _maxConcurrentAIRequests;

        [ObservableProperty]
        private bool _showThumbnails;

        [ObservableProperty]
        private ThumbnailDisplayMode _thumbnailDisplayMode;

        [ObservableProperty]
        private bool _isStandardThumbnailEnabled;

        [ObservableProperty]
        private bool _isCompactThumbnailEnabled;

        [ObservableProperty]
        private string _geminiApiKey = string.Empty;
        
        [ObservableProperty]
        private string _fileNameRegexPattern = string.Empty;
        
        [ObservableProperty]
        private string _mangaViewerPath = string.Empty;
        
        [ObservableProperty]
        private ObservableCollection<ColumnVisibilityItem> _columnSettings = new();

        // サムネイル関連設定
        [ObservableProperty]
        private double _thumbnailPopupHideDelay;

        [ObservableProperty]
        private double _thumbnailPopupLeaveDelay;

        [ObservableProperty]
        private bool _isModified;

        /// <summary>
        /// 設定が適用されたかどうかを示すフラグ
        /// </summary>
        public bool WasApplied { get; private set; }

        /// <summary>
        /// ステータスメッセージ更新用のアクション
        /// </summary>
        public Action<string>? UpdateStatusMessage { get; set; }

        // フォントサイズ設定
        [ObservableProperty]
        private double _uiFontSize;

        [ObservableProperty]
        private double _buttonFontSize;

        // データベース最適化関連
        [ObservableProperty]
        private bool _isNotOptimizing = true;

        [ObservableProperty]
        private string _optimizationStatus = string.Empty;

        [ObservableProperty]
        private string _databaseOptimizationResult = string.Empty;

        [ObservableProperty]
        private Visibility _databaseOptimizationResultVisibility = Visibility.Collapsed;

        public SettingsViewModel(
            ILogger<SettingsViewModel> logger,
            IConfigurationManager config,
            IDialogService dialogService,
            ISettingsChangedNotifier settingsChangedNotifier,
            ISystemSoundService systemSoundService,
            IMangaRepository repository)
        {
            _logger = logger;
            _config = config;
            _dialogService = dialogService;
            _settingsChangedNotifier = settingsChangedNotifier;
            _systemSoundService = systemSoundService;
            _repository = repository;

            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                MaxMemoryUsage = _config.GetSetting(Configuration.AppSettings.MaxMemoryUsage);
                CacheSize = _config.GetSetting(Configuration.AppSettings.CacheSize);
                MaxConcurrentAIRequests = _config.GetSetting(Configuration.AppSettings.MaxConcurrentAIRequests);
                ShowThumbnails = _config.GetSetting(Configuration.AppSettings.ShowThumbnails);
                ThumbnailDisplayMode = _config.GetSetting(Configuration.AppSettings.ThumbnailDisplay);
                
                // チェックボックスの初期状態を設定
                IsStandardThumbnailEnabled = ThumbnailDisplayMode == ThumbnailDisplayMode.Standard;
                IsCompactThumbnailEnabled = ThumbnailDisplayMode == ThumbnailDisplayMode.Compact;
                GeminiApiKey = _config.GetSetting<string>("GeminiApiKey", string.Empty) ?? string.Empty;
                MangaViewerPath = _config.GetSetting<string>("MangaViewerPath", string.Empty) ?? string.Empty;
                
                // デフォルトの正規表現パターン
                var defaultRegexPattern = @"\[一般コミック\]\s*\[([あ-んア-ンーA-Za-z]+)\]\s*\[([^×\]]+)(?:×([^\]]+))?\]\s*(.+?)(?:\s+第(\d+(?:-\d+)?)巻|\s+第0*(\d+(?:-\d+)?)巻)(?:\s*.*?)?(?:\.[^\.]+)?$";
                FileNameRegexPattern = _config.GetSetting<string>("FileNameRegexPattern", defaultRegexPattern) ?? defaultRegexPattern;
                
                // カラム表示設定を読み込む
                LoadColumnSettings();

                // サムネイル関連設定を読み込む
                ThumbnailPopupHideDelay = _config.GetSetting<double>("ThumbnailPopupHideDelay", 3.0);
                ThumbnailPopupLeaveDelay = _config.GetSetting<double>("ThumbnailPopupLeaveDelay", 0.5);

                // フォントサイズ設定を読み込む
                UiFontSize = _config.GetSetting<double>("UIFontSize", 12.0);
                ButtonFontSize = _config.GetSetting<double>("ButtonFontSize", 12.0);

                IsModified = false;
                _logger.LogInformation("設定を読み込みました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定の読み込み中にエラーが発生しました");
                _dialogService.ShowError("設定の読み込み中にエラーが発生しました: " + ex.Message);
            }
        }

        [RelayCommand]
        private async Task SaveSettingsAsync()
        {
            try
            {
                _config.SetSetting("MaxMemoryUsage", MaxMemoryUsage);
                _config.SetSetting("CacheSize", CacheSize);
                _config.SetSetting("MaxConcurrentAIRequests", MaxConcurrentAIRequests);
                _config.SetSetting("ShowThumbnails", ShowThumbnails);
                _config.SetSetting("ThumbnailDisplay", ThumbnailDisplayMode);
                _config.SetSetting("GeminiApiKey", GeminiApiKey);
                _config.SetSetting("MangaViewerPath", MangaViewerPath);
                _config.SetSetting("FileNameRegexPattern", FileNameRegexPattern);
                
                // カラム表示設定を保存
                SaveColumnSettings();
                
                // サムネイル関連設定を保存
                _config.SetSetting("ThumbnailPopupHideDelay", ThumbnailPopupHideDelay);
                _config.SetSetting("ThumbnailPopupLeaveDelay", ThumbnailPopupLeaveDelay);

                // フォントサイズ設定を保存
                _config.SetSetting("UIFontSize", UiFontSize);
                _config.SetSetting("ButtonFontSize", ButtonFontSize);

                await _config.SaveAsync();

                // 設定変更を通知
                _settingsChangedNotifier.NotifySettingsChanged("DataGridSettings");
                _settingsChangedNotifier.NotifySettingsChanged("ShowThumbnails");
                _settingsChangedNotifier.NotifySettingsChanged("ThumbnailDisplay");
                _settingsChangedNotifier.NotifySettingsChanged("ThumbnailSettings");
                _settingsChangedNotifier.NotifySettingsChanged("FontSettings");

                IsModified = false;
                WasApplied = true;
                _logger.LogInformation("設定を保存しました");
                
                // ステータスメッセージを更新
                UpdateStatusMessage?.Invoke("設定を適用しました");
                
                // 設定適用音を再生
                _logger.LogInformation("設定適用音を再生します");
                _systemSoundService.PlaySettingsAppliedSound();
                _logger.LogInformation("設定適用音の再生を完了しました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定の保存中にエラーが発生しました");
                _dialogService.ShowError("設定の保存中にエラーが発生しました: " + ex.Message);
                
                // ステータスメッセージを更新
                UpdateStatusMessage?.Invoke("設定の保存に失敗しました");
                
                // エラー音を再生
                _systemSoundService.PlayErrorSound();
            }
        }

        [RelayCommand]
        private void ResetSettings()
        {
            if (_dialogService.ShowConfirmation("設定をデフォルト値にリセットしますか？"))
            {
                MaxMemoryUsage = 8L * 1024 * 1024 * 1024; // 8GB
                CacheSize = 1024 * 1024 * 1024; // 1GB
                MaxConcurrentAIRequests = 30;
                ShowThumbnails = true;
                ThumbnailDisplayMode = ThumbnailDisplayMode.Standard;
                GeminiApiKey = string.Empty;
                MangaViewerPath = string.Empty;
                
                // デフォルトの正規表現パターンに戻す
                var defaultRegexPattern = @"\[一般コミック\]\s*\[([あ-んア-ンーA-Za-z]+)\]\s*\[([^×\]]+)(?:×([^\]]+))?\]\s*(.+?)(?:\s+第(\d+(?:-\d+)?)巻|\s+第0*(\d+(?:-\d+)?)巻)(?:\s*.*?)?(?:\.[^\.]+)?$";
                FileNameRegexPattern = defaultRegexPattern;
                
                // カラム表示設定をデフォルトに戻す
                ResetColumnSettings();
                
                // サムネイル関連設定をデフォルトに戻す
                ThumbnailPopupHideDelay = 3.0;
                ThumbnailPopupLeaveDelay = 0.5;

                // フォントサイズをデフォルトに戻す
                UiFontSize = 12.0;
                ButtonFontSize = 12.0;

                IsModified = true;
            }
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            if (IsModified)
            {
                var result = Mangaanya.Views.CustomMessageBox.Show(
                    "変更を保存しますか？",
                    "設定の保存",
                    Mangaanya.Views.CustomMessageBoxButton.YesNoCancel);
                
                if (result == Mangaanya.Views.CustomMessageBoxResult.Yes)
                {
                    SaveSettingsCommand.Execute(null);
                    window.Close();
                }
                else if (result == Mangaanya.Views.CustomMessageBoxResult.No)
                {
                    window.Close();
                }
                // Cancel の場合は何もしない（ウィンドウを閉じない）
            }
            else
            {
                window.Close();
            }
        }

        partial void OnMaxMemoryUsageChanged(long value)
        {
            IsModified = true;
        }

        partial void OnCacheSizeChanged(long value)
        {
            IsModified = true;
        }

        partial void OnMaxConcurrentAIRequestsChanged(int value)
        {
            IsModified = true;
        }

        partial void OnShowThumbnailsChanged(bool value)
        {
            IsModified = true;
        }

        partial void OnThumbnailDisplayModeChanged(ThumbnailDisplayMode value)
        {
            IsStandardThumbnailEnabled = value == ThumbnailDisplayMode.Standard;
            IsCompactThumbnailEnabled = value == ThumbnailDisplayMode.Compact;
            IsModified = true;
        }

        partial void OnIsStandardThumbnailEnabledChanged(bool value)
        {
            if (value)
            {
                ThumbnailDisplayMode = ThumbnailDisplayMode.Standard;
                IsCompactThumbnailEnabled = false;
            }
            else if (!IsCompactThumbnailEnabled)
            {
                ThumbnailDisplayMode = ThumbnailDisplayMode.Hidden;
            }
        }

        partial void OnIsCompactThumbnailEnabledChanged(bool value)
        {
            if (value)
            {
                ThumbnailDisplayMode = ThumbnailDisplayMode.Compact;
                IsStandardThumbnailEnabled = false;
            }
            else if (!IsStandardThumbnailEnabled)
            {
                ThumbnailDisplayMode = ThumbnailDisplayMode.Hidden;
            }
        }

        partial void OnGeminiApiKeyChanged(string value)
        {
            IsModified = true;
        }
        
        partial void OnFileNameRegexPatternChanged(string value)
        {
            IsModified = true;
        }
        
        partial void OnMangaViewerPathChanged(string value)
        {
            IsModified = true;
        }
        
        private DataGridSettings GetDefaultDataGridSettings()
        {
            return new DataGridSettings
            {
                Columns = new List<DataGridColumnSettings>
                {
                    new DataGridColumnSettings { Header = "サムネイル", Width = 190, DisplayIndex = 0, IsVisible = true },
                    new DataGridColumnSettings { Header = "パス", Width = 300, DisplayIndex = 1, IsVisible = true },
                    new DataGridColumnSettings { Header = "ファイル名", Width = 200, DisplayIndex = 2, IsVisible = true },
                    new DataGridColumnSettings { Header = "サイズ", Width = 80, DisplayIndex = 3, IsVisible = true },
                    new DataGridColumnSettings { Header = "種類", Width = 60, DisplayIndex = 4, IsVisible = true },
                    new DataGridColumnSettings { Header = "作成日時", Width = 120, DisplayIndex = 5, IsVisible = true },
                    new DataGridColumnSettings { Header = "更新日時", Width = 120, DisplayIndex = 6, IsVisible = true },
                    new DataGridColumnSettings { Header = "かな", Width = 100, DisplayIndex = 7, IsVisible = true },
                    new DataGridColumnSettings { Header = "原作者", Width = 100, DisplayIndex = 8, IsVisible = true },
                    new DataGridColumnSettings { Header = "作画者", Width = 100, DisplayIndex = 9, IsVisible = true },
                    new DataGridColumnSettings { Header = "タイトル", Width = 150, DisplayIndex = 10, IsVisible = true },
                    new DataGridColumnSettings { Header = "巻数", Width = 60, DisplayIndex = 11, IsVisible = true },
                    new DataGridColumnSettings { Header = "タグ", Width = 200, DisplayIndex = 12, IsVisible = false },
                    new DataGridColumnSettings { Header = "ジャンル", Width = 100, DisplayIndex = 13, IsVisible = false },
                    new DataGridColumnSettings { Header = "出版社", Width = 100, DisplayIndex = 14, IsVisible = false },
                    new DataGridColumnSettings { Header = "発行日", Width = 100, DisplayIndex = 15, IsVisible = false },
                    new DataGridColumnSettings { Header = "評価", Width = 80, DisplayIndex = 16, IsVisible = true },
                    new DataGridColumnSettings { Header = "タグ取得済", Width = 80, DisplayIndex = 17, IsVisible = false }
                }
            };
        }

        private void LoadColumnSettings()
        {
            try
            {
                // DataGridSettingsから設定を読み込む
                var dataGridSettings = _config.GetSetting<DataGridSettings>("DataGridSettings", new DataGridSettings());
                
                // settings.jsonがない場合、またはDataGridSettingsが空の場合はデフォルト設定を使用
                if (dataGridSettings.Columns.Count == 0)
                {
                    _logger.LogInformation("DataGridSettings設定が見つかりません。デフォルト値を使用します。");
                    dataGridSettings = GetDefaultDataGridSettings();
                }
                else
                {
                    _logger.LogInformation("DataGridSettings設定を読み込みました: {Count}列", dataGridSettings.Columns.Count);
                }
                
                // ObservableCollectionに変換（DataGridSettingsから）
                ColumnSettings.Clear();
                foreach (var columnSetting in dataGridSettings.Columns)
                {
                    // サムネイルは ShowThumbnails 設定で管理されるため、ここには含めない
                    if (columnSetting.Header == "サムネイル")
                        continue;
                        
                    var item = new ColumnVisibilityItem(columnSetting.Header, columnSetting.IsVisible);
                    // PropertyChangedイベントを監視して自動保存
                    item.PropertyChanged += OnColumnVisibilityItemChanged;
                    ColumnSettings.Add(item);
                    
                    _logger.LogInformation("列設定を読み込み: {Column} = {Visible}", columnSetting.Header, columnSetting.IsVisible);
                }
                
                _logger.LogInformation("列表示設定の読み込み完了: {Count}項目", ColumnSettings.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "カラム表示設定の読み込み中にエラーが発生しました");
            }
        }
        
        private void OnColumnVisibilityItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ColumnVisibilityItem.IsVisible))
            {
                // 自動保存は行わず、変更フラグのみ設定
                IsModified = true;
                _logger.LogInformation("列表示設定が変更されました（未保存）");
            }
        }
        
        private async Task SaveColumnSettingsAsync()
        {
            try
            {
                // 現在のDataGridSettingsを読み込み
                var dataGridSettings = _config.GetSetting<DataGridSettings>("DataGridSettings", new DataGridSettings());
                
                // 設定が空の場合はデフォルト設定をベースにする
                if (dataGridSettings.Columns.Count == 0)
                {
                    dataGridSettings = GetDefaultDataGridSettings();
                }
                
                // ColumnSettingsの変更をDataGridSettingsに反映
                foreach (var item in ColumnSettings)
                {
                    var columnSetting = dataGridSettings.Columns.FirstOrDefault(c => c.Header == item.Header);
                    if (columnSetting != null)
                    {
                        columnSetting.IsVisible = item.IsVisible;
                        _logger.LogInformation("列設定を更新: {Column} = {Visible}", item.Header, item.IsVisible);
                    }
                }
                
                // DataGridSettingsを保存
                _config.SetSetting("DataGridSettings", dataGridSettings);
                await _config.SaveAsync();
                
                _logger.LogInformation("DataGridSettings設定を保存しました: {Count}列", dataGridSettings.Columns.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "列表示設定の保存中にエラーが発生しました");
            }
        }
        
        private void SaveColumnSettings()
        {
            try
            {
                // 現在のDataGridSettingsを読み込み
                var dataGridSettings = _config.GetSetting<DataGridSettings>("DataGridSettings", new DataGridSettings());
                
                // 設定が空の場合はデフォルト設定をベースにする
                if (dataGridSettings.Columns.Count == 0)
                {
                    dataGridSettings = GetDefaultDataGridSettings();
                }
                
                // ColumnSettingsの変更をDataGridSettingsに反映
                foreach (var item in ColumnSettings)
                {
                    var columnSetting = dataGridSettings.Columns.FirstOrDefault(c => c.Header == item.Header);
                    if (columnSetting != null)
                    {
                        columnSetting.IsVisible = item.IsVisible;
                    }
                }
                
                // DataGridSettingsを保存（同期版 - SaveSettingsAsync内で使用）
                _config.SetSetting("DataGridSettings", dataGridSettings);
                
                _logger.LogInformation("列表示設定を保存しました（適用ボタン経由）: {Count}列", dataGridSettings.Columns.Count);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "カラム表示設定の保存中にエラーが発生しました");
            }
        }
        
        private void ResetColumnSettings()
        {
            // すべてのカラムを表示に設定
            foreach (var item in ColumnSettings)
            {
                item.IsVisible = true;
            }
        }
        
        [RelayCommand]
        private void ResetRegex()
        {
            if (_dialogService.ShowConfirmation("正規表現パターンをデフォルトに戻しますか？"))
            {
                var defaultRegexPattern = @"\[一般コミック\]\s*\[([あ-んア-ンーA-Za-z]+)\]\s*\[([^×\]]+)(?:×([^\]]+))?\]\s*(.+?)(?:\s+第(\d+(?:-\d+)?)巻|\s+第0*(\d+(?:-\d+)?)巻)(?:\s*.*?)?(?:\.[^\.]+)?$";
                FileNameRegexPattern = defaultRegexPattern;
                IsModified = true;
            }
        }

        [RelayCommand]
        private void TestRegex()
        {
            try
            {
                // 正規表現の構文チェック
                var regex = new System.Text.RegularExpressions.Regex(FileNameRegexPattern);
                
                // テスト用のファイル名
                var testFileName = "[一般コミック] [あいうえお] [テスト作者×テスト作画] テストタイトル 第1巻 [追加情報].zip";
                var match = regex.Match(testFileName);
                
                if (match.Success)
                {
                    var result = $"テスト成功！\n\n";
                    result += $"テストファイル名: {testFileName}\n\n";
                    result += $"抽出結果:\n";
                    result += $"グループ1 (よみがな): {match.Groups[1].Value}\n";
                    result += $"グループ2 (原作者): {match.Groups[2].Value}\n";
                    result += $"グループ3 (作画者): {match.Groups[3].Value}\n";
                    result += $"グループ4 (タイトル): {match.Groups[4].Value}\n";
                    result += $"グループ5 (巻数1): {match.Groups[5].Value}\n";
                    result += $"グループ6 (巻数2): {match.Groups[6].Value}";
                    
                    _dialogService.ShowInformation(result);
                }
                else
                {
                    _dialogService.ShowInformation($"テストファイル名にマッチしませんでした。\n\nテストファイル名: {testFileName}\n\n正規表現パターンを確認してください。");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"正規表現エラー: {ex.Message}\n\n正規表現の構文を確認してください。");
            }
        }
        
        [RelayCommand]
        private void SelectMangaViewer()
        {
            try
            {
                var selectedFile = _dialogService.SelectFile("漫画ビューアアプリを選択してください", "実行ファイル (*.exe)|*.exe");
                if (!string.IsNullOrEmpty(selectedFile))
                {
                    MangaViewerPath = selectedFile;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "漫画ビューアアプリの選択中にエラーが発生しました");
                _dialogService.ShowError($"ファイル選択エラー: {ex.Message}");
            }
        }

        // メモリサイズを人間が読みやすい形式に変換するヘルパーメソッド
        public static string FormatMemorySize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        // 人間が読みやすい形式からバイト数に変換するヘルパーメソッド
        public static long ParseMemorySize(string formattedSize)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            string[] parts = formattedSize.Split(' ');
            
            if (parts.Length != 2)
                return 0;
                
            if (!double.TryParse(parts[0], out double value))
                return 0;
                
            int order = Array.IndexOf(sizes, parts[1].ToUpper());
            if (order < 0)
                return 0;
                
            return (long)(value * Math.Pow(1024, order));
        }

        [RelayCommand]
        private async Task OptimizeDatabaseAsync()
        {
            if (!_dialogService.ShowConfirmation(
                "データベースの最適化を実行しますか？\n\n" +
                "※この処理には時間がかかる場合があります。\n" +
                "※処理中はアプリケーションが応答しなくなる場合があります。"))
            {
                return;
            }

            IsNotOptimizing = false;
            OptimizationStatus = "最適化中...";
            DatabaseOptimizationResult = string.Empty;
            DatabaseOptimizationResultVisibility = Visibility.Collapsed;

            try
            {
                _logger.LogInformation("データベース最適化を開始します");

                var (beforeSize, afterSize) = await Task.Run(async () =>
                {
                    return await _repository.OptimizeDatabaseAsync();
                });

                var savedSize = beforeSize - afterSize;
                var savedPercentage = beforeSize > 0 ? (double)savedSize / beforeSize * 100 : 0;

                // シンプルな成功メッセージのみ表示
                DatabaseOptimizationResult = "データベースの最適化が完了しました！";
                DatabaseOptimizationResultVisibility = Visibility.Visible;
                OptimizationStatus = string.Empty;

                _logger.LogInformation("データベース最適化が完了しました。最適化前: {BeforeSize:N0} bytes, 最適化後: {AfterSize:N0} bytes, 削減サイズ: {SavedSize:N0} bytes ({SavedPercentage:F1}%)", 
                    beforeSize, afterSize, savedSize, savedPercentage);
                _systemSoundService.PlaySettingsAppliedSound();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "データベース最適化中にエラーが発生しました");
                
                DatabaseOptimizationResult = $"最適化に失敗しました: {ex.Message}";
                DatabaseOptimizationResultVisibility = Visibility.Visible;
                OptimizationStatus = string.Empty;
                
                _dialogService.ShowError($"データベース最適化中にエラーが発生しました:\n{ex.Message}");
                _systemSoundService.PlayErrorSound();
            }
            finally
            {
                IsNotOptimizing = true;
            }
        }
    }
}
