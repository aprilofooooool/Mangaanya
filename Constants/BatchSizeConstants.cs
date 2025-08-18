namespace Mangaanya.Constants
{
    /// <summary>
    /// バッチ処理関連の定数を定義するクラス
    /// </summary>
    public static class BatchSizeConstants
    {
        /// <summary>
        /// ファイルスキャン時のデフォルトバッチサイズ
        /// </summary>
        public const int DefaultScanBatch = 200;

        /// <summary>
        /// データベース操作時のデフォルトバッチサイズ
        /// </summary>
        public const int DefaultDatabaseBatch = 1000;

        /// <summary>
        /// サムネイル生成時のデフォルトバッチサイズ
        /// </summary>
        public const int DefaultThumbnailBatch = 10;

        /// <summary>
        /// AI処理時のデフォルト並行処理数
        /// </summary>
        public const int DefaultAIConcurrency = 30;

        /// <summary>
        /// サムネイル一括取得時の最大バッチサイズ
        /// パフォーマンス最適化のため100件ずつ処理
        /// </summary>
        public const int ThumbnailRetrievalBatch = 100;

        /// <summary>
        /// 削除処理の進捗報告間隔
        /// </summary>
        public const int DeleteProgressInterval = 10;

        /// <summary>
        /// 検索結果の取得件数制限
        /// </summary>
        public const int SearchResultLimit = 10;
    }
}