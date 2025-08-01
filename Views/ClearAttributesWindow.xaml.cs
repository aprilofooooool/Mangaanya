using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using Mangaanya.ViewModels;
using Mangaanya.Models;

namespace Mangaanya.Views
{
    public partial class ClearAttributesWindow : Window
    {
        private readonly MainViewModel _viewModel;
        
        public ClearAttributesWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = new { SelectedFilesCount = _viewModel.SelectedMangaFiles.Count };
        }
        
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            // すべてのチェックボックスを選択
            TitleCheckBox.IsChecked = true;
            OriginalAuthorCheckBox.IsChecked = true;
            ArtistCheckBox.IsChecked = true;
            AuthorReadingCheckBox.IsChecked = true;
            VolumeNumberCheckBox.IsChecked = true;
            GenreCheckBox.IsChecked = true;
            PublishDateCheckBox.IsChecked = true;
            PublisherCheckBox.IsChecked = true;
            TagsCheckBox.IsChecked = true;
            AIProcessedCheckBox.IsChecked = true;
        }
        
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // 選択された属性のリストを作成
            var selectedAttributes = new List<string>();
            
            if (TitleCheckBox.IsChecked == true) selectedAttributes.Add("タイトル");
            if (OriginalAuthorCheckBox.IsChecked == true) selectedAttributes.Add("原作者");
            if (ArtistCheckBox.IsChecked == true) selectedAttributes.Add("作画者");
            if (AuthorReadingCheckBox.IsChecked == true) selectedAttributes.Add("作者読み");
            if (VolumeNumberCheckBox.IsChecked == true) selectedAttributes.Add("巻数");
            if (GenreCheckBox.IsChecked == true) selectedAttributes.Add("ジャンル");
            if (PublishDateCheckBox.IsChecked == true) selectedAttributes.Add("発行日");
            if (PublisherCheckBox.IsChecked == true) selectedAttributes.Add("出版社");
            if (TagsCheckBox.IsChecked == true) selectedAttributes.Add("タグ");
            if (AIProcessedCheckBox.IsChecked == true) selectedAttributes.Add("AI処理済みフラグ");
            
            // 何も選択されていない場合
            if (selectedAttributes.Count == 0)
            {
                Mangaanya.Views.CustomMessageBox.Show("クリアする属性が選択されていません。", "警告", Mangaanya.Views.CustomMessageBoxButton.OK);
                return;
            }
            
            // 確認ダイアログを表示
            var message = $"選択された{_viewModel.SelectedMangaFiles.Count}件のファイルから、以下の属性情報をクリアします：\n\n" +
                          string.Join("\n", selectedAttributes) + "\n\n" +
                          "この操作は元に戻せません。続行しますか？";
                          
            var result = Mangaanya.Views.CustomMessageBox.Show(message, "確認", Mangaanya.Views.CustomMessageBoxButton.YesNo);
            
            if (result == Mangaanya.Views.CustomMessageBoxResult.Yes)
            {
                // 属性クリアを実行
                _viewModel.ClearAttributesCommand.Execute(new ClearAttributesParameters
                {
                    ClearTitle = TitleCheckBox.IsChecked == true,
                    ClearOriginalAuthor = OriginalAuthorCheckBox.IsChecked == true,
                    ClearArtist = ArtistCheckBox.IsChecked == true,
                    ClearAuthorReading = AuthorReadingCheckBox.IsChecked == true,
                    ClearVolumeNumber = VolumeNumberCheckBox.IsChecked == true,
                    ClearGenre = GenreCheckBox.IsChecked == true,
                    ClearPublishDate = PublishDateCheckBox.IsChecked == true,
                    ClearPublisher = PublisherCheckBox.IsChecked == true,
                    ClearTags = TagsCheckBox.IsChecked == true,
                    ClearAIProcessed = AIProcessedCheckBox.IsChecked == true
                });
                
                Close();
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
