using Mangaanya.Models;

namespace Mangaanya.Services
{
    public interface IAIService : IDisposable
    {
        Task<AIResult> GetMangaInfoAsync(string title, string author);
        Task<List<AIResult>> GetMangaInfoBatchAsync(List<MangaFile> files, int maxConcurrency = 30);
        bool IsApiAvailable();
        void ClearCache();
        void CleanupCache();
    }

    public class AIResult
    {
        public bool Success { get; set; }
        public string? Genre { get; set; }
        public DateTime? PublishDate { get; set; }
        public string? Publisher { get; set; }
        public string? Tags { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
