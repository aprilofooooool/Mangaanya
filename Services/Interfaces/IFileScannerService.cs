using Mangaanya.Models;
using Mangaanya.Common;

namespace Mangaanya.Services
{
    public interface IFileScannerService
    {
        Task<Result<ScanResult>> PerformIncrementalScanAsync(IProgress<ScanProgress> progress);
        Task<Result<ScanResult>> PerformFullScanAsync(string folderPath, IProgress<ScanProgress> progress);
        Task<Result<ParsedFileInfo>> ParseFileNameAsync(string fileName);
    }

    public class ScanResult
    {
        public int FilesProcessed { get; set; }
        public int FilesAdded { get; set; }
        public int FilesUpdated { get; set; }
        public int FilesRemoved { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class ScanProgress
    {
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class ParsedFileInfo
    {
        public string? Title { get; set; }
        public string? OriginalAuthor { get; set; }
        public string? Artist { get; set; }
        public string? AuthorReading { get; set; }
        public int? VolumeNumber { get; set; }
        public string? VolumeString { get; set; }
    }
}
