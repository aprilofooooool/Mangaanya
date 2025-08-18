namespace Mangaanya.Models
{
    /// <summary>
    /// フォルダ表示用のアイテム
    /// </summary>
    public class FolderDisplayItem
    {
        /// <summary>
        /// フォルダパス
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// 表示テキスト（フォルダ名 + 統計情報）
        /// </summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>
        /// フォルダ名のみ
        /// </summary>
        public string FolderName { get; set; } = string.Empty;

        /// <summary>
        /// 統計情報テキスト
        /// </summary>
        public string StatisticsText { get; set; } = string.Empty;
    }
}