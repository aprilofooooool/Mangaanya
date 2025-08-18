using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Media;
using System.Linq;
using System.Threading.Tasks;
using Mangaanya.Models;
using Mangaanya.ViewModels;
using Mangaanya.Services;

namespace Mangaanya
{
    /// <summary>
    /// MainWindow のイベントハンドラー処理を担当する部分クラス
    /// マウス、キーボード、ウィンドウイベントハンドラーを含む
    /// </summary>
    public partial class MainWindow : Window
    {
        #region ウィンドウイベントハンドラー

        /// <summary>
        /// ウィンドウの初期化完了時の処理
        /// </summary>
        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // ウィンドウ位置を表示前に復元（ちらつき防止）
            RestoreWindowPosition();
            
            // DataGridの設定を表示前に適用（ちらつき防止）
            LoadDataGridSettings();
            ApplyFontSettings();
        }

        /// <summary>
        /// ウィンドウ読み込み完了時の処理
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // SourceInitializedで設定が適用されなかった場合のフォールバック
            // DataGridが完全に初期化された後に再度設定を適用
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                new Action(() => 
                {
                    // DataGridの列数をチェックして、設定が適用されているか確認
                    if (MangaFilesGrid != null && MangaFilesGrid.Columns.Count > 0)
                    {
                        var hasVisibleColumns = MangaFilesGrid.Columns.Any(c => c.Visibility == Visibility.Visible);
                        if (!hasVisibleColumns)
                        {
                            System.Diagnostics.Debug.WriteLine("DataGrid設定が適用されていないため、再適用します。");
                            LoadDataGridSettings();
                            ApplyFontSettings();
                        }
                    }
                }),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// DataContext変更時の処理
        /// </summary>
        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 古いViewModelのイベントを解除
            if (e.OldValue is MainViewModel oldViewModel)
            {
                oldViewModel.ShowRatingFeedback -= ShowRatingFeedback;
                oldViewModel.ShowNotificationEvent -= (title, message, seconds) => ShowNotification(title, message, NotificationType.Information, seconds);
                oldViewModel.ShowSuccessNotificationEvent -= (title, message, seconds) => ShowNotification(title, message, NotificationType.Success, seconds);
                oldViewModel.ShowErrorNotificationEvent -= (title, message, seconds) => ShowNotification(title, message, NotificationType.Error, seconds);
                oldViewModel.ShowConfirmationEvent -= ShowConfirmationAsync;
                oldViewModel.ShowThumbnailModeSelectionEvent -= ShowThumbnailModeSelectionAsync;
                oldViewModel.ResetSortEvent -= ResetDataGridSort;
            }
            
