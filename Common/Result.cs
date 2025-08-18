using System;

namespace Mangaanya.Common
{
    /// <summary>
    /// 操作の成功・失敗状態を表現するクラス
    /// </summary>
    /// <typeparam name="T">成功時の戻り値の型</typeparam>
    public class Result<T>
    {
        /// <summary>
        /// 操作が成功したかどうかを示す値
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 操作が失敗したかどうかを示す値
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// 成功時の戻り値
        /// </summary>
        public T? Value { get; private set; }

        /// <summary>
        /// 失敗時のエラーメッセージ
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// 失敗時の例外情報
        /// </summary>
        public Exception? Exception { get; private set; }

        /// <summary>
        /// プライベートコンストラクタ
        /// </summary>
        private Result() { }

        /// <summary>
        /// 成功結果を作成します
        /// </summary>
        /// <param name="value">成功時の戻り値</param>
        /// <returns>成功を表すResult</returns>
        public static Result<T> Success(T value)
        {
            return new Result<T>
            {
                IsSuccess = true,
                Value = value
            };
        }

        /// <summary>
        /// 失敗結果を作成します
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>失敗を表すResult</returns>
        public static Result<T> Failure(string errorMessage)
        {
            return new Result<T>
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 例外から失敗結果を作成します
        /// </summary>
        /// <param name="exception">例外情報</param>
        /// <returns>失敗を表すResult</returns>
        public static Result<T> Failure(Exception exception)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Exception = exception,
                ErrorMessage = exception.Message
            };
        }

        /// <summary>
        /// エラーメッセージと例外から失敗結果を作成します
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <param name="exception">例外情報</param>
        /// <returns>失敗を表すResult</returns>
        public static Result<T> Failure(string errorMessage, Exception exception)
        {
            return new Result<T>
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
    }
}

namespace Mangaanya.Common
{
    /// <summary>
    /// 戻り値を持たない操作の成功・失敗状態を表現するクラス
    /// </summary>
    public class Result
    {
        /// <summary>
        /// 操作が成功したかどうかを示す値
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// 操作が失敗したかどうかを示す値
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// 失敗時のエラーメッセージ
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// 失敗時の例外情報
        /// </summary>
        public Exception? Exception { get; private set; }

        /// <summary>
        /// プライベートコンストラクタ
        /// </summary>
        private Result() { }

        /// <summary>
        /// 成功結果を作成します
        /// </summary>
        /// <returns>成功を表すResult</returns>
        public static Result Success()
        {
            return new Result
            {
                IsSuccess = true
            };
        }

        /// <summary>
        /// 失敗結果を作成します
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <returns>失敗を表すResult</returns>
        public static Result Failure(string errorMessage)
        {
            return new Result
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 例外から失敗結果を作成します
        /// </summary>
        /// <param name="exception">例外情報</param>
        /// <returns>失敗を表すResult</returns>
        public static Result Failure(Exception exception)
        {
            return new Result
            {
                IsSuccess = false,
                Exception = exception,
                ErrorMessage = exception.Message
            };
        }

        /// <summary>
        /// エラーメッセージと例外から失敗結果を作成します
        /// </summary>
        /// <param name="errorMessage">エラーメッセージ</param>
        /// <param name="exception">例外情報</param>
        /// <returns>失敗を表すResult</returns>
        public static Result Failure(string errorMessage, Exception exception)
        {
            return new Result
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
    }
}