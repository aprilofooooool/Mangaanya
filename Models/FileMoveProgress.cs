namespace Mangaanya.Models
{
    /// <summary>
    /// ファイル移動処理の進捗情報を表すクラス
    /// </summary>
    public class FileMoveProgress
    {
        /// <summary>
        /// 現在処理中のファイル番号（1から開始）
        /// </summary>
        public int CurrentFile { get; set; }

        /// <summary>
        /// 処理対象の総ファイル数
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// 現在処理中のファイル名
        /// </summary>
        public string CurrentFileName { get; set; } = string.Empty;

        /// <summary>
        /// 現在実行中の操作
        /// </summary>
        public FileMoveOperation Operation { get; set; }

        /// <summary>
        /// 進捗率（0-100）
        /// </summary>
        public double ProgressPercentage => TotalFiles > 0 ? (double)CurrentFile / TotalFiles * 100 : 0;

        /// <summary>
        /// 進捗の説明文を取得
        /// </summary>
        /// <returns>進捗の説明文</returns>
        public string GetProgressDescription()
        {
            var operationText = Operation switch
            {
                FileMoveOperation.Validating => "検証中",
                FileMoveOperation.Moving => "移動中",
                FileMoveOperation.UpdatingDatabase => "データベース更新中",
                FileMoveOperation.Complete => "完了",
                _ => "処理中"
            };

            return $"{operationText}: {CurrentFileName} ({CurrentFile}/{TotalFiles})";
        }
    }
}