            // 新しいViewModelのイベントを購読
            if (e.NewValue is MainViewModel newViewModel)
            {
                newViewModel.ShowRatingFeedback += ShowRatingFeedback;
                newViewModel.ShowNotificationEvent += (title, message, seconds) => ShowNotification(title, message, NotificationType.Information, seconds);
                newViewModel.ShowSuccessNotificationEvent += (title, message, seconds) => ShowNotification(title, message, NotificationType.Success, seconds);
                newViewModel.ShowErrorNotificationEvent += (title, message, seconds) => ShowNotification(title, message, NotificationType.Error, seconds);
                newViewModel.ShowConfirmationEvent += ShowConfirmationAsync;
                newViewModel.ShowThumbnailModeSelectionEvent += ShowThumbnailModeSelectionAsync;
                newViewModel.ResetSortEvent += ResetDataGridSort;
            }
        }

        /// <summary>
        /// ウィンドウ終了時の処理
        /// </summary>
        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 初回のClosingイベントでは処理をキャンセルし、設定保存を完了させる
            if (!_isClosingInProgress)
            {
                e.Cancel = true; // 一旦終了をキャンセル
                _isClosingInProgress = true;
                
                try
                {
                    // 設定変更通知の購読を解除
                    if (_settingsChangedNotifier != null)
                    {
                        _settingsChangedNotifier.SettingsChanged -= SettingsChangedNotifier_SettingsChanged;
                    }
                    
                    // 設定保存を確実に完了させる
                    await SaveAllSettingsOnClosingAsync();
                    
                    // データベース最適化を実行（バックグラウンドで）
                    try
                    {
                        var app = System.Windows.Application.Current as App;
                        if (app != null)
                        {
                            var repository = app.Services.GetService<IMangaRepository>();
                            if (repository != null)
                            {
                                // 非同期でデータベース最適化を実行（UIをブロックしない）
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await repository.OptimizeDatabaseAsync();
                                        System.Diagnostics.Debug.WriteLine("アプリケーション終了時のデータベース最適化が完了しました");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"アプリケーション終了時のデータベース最適化でエラーが発生しました: {ex.Message}");
                                    }
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"データベース最適化の開始でエラーが発生しました: {ex.Message}");
                    }
                    
                    System.Diagnostics.Debug.WriteLine("終了処理が完了しました。アプリケーションを終了します。");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"終了処理でエラーが発生しました: {ex.Message}");
                }
                finally
                {
                    // 処理完了後に再度Closeを呼び出し（今度はキャンセルしない）
                    this.Close();
                }
            }
            // 2回目のClosingイベントでは何もせずに終了を許可
        }

        /// <summary>
        /// 設定変更通知の処理
        /// </summary>
        private void SettingsChangedNotifier_SettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            // 設定変更通知を受け取ったら、DataGrid設定を再読み込み
            try
            {
                System.Diagnostics.Debug.WriteLine($"設定変更通知を受信: {e.SettingKey}");
                
                // UI関連の設定変更の場合のみ処理
                if (e.SettingKey == "DataGridSettings" || 
                    e.SettingKey == "ShowThumbnails" || 
                    e.SettingKey == "ThumbnailDisplay" ||
                    e.SettingKey == "FontSize" ||
                    e.SettingKey == "FontFamily")
                {
                    // UIスレッドで実行
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (e.SettingKey == "DataGridSettings")
                            {
                                LoadDataGridSettings();
                            }
                            else if (e.SettingKey == "ShowThumbnails" || e.SettingKey == "ThumbnailDisplay")
                            {
                                ApplyThumbnailVisibilitySettings();
                            }
                            else if (e.SettingKey == "FontSize" || e.SettingKey == "FontFamily")
                            {
                                ApplyFontSettings();
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"設定変更を適用しました: {e.SettingKey}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"設定変更の適用に失敗しました: {ex.Message}");
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定変更通知の処理に失敗しました: {ex.Message}");
            }
        }

        #endregion

        #region DataGridイベントハンドラー

        /// <summary>
        /// DataGrid選択変更時の処理
        /// </summary>
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

        /// <summary>
        /// DataGridダブルクリック時の処理
        /// </summary>
        private void MangaFilesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && viewModel.SelectedMangaFile != null)
            {
                // ダブルクリックされたカラムに応じて動作を変更
                HandleDoubleClickByColumn(viewModel.SelectedMangaFile, _selectedColumnHeader);
            }
        }

        /// <summary>
        /// DataGridマウス左ボタン押下時の処理（カラム特定用）
        /// </summary>
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

        #endregion

        #region キーボードイベントハンドラー

        /// <summary>
        /// DataGridキーダウン時の処理
        /// </summary>
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
                    // Ctrl+C: 選択中の行の全情報をコピー
                    CopySelectedRowsToClipboard();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                // Deleteキー: 選択中のファイルを削除
                if (DataContext is MainViewModel viewModel && viewModel.DeleteSelectedFilesCommand.CanExecute(null))
                {
                    viewModel.DeleteSelectedFilesCommand.Execute(null);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                // F5キー: 再スキャン
                if (DataContext is MainViewModel viewModel && viewModel.ScanFolderCommand.CanExecute(null))
                {
                    viewModel.ScanFolderCommand.Execute(null);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                // Enterキー: ファイルを開く
                if (DataContext is MainViewModel viewModel && viewModel.SelectedMangaFile != null)
                {
                    viewModel.OpenFileCommand.Execute(viewModel.SelectedMangaFile);
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// フォルダリストボックスキーダウン時の処理
        /// </summary>
        private void FolderListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete && DataContext is MainViewModel viewModel)
            {
                // 選択されたフォルダを削除
                if (viewModel.SelectedScanFolder != null)
                {
                    viewModel.RemoveScanFolderCommand.Execute(viewModel.SelectedScanFolder);
                }
                e.Handled = true;
            }
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

        #region マウスイベントハンドラー

        /// <summary>
        /// サムネイル画像マウス進入時の処理
        /// </summary>
        private void ThumbnailImage_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Image image && image.DataContext is MangaFile mangaFile)
                {
                    // サムネイルポップアップを表示
                    if (_thumbnailPopup == null)
                    {
                        _thumbnailPopup = new Controls.ThumbnailPopup();
                    }
                    
                    _thumbnailPopup.ShowThumbnail(mangaFile, image);
                    
                    // タイマーをリセット
                    _hidePopupTimer?.Stop();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"サムネイルポップアップ表示エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// サムネイル画像マウス離脱時の処理
        /// </summary>
        private void ThumbnailImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // マウスが離れても即座に非表示にはしない（ポップアップに移動する可能性があるため）
            StartHidePopupTimer();
        }

        /// <summary>
        /// サムネイル画像マウス移動時の処理
        /// </summary>
        private void ThumbnailImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (sender is System.Windows.Controls.Image image && _thumbnailPopup != null)
                {
                    // ポップアップの位置を更新
                    _thumbnailPopup.UpdatePosition(image);
                    
                    // タイマーをリセット
                    _hidePopupTimer?.Stop();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"サムネイルポップアップ位置更新エラー: {ex.Message}");
            }
        }

        #endregion

        #region リストボックスイベントハンドラー

        /// <summary>
        /// フォルダリストボックス選択変更時の処理
        /// </summary>
        private void FolderListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // フォルダ選択時の処理はViewModelのOnSelectedScanFolderChangedで行われる
        }

        #endregion

        #region コンテキストメニューイベントハンドラー

        /// <summary>
        /// ファイルリストコンテキストメニュー開始時の処理
        /// </summary>
        private void FileListContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is MainViewModel viewModel)
                {
                    // 選択されたファイル数に応じてメニュー項目の表示を制御
                    var selectedCount = viewModel.SelectedMangaFiles.Count;
                    
                    // メニュー項目の有効/無効を設定
                    if (sender is ContextMenu contextMenu)
                    {
                        foreach (var item in contextMenu.Items)
                        {
                            if (item is MenuItem menuItem)
                            {
                                switch (menuItem.Header?.ToString())
                                {
                                    case "ファイルを開く":
                                    case "エクスプローラーで表示":
                                        menuItem.IsEnabled = selectedCount == 1;
                                        break;
                                    case "ファイルを削除":
                                    case "属性をクリア":
                                    case "ファイルを移動":
                                        menuItem.IsEnabled = selectedCount > 0;
                                        break;
                                    case "サムネイル生成":
                                        menuItem.IsEnabled = selectedCount > 0;
                                        break;
                                    case "AI情報取得":
                                        menuItem.IsEnabled = selectedCount > 0;
                                        break;
                                    case "評価をクリア":
                                        menuItem.IsEnabled = selectedCount > 0 && 
                                            viewModel.SelectedMangaFiles.Any(f => f.Rating.HasValue);
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"コンテキストメニュー開始処理エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ソートリセットメニュー項目クリック時の処理
        /// </summary>
        private void ResetSortMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ResetDataGridSort();
        }

        #endregion

        #region ボタンクリックイベントハンドラー

        /// <summary>
        /// 属性クリアボタンクリック時の処理
        /// </summary>
        private void ClearAttributes_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                // 属性クリアウィンドウを表示
                var clearAttributesWindow = new Views.ClearAttributesWindow();
                clearAttributesWindow.Owner = this;
                
                if (clearAttributesWindow.ShowDialog() == true)
                {
                    // 選択された属性をクリア
                    var parameters = clearAttributesWindow.GetClearParameters();
                    viewModel.ClearAttributesCommand.Execute(parameters);
                }
            }
        }

        /// <summary>
        /// ファイル移動メニュー項目クリック時の処理
        /// </summary>
        private void MoveFilesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                // ファイル移動処理を実行
                viewModel.MoveFilesCommand.Execute(null);
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
                
                System.Diagnostics.Debug.WriteLine($"サムネイル生成モード選択: OK選択 - {selectedMode}");
                _thumbnailModeResult?.SetResult(selectedMode);
            }
            // 確認ダイアログの場合
            else
            {
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
                System.Diagnostics.Debug.WriteLine("サムネイル生成モード選択: キャンセル選択");
                _thumbnailModeResult?.SetResult(ThumbnailGenerationMode.Cancel);
            }
            // 確認ダイアログの場合
            else
            {
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

        #endregion 
   }
}  
      #region ヘルパーメソッド

        /// <summary>
        /// ダブルクリックされたカラムに応じた処理を実行
        /// </summary>
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
                case "評価":
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

        /// <summary>
        /// フォルダをエクスプローラーで開く
        /// </summary>
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

        /// <summary>
        /// ファイルを選択してエクスプローラーで開く
        /// </summary>
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

        /// <summary>
        /// 漫画ビューアアプリでファイルを開く
        /// </summary>
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

        /// <summary>
        /// DataGridセルを編集モードにする
        /// </summary>
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

        #endregion