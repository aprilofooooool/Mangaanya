using System;

namespace Mangaanya.Exceptions
{
    /// <summary>
    /// サムネイル生成に関する例外を表します
    /// </summary>
    public class ThumbnailGenerationException : MangaException
    {
        /// <summary>
        /// サムネイル生成に失敗したファイルのパス
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 生成しようとしたサムネイルのサイズ
        /// </summary>
        public string? ThumbnailSize { get; }

        /// <summary>
        /// ThumbnailGenerationExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="filePath">サムネイル生成に失敗したファイルのパス</param>
        /// <param name="message">例外を説明するメッセージ</param>
        public ThumbnailGenerationException(string filePath, string message) : base(message)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        /// <summary>
        /// ThumbnailGenerationExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="filePath">サムネイル生成に失敗したファイルのパス</param>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="thumbnailSize">生成しようとしたサムネイルのサイズ</param>
        public ThumbnailGenerationException(string filePath, string message, string thumbnailSize) : base(message)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            ThumbnailSize = thumbnailSize;
        }

        /// <summary>
        /// ThumbnailGenerationExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="filePath">サムネイル生成に失敗したファイルのパス</param>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="innerException">現在の例外の原因である例外</param>
        public ThumbnailGenerationException(string filePath, string message, Exception innerException) : base(message, innerException)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        /// <summary>
        /// ThumbnailGenerationExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="filePath">サムネイル生成に失敗したファイルのパス</param>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="thumbnailSize">生成しようとしたサムネイルのサイズ</param>
        /// <param name="innerException">現在の例外の原因である例外</param>
        public ThumbnailGenerationException(string filePath, string message, string thumbnailSize, Exception innerException) : base(message, innerException)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            ThumbnailSize = thumbnailSize;
        }
    }
}