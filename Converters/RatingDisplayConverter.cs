using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Mangaanya.Converters
{
    /// <summary>
    /// 評価値を最適化された★マーク表示に変換するコンバーター
    /// </summary>
    public class RatingDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rating && rating >= 1 && rating <= 5)
            {
                // 塗りつぶし星と空星を組み合わせて表示
                var filledStars = new string('★', rating);
                var emptyStars = new string('☆', 5 - rating);
                return filledStars + emptyStars;
            }
            
            return string.Empty; // 未評価の場合は空文字
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 評価値に基づいて色を決定するコンバーター
    /// </summary>
    public class RatingColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rating && rating >= 1 && rating <= 5)
            {
                // 評価に応じて色を変更
                return rating switch
                {
                    1 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 140, 140)), // 薄い赤
                    2 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),   // オレンジ
                    3 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)),   // ゴールド
                    4 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 205, 50)),   // ライムグリーン
                    5 => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0)),   // ゴールド（最高評価）
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            
            return new SolidColorBrush(Colors.Gray); // 未評価の場合はグレー
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 評価値に基づいてフォントウェイトを決定するコンバーター
    /// </summary>
    public class RatingFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int rating && rating >= 4)
            {
                // 高評価（4-5）の場合は太字
                return FontWeights.Bold;
            }
            
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}