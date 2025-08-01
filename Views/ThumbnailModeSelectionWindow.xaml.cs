using System.Windows;

namespace Mangaanya.Views
{
    public partial class ThumbnailModeSelectionWindow : Window
    {
        public ThumbnailGenerationMode SelectedMode { get; private set; } = ThumbnailGenerationMode.Cancel;

        public ThumbnailModeSelectionWindow(int fileCount)
        {
            InitializeComponent();
            FileCountText.Text = $"対象ファイル数: {fileCount}件";
        }

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (OnlyMissingRadio.IsChecked == true)
            {
                SelectedMode = ThumbnailGenerationMode.OnlyMissing;
            }
            else if (RegenerateAllRadio.IsChecked == true)
            {
                SelectedMode = ThumbnailGenerationMode.RegenerateAll;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedMode = ThumbnailGenerationMode.Cancel;
            DialogResult = false;
            Close();
        }
    }

    public enum ThumbnailGenerationMode
    {
        Cancel,
        OnlyMissing,
        RegenerateAll
    }
}
