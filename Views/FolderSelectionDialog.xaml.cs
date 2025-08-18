using System.Windows;
using Mangaanya.ViewModels;

namespace Mangaanya.Views
{
    /// <summary>
    /// FolderSelectionDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class FolderSelectionDialog : Window
    {
        public FolderSelectionViewModel ViewModel { get; }

        public FolderSelectionDialog(FolderSelectionViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
            
            // ダイアログの初期化
            Loaded += FolderSelectionDialog_Loaded;
        }

        private async void FolderSelectionDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // フォルダリストを読み込み
                await ViewModel.LoadFoldersAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"フォルダリストの読み込み中にエラーが発生しました: {ex.Message}", 
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedFolder != null)
            {
                DialogResult = true;
            }
            else
            {
                System.Windows.MessageBox.Show("移動先フォルダを選択してください。", 
                    "選択エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// 選択されたフォルダパスを取得
        /// </summary>
        public string? SelectedFolderPath => ViewModel.SelectedFolder?.FolderPath;
    }
}