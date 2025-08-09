using System.Windows;
using System.Windows.Controls;

namespace Mangaanya.Views
{
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBoxResult Result { get; private set; } = CustomMessageBoxResult.Cancel;

        private CustomMessageBox(string message, string title, CustomMessageBoxButton buttons)
        {
            InitializeComponent();
            Title = title;
            MessageText.Text = message;
            CreateButtons(buttons);
            
            // SizeToContentを設定して自動サイズ調整
            SizeToContent = SizeToContent.Height;
            
            // 最大高さを設定
            MaxHeight = SystemParameters.PrimaryScreenHeight * 0.8;
        }

        private void CreateButtons(CustomMessageBoxButton buttons)
        {
            switch (buttons)
            {
                case CustomMessageBoxButton.OK:
                    AddButton("OK", CustomMessageBoxResult.OK, true, true);
                    break;
                    
                case CustomMessageBoxButton.OKCancel:
                    AddButton("OK", CustomMessageBoxResult.OK, true, false);
                    AddButton("キャンセル", CustomMessageBoxResult.Cancel, false, true);
                    break;
                    
                case CustomMessageBoxButton.YesNo:
                    AddButton("はい", CustomMessageBoxResult.Yes, true, false);
                    AddButton("いいえ", CustomMessageBoxResult.No, false, true);
                    break;
                    
                case CustomMessageBoxButton.YesNoCancel:
                    AddButton("はい", CustomMessageBoxResult.Yes, true, false);
                    AddButton("いいえ", CustomMessageBoxResult.No, false, false);
                    AddButton("キャンセル", CustomMessageBoxResult.Cancel, false, true);
                    break;
                    
                case CustomMessageBoxButton.SkipCancel:
                    AddButton("スキップ", CustomMessageBoxResult.Skip, true, false);
                    AddButton("キャンセル", CustomMessageBoxResult.Cancel, false, true);
                    break;
                    
                case CustomMessageBoxButton.OverwriteSkipCancel:
                    AddButton("上書き", CustomMessageBoxResult.Overwrite, false, false);
                    AddButton("スキップ", CustomMessageBoxResult.Skip, true, false);
                    AddButton("キャンセル", CustomMessageBoxResult.Cancel, false, true);
                    break;
            }
        }

        private void AddButton(string content, CustomMessageBoxResult result, bool isDefault, bool isCancel)
        {
            var button = new System.Windows.Controls.Button
            {
                Content = content,
                Width = 80,
                Height = 30,
                Margin = new Thickness(8, 0, 0, 0),
                IsDefault = isDefault,
                IsCancel = isCancel,
                FontSize = 12
            };

            button.Click += (sender, e) =>
            {
                Result = result;
                DialogResult = result != CustomMessageBoxResult.Cancel;
                Close();
            };

            ButtonPanel.Children.Add(button);
        }

        public static CustomMessageBoxResult Show(string message, string title = "確認", CustomMessageBoxButton buttons = CustomMessageBoxButton.OK)
        {
            var dialog = new CustomMessageBox(message, title, buttons)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            dialog.ShowDialog();
            return dialog.Result;
        }
    }

    public enum CustomMessageBoxButton
    {
        OK,
        OKCancel,
        YesNo,
        YesNoCancel,
        SkipCancel,
        OverwriteSkipCancel
    }

    public enum CustomMessageBoxResult
    {
        OK,
        Cancel,
        Yes,
        No,
        Skip,
        Overwrite
    }
}
