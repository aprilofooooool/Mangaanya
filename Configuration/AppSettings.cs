using Mangaanya.Models;
using Mangaanya.Services;

namespace Mangaanya.Configuration
{
    /// <summary>
    /// アプリケーション設定の型安全なキー定義クラス
    /// </summary>
    public static class AppSettings
    {
        /// <summary>
        /// 最大メモリ使用量（バイト）
        /// </summary>
        public static readonly SettingKey<long> MaxMemoryUsage = new("MaxMemoryUsage", 8L * 1024 * 1024 * 1024); // 8GB

        /// <summary>
        /// キャッシュサイズ（バイト）
        /// </summary>
        public static readonly SettingKey<long> CacheSize = new("CacheSize", 1024 * 1024 * 1024); // 1GB

        /// <summary>
        /// AI API の最大同時リクエスト数
        /// </summary>
        public static readonly SettingKey<int> MaxConcurrentAIRequests = new("MaxConcurrentAIRequests", 30);

        /// <summary>
        /// サムネイル表示の有効/無効
        /// </summary>
        public static readonly SettingKey<bool> ShowThumbnails = new("ShowThumbnails", true);

        /// <summary>
        /// サムネイル表示モード
        /// </summary>
        public static readonly SettingKey<ThumbnailDisplayMode> ThumbnailDisplay = new("ThumbnailDisplay", ThumbnailDisplayMode.Standard);

        /// <summary>
        /// サムネイルポップアップの非表示遅延時間（秒）
        /// </summary>
        public static readonly SettingKey<double> ThumbnailPopupHideDelay = new("ThumbnailPopupHideDelay", 3.0);

        /// <summary>
        /// サムネイルポップアップのマウス離脱遅延時間（秒）
        /// </summary>
        public static readonly SettingKey<double> ThumbnailPopupLeaveDelay = new("ThumbnailPopupLeaveDelay", 0.5);

        /// <summary>
        /// スキャン対象フォルダのリスト
        /// </summary>
        public static readonly SettingKey<List<string>> ScanFolders = new("ScanFolders", new List<string>());

        /// <summary>
        /// Gemini API キー
        /// </summary>
        public static readonly SettingKey<string?> GeminiApiKey = new("GeminiApiKey", null);

        /// <summary>
        /// DataGrid の設定
        /// </summary>
        public static readonly SettingKey<DataGridSettings> DataGridSettings = new("DataGridSettings", new DataGridSettings());
    }
}