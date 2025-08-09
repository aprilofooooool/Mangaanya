namespace Mangaanya.Models
{
    /// <summary>
    /// フォルダ統計情報を表すデータモデル
    /// </summary>
    public class FolderStatistics
    {
        /// <summary>
        /// フォルダパス
        /// </summary>
        public string FolderPath { get; set; } = string.Empty;

        /// <summary>
        /// フォルダ内のファイル数
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        /// フォルダ内の総ファイルサイズ（バイト）
        /// </summary>
        public long TotalSize { get; set; }
    }
}