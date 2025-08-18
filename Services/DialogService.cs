using Microsoft.Win32;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using Mangaanya.Models;
using Mangaanya.ViewModels;
using Mangaanya.Views;

namespace Mangaanya.Services
{
    public class DialogService : IDialogService
    {
        public (string? SelectedFolder, bool IncludeSubfolders) SelectFolderWithOptions(string title = "フォルダを選択してください")
        {
            // カスタムダイアログを作成
            var dialog = new System.Windows.Window
            {
                Title = title,
                Width = 500,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize
            };
            
            // レイアウト
            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            
            // フォルダ選択部分
            var folderPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(10) };
            var folderTextBox = new System.Windows.Controls.TextBox { Width = 350, IsReadOnly = true, Margin = new Thickness(0, 0, 5, 0) };
            var browseButton = new System.Windows.Controls.Button { Content = "参照...", Width = 80 };
            folderPanel.Children.Add(folderTextBox);
            folderPanel.Children.Add(browseButton);
            
            // サブフォルダオプション
            var includeSubfoldersCheckBox = new System.Windows.Controls.CheckBox 
            { 
                Content = "サブフォルダも含める", 
                Margin = new Thickness(10),
                IsChecked = true // デフォルトでチェック
            };
            
            // ボタン
            var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(10) };
            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 5, 0), IsEnabled = false };
            var cancelButton = new System.Windows.Controls.Button { Content = "キャンセル", Width = 80 };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            // グリッドに配置
            grid.Children.Add(folderPanel);
            grid.Children.Add(includeSubfoldersCheckBox);
            grid.Children.Add(buttonPanel);
            
            System.Windows.Controls.Grid.SetRow(folderPanel, 0);
            System.Windows.Controls.Grid.SetRow(includeSubfoldersCheckBox, 1);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 2);
            
            dialog.Content = grid;
            
            // イベント
            string? selectedPath = null;
            
            browseButton.Click += (s, e) =>
            {
                using var folderDialog = new FolderBrowserDialog
                {
                    Description = title,
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true
                };
                
                var result = folderDialog.ShowDialog();
                if (result == DialogResult.OK)
                {
                    selectedPath = folderDialog.SelectedPath;
                    folderTextBox.Text = selectedPath;
                    okButton.IsEnabled = true;
                }
            };
            
            okButton.Click += (s, e) => { dialog.DialogResult = true; };
            cancelButton.Click += (s, e) => { dialog.DialogResult = false; };
            
            // ダイアログ表示
            var dialogResult = dialog.ShowDialog();
            
            return dialogResult == true 
                ? (selectedPath, includeSubfoldersCheckBox.IsChecked == true) 
                : (null, false);
        }
        
        public string? SelectFolder(string title = "フォルダを選択してください")
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = title,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            var result = dialog.ShowDialog();
            return result == DialogResult.OK ? dialog.SelectedPath : null;
        }
        
        public string? SelectFile(string title = "ファイルを選択してください", string filter = "すべてのファイル (*.*)|*.*")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                CheckPathExists = true
            };

            var result = dialog.ShowDialog(System.Windows.Application.Current.MainWindow);
            return result == true ? dialog.FileName : null;
        }

        public bool ShowConfirmation(string message, string title = "確認")
        {
            var result = Mangaanya.Views.CustomMessageBox.Show(message, title, Mangaanya.Views.CustomMessageBoxButton.YesNo);
            return result == Mangaanya.Views.CustomMessageBoxResult.Yes;
        }

        public void ShowInformation(string message, string title = "情報")
        {
            Mangaanya.Views.CustomMessageBox.Show(message, title, Mangaanya.Views.CustomMessageBoxButton.OK);
        }

        public void ShowError(string message, string title = "エラー")
        {
            Mangaanya.Views.CustomMessageBox.Show(message, title, Mangaanya.Views.CustomMessageBoxButton.OK);
        }
        
        public Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "")
        {
            var tcs = new TaskCompletionSource<string?>();
            
            // カスタムダイアログを作成
            var dialog = new System.Windows.Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize
            };
            
            // レイアウト
            var grid = new System.Windows.Controls.Grid();
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            
            // メッセージ
            var messageTextBlock = new System.Windows.Controls.TextBlock
            {
                Text = message,
                Margin = new Thickness(10),
                TextWrapping = TextWrapping.Wrap
            };
            
            // 入力フィールド
            var inputTextBox = new System.Windows.Controls.TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(10),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };
            
            // ボタン
            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            
            var okButton = new System.Windows.Controls.Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 5, 0),
                IsDefault = true
            };
            
            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "キャンセル",
                Width = 80,
                IsCancel = true
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            // グリッドに配置
            grid.Children.Add(messageTextBlock);
            grid.Children.Add(inputTextBox);
            grid.Children.Add(buttonPanel);
            
            System.Windows.Controls.Grid.SetRow(messageTextBlock, 0);
            System.Windows.Controls.Grid.SetRow(inputTextBox, 1);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 2);
            
            dialog.Content = grid;
            
            // イベント
            okButton.Click += (s, e) =>
            {
                dialog.DialogResult = true;
            };
            
            dialog.Loaded += (s, e) =>
            {
                inputTextBox.Focus();
                inputTextBox.SelectAll();
            };
            
            // ダイアログ表示
            var result = dialog.ShowDialog();
            
            if (result == true)
            {
                tcs.SetResult(inputTextBox.Text);
            }
            else
            {
                tcs.SetResult(null);
            }
            
            return tcs.Task;
        }
        
        public async Task<ConflictResolution> ShowConflictResolutionDialogAsync(string title, string message, FileMoveConflictType conflictType)
        {
            // MainWindowから通知システムを使用して競合解決ダイアログを表示
            var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                return await mainWindow.ShowConflictResolutionAsync(title, message, conflictType);
            }
            
            // フォールバック: CustomMessageBoxを使用
            CustomMessageBoxButton buttonType = conflictType switch
            {
                FileMoveConflictType.SameFolder => CustomMessageBoxButton.SkipCancel,
                FileMoveConflictType.FileExists => CustomMessageBoxButton.OverwriteSkipCancel,
                _ => CustomMessageBoxButton.OK
            };
            
            var result = CustomMessageBox.Show(message, title, buttonType);
            
            return result switch
            {
                CustomMessageBoxResult.Skip => ConflictResolution.Skip,
                CustomMessageBoxResult.Overwrite => ConflictResolution.Cancel,
                CustomMessageBoxResult.Cancel => ConflictResolution.Cancel,
                _ => ConflictResolution.Cancel
            };
        }
    }
}
