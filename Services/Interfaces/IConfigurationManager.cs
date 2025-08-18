namespace Mangaanya.Services
{
    public enum ThumbnailDisplayMode
    {
        Hidden = 0,     // 非表示
        Compact = 1,    // 縮小
        Standard = 2    // 標準（現在のデフォルト）
    }

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
        public ThumbnailDisplayMode ThumbnailDisplay { get; set; } = ThumbnailDisplayMode.Standard;
        public double ThumbnailPopupHideDelay { get; set; } = 3.0;
        public double ThumbnailPopupLeaveDelay { get; set; } = 0.5;
        public List<string> ScanFolders { get; set; } = new();
        public string? GeminiApiKey { get; set; }
        public Models.DataGridSettings DataGridSettings { get; set; } = new Models.DataGridSettings();
    }
}
