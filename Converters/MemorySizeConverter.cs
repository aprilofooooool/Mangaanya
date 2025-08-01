using System;
using System.Globalization;
using System.Windows.Data;

namespace Mangaanya.Converters
{
    public class MemorySizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string formattedSize)
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                string[] parts = formattedSize.Split(' ');
                
                if (parts.Length != 2)
                    return 0L;
                    
                if (!double.TryParse(parts[0], out double numValue))
                    return 0L;
                    
                int order = Array.IndexOf(sizes, parts[1].ToUpper());
                if (order < 0)
                    return 0L;
                    
                return (long)(numValue * Math.Pow(1024, order));
            }
            return 0L;
        }
    }
}
