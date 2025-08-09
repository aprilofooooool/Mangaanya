namespace Mangaanya.Models
{
    /// <summary>
    /// ファイルサイズ統計情報を表すデータモデル
    /// </summary>
    public class FileSizeStatistics
    {
        /// <summary>
        /// 総ファイル数
        /// </summary>
        public int TotalFileCount { get; set; }

        /// <summary>
        /// 総ファイルサイズ（バイト）
        /// </summary>
        public long TotalFileSize { get; set; }

        /// <summary>
        /// AI処理済みファイル数
        /// </summary>
        public int AIProcessedCount { get; set; }
    }
}