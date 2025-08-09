using Mangaanya.Models;

namespace Mangaanya.Services
{
    public interface IThumbnailService
    {
        /// <summary>
        /// 指定されたファイルのサムネイルを生成します
        /// </summary>
        /// <param name="mangaFile">サムネイルを生成する漫画ファイル</param>
        /// <returns>サムネイル生成結果</returns>
        Task<ThumbnailGenerationResult> GenerateThumbnailAsync(MangaFile mangaFile);

        /// <summary>
        /// 複数のファイルのサムネイルを一括生成します
        /// </summary>
        /// <param name="mangaFiles">サムネイルを生成する漫画ファイルのリスト</param>
        /// <param name="progress">進捗報告用</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <param name="skipExisting">既存のサムネイルをスキップするかどうか</param>
        /// <returns>サムネイル生成結果のリスト</returns>
        Task<List<ThumbnailGenerationResult>> GenerateThumbnailsBatchAsync(
            IEnumerable<MangaFile> mangaFiles, 
            IProgress<ThumbnailProgress>? progress = null,
            CancellationToken cancellationToken = default,
            bool skipExisting = true);

        /// <summary>
        /// サムネイルファイルが存在するかチェックします
        /// </summary>
        /// <param name="thumbnailPath">サムネイルファイルのパス</param>
        /// <returns>存在する場合はtrue</returns>
        bool ThumbnailExists(string? thumbnailPath);

        /// <summary>
        /// サムネイルファイルを削除します
        /// </summary>
        /// <param name="thumbnailPath">削除するサムネイルファイルのパス</param>
        /// <returns>削除に成功した場合はtrue</returns>
        Task<bool> DeleteThumbnailAsync(string? thumbnailPath);

        /// <summary>
        /// デフォルトのダミー画像パスを取得します
        /// </summary>
        /// <returns>ダミー画像のパス</returns>
        string GetDefaultThumbnailPath();

        /// <summary>
        /// 指定されたファイルのサムネイル画像を取得します（遅延読み込み用）
        /// </summary>
        /// <param name="mangaFile">サムネイルを取得する漫画ファイル</param>
        /// <returns>サムネイル画像のBitmapImage</returns>
        Task<System.Windows.Media.Imaging.BitmapImage> GetThumbnailImageAsync(MangaFile mangaFile);
    }

    public class ThumbnailGenerationResult
    {
        public bool Success { get; set; }
        public string? ThumbnailPath { get; set; }
        public string? ErrorMessage { get; set; }
        public MangaFile? MangaFile { get; set; }
    }

    public class ThumbnailProgress
    {
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
