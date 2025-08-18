using System;

namespace Mangaanya.Configuration
{
    /// <summary>
    /// 型安全な設定キーを表現するクラス
    /// </summary>
    /// <typeparam name="T">設定値の型</typeparam>
    public class SettingKey<T>
    {
        /// <summary>
        /// 設定キー名
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// デフォルト値
        /// </summary>
        public T DefaultValue { get; }

        /// <summary>
        /// SettingKey<T> クラスの新しいインスタンスを初期化します
        /// </summary>
        /// <param name="key">設定キー名</param>
        /// <param name="defaultValue">デフォルト値</param>
        /// <exception cref="ArgumentNullException">key が null の場合</exception>
        public SettingKey(string key, T defaultValue)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// 設定キーの文字列表現を取得します
        /// </summary>
        /// <returns>設定キー名</returns>
        public override string ToString()
        {
            return Key;
        }

        /// <summary>
        /// 指定されたオブジェクトが現在のオブジェクトと等しいかどうかを判断します
        /// </summary>
        /// <param name="obj">比較するオブジェクト</param>
        /// <returns>等しい場合は true、それ以外の場合は false</returns>
        public override bool Equals(object? obj)
        {
            if (obj is SettingKey<T> other)
            {
                return Key.Equals(other.Key, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>
        /// このインスタンスのハッシュコードを取得します
        /// </summary>
        /// <returns>ハッシュコード</returns>
        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }
    }
}