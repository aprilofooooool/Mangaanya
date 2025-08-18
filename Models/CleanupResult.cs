namespace Mangaanya.Models
{
    /// <summary>
    /// クリーンアップ処理の結果を表すクラス
    /// </summary>
    public class CleanupResult
    {
        public bool Success { get; set; }
        public int DeletedCount { get; set; }
        public string? ErrorMessage { get; set; }
        
        public static CleanupResult CreateSuccess(int deletedCount)
        {
            return new CleanupResult { Success = true, DeletedCount = deletedCount };
        }
        
        public static CleanupResult CreateError(string errorMessage)
        {
            return new CleanupResult { Success = false, DeletedCount = 0, ErrorMessage = errorMessage };
        }
    }
}
