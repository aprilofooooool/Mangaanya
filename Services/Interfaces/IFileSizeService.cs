using Mangaanya.Models;

namespace Mangaanya.Services
{
    /// <summary>
    /// ファイルサイズ統計サービスのインターフェース
    /// </summary>
    public interface IFileSizeService
    {
        /// <summary>
        /// 総ファイルサイズ統計を取得します
        /// </summary>
        /// <returns>ファイルサイズ統計情報</returns>
        Task<FileSizeStatistics> GetTotalFileSizeAsync();

        /// <summary>
        /// フォルダごとの統計情報を取得します
        /// </summary>
        /// <returns>フォルダパスをキーとした統計情報の辞書</returns>
        Task<Dictionary<string, FolderStatistics>> GetFolderStatisticsAsync();

        /// <summary>
        /// ファイルサイズを人間が読みやすい形式にフォーマットします（TB対応）
        /// </summary>
        /// <param name="bytes">バイト数</param>
        /// <returns>フォーマットされたファイルサイズ文字列</returns>
        string FormatFileSize(long bytes);
    }
}