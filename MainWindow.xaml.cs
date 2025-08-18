using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using Mangaanya.Models;
using Mangaanya.ViewModels;
using Mangaanya.Views;
using Mangaanya.Services;
using Mangaanya.Constants;


namespace Mangaanya
{
    public partial class MainWindow : Window
    {
        private readonly IConfigurationManager? _configManager;
        private readonly ISettingsChangedNotifier? _settingsChangedNotifier;
        
        // 選択中のカラムを記録する変数
        private string? _selectedColumnHeader = null;
        
        // アプリケーション終了処理の状態管理
        private bool _isClosingInProgress = false;
        
        public MainWindow()
        {
            InitializeComponent();
            
            // サービスを取得
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app != null)
                {
                    _configManager = app.Services.GetRequiredService<IConfigurationManager>();
                    _settingsChangedNotifier = app.Services.GetRequiredService<ISettingsChangedNotifier>();
                    
                    // 設定変更通知を購読
                    _settingsChangedNotifier.SettingsChanged += SettingsChangedNotifier_SettingsChanged;
                }
            }
            catch (Exception)
            {
                // サービス取得エラーは無視（ログ出力なし）
            }
            
            // ウィンドウ位置を表示前に復元（ちらつき防止）
            SourceInitialized += MainWindow_SourceInitialized;
            
            // ウィンドウが読み込まれた後にDataGridの設定を適用
            Loaded += MainWindow_Loaded;
            
            // ウィンドウが閉じられる前にDataGridの設定を保存
            Closing += MainWindow_Closing;
            
