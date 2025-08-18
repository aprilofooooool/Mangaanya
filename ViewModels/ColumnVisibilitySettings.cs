using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mangaanya.ViewModels
{
    public partial class ColumnVisibilityItem : ObservableObject
    {
        [ObservableProperty]
        private string _header;

        [ObservableProperty]
        private bool _isVisible;

        public ColumnVisibilityItem(string header, bool isVisible)
        {
            _header = header;
            _isVisible = isVisible;
        }
    }

    public class ColumnVisibilitySettings
    {
        public ObservableCollection<ColumnVisibilityItem> Columns { get; set; } = new ObservableCollection<ColumnVisibilityItem>();

        public ColumnVisibilitySettings()
        {
        }

        public ColumnVisibilitySettings(IEnumerable<ColumnVisibilityItem> columns)
        {
            foreach (var column in columns)
            {
                Columns.Add(column);
            }
        }

        public Dictionary<string, bool> ToDictionary()
        {
            var dict = new Dictionary<string, bool>();
            foreach (var column in Columns)
            {
                dict[column.Header] = column.IsVisible;
            }
            return dict;
        }

        public static ColumnVisibilitySettings FromDictionary(Dictionary<string, bool> dict)
        {
            var settings = new ColumnVisibilitySettings();
            foreach (var kvp in dict)
            {
                settings.Columns.Add(new ColumnVisibilityItem(kvp.Key, kvp.Value));
            }
            return settings;
        }
    }
}
