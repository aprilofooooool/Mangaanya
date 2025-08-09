using Mangaanya.Models;

namespace Mangaanya.Services
{
    public interface IMangaRepository
    {
        Task<List<MangaFile>> GetAllAsync();
        Task<MangaFile?> GetByIdAsync(int id);
        Task<List<MangaFile>> SearchAsync(SearchCriteria criteria);
        Task<int> InsertAsync(MangaFile manga);
        Task<int> InsertBatchAsync(IEnumerable<MangaFile> mangaFiles);
        Task UpdateAsync(MangaFile manga);
        Task UpdateBatchAsync(IEnumerable<MangaFile> mangaFiles);
        Task UpdateFilePathsBatchAsync(IEnumerable<(int Id, string NewFilePath, string? NewThumbnailPath)> filePathUpdates);
        Task DeleteAsync(int id);
        Task<int> DeleteByFolderPathAsync(string folderPath);
        Task<int> GetTotalCountAsync();
        Task InitializeDatabaseAsync();
        Task<List<string>> GetAllThumbnailPathsAsync();
        Task ClearAllAsync();
        Task<List<MangaFile>> SearchByRatingAsync(int? rating);
        Task UpdateRatingBatchAsync(IEnumerable<int> fileIds, int? rating);
        Task<FileSizeStatistics> GetFileSizeStatisticsAsync();
        Task<Dictionary<string, FolderStatistics>> GetFolderStatisticsAsync();
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
