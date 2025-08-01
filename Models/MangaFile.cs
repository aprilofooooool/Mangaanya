using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;

namespace Mangaanya.Models
{
    public class MangaFile : INotifyPropertyChanged
    {
        private int _id;
        private string _filePath = string.Empty;
        private string _fileName = string.Empty;
        private long _fileSize;
        private DateTime _createdDate;
        private DateTime _modifiedDate;
        private string _fileType = string.Empty;
        private bool _isCorrupted;
        private string? _title;
        private string? _originalAuthor;
        private string? _artist;
        private string? _authorReading;
        private int? _volumeNumber;
        private string? _volumeString;
        private string? _genre;
        private DateTime? _publishDate;
        private string? _publisher;
        private string? _tags;
        private bool _isAIProcessed;
        private string? _thumbnailPath;
        private DateTime? _thumbnailCreated;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        // 親フォルダパスを取得する読み取り専用プロパティ
        public string FolderPath 
        { 
            get 
            {
                try
                {
                    if (string.IsNullOrEmpty(FilePath))
                        return string.Empty;
                    
                    var directoryName = Path.GetDirectoryName(FilePath);
                    return directoryName ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public long FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }

        public string FileSizeFormatted => FormatFileSize(FileSize);

        public DateTime CreatedDate
        {
            get => _createdDate;
            set => SetProperty(ref _createdDate, value);
        }

        public DateTime ModifiedDate
        {
            get => _modifiedDate;
            set => SetProperty(ref _modifiedDate, value);
        }

        public string FileType
        {
            get => _fileType;
            set => SetProperty(ref _fileType, value);
        }

        public bool IsCorrupted
        {
            get => _isCorrupted;
            set => SetProperty(ref _isCorrupted, value);
        }

        public string? Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string? OriginalAuthor
        {
            get => _originalAuthor;
            set => SetProperty(ref _originalAuthor, value);
        }

        public string? Artist
        {
            get => _artist;
            set => SetProperty(ref _artist, value);
        }

        public string? AuthorReading
        {
            get => _authorReading;
            set => SetProperty(ref _authorReading, value);
        }

        public int? VolumeNumber
        {
            get => _volumeNumber;
            set => SetProperty(ref _volumeNumber, value);
        }

        public string? VolumeString
        {
            get => _volumeString;
            set => SetProperty(ref _volumeString, value);
        }

        // 表示用の巻数プロパティ（VolumeStringがあればそれを、なければVolumeNumberを表示）
        public string VolumeDisplay 
        { 
            get => VolumeString ?? VolumeNumber?.ToString() ?? string.Empty;
            set
            {
                // 入力値が数値かどうかチェック
                if (int.TryParse(value, out int numericValue))
                {
                    // 数値の場合はVolumeNumberに設定し、VolumeStringはクリア
                    VolumeNumber = numericValue;
                    VolumeString = null;
                }
                else
                {
                    // 数値でない場合はVolumeStringに設定し、VolumeNumberはクリア
                    VolumeString = string.IsNullOrWhiteSpace(value) ? null : value;
                    VolumeNumber = null;
                }
                OnPropertyChanged();
            }
        }

        public string? Genre
        {
            get => _genre;
            set => SetProperty(ref _genre, value);
        }

        public DateTime? PublishDate
        {
            get => _publishDate;
            set => SetProperty(ref _publishDate, value);
        }

        public string? Publisher
        {
            get => _publisher;
            set => SetProperty(ref _publisher, value);
        }

        public string? Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        public bool IsAIProcessed
        {
            get => _isAIProcessed;
            set => SetProperty(ref _isAIProcessed, value);
        }

        public string? ThumbnailPath
        {
            get => _thumbnailPath;
            set => SetProperty(ref _thumbnailPath, value);
        }

        public DateTime? ThumbnailCreated
        {
            get => _thumbnailCreated;
            set => SetProperty(ref _thumbnailCreated, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
