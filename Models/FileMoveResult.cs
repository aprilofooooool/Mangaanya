namespace Mangaanya.Models
{
    /// <summary>
    /// ファイル移動処理の結果を表すクラス
    /// </summary>
    public class FileMoveResult
    {
        /// <summary>
        /// 正常に移動されたファイル数
        /// </summary>
        public int SuccessCount { get; set; }

        /// <summary>
        /// スキップされたファイル数
        /// </summary>
        public int SkippedCount { get; set; }

        /// <summary>
        /// エラーが発生したファイル数
        /// </summary>
        public int ErrorCount { get; set; }

        /// <summary>
        /// エラーの詳細情報リスト
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 処理がキャンセルされたかどうか
        /// </summary>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// 処理が成功したかどうか（全ファイルが正常に処理された場合true）
        /// </summary>
        public bool IsSuccess => ErrorCount == 0 && !IsCancelled;

        /// <summary>
        /// 処理されたファイルの総数
        /// </summary>
        public int TotalCount => SuccessCount + SkippedCount + ErrorCount;

        /// <summary>
        /// 結果の概要を文字列で取得
        /// </summary>
        /// <returns>結果の概要文字列</returns>
        public string GetSummary()
        {
            if (IsCancelled)
            {
                return "キャンセルされました";
            }
            return $"成功: {SuccessCount}, スキップ: {SkippedCount}, エラー: {ErrorCount}";
        }
    }
}