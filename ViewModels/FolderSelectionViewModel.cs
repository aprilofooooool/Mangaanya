using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using Mangaanya.Services;
using System.Collections.ObjectModel;
using System.IO;

namespace Mangaanya.ViewModels
{
    /// <summary>
    /// フォルダ選択ダイアログのViewModel
    /// </summary>
    public partial class FolderSelectionViewModel : ObservableObject
    {
        private readonly ILogger<FolderSelectionViewModel> _logger;
        private readonly IConfigurationManager _config;
        private readonly IMangaRepository _repository;
        private readonly IFileSizeService _fileSizeService;

        [ObservableProperty]
        private ObservableCollection<FolderDisplayItem> _folderItems = new();

        [ObservableProperty]
        private FolderDisplayItem? _selectedFolder;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "フォルダを読み込み中...";

        [ObservableProperty]
        private string? _sourceFolder; // 移動元フォルダ

        /// <summary>
        /// OKボタンが有効かどうか
        /// </summary>
        public bool IsOkButtonEnabled => SelectedFolder != null && !IsLoading;

        public FolderSelectionViewModel(
            ILogger<FolderSelectionViewModel> logger,
            IConfigurationManager config,
            IMangaRepository repository,
            IFileSizeService fileSizeService)
        {
            _logger = logger;
            _config = config;
            _repository = repository;
            _fileSizeService = fileSizeService;
        }

        /// <summary>
        /// 移動元フォルダを設定
        /// </summary>
        public void SetSourceFolder(string? sourceFolder)
        {
            SourceFolder = sourceFolder;
            _logger.LogDebug("移動元フォルダが設定されました: {SourceFolder}", sourceFolder);
        }

        /// <summary>
        /// フォルダリストを非同期で読み込む
        /// </summary>
        public async Task LoadFoldersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "フォルダを読み込み中...";
                FolderItems.Clear();

                _logger.LogInformation("フォルダ選択ダイアログ用のフォルダリストを読み込み開始");

                // スキャン対象フォルダを取得
                var scanFolders = _config.GetSetting<List<string>>("ScanFolders", new List<string>());
                if (scanFolders == null || !scanFolders.Any())
                {
                    StatusMessage = "スキャン対象フォルダが設定されていません";
                    _logger.LogWarning("スキャン対象フォルダが設定されていません");
                    return;
                }

                // フォルダ統計情報を取得
                StatusMessage = "フォルダ統計情報を取得中...";
                var folderStatistics = await _repository.GetFolderStatisticsAsync();

                // フォルダ表示アイテムを作成
                var sortedFolders = scanFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                
                foreach (var folderPath in sortedFolders)
                {
                    // フォルダが存在するかチェック
                    if (!Directory.Exists(folderPath))
                    {
                        _logger.LogWarning("フォルダが存在しません: {FolderPath}", folderPath);
                        continue;
                    }

                    // 移動元フォルダを除外
                    if (!string.IsNullOrEmpty(SourceFolder) && 
                        string.Equals(folderPath, SourceFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogDebug("移動元フォルダを除外しました: {FolderPath}", folderPath);
                        continue;
                    }

                    var folderName = Path.GetFileName(folderPath);
                    if (string.IsNullOrEmpty(folderName))
                    {
                        folderName = folderPath; // ルートドライブの場合
                    }

                    var statisticsText = "(0件, 0 B)";
                    
                    if (folderStatistics.TryGetValue(folderPath, out var statistics))
                    {
                        var formattedSize = _fileSizeService.FormatFileSize(statistics.TotalSize);
                        statisticsText = $"({statistics.FileCount}件, {formattedSize})";
                    }

                    var folderItem = new FolderDisplayItem
                    {
                        FolderPath = folderPath,
                        FolderName = folderName,
                        StatisticsText = statisticsText,
                        DisplayText = $"{folderName} {statisticsText}"
                    };

                    FolderItems.Add(folderItem);
                }

                StatusMessage = $"{FolderItems.Count}個のフォルダを読み込みました";
                _logger.LogInformation("フォルダ選択ダイアログ用のフォルダリスト読み込み完了: {Count}個", FolderItems.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フォルダリストの読み込み中にエラーが発生しました");
                StatusMessage = "フォルダリストの読み込みに失敗しました";
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 選択されたフォルダが変更された時の処理
        /// </summary>
        partial void OnSelectedFolderChanged(FolderDisplayItem? value)
        {
            OnPropertyChanged(nameof(IsOkButtonEnabled));
            
            if (value != null)
            {
                _logger.LogDebug("フォルダが選択されました: {FolderPath}", value.FolderPath);
            }
        }

        /// <summary>
        /// ローディング状態が変更された時の処理
        /// </summary>
        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsOkButtonEnabled));
        }
    }
}