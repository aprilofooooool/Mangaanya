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
        /// サムネイル状況の詳細診断を実行します
        /// </summary>
        /// <returns>診断結果</returns>
        Task<string> DiagnoseThumbnailStatusAsync();

        /// <summary>
        /// 孤立したサムネイルファイルの数を取得します
        /// </summary>
        /// <returns>孤立ファイル数</returns>
        Task<int> GetOrphanedThumbnailCountAsync();
    }
}