            // ViewModelのイベントを購読
            DataContextChanged += MainWindow_DataContextChanged;
        }
        

        

        

        

        
        private async Task SaveAllSettingsOnClosingAsync()
        {
            try
            {
                if (_configManager == null) return;
                
                System.Diagnostics.Debug.WriteLine("アプリケーション終了時の設定保存を開始します");
                
                // ウィンドウ位置設定をConfigManagerに設定
                SaveWindowPositionToConfigManager();
                
                // DataGrid設定をConfigManagerに設定
                SaveDataGridSettingsToConfigManager();
                
                // タイムアウト付きで保存実行（5秒でタイムアウト）
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    try
                    {
                        await _configManager.SaveAsync();
                        System.Diagnostics.Debug.WriteLine("アプリケーション終了時の設定保存が完了しました");
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine("設定保存がタイムアウトしました");
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"アプリケーション終了時の設定保存に失敗しました: {ex.Message}");
                throw; // 例外を再スローして呼び出し元に伝える
            }
        }
        
        private DataGridSettings GetDefaultDataGridSettings()
        {
            return new DataGridSettings
            {
                Columns = new List<DataGridColumnSettings>
                {
                    new DataGridColumnSettings { Header = "サムネイル", Width = UIConstants.ColumnWidths.Thumbnail, DisplayIndex = 0, IsVisible = true },
                    new DataGridColumnSettings { Header = "パス", Width = UIConstants.ColumnWidths.Path, DisplayIndex = 1, IsVisible = true },
                    new DataGridColumnSettings { Header = "ファイル名", Width = UIConstants.ColumnWidths.FileName, DisplayIndex = 2, IsVisible = true },
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

        private void LoadDataGridSettings()
        {
            try
            {
                if (_configManager == null) return;
                
                // DataGridが初期化されているかチェック
                if (MangaFilesGrid == null || MangaFilesGrid.Columns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("DataGridがまだ初期化されていません。設定適用をスキップします。");
                    return;
                }
                
                var settings = _configManager.GetSetting<DataGridSettings>("DataGridSettings", new DataGridSettings());
                
                // settings.jsonがない場合、またはDataGridSettingsが空の場合はデフォルト設定を使用
                if (settings.Columns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("DataGridSettings設定がありません。デフォルト設定を適用します。");
                    settings = GetDefaultDataGridSettings();
                }
                else
                {
                    // DataGridSettings設定を読み込み
                }
                
                // DataGridSettingsのみを使用（ColumnVisibilityは廃止）
                foreach (var columnSetting in settings.Columns)
                {
                    var column = MangaFilesGrid.Columns.FirstOrDefault(c => 
                        (c.Header?.ToString() ?? string.Empty) == columnSetting.Header);
                    
                    if (column != null)
                    {
                        column.Width = new DataGridLength(columnSetting.Width);
                        column.Visibility = columnSetting.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                        
                        // 列設定を適用
                    }
                }
                    
                // DisplayIndexの設定を適用
                foreach (var columnSetting in settings.Columns)
                {
                    var column = MangaFilesGrid.Columns.FirstOrDefault(c => 
                        (c.Header?.ToString() ?? string.Empty) == columnSetting.Header);
                    
                    if (column != null)
                    {
                        try
                        {
                            column.DisplayIndex = columnSetting.DisplayIndex;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"DisplayIndexの設定に失敗しました: {ex.Message}");
                        }
                    }
                }
                
                // サムネイル表示設定を適用
                ApplyThumbnailVisibilitySettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataGridの設定読み込みに失敗しました: {ex.Message}");
            }
        }
        
        private void ApplyThumbnailVisibilitySettings()
        {
            try
            {
                if (_configManager == null) return;
                
                var showThumbnails = _configManager.GetSetting<bool>("ShowThumbnails", true);
                var thumbnailMode = _configManager.GetSetting<ThumbnailDisplayMode>("ThumbnailDisplay", ThumbnailDisplayMode.Standard);
                var thumbnailColumn = MangaFilesGrid.Columns.FirstOrDefault(c => 
                    (c.Header?.ToString() ?? string.Empty) == "サムネイル");
                
                if (thumbnailColumn != null)
                {
                    // サムネイル表示設定を適用中
                    
                    switch (thumbnailMode)
                    {
                        case ThumbnailDisplayMode.Hidden:
                            MangaFilesGrid.RowHeight = ThumbnailConstants.CompactRowHeight;
                            thumbnailColumn.Visibility = Visibility.Collapsed;
                            break;
                        case ThumbnailDisplayMode.Compact:
                            MangaFilesGrid.RowHeight = ThumbnailConstants.CompactRowHeight;  // 縮小時は25px
                            thumbnailColumn.Visibility = Visibility.Visible;
                            break;
                        case ThumbnailDisplayMode.Standard:
                            MangaFilesGrid.RowHeight = ThumbnailConstants.StandardRowHeight;  // 標準時は84px
                            thumbnailColumn.Visibility = Visibility.Visible;
                            break;
                    }
                    
                    // ViewModelの設定も更新
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.ShowThumbnails = showThumbnails;
                        viewModel.ThumbnailDisplayMode = thumbnailMode;
                    }
                    
                    // 強制的にレイアウトを更新
                    MangaFilesGrid.UpdateLayout();
                    MangaFilesGrid.InvalidateVisual();
                    
                    // サムネイル表示設定を適用完了
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"サムネイル表示設定の適用に失敗しました: {ex.Message}");
            }
        }
        
        private async void SaveDataGridSettings()
        {
            try
            {
                if (_configManager == null) return;
                
                SaveDataGridSettingsToConfigManager();
                await _configManager.SaveAsync();
                
                // DataGridの設定を保存
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataGridの設定保存に失敗しました: {ex.Message}");
            }
        }
        
        private void SaveDataGridSettingsToConfigManager()
        {
            try
            {
                if (_configManager == null) return;
                
                var settings = new DataGridSettings();
                
                foreach (var column in MangaFilesGrid.Columns)
                {
                    string header = column.Header?.ToString() ?? string.Empty;
                    bool isVisible = column.Visibility == Visibility.Visible;
                    
                    settings.Columns.Add(new DataGridColumnSettings
                    {
                        Header = header,
                        Width = column.ActualWidth,
                        DisplayIndex = column.DisplayIndex,
                        IsVisible = isVisible
                    });
                }
                
                _configManager.SetSetting("DataGridSettings", settings);
                
                // DataGrid設定を準備
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataGrid設定の準備に失敗しました: {ex.Message}");
                throw; // 例外を再スローして問題を明確にする
            }
        }
        

        

        

        
        private void MangaFilesGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    // Ctrl+Shift+C: 選択中のカラムの値のみをコピー
                    CopySelectedColumnToClipboard();
                }
                else
                {
                    // Ctrl+C: 従来通り行全体をコピー
                    CopySelectedCellsToClipboard();
                }
                e.Handled = true;
            }
        }
        
        private void CopySelectedCellsToClipboard()
        {
            try
            {
                // 行選択モードでは、選択された行の全データをタブ区切りでコピー
                var selectedItems = MangaFilesGrid.SelectedItems.Cast<MangaFile>().ToList();
                if (selectedItems.Count == 0)
                    return;
                
                var lines = new List<string>();
                
                foreach (var item in selectedItems)
                {
                    // 各行のデータをタブ区切りで結合
                    var rowData = new List<string>
                    {
                        item.FileName ?? "",
                        item.FolderPath ?? "",
                        item.Title ?? "",
                        item.OriginalAuthor ?? "",
                        item.Artist ?? "",
                        item.AuthorReading ?? "",
                        item.VolumeDisplay ?? "",
                        item.Genre ?? "",
                        item.Publisher ?? "",
                        item.PublishDate?.ToString("yyyy/MM/dd") ?? "",
                        item.Tags ?? "",
                        item.FileSizeFormatted ?? "",
                        item.CreatedDate.ToString("yyyy/MM/dd HH:mm"),
                        item.ModifiedDate.ToString("yyyy/MM/dd HH:mm"),
                        item.FileType ?? "",
                        item.IsAIProcessed.ToString()
                    };
                    
                    lines.Add(string.Join("\t", rowData));
                }
                
                if (lines.Count > 0)
                {
                    var clipboardText = string.Join("\n", lines);
                    System.Windows.Clipboard.SetText(clipboardText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"クリップボードへのコピーに失敗しました: {ex.Message}");
            }
        }
        
        private void CopySelectedColumnToClipboard()
        {
            try
            {
                if (string.IsNullOrEmpty(_selectedColumnHeader))
                {
                    // カラムが選択されていない場合は、従来通り行全体をコピー
                    CopySelectedCellsToClipboard();
                    return;
                }
                
                var selectedItems = MangaFilesGrid.SelectedItems.Cast<MangaFile>().ToList();
                if (selectedItems.Count == 0)
                    return;
                
                var columnValues = new List<string>();
                
                foreach (var item in selectedItems)
                {
                    string value = GetColumnValue(item, _selectedColumnHeader);
                    columnValues.Add(value);
                }
                
                if (columnValues.Count > 0)
                {
                    var clipboardText = string.Join("\n", columnValues);
                    System.Windows.Clipboard.SetText(clipboardText);
                    
                    // ステータスメッセージを表示
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.StatusMessage = $"カラム '{_selectedColumnHeader}' の値をコピーしました ({columnValues.Count}件)";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"カラムのクリップボードコピーに失敗しました: {ex.Message}");
            }
        }
        
        private string GetColumnValue(MangaFile item, string columnHeader)
        {
            return columnHeader switch
            {
                "ファイル名" => item.FileName ?? "",
                "パス" => item.FolderPath ?? "",
                "タイトル" => item.Title ?? "",
                "原作者" => item.OriginalAuthor ?? "",
                "作画者" => item.Artist ?? "",
                "かな" => item.AuthorReading ?? "",
                "巻数" => item.VolumeDisplay ?? "",
                "ジャンル" => item.Genre ?? "",
                "出版社" => item.Publisher ?? "",
                "発行日" => item.PublishDate?.ToString("yyyy/MM/dd") ?? "",
                "タグ" => item.Tags ?? "",
                "ファイルサイズ" => item.FileSizeFormatted ?? "",
                "作成日時" => item.CreatedDate.ToString("yyyy/MM/dd HH:mm"),
                "更新日時" => item.ModifiedDate.ToString("yyyy/MM/dd HH:mm"),
                "種類" => item.FileType ?? "",
                "評価" => item.RatingDisplay ?? "",
                "タグ取得済" => item.IsAIProcessed.ToString(),
                _ => ""
            };
        }



        

        
        private void FolderListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // フォルダ選択時の処理はViewModelのOnSelectedScanFolderChangedで行われる
        }
        
        private void FolderListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete && DataContext is MainViewModel viewModel)
            {
                var listBox = sender as System.Windows.Controls.ListBox;
                if (listBox?.SelectedItem is string selectedFolder)
                {
                    // ViewModelの削除コマンドを実行
                    if (viewModel.DeleteSelectedFolderCommand.CanExecute(selectedFolder))
                    {
                        viewModel.DeleteSelectedFolderCommand.Execute(selectedFolder);
                    }
                }
                e.Handled = true;
            }
        }
        
        private void FileListContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is MainViewModel viewModel)
                {
                    var currentRating = viewModel.GetCurrentRating();
                    var selectedCount = viewModel.SelectedMangaFiles.Count;
                    if (selectedCount == 0 && viewModel.SelectedMangaFile != null)
                        selectedCount = 1;
                    
                    // 全てのチェックマークを非表示にする
                    Rating1Check.Visibility = Visibility.Collapsed;
                    Rating2Check.Visibility = Visibility.Collapsed;
                    Rating3Check.Visibility = Visibility.Collapsed;
                    Rating4Check.Visibility = Visibility.Collapsed;
                    Rating5Check.Visibility = Visibility.Collapsed;
                    ClearRatingCheck.Visibility = Visibility.Collapsed;
                    
                    // メインメニューのアイコンを更新
                    if (currentRating.HasValue)
                    {
                        RatingMenuIcon.Text = new string('★', currentRating.Value);
                        RatingMenuIcon.Foreground = new SolidColorBrush(Colors.Gold);
                        
                        // 現在の評価にチェックマークを表示
                        switch (currentRating.Value)
                        {
                            case 1:
                                Rating1Check.Visibility = Visibility.Visible;
                                break;
                            case 2:
                                Rating2Check.Visibility = Visibility.Visible;
                                break;
                            case 3:
                                Rating3Check.Visibility = Visibility.Visible;
                                break;
                            case 4:
                                Rating4Check.Visibility = Visibility.Visible;
                                break;
                            case 5:
                                Rating5Check.Visibility = Visibility.Visible;
                                break;
                        }
                    }
                    else
                    {
                        RatingMenuIcon.Text = "☆";
                        RatingMenuIcon.Foreground = new SolidColorBrush(Colors.Gray);
                        ClearRatingCheck.Visibility = Visibility.Visible;
                    }
                    
                    // 複数選択時の表示調整
                    if (selectedCount > 1)
                    {
                        // 複数選択時は評価が混在している可能性があるため、アイコンを調整
                        var selectedFiles = viewModel.SelectedMangaFiles.ToList();
                        var ratings = selectedFiles.Select(f => f.Rating).Distinct().ToList();
                        
                        if (ratings.Count > 1)
                        {
                            // 評価が混在している場合
                            RatingMenuIcon.Text = "★?";
                            RatingMenuIcon.Foreground = new SolidColorBrush(Colors.Orange);
                            
                            // 全てのチェックマークを非表示
                            Rating1Check.Visibility = Visibility.Collapsed;
                            Rating2Check.Visibility = Visibility.Collapsed;
                            Rating3Check.Visibility = Visibility.Collapsed;
                            Rating4Check.Visibility = Visibility.Collapsed;
                            Rating5Check.Visibility = Visibility.Collapsed;
                            ClearRatingCheck.Visibility = Visibility.Collapsed;
                        }
                    }
                    
                    // コマンドの有効/無効を設定
                    var canSetRating = viewModel.CanSetRating;
                    Rating1MenuItem.IsEnabled = canSetRating;
                    Rating2MenuItem.IsEnabled = canSetRating;
                    Rating3MenuItem.IsEnabled = canSetRating;
                    Rating4MenuItem.IsEnabled = canSetRating;
                    Rating5MenuItem.IsEnabled = canSetRating;
                    ClearRatingMenuItem.IsEnabled = canSetRating;
                    
                    // 移動メニューの有効/無効を設定
                    var canMoveFiles = selectedCount > 0 && viewModel.MoveFilesCommand?.CanExecute(null) == true;
                    MoveFilesMenuItem.IsEnabled = canMoveFiles;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"コンテキストメニューの状態更新中にエラーが発生しました: {ex.Message}");
            }
        }

        private void SettingsChangedNotifier_SettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            // 設定変更通知を受け取ったら、DataGrid設定を再読み込み
            if (e.SettingName == "DataGridSettings" || e.SettingName == "ColumnVisibility")
            {
                // 設定が変更されました。再読み込みします。
                
                // UIスレッドで実行
                Dispatcher.Invoke(() =>
                {
                    LoadDataGridSettings();
                });
            }
            else if (e.SettingName == "ShowThumbnails" || e.SettingName == "ThumbnailDisplay")
            {
                // サムネイル表示設定が変更されました
                
                // UIスレッドで実行（少し遅延を入れて確実に反映）
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    // 少し待ってから実行
                    await Task.Delay(100);
                    ApplyThumbnailVisibilitySettings();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            else if (e.SettingName == "ThumbnailSettings")
            {
                // サムネイル設定が変更されました。キャッシュをクリア
                
                // UIスレッドで実行
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // LazyThumbnailConverterOptimizedのキャッシュをクリア
                        Mangaanya.Converters.LazyThumbnailConverterOptimized.OnSettingsChanged();
                        
                        // ポップアップの設定も更新
                        if (ThumbnailPopupControl != null)
                        {
                            ThumbnailPopupControl.OnSettingsChanged();
                        }
                        
                        // サムネイル設定の変更を適用
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"サムネイル設定変更の適用中にエラーが発生しました: {ex.Message}");
                    }
                });
            }
            else if (e.SettingName == "FontSettings")
            {
                // フォント設定が変更されました
                
                // UIスレッドで実行
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        ApplyFontSettings();
                        // フォント設定の変更を適用
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"フォント設定変更の適用中にエラーが発生しました: {ex.Message}");
                    }
                });
            }
        }
        
        private void ClearAttributes_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                // 選択されたファイルがあるか確認
                if (viewModel.SelectedMangaFiles.Count == 0)
                {
                    Mangaanya.Views.CustomMessageBox.Show("クリアするファイルが選択されていません。", "情報", Mangaanya.Views.CustomMessageBoxButton.OK);
                    return;
                }
                
                // 属性クリアダイアログを表示
                var clearAttributesWindow = new ClearAttributesWindow(viewModel);
                clearAttributesWindow.Owner = this;
                clearAttributesWindow.ShowDialog();
            }
        }
        
        private void MoveFilesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.MoveFilesCommand?.Execute(null);
            }
        }
        
        private void RestoreWindowPosition()
        {
            try
            {
                if (_configManager == null) return;
                
                // ウィンドウ位置とサイズを復元
                var left = _configManager.GetSetting<double>("WindowLeft", -1);
                var top = _configManager.GetSetting<double>("WindowTop", -1);
                var width = _configManager.GetSetting<double>("WindowWidth", Width);
                var height = _configManager.GetSetting<double>("WindowHeight", Height);
                var windowState = _configManager.GetSetting<WindowState>("WindowState", WindowState.Normal);
                
                // GridSplitterの位置を復元
                var leftColumnWidth = _configManager.GetSetting<double>("LeftColumnWidth", 280);
                MainGrid.ColumnDefinitions[0].Width = new GridLength(leftColumnWidth);
                
                // 保存された位置がない場合は中央に配置
                if (left < 0 || top < 0)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    Width = width;
                    Height = height;
                    WindowState = windowState;
                    
                    System.Diagnostics.Debug.WriteLine($"初回起動: 中央配置でウィンドウを表示");
                    return;
                }
                
                // 画面の境界内に収まるかチェック
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                
                // ウィンドウが画面外に出ないように調整
                if (left < 0) left = 0;
                if (top < 0) top = 0;
                if (left + width > screenWidth) left = screenWidth - width;
                if (top + height > screenHeight) top = screenHeight - height;
                
                // 最小サイズより小さくならないように調整
                if (width < MinWidth) width = MinWidth;
                if (height < MinHeight) height = MinHeight;
                
                // 位置とサイズを設定（ちらつき防止のため、表示前に設定）
                Left = left;
                Top = top;
                Width = width;
                Height = height;
                WindowState = windowState;
                
                System.Diagnostics.Debug.WriteLine($"ウィンドウ位置を復元しました: Left={left}, Top={top}, Width={width}, Height={height}, State={windowState}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ウィンドウ位置の復元に失敗しました: {ex.Message}");
                // エラーが発生した場合は中央配置にフォールバック
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
        
        private void SaveWindowPosition()
        {
            try
            {
                if (_configManager == null) return;
                
                SaveWindowPositionToConfigManager();
                
                // 非同期で保存（アプリケーション終了を遅延させないため）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _configManager.SaveAsync();
                        // ウィンドウ位置を保存
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ウィンドウ位置の保存に失敗しました: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ウィンドウ位置の保存に失敗しました: {ex.Message}");
            }
        }
        
        private void SaveWindowPositionToConfigManager()
        {
            try
            {
                if (_configManager == null) return;
                
                // 最大化・最小化状態の場合は、通常状態での位置とサイズを保存
                var bounds = RestoreBounds;
                if (WindowState == WindowState.Normal)
                {
                    bounds = new Rect(Left, Top, Width, Height);
                }
                
                System.Diagnostics.Debug.WriteLine($"ウィンドウ位置を設定中: Left={bounds.Left}, Top={bounds.Top}, Width={bounds.Width}, Height={bounds.Height}, State={WindowState}");
                
                _configManager.SetSetting("WindowLeft", bounds.Left);
                _configManager.SetSetting("WindowTop", bounds.Top);
                _configManager.SetSetting("WindowWidth", bounds.Width);
                _configManager.SetSetting("WindowHeight", bounds.Height);
                _configManager.SetSetting("WindowState", WindowState);
                
                // GridSplitterの位置を保存
                var leftColumnWidth = MainGrid.ColumnDefinitions[0].ActualWidth;
                _configManager.SetSetting("LeftColumnWidth", leftColumnWidth);
                
                System.Diagnostics.Debug.WriteLine($"ウィンドウ位置設定を準備しました: State={WindowState}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ウィンドウ位置設定の準備に失敗しました: {ex.Message}");
                throw; // 例外を再スローして問題を明確にする
            }
        }



        #region サムネイルポップアップ処理

        private void ThumbnailImage_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Image image && image.DataContext is MangaFile mangaFile)
                {
                    // マウス位置を取得
                    var mousePosition = e.GetPosition(this);
                    
                    // ポップアップ位置を調整（画面端を考慮）
                    var popupX = mousePosition.X + 20; // マウスから少し右にずらす
                    var popupY = mousePosition.Y - 200; // マウスから上にずらす
                    
                    // 画面端チェック
                    if (popupX + 620 > ActualWidth) // ポップアップ幅600 + マージン20
                    {
                        popupX = mousePosition.X - 620;
                    }
                    if (popupY < 0)
                    {
                        popupY = mousePosition.Y + 20;
                    }
                    
                    // ポップアップ位置を設定
                    ThumbnailPopupControl.Margin = new Thickness(popupX, popupY, 0, 0);
                    
                    // ポップアップを表示
                    ThumbnailPopupControl.ShowThumbnail(mangaFile);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"サムネイルポップアップ表示エラー: {ex.Message}");
            }
        }

        private void ThumbnailImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // マウスが離れても即座に非表示にはしない（ポップアップに移動する可能性があるため）
            // ThumbnailPopupControlの内部タイマーで自動非表示される
        }

        private void ThumbnailImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (ThumbnailPopupControl.Visibility == Visibility.Visible)
                {
                    // ポップアップが表示中の場合、非表示タイマーをリセット
                    ThumbnailPopupControl.ResetHideTimer();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"サムネイルポップアップ更新エラー: {ex.Message}");
            }
        }

        #endregion

        private void ApplyFontSettings()
        {
            try
            {
                if (_configManager == null) return;
                
                var uiFontSize = _configManager.GetSetting<double>("UIFontSize", 12.0);
                var buttonFontSize = _configManager.GetSetting<double>("ButtonFontSize", 12.0);
                
                // ウィンドウ全体のフォントサイズを設定
                FontSize = uiFontSize;
                
                // ツールバーのボタンのフォントサイズを設定
                var toolbarButtons = FindVisualChildren<System.Windows.Controls.Button>(this).Where(b => 
                    b.Parent is StackPanel sp && sp.Parent is Border border && 
                    border.Background != null && border.Background.ToString() == "#FFF8F8F8");
                
                foreach (var button in toolbarButtons)
                {
                    button.FontSize = buttonFontSize;
                }
                
                System.Diagnostics.Debug.WriteLine($"フォント設定を適用しました: UI={uiFontSize}px, Button={buttonFontSize}px");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"フォント設定の適用に失敗しました: {ex.Message}");
            }
        }
        
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    if (child != null)
                    {
                        foreach (T childOfChild in FindVisualChildren<T>(child))
                        {
                            yield return childOfChild;
                        }
                    }
                }
            }
        }

        #region ソートリセット機能

        /// <summary>
        /// DataGridのソート状態をリセットする
        /// </summary>
        private void ResetDataGridSort()
        {
            try
            {
                var viewSource = (CollectionViewSource)FindResource("MangaFilesViewSource");
                if (viewSource?.View != null)
                {
                    viewSource.View.SortDescriptions.Clear();
                    
                    // DataGridの列ヘッダーのソート表示もクリア
                    foreach (var column in MangaFilesGrid.Columns)
                    {
                        column.SortDirection = null;
                    }
                    
                    // ステータスメッセージでフィードバック
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.StatusMessage = "ソート状態をリセットしました";
                    }
                    
                    System.Diagnostics.Debug.WriteLine("DataGridのソート状態をリセットしました");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ソートリセット中にエラーが発生しました: {ex.Message}");
                
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.StatusMessage = $"ソートリセットエラー: {ex.Message}";
                }
            }
        }

        /// <summary>
        /// コンテキストメニューからのソートリセット
        /// </summary>
        private void ResetSortMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ResetDataGridSort();
        }

        #endregion

        #region 統一通知システム

        /// <summary>
        /// 通知タイプ
        /// </summary>
        public enum NotificationType
        {
            Information,
            Success,
            Warning,
            Error,
            Confirmation,
            ConflictResolution
        }

        private TaskCompletionSource<bool>? _confirmationResult;
        private TaskCompletionSource<ConflictResolution>? _conflictResolutionResult;
        private System.Windows.Threading.DispatcherTimer? _currentHideTimer;

        /// <summary>
        /// 情報通知を表示
        /// </summary>
        public void ShowNotification(string title, string message, NotificationType type = NotificationType.Information, int autoHideSeconds = 3)
        {
            System.Diagnostics.Debug.WriteLine($"ShowNotification: {title}, メッセージ長: {message?.Length ?? 0}, 自動非表示: {autoHideSeconds}秒");
            ShowNotificationInternal(title, message ?? "", type, autoHideSeconds, false);
        }

        /// <summary>
        /// 確認ダイアログを表示
        /// </summary>
        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            System.Diagnostics.Debug.WriteLine($"ShowConfirmationAsync: {title}");
            _confirmationResult = new TaskCompletionSource<bool>();
            ShowNotificationInternal(title, message, NotificationType.Confirmation, 0, true);
            var result = await _confirmationResult.Task;
            System.Diagnostics.Debug.WriteLine($"確認ダイアログ結果: {result}");
            return result;
        }

        /// <summary>
        /// 競合解決ダイアログを表示
        /// </summary>
        public async Task<ConflictResolution> ShowConflictResolutionAsync(string title, string message, FileMoveConflictType conflictType)
        {
            System.Diagnostics.Debug.WriteLine($"ShowConflictResolutionAsync: {title}");
            _conflictResolutionResult = new TaskCompletionSource<ConflictResolution>();
            ShowConflictResolutionInternal(title, message, conflictType);
            var result = await _conflictResolutionResult.Task;
            System.Diagnostics.Debug.WriteLine($"競合解決ダイアログ結果: {result}");
            return result;
        }

        /// <summary>
        /// 評価設定完了時の視覚的フィードバックを表示
        /// </summary>
        public void ShowRatingFeedback(int? rating, int fileCount, bool isCleared = false)
        {
            string title, message, icon;
            var color = Colors.Gold;

            if (isCleared)
            {
                icon = "✕";
                color = Colors.Red;
                title = "評価を解除しました";
                message = $"{fileCount}件のファイルの評価を解除";
            }
            else if (rating.HasValue)
            {
                icon = new string('★', rating.Value);
                title = "評価を設定しました";
                message = $"{fileCount}件のファイルに★{rating.Value}を設定";
            }
            else
            {
                return;
            }

            ShowNotificationWithCustomIcon(title, message, icon, color, 2);
        }

        /// <summary>
        /// カスタムアイコンで通知を表示
        /// </summary>
        private void ShowNotificationWithCustomIcon(string title, string message, string icon, System.Windows.Media.Color iconColor, int autoHideSeconds)
        {
            try
            {
                NotificationIcon.Text = icon;
                NotificationIcon.Foreground = new SolidColorBrush(iconColor);
                NotificationTitle.Text = title;
                NotificationMessage.Text = message;
                NotificationButtons.Visibility = Visibility.Collapsed;
                NotificationCloseButton.Visibility = Visibility.Visible;

                ShowNotificationAnimation(autoHideSeconds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"カスタム通知表示エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 内部通知表示メソッド
        /// </summary>
        private void ShowNotificationInternal(string title, string message, NotificationType type, int autoHideSeconds, bool showButtons)
        {
            try
            {
                // アイコンと色を設定
                switch (type)
                {
                    case NotificationType.Information:
                        NotificationIcon.Text = "ℹ";
                        NotificationIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 144, 226)); // #4A90E2
                        break;
                    case NotificationType.Success:
                        NotificationIcon.Text = "✓";
                        NotificationIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // #4CAF50
                        break;
                    case NotificationType.Warning:
                        NotificationIcon.Text = "⚠";
                        NotificationIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0)); // #FF9800
                        break;
                    case NotificationType.Error:
                        NotificationIcon.Text = "✕";
                        NotificationIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)); // #F44336
                        break;
                    case NotificationType.Confirmation:
                        NotificationIcon.Text = "?";
                        NotificationIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 39, 176)); // #9C27B0
                        break;
                }

                NotificationTitle.Text = title;
                NotificationMessage.Text = message;
                NotificationButtons.Visibility = showButtons ? Visibility.Visible : Visibility.Collapsed;
                NotificationCloseButton.Visibility = showButtons ? Visibility.Collapsed : Visibility.Visible;
                
                // 確認ダイアログの場合はメッセージを中央揃えに戻す
                if (type == NotificationType.Confirmation)
                {
                    NotificationMessage.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                    NotificationMessage.TextAlignment = System.Windows.TextAlignment.Center;
                    NotificationMessage.FontFamily = new System.Windows.Media.FontFamily("Segoe UI"); // 通常フォントに戻す
                }
                else
                {
                    NotificationMessage.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                    NotificationMessage.TextAlignment = System.Windows.TextAlignment.Left;
                    NotificationMessage.FontFamily = new System.Windows.Media.FontFamily("Consolas, 'Courier New', monospace");
                }

                ShowNotificationAnimation(autoHideSeconds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知表示エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 競合解決ダイアログを表示
        /// </summary>
        private void ShowConflictResolutionInternal(string title, string message, FileMoveConflictType conflictType)
        {
            try
            {
                // アイコンと色を設定（警告アイコンを使用）
                NotificationIcon.Text = "⚠";
                NotificationIcon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0)); // #FF9800

                NotificationTitle.Text = title;
                NotificationMessage.Text = message;
                
                // 通常のボタンを非表示にして、競合解決ボタンを表示
                NotificationButtons.Visibility = Visibility.Collapsed;
                ConflictResolutionButtons.Visibility = Visibility.Visible;
                NotificationCloseButton.Visibility = Visibility.Collapsed;
                
                // 全ての競合で統一されたボタン表示
                OverwriteButton.Visibility = Visibility.Visible;
                SkipButton.Visibility = Visibility.Visible;
                ConflictCancelButton.Visibility = Visibility.Visible;
                
                // メッセージを中央揃えに設定
                NotificationMessage.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                NotificationMessage.TextAlignment = System.Windows.TextAlignment.Center;
                NotificationMessage.FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

                ShowNotificationAnimation(0); // 自動非表示なし
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"競合解決ダイアログ表示エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知アニメーションを開始
        /// </summary>
        private void ShowNotificationAnimation(int autoHideSeconds)
        {
            System.Diagnostics.Debug.WriteLine($"ShowNotificationAnimation開始: 自動非表示={autoHideSeconds}秒");
            
            // 既存のタイマーがあれば停止
            if (_currentHideTimer != null)
            {
                System.Diagnostics.Debug.WriteLine("既存の自動非表示タイマーを停止");
                _currentHideTimer.Stop();
                _currentHideTimer = null;
            }
            
            UnifiedNotification.Visibility = Visibility.Visible;
            
            // 通知にフォーカスを設定（Enterキーで閉じられるように）
            UnifiedNotification.Focus();
            
            var showAnimation = FindResource("NotificationShowAnimation") as System.Windows.Media.Animation.Storyboard;
            showAnimation?.Begin();

            if (autoHideSeconds > 0)
            {
                System.Diagnostics.Debug.WriteLine($"自動非表示タイマー開始: {autoHideSeconds}秒後に非表示");
                _currentHideTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(autoHideSeconds)
                };
                
                _currentHideTimer.Tick += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("自動非表示タイマー発火");
                    _currentHideTimer?.Stop();
                    _currentHideTimer = null;
                    HideNotification();
                };
                
                _currentHideTimer.Start();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("自動非表示なし - 手動でのみ閉じる");
            }
        }

        /// <summary>
        /// 通知を非表示にする
        /// </summary>
        private void HideNotification()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("HideNotification呼び出し");
                
                // 自動非表示タイマーがあれば停止
                if (_currentHideTimer != null)
                {
                    System.Diagnostics.Debug.WriteLine("自動非表示タイマーを停止");
                    _currentHideTimer.Stop();
                    _currentHideTimer = null;
                }
                
                var hideAnimation = FindResource("NotificationHideAnimation") as System.Windows.Media.Animation.Storyboard;
                
                if (hideAnimation != null)
                {
                    hideAnimation.Completed += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine("非表示アニメーション完了");
                        UnifiedNotification.Visibility = Visibility.Collapsed;
                        
                        // 確認ダイアログの場合、結果がまだ設定されていなければキャンセルとして処理
                        if (_confirmationResult != null && !_confirmationResult.Task.IsCompleted)
                        {
                            System.Diagnostics.Debug.WriteLine("確認ダイアログが未完了のため、キャンセルとして処理");
                            _confirmationResult.SetResult(false);
                        }
                        _confirmationResult = null;
                        
                        // サムネイル生成モード選択の場合、結果がまだ設定されていなければキャンセルとして処理
                        if (_thumbnailModeResult != null && !_thumbnailModeResult.Task.IsCompleted)
                        {
                            System.Diagnostics.Debug.WriteLine("サムネイル生成モード選択が未完了のため、キャンセルとして処理");
                            _thumbnailModeResult.SetResult(ThumbnailGenerationMode.Cancel);
                        }
                        _thumbnailModeResult = null;
                        
                        // 競合解決ダイアログの場合、結果がまだ設定されていなければキャンセルとして処理
                        if (_conflictResolutionResult != null && !_conflictResolutionResult.Task.IsCompleted)
                        {
                            System.Diagnostics.Debug.WriteLine("競合解決ダイアログが未完了のため、キャンセルとして処理");
                            _conflictResolutionResult.SetResult(ConflictResolution.Cancel);
                        }
                        _conflictResolutionResult = null;
                        
                        // UI要素をリセット
                        ThumbnailModeSelection.Visibility = Visibility.Collapsed;
                        ConflictResolutionButtons.Visibility = Visibility.Collapsed;
                    };
                    
                    hideAnimation.Begin();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("非表示アニメーションが見つからない - 即座に非表示");
                    UnifiedNotification.Visibility = Visibility.Collapsed;
                    
                    // 確認ダイアログの場合、結果がまだ設定されていなければキャンセルとして処理
                    if (_confirmationResult != null && !_confirmationResult.Task.IsCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine("確認ダイアログが未完了のため、キャンセルとして処理");
                        _confirmationResult.SetResult(false);
                    }
                    _confirmationResult = null;
                    
                    // サムネイル生成モード選択の場合、結果がまだ設定されていなければキャンセルとして処理
                    if (_thumbnailModeResult != null && !_thumbnailModeResult.Task.IsCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine("サムネイル生成モード選択が未完了のため、キャンセルとして処理");
                        _thumbnailModeResult.SetResult(ThumbnailGenerationMode.Cancel);
                    }
                    _thumbnailModeResult = null;
                    
                    // 競合解決ダイアログの場合、結果がまだ設定されていなければキャンセルとして処理
                    if (_conflictResolutionResult != null && !_conflictResolutionResult.Task.IsCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine("競合解決ダイアログが未完了のため、キャンセルとして処理");
                        _conflictResolutionResult.SetResult(ConflictResolution.Cancel);
                    }
                    _conflictResolutionResult = null;
                    
                    // UI要素をリセット
                    ThumbnailModeSelection.Visibility = Visibility.Collapsed;
                    ConflictResolutionButtons.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"通知非表示エラー: {ex.Message}");
                UnifiedNotification.Visibility = Visibility.Collapsed;
                
                // エラー時も確認ダイアログの結果を処理
                if (_confirmationResult != null && !_confirmationResult.Task.IsCompleted)
                {
                    _confirmationResult.SetResult(false);
                }
                _confirmationResult = null;
                
                // エラー時もサムネイル生成モード選択の結果を処理
                if (_thumbnailModeResult != null && !_thumbnailModeResult.Task.IsCompleted)
                {
                    _thumbnailModeResult.SetResult(ThumbnailGenerationMode.Cancel);
                }
                _thumbnailModeResult = null;
                
                // エラー時も競合解決ダイアログの結果を処理
                if (_conflictResolutionResult != null && !_conflictResolutionResult.Task.IsCompleted)
                {
                    _conflictResolutionResult.SetResult(ConflictResolution.Cancel);
                }
                _conflictResolutionResult = null;
                
                // UI要素をリセット
                ThumbnailModeSelection.Visibility = Visibility.Collapsed;
                ConflictResolutionButtons.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 確認ダイアログのOKボタンクリック
        /// </summary>
        private void NotificationOkButton_Click(object sender, RoutedEventArgs e)
        {
            // サムネイル生成モード選択の場合
            if (ThumbnailModeSelection.Visibility == Visibility.Visible)
            {
                var selectedMode = OnlyMissingRadio.IsChecked == true 
                    ? ThumbnailGenerationMode.OnlyMissing 
                    : ThumbnailGenerationMode.RegenerateAll;
                
                System.Diagnostics.Debug.WriteLine($"サムネイル生成モード選択: {selectedMode}");
                _thumbnailModeResult?.SetResult(selectedMode);
            }
            else
            {
                // 通常の確認ダイアログの場合
                System.Diagnostics.Debug.WriteLine("確認ダイアログ: OK選択");
                _confirmationResult?.SetResult(true);
            }
            
            HideNotification();
        }

        /// <summary>
        /// 確認ダイアログのキャンセルボタンクリック
        /// </summary>
        private void NotificationCancelButton_Click(object sender, RoutedEventArgs e)
        {
            // サムネイル生成モード選択の場合
            if (ThumbnailModeSelection.Visibility == Visibility.Visible)
            {
                System.Diagnostics.Debug.WriteLine("サムネイル生成モード選択: キャンセル");
                _thumbnailModeResult?.SetResult(ThumbnailGenerationMode.Cancel);
            }
            else
            {
                // 通常の確認ダイアログの場合
                System.Diagnostics.Debug.WriteLine("確認ダイアログ: キャンセル選択");
                _confirmationResult?.SetResult(false);
            }
            
            HideNotification();
        }

        /// <summary>
        /// 通知のOKボタンクリック
        /// </summary>
        private void NotificationCloseButton_Click(object sender, RoutedEventArgs e)
        {
            HideNotification();
        }

        /// <summary>
        /// 競合解決ダイアログの上書きボタンクリック
        /// </summary>
        private void OverwriteButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("競合解決ダイアログ: 上書き選択");
            _conflictResolutionResult?.SetResult(ConflictResolution.Overwrite);
            HideNotification();
        }

        /// <summary>
        /// 競合解決ダイアログのスキップボタンクリック
        /// </summary>
        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("競合解決ダイアログ: スキップ選択");
            _conflictResolutionResult?.SetResult(ConflictResolution.Skip);
            HideNotification();
        }

        /// <summary>
        /// 競合解決ダイアログのキャンセルボタンクリック
        /// </summary>
        private void ConflictCancelButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("競合解決ダイアログ: キャンセル選択");
            _conflictResolutionResult?.SetResult(ConflictResolution.Cancel);
            HideNotification();
        }

        /// <summary>
        /// 通知のキーダウンイベント（Enterキーで閉じる）
        /// </summary>
        private void UnifiedNotification_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // サムネイル生成モード選択が表示されている場合
            if (ThumbnailModeSelection.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Enter)
                {
                    var selectedMode = OnlyMissingRadio.IsChecked == true 
                        ? ThumbnailGenerationMode.OnlyMissing 
                        : ThumbnailGenerationMode.RegenerateAll;
                    
                    System.Diagnostics.Debug.WriteLine($"サムネイル生成モード選択: EnterキーでOK選択 - {selectedMode}");
                    _thumbnailModeResult?.SetResult(selectedMode);
                    HideNotification();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    System.Diagnostics.Debug.WriteLine("サムネイル生成モード選択: Escapeキーでキャンセル選択");
                    _thumbnailModeResult?.SetResult(ThumbnailGenerationMode.Cancel);
                    HideNotification();
                    e.Handled = true;
                }
            }
            // 競合解決ダイアログが表示されている場合
            else if (ConflictResolutionButtons.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Enter)
                {
                    // Enterキーでスキップを選択（デフォルト動作）
                    System.Diagnostics.Debug.WriteLine("競合解決ダイアログ: Enterキーでスキップ選択");
                    _conflictResolutionResult?.SetResult(ConflictResolution.Skip);
                    HideNotification();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    System.Diagnostics.Debug.WriteLine("競合解決ダイアログ: Escapeキーでキャンセル選択");
                    _conflictResolutionResult?.SetResult(ConflictResolution.Cancel);
                    HideNotification();
                    e.Handled = true;
                }
            }
            // 確認ダイアログが表示されている場合
            else if (NotificationButtons.Visibility == Visibility.Visible)
            {
                if (e.Key == Key.Enter)
                {
                    System.Diagnostics.Debug.WriteLine("確認ダイアログ: EnterキーでOK選択");
                    _confirmationResult?.SetResult(true);
                    HideNotification();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    System.Diagnostics.Debug.WriteLine("確認ダイアログ: Escapeキーでキャンセル選択");
                    _confirmationResult?.SetResult(false);
                    HideNotification();
                    e.Handled = true;
                }
            }
            else
            {
                // 通常の通知の場合
                if (e.Key == Key.Enter || e.Key == Key.Escape)
                {
                    HideNotification();
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region サムネイル生成モード選択

        private TaskCompletionSource<ThumbnailGenerationMode>? _thumbnailModeResult;

        /// <summary>
        /// サムネイル生成モード選択ダイアログを表示
        /// </summary>
        public async Task<ThumbnailGenerationMode> ShowThumbnailModeSelectionAsync(string title, string message, int fileCount)
        {
            System.Diagnostics.Debug.WriteLine($"ShowThumbnailModeSelectionAsync: {title}");
            _thumbnailModeResult = new TaskCompletionSource<ThumbnailGenerationMode>();
            ShowThumbnailModeSelectionInternal(title, message, fileCount);
            var result = await _thumbnailModeResult.Task;
            System.Diagnostics.Debug.WriteLine($"サムネイル生成モード選択結果: {result}");
            return result;
        }

        /// <summary>
        /// サムネイル生成モード選択の内部表示メソッド
        /// </summary>
        private void ShowThumbnailModeSelectionInternal(string title, string message, int fileCount)
        {
            try
            {
                // 通知の基本設定
                NotificationIcon.Text = "🖼";
                NotificationIcon.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 144, 226));
                NotificationTitle.Text = title;
                NotificationMessage.Text = message;

                // ファイル数を設定
                ThumbnailFileCountText.Text = $"対象ファイル数: {fileCount}件";

                // ラジオボタンを初期化
                OnlyMissingRadio.IsChecked = true;
                RegenerateAllRadio.IsChecked = false;

                // 表示要素の制御
                NotificationCloseButton.Visibility = Visibility.Collapsed;
                NotificationButtons.Visibility = Visibility.Visible;
                ThumbnailModeSelection.Visibility = Visibility.Visible;

                // ボタンのテキストを設定
                NotificationOkButton.Content = "実行";
                NotificationCancelButton.Content = "キャンセル";

                ShowNotificationAnimation(0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"サムネイル生成モード選択表示エラー: {ex.Message}");
                _thumbnailModeResult?.SetResult(ThumbnailGenerationMode.Cancel);
            }
        }



        #endregion
    }
}
