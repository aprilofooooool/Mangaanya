using System;

namespace Mangaanya.Exceptions
{
    /// <summary>
    /// 設定に関する例外を表します
    /// </summary>
    public class ConfigurationException : MangaException
    {
        /// <summary>
        /// 問題のある設定キー
        /// </summary>
        public string? SettingKey { get; }

        /// <summary>
        /// 問題のある設定値
        /// </summary>
        public string? SettingValue { get; }

        /// <summary>
        /// ConfigurationExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        public ConfigurationException(string message) : base(message)
        {
        }

        /// <summary>
        /// ConfigurationExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="settingKey">問題のある設定キー</param>
        public ConfigurationException(string message, string settingKey) : base(message)
        {
            SettingKey = settingKey;
        }

        /// <summary>
        /// ConfigurationExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="settingKey">問題のある設定キー</param>
        /// <param name="settingValue">問題のある設定値</param>
        public ConfigurationException(string message, string settingKey, string settingValue) : base(message)
        {
            SettingKey = settingKey;
            SettingValue = settingValue;
        }

        /// <summary>
        /// ConfigurationExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="innerException">現在の例外の原因である例外</param>
        public ConfigurationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// ConfigurationExceptionクラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="message">例外を説明するメッセージ</param>
        /// <param name="settingKey">問題のある設定キー</param>
        /// <param name="innerException">現在の例外の原因である例外</param>
        public ConfigurationException(string message, string settingKey, Exception innerException) : base(message, innerException)
        {
            SettingKey = settingKey;
        }
    }
}