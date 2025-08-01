using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Mangaanya.Models;
using Mangaanya.ViewModels;
using Mangaanya.Views;
using Mangaanya.Services;


namespace Mangaanya
{
    public partial class MainWindow : Window
    {
        private readonly IConfigurationManager? _configManager;
        private readonly ISettingsChangedNotifier? _settingsChangedNotifier;
        
        // 選択中のカラムを記録する変数
        private string? _selectedColumnHeader = null;
        
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
            
            // ウィンドウが読み込まれた後にDataGridの設定を適用
            Loaded += MainWindow_Loaded;
            
            // ウィンドウが閉じられる前にDataGridの設定を保存
            Closing += MainWindow_Closing;
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // ウィンドウ位置を復元
            RestoreWindowPosition();
            
            // DataGridの読み込みが完了した後に設定を適用するために、
            // Dispatcherを使用して非同期で実行する
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                new Action(() => 
                {
                    LoadDataGridSettings();
                    ApplyFontSettings();
                }),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
        
        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 設定変更通知の購読を解除
            if (_settingsChangedNotifier != null)
            {
                _settingsChangedNotifier.SettingsChanged -= SettingsChangedNotifier_SettingsChanged;
            }
            
            // 古いサムネイルファイルのクリーンアップを実行
            await PerformThumbnailCleanupOnExit();
            
            // ウィンドウ位置を保存
            SaveWindowPosition();
            
