namespace Mangaanya.Constants
{
    /// <summary>
    /// UI関連の定数を定義するクラス
    /// </summary>
    public static class UIConstants
    {
        /// <summary>
        /// デフォルトの列幅設定
        /// </summary>
        public static class ColumnWidths
        {
            public const double Thumbnail = 190;
            public const double Path = 300;
            public const double FileName = 200;
            public const double Size = 80;
            public const double Type = 60;
            public const double OriginalAuthor = 100;
            public const double Artist = 100;
            public const double Title = 150;
            public const double Volume = 60;
            public const double Tags = 200;
            public const double Genre = 100;
            public const double Publisher = 100;
        }

        /// <summary>
        /// ポップアップ表示位置の調整値
        /// </summary>
        public static class PopupOffset
        {
            /// <summary>
            /// マウス位置からの水平オフセット（ピクセル）
            /// </summary>
            public const double HorizontalOffset = 20;

            /// <summary>
            /// マウス位置からの垂直オフセット（ピクセル）
            /// </summary>
            public const double VerticalOffset = -200;
        }

        /// <summary>
        /// 通知色の設定
        /// </summary>
        public static class NotificationColors
        {
            /// <summary>
            /// 警告色（RGB値）
            /// </summary>
            public static readonly System.Windows.Media.Color Warning = 
                System.Windows.Media.Color.FromRgb(255, 152, 0); // #FF9800
        }

        /// <summary>
        /// ダイアログのデフォルトサイズ
        /// </summary>
        public static class DialogSize
        {
            public const double DefaultWidth = 500;
            public const double DefaultHeight = 200;
        }

        /// <summary>
        /// サムネイル描画時の枠線設定
        /// </summary>
        public static class ThumbnailBorder
        {
            public const int X = 10;
            public const int Y = 10;
            public const int Width = 460;
            public const int Height = 300;
            public const int BorderWidth = 2;
        }
    }
}