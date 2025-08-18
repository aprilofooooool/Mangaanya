using System;

namespace Mangaanya.Exceptions
{
    /// <summary>
    /// データベース操作に関する例外を表します
    /// </summary>
    public class DatabaseException : MangaException
    {
        /// <summary>
        /// 実行に失敗したSQL文またはクエリ
        /// </summary>
        public string? Query { get; }

        /// <summary>
        /// DatabaseExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        public DatabaseException(string message) : base(message)
        {
        }

        /// <summary>
        /// DatabaseExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="query">実行に失敗したSQL文またはクエリ</param>
        public DatabaseException(string message, string query) : base(message)
        {
            Query = query;
        }

        /// <summary>
        /// DatabaseExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="innerException">現在の例外の原因である例外</param>
        public DatabaseException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// DatabaseExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="query">実行に失敗したSQL文またはクエリ</param>
        /// <param name="innerException">現在の例外の原因である例外</param>
        public DatabaseException(string message, string query, Exception innerException) : base(message, innerException)
        {
            Query = query;
        }
    }
}