using System.Collections.Generic;
using System.Windows.Controls;

namespace Mangaanya.Models
{
    public class DataGridColumnSettings
    {
        public string Header { get; set; } = string.Empty;
        public double Width { get; set; }
        public int DisplayIndex { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    public class DataGridSettings
    {
        public List<DataGridColumnSettings> Columns { get; set; } = new List<DataGridColumnSettings>();
    }
}
