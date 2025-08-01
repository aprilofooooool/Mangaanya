namespace Mangaanya.Services
{
    public interface IConfigurationManager
    {
        T GetSetting<T>(string key, T defaultValue = default!);
        void SetSetting<T>(string key, T value);
        Task SaveAsync();
        Task LoadAsync();
    }

    public class AppSettings
    {
        public long MaxMemoryUsage { get; set; } = 8L * 1024 * 1024 * 1024; // 8GB
        public long CacheSize { get; set; } = 1024 * 1024 * 1024; // 1GB
        public int MaxConcurrentAIRequests { get; set; } = 30;
        public bool ShowThumbnails { get; set; } = true;
        public List<string> ScanFolders { get; set; } = new();
        public string? GeminiApiKey { get; set; }
        public Models.DataGridSettings DataGridSettings { get; set; } = new Models.DataGridSettings();
    }
}
