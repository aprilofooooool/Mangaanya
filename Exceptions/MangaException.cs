using System;

namespace Mangaanya.Exceptions
{
    /// <summary>
    /// Mangaanyaアプリケーション固有の基底例外クラス
    /// </summary>
    public abstract class MangaException : Exception
    {
        /// <summary>
        /// MangaExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        protected MangaException(string message) : base(message)
        {
        }

        /// <summary>
        /// MangaExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="innerException">現在の例外の原因である例外</param>
        protected MangaException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}