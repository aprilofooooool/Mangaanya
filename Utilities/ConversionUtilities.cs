using System;
using System.Globalization;

namespace Mangaanya.Utilities
{
    /// <summary>
    /// データ変換に関する共通ユーティリティクラス
    /// </summary>
    public static class ConversionUtilities
    {
        /// <summary>
        /// 評価値を★マーク表示に変換します
        /// </summary>
        /// <param name="rating">評価値（1-5）</param>
        /// <returns>★マークで表現された評価文字列</returns>
        public static string FormatRating(int? rating)
        {
            if (!rating.HasValue)
                return string.Empty;

            // 1-5の範囲内であることを確認
            var validRating = Math.Max(0, Math.Min(5, rating.Value));
            return new string('★', validRating);
        }

        /// <summary>
        /// 評価値を★と☆を組み合わせた5段階表示に変換します
        /// </summary>
        /// <param name="rating">評価値（1-5）</param>
        /// <returns>★（塗りつぶし）と☆（空）を組み合わせた5文字の評価文字列</returns>
        public static string FormatRatingWithEmpty(int? rating)
        {
            if (!rating.HasValue)
                return "☆☆☆☆☆";

            // 1-5の範囲内であることを確認
            var validRating = Math.Max(0, Math.Min(5, rating.Value));
            var filledStars = new string('★', validRating);
            var emptyStars = new string('☆', 5 - validRating);
            return filledStars + emptyStars;
        }

        /// <summary>
        /// 文字列を安全にDateTime型に変換します
        /// </summary>
        /// <param name="dateString">日付文字列</param>
        /// <returns>変換に成功した場合はDateTime、失敗した場合はnull</returns>
        public static DateTime? ParseDateSafely(string? dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return null;

            // 標準的な日付形式での変換を試行
            if (DateTime.TryParse(dateString, out var date))
            {
                // 明らかに不適切な日付をフィルタリング
                if (date.Year < 1900 || date.Year > DateTime.Now.Year + 10)
                    return null;

                return date;
            }

            return null;
        }

        /// <summary>
        /// 文字列を指定されたカルチャで安全にDateTime型に変換します
        /// </summary>
        /// <param name="dateString">日付文字列</param>
        /// <param name="culture">カルチャ情報</param>
        /// <returns>変換に成功した場合はDateTime、失敗した場合はnull</returns>
        public static DateTime? ParseDateSafely(string? dateString, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(dateString))
                return null;

            if (DateTime.TryParse(dateString, culture, DateTimeStyles.None, out var date))
            {
                // 明らかに不適切な日付をフィルタリング
                if (date.Year < 1900 || date.Year > DateTime.Now.Year + 10)
                    return null;

                return date;
            }

            return null;
        }

        /// <summary>
        /// 数値を安全に整数に変換します
        /// </summary>
        /// <param name="value">変換する値</param>
        /// <param name="defaultValue">変換に失敗した場合のデフォルト値</param>
        /// <returns>変換された整数値</returns>
        public static int ParseIntSafely(string? value, int defaultValue = 0)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 数値を安全にlong型に変換します
        /// </summary>
        /// <param name="value">変換する値</param>
        /// <param name="defaultValue">変換に失敗した場合のデフォルト値</param>
        /// <returns>変換されたlong値</returns>
        public static long ParseLongSafely(string? value, long defaultValue = 0L)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return long.TryParse(value, out var result) ? result : defaultValue;
        }
    }
}