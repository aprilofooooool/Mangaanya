using Microsoft.Extensions.Logging;
using Mangaanya.Models;

namespace Mangaanya.Services
{
    /// <summary>
    /// ファイルサイズ統計サービスの実装
    /// </summary>
    public class FileSizeService : IFileSizeService
    {
        private readonly IMangaRepository _repository;
        private readonly ILogger<FileSizeService> _logger;

        public FileSizeService(IMangaRepository repository, ILogger<FileSizeService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        /// <summary>
        /// 総ファイルサイズ統計を取得します
        /// </summary>
        public async Task<FileSizeStatistics> GetTotalFileSizeAsync()
        {
            try
            {
                return await _repository.GetFileSizeStatisticsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "総ファイルサイズ統計の取得中にエラーが発生しました");
                
                // エラー時はデフォルト値を返す（グレースフル劣化）
                return new FileSizeStatistics
                {
                    TotalFileCount = 0,
                    TotalFileSize = 0,
                    AIProcessedCount = 0
                };
            }
        }

        /// <summary>
        /// フォルダごとの統計情報を取得します
        /// </summary>
        public async Task<Dictionary<string, FolderStatistics>> GetFolderStatisticsAsync()
        {
            try
            {
                return await _repository.GetFolderStatisticsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フォルダ統計情報の取得中にエラーが発生しました");
                
                // エラー時は空の辞書を返す（グレースフル劣化）
                return new Dictionary<string, FolderStatistics>();
            }
        }

        /// <summary>
        /// ファイルサイズを人間が読みやすい形式にフォーマットします（TB対応）
        /// </summary>
        public string FormatFileSize(long bytes)
        {
            if (bytes < 0)
                return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
    }
}