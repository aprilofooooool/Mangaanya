using Mangaanya.Models;
using Mangaanya.Common;
using System.Windows.Media.Imaging;

namespace Mangaanya.Services
{
    public interface IThumbnailService
    {
        /// <summary>
        /// 指定されたファイルのサムネイルを生成します
        /// </summary>
        /// <param name="mangaFile">サムネイルを生成する漫画ファイル</param>
        /// <returns>サムネイル生成結果</returns>
        Task<Result<ThumbnailGenerationResult>> GenerateThumbnailAsync(MangaFile mangaFile);

        /// <summary>
        /// 複数のファイルのサムネイルを一括生成します
        /// </summary>
        /// <param name="mangaFiles">サムネイルを生成する漫画ファイルのリスト</param>
        /// <param name="progress">進捗報告用</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <param name="skipExisting">既存のサムネイルをスキップするかどうか</param>
        /// <returns>サムネイル生成結果のリスト</returns>
        Task<Result<List<ThumbnailGenerationResult>>> GenerateThumbnailsBatchAsync(
            IEnumerable<MangaFile> mangaFiles, 
            IProgress<ThumbnailProgress>? progress = null,
            CancellationToken cancellationToken = default,
            bool skipExisting = true);

        /// <summary>
        /// 指定されたファイルのサムネイル画像を取得します
        /// </summary>
        /// <param name="mangaFile">サムネイル画像を取得する漫画ファイル</param>
        /// <returns>サムネイル画像、存在しない場合はnull</returns>
        BitmapImage? GetThumbnailImage(MangaFile mangaFile);

        /// <summary>
        /// デフォルトのサムネイル画像を取得します
        /// </summary>
        /// <returns>デフォルトサムネイル画像</returns>
        BitmapImage GetDefaultThumbnail();


    }

    public class ThumbnailGenerationResult
    {
        public MangaFile? MangaFile { get; set; }
        public bool WasSkipped { get; set; }
    }

    public class ThumbnailProgress
    {
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
