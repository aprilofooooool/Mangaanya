using Mangaanya.Services;

namespace Mangaanya.Configuration
{
    /// <summary>
    /// IConfigurationManager の型安全な設定アクセス拡張メソッド
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// 型安全な設定キーを使用して設定値を取得します
        /// </summary>
        /// <typeparam name="T">設定値の型</typeparam>
        /// <param name="configManager">設定マネージャー</param>
        /// <param name="settingKey">型安全な設定キー</param>
        /// <returns>設定値</returns>
        public static T GetSetting<T>(this IConfigurationManager configManager, SettingKey<T> settingKey)
        {
            return configManager.GetSetting(settingKey.Key, settingKey.DefaultValue);
        }

        /// <summary>
        /// 型安全な設定キーを使用して設定値を設定します
        /// </summary>
        /// <typeparam name="T">設定値の型</typeparam>
        /// <param name="configManager">設定マネージャー</param>
        /// <param name="settingKey">型安全な設定キー</param>
        /// <param name="value">設定する値</param>
        public static void SetSetting<T>(this IConfigurationManager configManager, SettingKey<T> settingKey, T value)
        {
            configManager.SetSetting(settingKey.Key, value);
        }
    }
}