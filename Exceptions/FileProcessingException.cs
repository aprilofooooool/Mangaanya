using System;

namespace Mangaanya.Exceptions
{
    /// <summary>
    /// ファイル処理に関する例外を表します
    /// </summary>
    public class FileProcessingException : MangaException
    {
        /// <summary>
        /// 処理に失敗したファイルのパス
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// FileProcessingExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="filePath">処理に失敗したファイルのパス</param>
        /// <param name="message">例外を説明するメッセージ</param>
        public FileProcessingException(string filePath, string message) : base(message)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        /// <summary>
        /// FileProcessingExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="filePath">処理に失敗したファイルのパス</param>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="innerException">現在の例外の原因である例外</param>
        public FileProcessingException(string filePath, string message, Exception innerException) : base(message, innerException)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }
    }
}