namespace Mangaanya.Constants
{
    /// <summary>
    /// サムネイル関連の定数を定義するクラス
    /// </summary>
    public static class ThumbnailConstants
    {
        /// <summary>
        /// 標準表示モード時の行の高さ（ピクセル）
        /// </summary>
        public const double StandardRowHeight = 84.0;

        /// <summary>
        /// コンパクト表示モード時の行の高さ（ピクセル）
        /// </summary>
        public const double CompactRowHeight = 25.0;

        /// <summary>
        /// サムネイル画像の幅（ピクセル）
        /// </summary>
        public const int Width = 480;

        /// <summary>
        /// サムネイル画像の高さ（ピクセル）
        /// </summary>
        public const int Height = 320;

        /// <summary>
        /// JPEG圧縮品質（パーセント）
        /// </summary>
        public const int JpegQuality = 85;

        /// <summary>
        /// 平均的なサムネイル画像サイズ（バイト）
        /// 480x320ピクセル、圧縮済みの想定値
        /// </summary>
        public const long AverageThumbnailSize = 30 * 1024;

        /// <summary>
        /// サムネイル生成時の最大並列処理数
        /// </summary>
        public const int MaxConcurrency = 10;

        /// <summary>
        /// 進捗報告の間隔（処理件数）
        /// </summary>
        public const int ProgressReportInterval = 100;

        /// <summary>
        /// サムネイル表示の最大幅（ピクセル）
        /// </summary>
        public const double MaxWidth = 180;

        /// <summary>
        /// サムネイル表示の最大高さ（ピクセル）
        /// </summary>
        public const double MaxHeight = 120;
    }
}