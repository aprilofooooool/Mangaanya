using Mangaanya.Models;

namespace Mangaanya.Services
{
    public interface IMangaRepository
    {
        Task<List<MangaFile>> GetAllAsync();
        
        /// <summary>
        /// サムネイルデータを除外した軽量なファイル一覧を取得します。
        /// 大量ファイル環境でのパフォーマンス最適化のため、thumbnail_dataフィールドを除外します。
        /// 既存のサムネイル遅延読み込み機能（LazyThumbnailConverterOptimized）と連携して動作します。
        /// </summary>
        /// <returns>サムネイルデータを除外したMangaFileのリスト</returns>
        Task<List<MangaFile>> GetAllWithoutThumbnailsAsync();
        Task<MangaFile?> GetByIdAsync(int id);
        Task<List<MangaFile>> SearchAsync(SearchCriteria criteria);
        Task<int> InsertAsync(MangaFile manga);
        Task<int> InsertBatchAsync(IEnumerable<MangaFile> mangaFiles);
        Task UpdateAsync(MangaFile manga);
        Task UpdateBatchAsync(IEnumerable<MangaFile> mangaFiles);
        Task UpdateFilePathsBatchAsync(IEnumerable<(int Id, string NewFilePath)> filePathUpdates);
        Task DeleteAsync(int id);
        Task<int> DeleteBatchAsync(IEnumerable<int> ids);
        Task<int> DeleteByFolderPathAsync(string folderPath);
        Task<int> GetTotalCountAsync();
        Task InitializeDatabaseAsync();
        Task<List<string>> GetAllThumbnailPathsAsync();
        Task ClearAllAsync();
        Task<List<MangaFile>> SearchByRatingAsync(int? rating);
        Task UpdateRatingBatchAsync(IEnumerable<int> fileIds, int? rating);
        Task<FileSizeStatistics> GetFileSizeStatisticsAsync();
        Task<Dictionary<string, FolderStatistics>> GetFolderStatisticsAsync();
        Task<(long BeforeSize, long AfterSize)> OptimizeDatabaseAsync();
        
        /// <summary>
        /// 指定されたIDのサムネイルデータを取得します。
        /// 遅延読み込み機能で使用され、軽量読み込み後にサムネイル表示が必要な場合に呼び出されます。
        /// </summary>
        /// <param name="id">取得対象のファイルID</param>
        /// <returns>サムネイルデータ（存在しない場合はnull）</returns>
        Task<byte[]?> GetThumbnailDataByIdAsync(int id);
        
        /// <summary>
        /// 複数のIDのサムネイルデータを一括取得します。
        /// パフォーマンス最適化のため、最大100件ずつ処理します。
        /// </summary>
        /// <param name="ids">取得対象のファイルIDリスト</param>
        /// <returns>IDとサムネイルデータのディクショナリ</returns>
        Task<Dictionary<int, byte[]>> GetThumbnailDataBatchAsync(IEnumerable<int> ids);
    }

    public class SearchCriteria
    {
        public string? SearchText { get; set; }
        public bool? IsAIProcessed { get; set; }
        public bool? IsCorrupted { get; set; }
        public DateTime? ModifiedAfter { get; set; }
        public DateTime? ModifiedBefore { get; set; }
        public string? Genre { get; set; }
        public string? Publisher { get; set; }
        public int? Rating { get; set; }
    }
}