            SaveDataGridSettings();
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
                    new DataGridColumnSettings { Header = "タグ取得済", Width = 80, DisplayIndex = 16, IsVisible = false }
                }
            };
        }

        private void LoadDataGridSettings()
        {
            try
            {
                if (_configManager == null) return;
                
                var settings = _configManager.GetSetting<DataGridSettings>("DataGridSettings", new DataGridSettings());
                
                // settings.jsonがない場合、またはDataGridSettingsが空の場合はデフォルト設定を使用
                if (settings.Columns.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("DataGridSettings設定がありません。デフォルト設定を適用します。");
                    settings = GetDefaultDataGridSettings();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"DataGridSettings設定を読み込みました: {settings.Columns.Count}列");
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
                        
                        System.Diagnostics.Debug.WriteLine($"列設定を適用: {columnSetting.Header} = Width:{columnSetting.Width}, Visible:{columnSetting.IsVisible}");
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
                var thumbnailColumn = MangaFilesGrid.Columns.FirstOrDefault(c => 
                    (c.Header?.ToString() ?? string.Empty) == "サムネイル");
                
                if (thumbnailColumn != null)
                {
                    System.Diagnostics.Debug.WriteLine($"サムネイル表示設定を適用中: {showThumbnails}");
                    
                    // 行の高さを先に設定（サムネイル80px + 上下余白2pxずつ = 84px）
                    MangaFilesGrid.RowHeight = showThumbnails ? 84 : 25;
                    
                    // カラムの表示/非表示を設定
                    thumbnailColumn.Visibility = showThumbnails ? Visibility.Visible : Visibility.Collapsed;
                    
                    // 強制的にレイアウトを更新
                    MangaFilesGrid.UpdateLayout();
                    MangaFilesGrid.InvalidateVisual();
                    
                    // ViewModelの設定も更新
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.ShowThumbnails = showThumbnails;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"サムネイル表示設定を適用完了: {showThumbnails}, RowHeight: {MangaFilesGrid.RowHeight}");
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
                await _configManager.SaveAsync();
                
                System.Diagnostics.Debug.WriteLine($"DataGridの設定を保存しました: {settings.Columns.Count}列");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DataGridの設定保存に失敗しました: {ex.Message}");
            }
        }
        
        private void MangaFilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                // 選択されたアイテムを取得
                var selectedItems = MangaFilesGrid.SelectedItems.Cast<MangaFile>().ToList();
                
                // ViewModelの選択アイテムコレクションを更新
                viewModel.SelectedMangaFiles.Clear();
                foreach (var item in selectedItems)
                {
                    viewModel.SelectedMangaFiles.Add(item);
                }
            }
        }
        
        private void MangaFilesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && viewModel.SelectedMangaFile != null)
            {
                // ダブルクリックされたカラムに応じて動作を変更
                HandleDoubleClickByColumn(viewModel.SelectedMangaFile, _selectedColumnHeader);
            }
        }
        
        private void HandleDoubleClickByColumn(MangaFile selectedFile, string? columnHeader)
        {
            if (selectedFile == null) return;
            
            switch (columnHeader)
            {
                case "パス":
                    // パスカラム → 対象ファイルが選択された状態でエクスプローラーを開く
                    OpenFileInExplorer(selectedFile);
                    break;
                    
                case "ファイル名":
                    // ファイル名カラム → 漫画ビューアアプリで開く
                    OpenWithMangaViewer(selectedFile);
                    break;
                    
                case "かな":
                case "原作者":
                case "作画者":
                case "タイトル":
                case "巻数":
                case "ジャンル":
                case "出版社":
                case "発行日":
                case "タグ取得済":
                case "タグ":
                    // 編集可能なカラム → 編集モードに入る
                    EnterEditMode(selectedFile, columnHeader);
                    break;
                    
                case "サムネイル":
                    // サムネイルカラム → 対象ファイルが選択された状態でエクスプローラーを開く
                    OpenFileInExplorer(selectedFile);
                    break;
                    
                case "種類":
                case "サイズ":
                case "ファイルサイズ": // 後方互換性のため残す
                case "作成日時":
                case "更新日時":
                    // 読み取り専用カラム → 何もしない
                    break;
                    
                default:
                    // その他のカラムまたはカラムが特定できない場合 → デフォルトでファイルを開く
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.OpenFileCommand.Execute(selectedFile);
                    }
                    break;
            }
        }
        
        private void OpenFolderInExplorer(string? folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath) || !System.IO.Directory.Exists(folderPath))
                {
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.StatusMessage = "フォルダが存在しません";
                    }
                    return;
                }
                
                System.Diagnostics.Process.Start("explorer.exe", folderPath);
                
                if (DataContext is MainViewModel viewModel2)
                {
                    viewModel2.StatusMessage = $"フォルダを開きました: {folderPath}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"フォルダを開く際にエラーが発生しました: {ex.Message}");
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.StatusMessage = $"フォルダを開けませんでした: {ex.Message}";
                }
            }
        }
        
        private void OpenFileInExplorer(MangaFile selectedFile)
        {
            try
            {
                if (selectedFile == null || string.IsNullOrEmpty(selectedFile.FilePath) || !System.IO.File.Exists(selectedFile.FilePath))
                {
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.StatusMessage = "ファイルが存在しません";
                    }
                    return;
                }
                
                // /select パラメータを使用して、対象ファイルが選択された状態でエクスプローラーを開く
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{selectedFile.FilePath}\"");
                
                if (DataContext is MainViewModel viewModel2)
                {
                    viewModel2.StatusMessage = $"ファイルを選択してエクスプローラーを開きました: {selectedFile.FileName}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ファイルを選択してエクスプローラーを開く際にエラーが発生しました: {ex.Message}");
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.StatusMessage = $"ファイルを選択してエクスプローラーを開けませんでした: {ex.Message}";
                }
            }
        }
        
        private void OpenWithMangaViewer(MangaFile selectedFile)
        {
            try
            {
                // 設定から漫画ビューアアプリのパスを取得
                var mangaViewerPath = _configManager?.GetSetting<string>("MangaViewerPath", string.Empty);
                
                if (string.IsNullOrEmpty(mangaViewerPath) || !System.IO.File.Exists(mangaViewerPath))
                {
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.StatusMessage = "漫画ビューアアプリが設定されていません。設定画面で設定してください。";
                    }
                    return;
                }
                
                if (!System.IO.File.Exists(selectedFile.FilePath))
                {
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.StatusMessage = "ファイルが存在しません";
                    }
                    return;
                }
                
                // 漫画ビューアアプリでファイルを開く
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mangaViewerPath,
                    Arguments = $"\"{selectedFile.FilePath}\"",
                    UseShellExecute = true
                };
                
                System.Diagnostics.Process.Start(startInfo);
                
                if (DataContext is MainViewModel viewModel2)
                {
                    viewModel2.StatusMessage = $"漫画ビューアで開きました: {selectedFile.FileName}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"漫画ビューアでファイルを開く際にエラーが発生しました: {ex.Message}");
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.StatusMessage = $"漫画ビューアでファイルを開けませんでした: {ex.Message}";
                }
            }
        }
        
        private void EnterEditMode(MangaFile selectedFile, string columnHeader)
        {
            try
            {
                // DataGridの該当セルを編集モードにする
                var dataGrid = MangaFilesGrid;
                var selectedIndex = dataGrid.Items.IndexOf(selectedFile);
                
                if (selectedIndex >= 0)
                {
                    // 該当する行を選択
                    dataGrid.SelectedIndex = selectedIndex;
                    dataGrid.ScrollIntoView(selectedFile);
                    
                    // 該当するカラムを見つける
                    var column = dataGrid.Columns.FirstOrDefault(c => c.Header?.ToString() == columnHeader);
                    if (column != null)
                    {
                        // セルを編集モードにする
                        dataGrid.CurrentCell = new DataGridCellInfo(selectedFile, column);
                        dataGrid.BeginEdit();
                        
                        if (DataContext is MainViewModel viewModel)
                        {
                            viewModel.StatusMessage = $"編集モード: {columnHeader}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"編集モードに入る際にエラーが発生しました: {ex.Message}");
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.StatusMessage = $"編集モードに入れませんでした: {ex.Message}";
                }
            }
        }
        
        private void MangaFilesGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // クリックされた要素を取得
                var hitElement = e.OriginalSource as FrameworkElement;
                
                // DataGridColumnHeaderを探す
                while (hitElement != null && !(hitElement is DataGridColumnHeader))
                {
                    hitElement = VisualTreeHelper.GetParent(hitElement) as FrameworkElement;
                }
                
                if (hitElement is DataGridColumnHeader columnHeader)
                {
                    // カラムヘッダーがクリックされた場合、そのカラムを記録
                    _selectedColumnHeader = columnHeader.Content?.ToString();
                }
                else
                {
                    // セルがクリックされた場合、そのセルが属するカラムを特定
                    var cell = e.OriginalSource as FrameworkElement;
                    while (cell != null && !(cell is DataGridCell))
                    {
                        cell = VisualTreeHelper.GetParent(cell) as FrameworkElement;
                    }
                    
                    if (cell is DataGridCell dataGridCell)
                    {
                        var column = dataGridCell.Column;
                        if (column != null)
                        {
                            _selectedColumnHeader = column.Header?.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"カラム選択の検出に失敗しました: {ex.Message}");
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
        
        private void SettingsChangedNotifier_SettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            // 設定変更通知を受け取ったら、DataGrid設定を再読み込み
            if (e.SettingName == "DataGridSettings")
            {
                System.Diagnostics.Debug.WriteLine("DataGrid設定が変更されました。再読み込みします。");
                
                // UIスレッドで実行
                Dispatcher.Invoke(() =>
                {
                    LoadDataGridSettings();
                });
            }
            else if (e.SettingName == "ShowThumbnails")
            {
                System.Diagnostics.Debug.WriteLine("サムネイル表示設定が変更されました。");
                
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
                System.Diagnostics.Debug.WriteLine("サムネイル設定が変更されました。キャッシュをクリアします。");
                
                // UIスレッドで実行
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        // LazyThumbnailConverterのキャッシュをクリア
                        Mangaanya.Converters.LazyThumbnailConverter.OnSettingsChanged();
                        
                        // ポップアップの設定も更新
                        if (ThumbnailPopupControl != null)
                        {
                            ThumbnailPopupControl.OnSettingsChanged();
                        }
                        
                        System.Diagnostics.Debug.WriteLine("サムネイル設定の変更を適用しました。");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"サムネイル設定変更の適用中にエラーが発生しました: {ex.Message}");
                    }
                });
            }
            else if (e.SettingName == "FontSettings")
            {
                System.Diagnostics.Debug.WriteLine("フォント設定が変更されました。");
                
                // UIスレッドで実行
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        ApplyFontSettings();
                        System.Diagnostics.Debug.WriteLine("フォント設定の変更を適用しました。");
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
        
        private void RestoreWindowPosition()
        {
            try
            {
                if (_configManager == null) return;
                
                // ウィンドウ位置とサイズを復元
                var left = _configManager.GetSetting<double>("WindowLeft", Left);
                var top = _configManager.GetSetting<double>("WindowTop", Top);
                var width = _configManager.GetSetting<double>("WindowWidth", Width);
                var height = _configManager.GetSetting<double>("WindowHeight", Height);
                var windowState = _configManager.GetSetting<WindowState>("WindowState", WindowState.Normal);
                
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
                
                // GridSplitterの位置を復元
                var leftColumnWidth = _configManager.GetSetting<double>("LeftColumnWidth", 280);
                MainGrid.ColumnDefinitions[0].Width = new GridLength(leftColumnWidth);
                
                System.Diagnostics.Debug.WriteLine($"ウィンドウ位置を復元しました: Left={left}, Top={top}, Width={width}, Height={height}, State={windowState}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ウィンドウ位置の復元に失敗しました: {ex.Message}");
            }
        }
        
        private void SaveWindowPosition()
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
                
                _configManager.SetSetting("WindowLeft", bounds.Left);
                _configManager.SetSetting("WindowTop", bounds.Top);
                _configManager.SetSetting("WindowWidth", bounds.Width);
                _configManager.SetSetting("WindowHeight", bounds.Height);
                _configManager.SetSetting("WindowState", WindowState);
                
                // GridSplitterの位置を保存
                var leftColumnWidth = MainGrid.ColumnDefinitions[0].ActualWidth;
                _configManager.SetSetting("LeftColumnWidth", leftColumnWidth);
                
                // 非同期で保存（アプリケーション終了を遅延させないため）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _configManager.SaveAsync();
                        System.Diagnostics.Debug.WriteLine($"ウィンドウ位置を保存しました: Left={bounds.Left}, Top={bounds.Top}, Width={bounds.Width}, Height={bounds.Height}, State={WindowState}");
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

        /// <summary>
        /// アプリケーション終了時に古いサムネイルファイルのクリーンアップを実行します
        /// </summary>
        private async Task PerformThumbnailCleanupOnExit()
        {
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app != null)
                {
                    var thumbnailCleanupService = app.Services.GetService<IThumbnailCleanupService>();
                    if (thumbnailCleanupService != null)
                    {
                        // 自動削除設定をチェック
                        var autoCleanupEnabled = _configManager?.GetSetting<bool>("ThumbnailAutoCleanupEnabled", true) ?? true;
                        
                        if (!autoCleanupEnabled)
                        {
                            System.Diagnostics.Debug.WriteLine("自動削除が無効のため、アプリ終了時サムネイルクリーンアップをスキップします");
                            return;
                        }
                        
                        // 設定から保持期間と件数閾値を取得
                        var retentionDays = _configManager?.GetSetting<int>("ThumbnailRetentionDays", 30) ?? 30;
                        var maxFileCount = _configManager?.GetSetting<int>("ThumbnailMaxFileCount", 1000) ?? 1000;
                        
                        System.Diagnostics.Debug.WriteLine($"アプリ終了時サムネイルクリーンアップを開始します（保持期間: {retentionDays}日, 件数閾値: {maxFileCount}件）");
                        
                        // 件数条件付きで古いサムネイルファイルを削除
                        var result = await thumbnailCleanupService.CleanupOldThumbnailsIfExceedsCountAsync(retentionDays, maxFileCount);
                        
                        if (result.Success)
                        {
                            System.Diagnostics.Debug.WriteLine($"アプリ終了時サムネイルクリーンアップ完了: {result.DeletedCount}件削除");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"アプリ終了時サムネイルクリーンアップでエラー: {result.ErrorMessage}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"アプリ終了時サムネイルクリーンアップ中にエラーが発生しました: {ex.Message}");
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
    }
}
