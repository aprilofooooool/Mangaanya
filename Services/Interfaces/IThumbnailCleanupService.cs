using Mangaanya.Models;

namespace Mangaanya.Services
{
    /// <summary>
    /// サムネイルファイルのクリーンアップ機能を提供するサービスのインターフェース
    /// </summary>
    public interface IThumbnailCleanupService
    {
        /// <summary>
        /// データベースに登録されていない孤立したサムネイルファイルを削除します
        /// </summary>
        /// <returns>クリーンアップ結果</returns>
        Task<CleanupResult> CleanupOrphanedThumbnailsAsync();

        /// <summary>
        /// 指定した日数より古いサムネイルファイルを削除します
        /// </summary>
        /// <param name="maxAgeDays">保持する最大日数</param>
        /// <returns>クリーンアップ結果</returns>
        Task<CleanupResult> CleanupOldThumbnailsAsync(int maxAgeDays);

        /// <summary>
        /// サムネイル状況の詳細診断を実行します
        /// </summary>
        /// <returns>診断結果</returns>
        Task<string> DiagnoseThumbnailStatusAsync();

        /// <summary>
        /// 孤立したサムネイルファイルの数を取得します
        /// </summary>
        /// <returns>孤立ファイル数</returns>
        Task<int> GetOrphanedThumbnailCountAsync();

        /// <summary>
        /// サムネイル件数をチェックし、指定件数を超えている場合のみ古いファイルを削除します
        /// </summary>
        /// <param name="maxAgeDays">保持する最大日数</param>
        /// <param name="maxFileCount">削除を実行する最小ファイル数</param>
        /// <returns>クリーンアップ結果</returns>
        Task<CleanupResult> CleanupOldThumbnailsIfExceedsCountAsync(int maxAgeDays, int maxFileCount);
    }
}
