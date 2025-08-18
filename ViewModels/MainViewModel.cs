using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Mangaanya.Models;
using Mangaanya.Services;
using Mangaanya.Services.Interfaces;
using Mangaanya.Constants;
using Mangaanya.Utilities;
using Mangaanya.Common;
using Mangaanya.Configuration;

using System.Collections.ObjectModel;
using System.Windows;
using System.IO;
using System.Threading;

namespace Mangaanya.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ILogger<MainViewModel> _logger;
        private readonly IFileScannerService _fileScannerService;
        private readonly IAIService _aiService;
        private readonly ISearchEngineService _searchEngineService;
        private readonly IMangaRepository _repository;
        private readonly IConfigurationManager _config;
        private readonly IDialogService _dialogService;
        private readonly IFileOperationService _fileOperationService;
        private readonly IThumbnailService _thumbnailService;
        private readonly ISystemSoundService _systemSoundService;
        private readonly IFileSizeService _fileSizeService;
        private readonly IFileMoveService _fileMoveService;

        [ObservableProperty]
        private ObservableCollection<MangaFile> _mangaFiles = new();

        [ObservableProperty]
        private MangaFile? _selectedMangaFile;
        
        [ObservableProperty]
        private ObservableCollection<MangaFile> _selectedMangaFiles = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _statusMessage = "準備完了";

        [ObservableProperty]
        private string _fileCountMessage = "0件";

        [ObservableProperty]
        private bool _isProcessing;
        
        [ObservableProperty]
        private int _progressValue;
        
        [ObservableProperty]
        private bool _showThumbnails = true;

        [ObservableProperty]
        private ThumbnailDisplayMode _thumbnailDisplayMode = ThumbnailDisplayMode.Standard;

        [ObservableProperty]
        private double _thumbnailMaxWidth = ThumbnailConstants.MaxWidth;

        [ObservableProperty]
        private double _thumbnailMaxHeight = ThumbnailConstants.MaxHeight;
        
        [ObservableProperty]
        private int _progressMaximum = 100;
        
        [ObservableProperty]
        private ObservableCollection<string> _scanFolders = new();
        
        [ObservableProperty]
        private string? _selectedScanFolder;
        
        [ObservableProperty]
        private bool _includeSubfolders = true;
        
        [ObservableProperty]
        private Dictionary<string, FolderStatistics> _folderStatistics = new();
        
        [ObservableProperty]
        private ObservableCollection<FolderDisplayItem> _folderDisplayItems = new();
        
        // 全ファイルのキャッシュ
        private List<MangaFile> _allMangaFiles = new();

        public MainViewModel(
            ILogger<MainViewModel> logger,
            IFileScannerService fileScannerService,
            IAIService aiService,
            ISearchEngineService searchEngineService,
            IMangaRepository repository,
            IConfigurationManager config,
            IDialogService dialogService,
            IFileOperationService fileOperationService,
            IThumbnailService thumbnailService,
            ISystemSoundService systemSoundService,
            IFileSizeService fileSizeService,
            IFileMoveService fileMoveService)
        {
            _logger = logger;
            _fileScannerService = fileScannerService;
            _aiService = aiService;
            _searchEngineService = searchEngineService;
            _repository = repository;
            _config = config;
            _dialogService = dialogService;
            _fileOperationService = fileOperationService;
            _thumbnailService = thumbnailService;
            _systemSoundService = systemSoundService;
            _fileSizeService = fileSizeService;
            _fileMoveService = fileMoveService;
            
            // サムネイル表示設定を初期化
            ShowThumbnails = _config.GetSetting(Configuration.AppSettings.ShowThumbnails);
            ThumbnailDisplayMode = _config.GetSetting(Configuration.AppSettings.ThumbnailDisplay);
        }

        /// <summary>
        /// 初期化完了イベント
        /// </summary>
        public event EventHandler? InitializationCompleted;
        
        /// <summary>
        /// 評価フィードバック表示イベント
        /// </summary>
        /// <param name="rating">設定された評価値</param>
        /// <param name="fileCount">対象ファイル数</param>
        /// <param name="isCleared">評価がクリアされたかどうか</param>
        public event Action<int?, int, bool>? ShowRatingFeedback;
        
        /// <summary>
        /// 通知表示イベント（タイトル、メッセージ、自動非表示秒数）
        /// </summary>
        public event Action<string, string, int>? ShowNotificationEvent;
        
        /// <summary>
        /// 成功通知表示イベント（タイトル、メッセージ、自動非表示秒数）
        /// </summary>
        public event Action<string, string, int>? ShowSuccessNotificationEvent;
        
        /// <summary>
        /// エラー通知表示イベント（タイトル、メッセージ、自動非表示秒数）
        /// </summary>
        public event Action<string, string, int>? ShowErrorNotificationEvent;
        
        /// <summary>
        /// 確認ダイアログ表示イベント（タイトル、メッセージ）
        /// </summary>
        public event Func<string, string, Task<bool>>? ShowConfirmationEvent;
        
        /// <summary>
        /// サムネイル生成モード選択ダイアログ表示イベント（タイトル、メッセージ、ファイル数）
        /// </summary>
        public event Func<string, string, int, Task<ThumbnailGenerationMode>>? ShowThumbnailModeSelectionEvent;
        
        /// <summary>
        /// ソートリセットイベント
        /// </summary>
        public event Action? ResetSortEvent;
        
        /// <summary>
        /// 統一通知システムで確認ダイアログを表示
        /// </summary>
        private async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            if (ShowConfirmationEvent != null)
            {
                return await ShowConfirmationEvent.Invoke(title, message);
            }
            
            // フォールバック：従来のダイアログサービスを使用
            return _dialogService.ShowConfirmation(message, title);
        }

        /// <summary>
        /// 完了通知を遅延表示する（確認ダイアログ後の通知が一瞬で閉じる問題を回避）
        /// </summary>
        private async Task ShowDelayedSuccessNotification(string title, string message, int autoHideSeconds = 0, int delayMs = 300, bool playSound = false)
        {
            await Task.Delay(delayMs);
            if (playSound)
            {
                _systemSoundService.PlayCompletionSound();
            }
            ShowSuccessNotificationEvent?.Invoke(title, message, autoHideSeconds);
        }

        /// <summary>
        /// 外部からステータスメッセージを更新するためのメソッド
        /// </summary>
        public void UpdateStatusMessage(string message)
        {
            StatusMessage = message;
            _logger.LogInformation("ステータスメッセージを更新: {Message}", message);
        }

        private async void InitializeAsync()
        {
            try
            {
                StatusMessage = "初期化中...";
                _logger.LogInformation("アプリケーションの初期化を開始します");
                
                StatusMessage = "設定を読み込み中...";
                _logger.LogInformation("設定ファイルを読み込みます");
                await _config.LoadAsync();
                _logger.LogInformation("設定ファイルの読み込みが完了しました");
                
                StatusMessage = "データベースを初期化中...";
                _logger.LogInformation("データベースを初期化します");
                await _repository.InitializeDatabaseAsync();
                _logger.LogInformation("データベースの初期化が完了しました");
                
                StatusMessage = "スキャン対象フォルダを読み込み中...";
                _logger.LogInformation("スキャン対象フォルダを読み込みます");
                LoadScanFolders();
                _logger.LogInformation("スキャン対象フォルダの読み込みが完了しました");
                
                StatusMessage = "ファイル一覧を読み込み中...";
                _logger.LogInformation("ファイル一覧を読み込みます");
                await LoadMangaFilesAsync();
                _logger.LogInformation("ファイル一覧の読み込みが完了しました");
                

                
                StatusMessage = "準備完了";
                _logger.LogInformation("アプリケーションの初期化が完了しました");
                
                // 初期化完了を通知
                InitializationCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "初期化中にエラーが発生しました: {Message}", ex.Message);
                _logger.LogError(ex, "スタックトレース: {StackTrace}", ex.StackTrace);
                StatusMessage = $"初期化エラー: {ex.Message}";
                
                // エラーでも初期化完了を通知
                InitializationCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 外部から初期化を開始するメソッド
        /// </summary>
        public void StartInitialization()
        {
            InitializeAsync();
        }
        
        private void LoadScanFolders()
        {
            try
            {
                var folders = _config.GetSetting<List<string>>("ScanFolders", new List<string>());
                
                // デバッグ情報
                
                if (folders != null && folders.Count > 0)
                {
                    foreach (var folder in folders)
                    {
                        
                    }
                }
                
                ScanFolders.Clear();
                if (folders != null)
                {
                    // フォルダを昇順でソートしてから追加
                    var sortedFolders = folders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                    foreach (var folder in sortedFolders)
                    {
                        ScanFolders.Add(folder);
                    }
                }
                
                StatusMessage = $"スキャン対象フォルダ: {ScanFolders.Count}件";
                
                // フォルダ表示アイテムを更新
                UpdateFolderDisplayItems();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "スキャン対象フォルダの読み込み中にエラーが発生しました");
                StatusMessage = "フォルダ読み込みエラー";
            }
        }

        [RelayCommand]
        private async Task SelectFolderAsync()
        {
            try
            {
                var selectedFolder = _dialogService.SelectFolder("スキャン対象フォルダを選択してください");
                if (string.IsNullOrEmpty(selectedFolder))
                {
                    return;
                }

                // 現在の設定を取得
                var currentFolders = _config.GetSetting<List<string>>("ScanFolders", new List<string>());
                var foldersToAdd = new List<string>();
                
                // 選択されたフォルダを追加
                if (!currentFolders.Contains(selectedFolder))
                {
                    foldersToAdd.Add(selectedFolder);
                }
                
                // サブフォルダも含める場合
                if (IncludeSubfolders)
                {
                    try
                    {
                        var subFolders = Directory.GetDirectories(selectedFolder, "*", SearchOption.AllDirectories);
                        foreach (var subFolder in subFolders)
                        {
                            if (!currentFolders.Contains(subFolder))
                            {
                                foldersToAdd.Add(subFolder);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "サブフォルダの取得中にエラーが発生しました: {Folder}", selectedFolder);
                    }
                }
                
                // 既に全て追加済みの場合
                if (foldersToAdd.Count == 0)
                {
                    ShowNotificationEvent?.Invoke("フォルダ追加", "選択されたフォルダは既に追加されています。", 3);
                    return;
                }

                // フォルダを追加
                currentFolders.AddRange(foldersToAdd);
                _config.SetSetting("ScanFolders", currentFolders);
                await _config.SaveAsync();
                
                // UIを更新（ソート済み）
                LoadScanFolders();
                
                

                StatusMessage = $"スキャンフォルダを追加しました: {foldersToAdd.Count}件";
                _logger.LogInformation("スキャンフォルダを追加しました: {Count}件", foldersToAdd.Count);

                // 追加されたフォルダを自動的にスキャン
                await ScanSpecificFoldersAsync(foldersToAdd);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フォルダ選択中にエラーが発生しました");
                ShowErrorNotificationEvent?.Invoke("フォルダ選択エラー", ex.Message, 4);
            }
        }

        [RelayCommand]
        private async Task ScanFolderAsync()
        {
            var startTime = DateTime.Now;
            try
            {
                StatusMessage = "フォルダ再スキャンを開始しています...";

                var allScanFolders = _config.GetSetting<List<string>>("ScanFolders", new List<string>());
                if (!allScanFolders.Any())
                {
                    ShowNotificationEvent?.Invoke("スキャン対象フォルダなし", "スキャン対象フォルダが設定されていません。\n「フォルダ追加」ボタンでフォルダを追加してください。", 4);
                    return;
                }

                // スキャン対象フォルダをそのまま使用（各フォルダの直下のみスキャンするため重複なし）
                var scanFolders = allScanFolders;

                // 確認ダイアログを表示
                var result = await ShowConfirmationAsync("再スキャン確認", "全スキャン対象フォルダを再スキャンします。\n\n実行しますか？");
                if (!result)
                {
                    StatusMessage = "再スキャンがキャンセルされました";
                    return;
                }

                // 確認後にスピナーを開始
                IsProcessing = true;

                // 重い処理をバックグラウンドで実行
                var (totalFileCount, totalFilesAdded, totalFilesUpdated, totalFilesRemoved, allErrors) = await Task.Run(async () =>
                {
                    // データベースをクリア
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        StatusMessage = "データベースをクリア中...");
                    await _repository.ClearAllAsync();

                    // 全フォルダの総ファイル数をカウント
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        StatusMessage = "ファイル数をカウント中...");
                    
                    var totalFileCount = 0;
                    var supportedExtensions = FileUtilities.GetSupportedExtensions();
                    
                    foreach (var folder in scanFolders)
                    {
                        if (Directory.Exists(folder))
                        {
                            try
                            {
                                var files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                                    .ToList();
                                var folderCount = files.Count;
                                totalFileCount += folderCount;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "フォルダのファイル数カウント中にエラー: {Folder}", folder);
                            }
                        }
                    }

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                        StatusMessage = $"総ファイル数: {totalFileCount}件 - スキャン開始");

                    // 全体の進捗管理用変数
                    var totalFilesAdded = 0;
                    var totalFilesUpdated = 0;
                    var totalFilesRemoved = 0;
                    var allErrors = new List<string>();
                    
                    // 全体の進捗管理用変数
                    var globalProcessedCount = 0;
                
                    // 各フォルダを順番に全スキャン
                    for (int folderIndex = 0; folderIndex < scanFolders.Count; folderIndex++)
                    {
                        var folder = scanFolders[folderIndex];
                        
                        if (Directory.Exists(folder))
                        {
                            var folderName = Path.GetFileName(folder);
                            
                            // このフォルダで処理されるファイル数を事前に取得
                            var folderFileCount = 0;
                            try
                            {
                                var folderSupportedExtensions = FileUtilities.GetSupportedExtensions();
                                var files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                                    .Where(f => folderSupportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                                    .ToList();
                                folderFileCount = files.Count;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "フォルダのファイル数取得中にエラー: {Folder}", folder);
                            }
                            

                            
                            // 進捗報告（プログレスバーなし）
                            var progress = new Progress<ScanProgress>(p =>
                            {
                                if (p.TotalFiles > 0)
                                {
                                    // 軽量なステータス表示（ファイル名は表示しない）
                                    var currentGlobalProgress = globalProcessedCount + p.CurrentFile;
                                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                                        StatusMessage = $"処理中: {currentGlobalProgress}/{totalFileCount}");
                                }
                            });
                            
                            var scanResult = await _fileScannerService.PerformFullScanAsync(folder, progress);
                            
                            if (scanResult.IsSuccess)
                            {
                                var result = scanResult.Value!;
                                totalFilesAdded += result.FilesAdded;
                                totalFilesUpdated += result.FilesUpdated;
                                totalFilesRemoved += result.FilesRemoved;
                                // このフォルダで実際に処理されたファイル数を累積
                                globalProcessedCount += folderFileCount;
                            }
                            else
                            {
                                allErrors.Add(scanResult.ErrorMessage ?? "不明なエラー");
                                // エラーがあっても処理されたファイル数は累積
                                globalProcessedCount += folderFileCount;
                            }
                            
                            // フォルダ完了時のステータス更新
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                                StatusMessage = $"処理中: {globalProcessedCount}/{totalFileCount}");
                        }
                        else
                        {
                            allErrors.Add($"フォルダが存在しません: {folder}");
                        }
                    }

                    return (totalFileCount, totalFilesAdded, totalFilesUpdated, totalFilesRemoved, allErrors);
                });

                // ファイル一覧読み込み（ステータスメッセージ更新なし）
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await LoadMangaFilesAsync();
                    UpdateFileCountMessage();
                });
                
                var duration = DateTime.Now - startTime;
                
                // 完了通知の直前でスピナーを停止
                IsProcessing = false;
                
                if (allErrors.Count == 0)
                {
                    StatusMessage = $"スキャン完了: {totalFilesAdded}件追加 (処理時間: {duration.TotalSeconds:F1}秒)";
                    _logger.LogInformation("フォルダ再スキャンが完了しました: 追加={Added}, 更新={Updated}, 削除={Removed}", 
                        totalFilesAdded, totalFilesUpdated, totalFilesRemoved);
                    
                    await ShowDelayedSuccessNotification("再スキャン完了", $"追加: {totalFilesAdded}件\n更新: {totalFilesUpdated}件\n削除: {totalFilesRemoved}件\n処理時間: {duration.TotalSeconds:F1}秒", 0, 300, true);
                }
                else
                {
                    StatusMessage = $"スキャン完了（エラー{allErrors.Count}件）: {totalFilesAdded}件追加";
                    _logger.LogWarning("フォルダ再スキャンでエラーが発生しました: {Errors}", allErrors);
                    ShowErrorNotificationEvent?.Invoke("再スキャン完了（エラーあり）", $"追加: {totalFilesAdded}件\nエラー: {allErrors.Count}件\n\n最初の3件のエラー:\n{string.Join("\n", allErrors.Take(3))}" + 
                        (allErrors.Count > 3 ? $"\n...他{allErrors.Count - 3}件のエラー" : ""), 6);
                }
            }
            catch (Exception ex)
            {
                // エラー時もスピナーを停止
                IsProcessing = false;
                
                _logger.LogError(ex, "フォルダ再スキャン中にエラーが発生しました");
                StatusMessage = $"再スキャンエラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("再スキャンエラー", ex.Message, 4);
            }
            finally
            {
                // 念のため最終的にもfalseに設定
                IsProcessing = false;
            }
        }

        private async Task ScanSpecificFoldersAsync(List<string> foldersToScan)
        {
            var startTime = DateTime.Now;
            try
            {
                StatusMessage = $"新しいフォルダをスキャン中: {foldersToScan.Count}件";
                IsProcessing = true;

                await Task.Run(async () =>
                {
                    var totalFilesAdded = 0;
                    var allErrors = new List<string>();

                    foreach (var folder in foldersToScan)
                    {
                        if (Directory.Exists(folder))
                        {
                            var folderName = Path.GetFileName(folder);
                            
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                StatusMessage = $"スキャン中: {folderName}";
                            });

                            try
                            {
                                var progress = new Progress<ScanProgress>(p =>
                                {
                                    System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        StatusMessage = $"スキャン中: {folderName}";
                                    });
                                });

                                var scanResult = await _fileScannerService.PerformFullScanAsync(folder, progress);
                                if (scanResult.IsSuccess)
                                {
                                    var result = scanResult.Value!;
                                    totalFilesAdded += result.FilesAdded;
                                    _logger.LogInformation("フォルダスキャン完了: {Folder}, 追加={Added}件", folder, result.FilesAdded);
                                }
                                else
                                {
                                    allErrors.Add(scanResult.ErrorMessage ?? "不明なエラー");
                                    _logger.LogError("フォルダスキャンエラー: {Folder}, エラー={Error}", folder, scanResult.ErrorMessage);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "フォルダスキャン中にエラー: {Folder}", folder);
                                allErrors.Add($"フォルダ '{folder}' のスキャンエラー: {ex.Message}");
                            }
                        }
                        else
                        {
                            allErrors.Add($"フォルダが存在しません: {folder}");
                        }
                    }

                    // UIスレッドでファイル一覧を更新
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await LoadMangaFilesAsync();
                        UpdateFileCountMessage();
                    });

                    var duration = DateTime.Now - startTime;

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (allErrors.Count == 0)
                        {
                            StatusMessage = $"新しいフォルダのスキャン完了: {totalFilesAdded}件追加 (処理時間: {duration.TotalSeconds:F1}秒)";
                            _logger.LogInformation("新しいフォルダのスキャンが完了しました: 追加={Added}件", totalFilesAdded);

                        }
                        else
                        {
                            StatusMessage = $"スキャン完了（エラー{allErrors.Count}件）: {totalFilesAdded}件追加";
                            _logger.LogWarning("新しいフォルダのスキャンでエラーが発生しました: {Errors}", allErrors);
                            ShowErrorNotificationEvent?.Invoke("スキャン完了（エラーあり）", $"追加: {totalFilesAdded}件\nエラー: {allErrors.Count}件\n\n最初の3件のエラー:\n{string.Join("\n", allErrors.Take(3))}" + 
                                (allErrors.Count > 3 ? $"\n...他{allErrors.Count - 3}件のエラー" : ""), 6);
                        }
                    });
                    
                    // 処理完了音を再生（Dispatcher外で実行）
                    if (allErrors.Count == 0)
                    {
                        _systemSoundService.PlayCompletionSound();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "新しいフォルダのスキャン中にエラーが発生しました");
                StatusMessage = $"スキャンエラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("スキャンエラー", ex.Message, 4);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task GetAIInfoAsync()
        {
            try
            {
                if (SelectedMangaFile == null)
                {
                    StatusMessage = "ファイルが選択されていません";
                    return;
                }

                if (!_aiService.IsApiAvailable())
                {
                    StatusMessage = "GEMINI APIキーが設定されていません。設定画面で設定してください。";
                    return;
                }

                IsProcessing = true;
                StatusMessage = $"AI情報取得中: {SelectedMangaFile.FileName}";

                var title = SelectedMangaFile.Title ?? SelectedMangaFile.FileName;
                var author = SelectedMangaFile.OriginalAuthor ?? SelectedMangaFile.Artist ?? "";

                var result = await _aiService.GetMangaInfoAsync(title, author);

                if (result.Success)
                {
                    // AI取得情報をファイルに適用（ジャンルと出版社は除外）
                    // SelectedMangaFile.Genre = result.Genre; // ジャンルは対象外
                    SelectedMangaFile.PublishDate = result.PublishDate;
                    // SelectedMangaFile.Publisher = result.Publisher; // 出版社は対象外
                    SelectedMangaFile.Tags = result.Tags; // タグを設定
                    SelectedMangaFile.IsAIProcessed = true;

                    // データベースに保存
                    await _repository.UpdateAsync(SelectedMangaFile);

                    StatusMessage = $"AI情報取得完了: {SelectedMangaFile.FileName}";
                    _logger.LogInformation("AI情報取得が完了しました: {FileName}", SelectedMangaFile.FileName);
                }
                else
                {
                    StatusMessage = $"AI情報取得失敗: {result.ErrorMessage}";
                    _logger.LogWarning("AI情報取得に失敗しました: {FileName} - {Error}", 
                        SelectedMangaFile.FileName, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI情報取得中にエラーが発生しました");
                StatusMessage = $"AI情報取得エラー: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task BulkEditAsync()
        {
            try
            {
                // 選択されたファイルを取得
                var selectedFiles = SelectedMangaFiles.ToList();
                var selectedCount = selectedFiles.Count;

                if (selectedCount == 0)
                {
                    // 選択されたファイルがない場合は、現在選択されている単一ファイルを使用
                    if (SelectedMangaFile != null)
                    {
                        selectedFiles = new List<MangaFile> { SelectedMangaFile };
                        selectedCount = 1;
                    }
                    else
                    {
                        StatusMessage = "編集対象のファイルが選択されていません";
                        return;
                    }
                }

                if (!_aiService.IsApiAvailable())
                {
                    StatusMessage = "GEMINI APIキーが設定されていません。設定画面で設定してください。";
                    return;
                }

                var result = await ShowConfirmationAsync(
                    "一括タグ情報取得確認",
                    $"選択された{selectedCount}件のファイルに対してタグ情報取得を実行しますか？");

                if (!result)
                    return;

                IsProcessing = true;
                ProgressValue = 0;
                ProgressMaximum = selectedCount;
                StatusMessage = $"一括AI情報取得中: 0/{selectedCount}";

                var maxConcurrency = _config.GetSetting("MaxConcurrentAIRequests", 30);
                var aiResults = await _aiService.GetMangaInfoBatchAsync(selectedFiles, maxConcurrency);

                var successCount = 0;
                var failureCount = 0;

                for (int i = 0; i < selectedFiles.Count && i < aiResults.Count; i++)
                {
                    var file = selectedFiles[i];
                    var aiResult = aiResults[i];
                    
                    ProgressValue = i + 1;
                    StatusMessage = $"一括AI情報取得中: {i + 1}/{selectedCount} - {file.FileName}";
                    
                    if (aiResult.Success)
                    {
                        // AI取得情報をファイルに適用（ジャンルと出版社は除外）
                        // file.Genre = aiResult.Genre; // ジャンルは対象外
                        file.PublishDate = aiResult.PublishDate;
                        // file.Publisher = aiResult.Publisher; // 出版社は対象外
                        file.Tags = aiResult.Tags; // タグを設定
                        file.IsAIProcessed = true;
                        
                        // データベースに保存
                        await _repository.UpdateAsync(file);
                        successCount++;
                        
                        _logger.LogInformation("AI情報取得が完了しました: {FileName}", file.FileName);
                    }
                    else
                    {
                        failureCount++;
                        _logger.LogWarning("一括AI情報取得に失敗: {FileName} - {Error}", 
                            file.FileName, aiResult.ErrorMessage);
                    }
                }

                StatusMessage = $"一括AI情報取得完了: 成功={successCount}, 失敗={failureCount}";
                _logger.LogInformation("一括AI情報取得が完了しました: 成功={Success}, 失敗={Failure}", 
                    successCount, failureCount);
                
                // 処理結果を表示
                await ShowDelayedSuccessNotification("タグ情報取得完了", $"成功: {successCount}件\n失敗: {failureCount}件", 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "一括編集中にエラーが発生しました");
                StatusMessage = $"一括編集エラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("一括編集エラー", ex.Message, 4);
            }
            finally
            {
                IsProcessing = false;
                ProgressValue = 0;
            }
        }

        [RelayCommand]
        private async Task SetRatingAsync(object parameter)
        {
            var startTime = DateTime.Now;
            var originalRatings = new Dictionary<int, int?>();
            var selectedFiles = new List<MangaFile>();
            
            try
            {
                // 入力検証
                if (parameter == null || !int.TryParse(parameter.ToString(), out int rating))
                {
                    _logger.LogWarning("無効な評価値が指定されました: {Parameter}", parameter);
                    ShowErrorNotificationEvent?.Invoke("無効な評価値", "1から5の数値を指定してください。", 3);
                    return;
                }

                if (rating < 1 || rating > 5)
                {
                    _logger.LogWarning("評価値が範囲外です: {Rating}", rating);
                    ShowErrorNotificationEvent?.Invoke("評価値範囲外", "評価値は1から5の範囲で指定してください。", 3);
                    return;
                }

                // 選択されたファイルを取得
                selectedFiles = SelectedMangaFiles.ToList();
                if (selectedFiles.Count == 0 && SelectedMangaFile != null)
                {
                    selectedFiles = new List<MangaFile> { SelectedMangaFile };
                }

                if (selectedFiles.Count == 0)
                {
                    StatusMessage = "評価を設定するファイルが選択されていません";
                    ShowNotificationEvent?.Invoke("ファイル未選択", "評価を設定するファイルを選択してください。", 3);
                    return;
                }

                // 大量ファイル処理の確認
                if (selectedFiles.Count > 100)
                {
                    var result = await ShowConfirmationAsync(
                        "大量ファイル処理の確認",
                        $"{selectedFiles.Count}件のファイルに評価★{rating}を設定します。\n\n処理に時間がかかる場合があります。続行しますか？");
                    
                    if (!result)
                    {
                        StatusMessage = "評価設定がキャンセルされました";
                        return;
                    }
                }

                // 処理開始
                IsProcessing = true;
                StatusMessage = $"評価を設定中: ★{rating} ({selectedFiles.Count}件)";
                
                // 元の評価値を保存（エラー時のロールバック用）
                foreach (var file in selectedFiles)
                {
                    originalRatings[file.Id] = file.Rating;
                }

                // UI更新のため先にモデルを更新
                foreach (var file in selectedFiles)
                {
                    file.Rating = rating;
                }

                // データベースを一括更新（パフォーマンス向上）
                var fileIds = selectedFiles.Select(f => f.Id).ToList();
                
                try
                {
                    if (fileIds.Count > 1)
                    {
                        // 複数ファイルの場合は一括更新を使用
                        await _repository.UpdateRatingBatchAsync(fileIds, rating);
                    }
                    else
                    {
                        // 単一ファイルの場合は通常の更新を使用
                        await _repository.UpdateAsync(selectedFiles.First());
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // データベース更新失敗時はUIを元に戻す
                    foreach (var file in selectedFiles)
                    {
                        if (originalRatings.TryGetValue(file.Id, out var originalRating))
                        {
                            file.Rating = originalRating;
                        }
                    }
                    throw new InvalidOperationException("データベースの更新に失敗しました。", ex);
                }

                var duration = DateTime.Now - startTime;
                StatusMessage = $"評価設定完了: ★{rating} ({selectedFiles.Count}件) - {duration.TotalSeconds:F1}秒";
                _logger.LogInformation("評価を設定しました: ★{Rating} ({Count}件) - 処理時間: {Duration:F1}秒", 
                    rating, selectedFiles.Count, duration.TotalSeconds);
                
                // 視覚的フィードバックを表示
                ShowRatingFeedback?.Invoke(rating, selectedFiles.Count, false);
                
                // 大量処理の場合は完了通知（音も遅延）
                if (selectedFiles.Count > 100)
                {
                    await ShowDelayedSuccessNotification("評価設定完了", $"対象ファイル: {selectedFiles.Count}件\n評価: ★{rating}\n処理時間: {duration.TotalSeconds:F1}秒", 0, 300, true);
                }
                else
                {
                    // 通常処理の場合は即座に音を再生
                    _systemSoundService.PlayCompletionSound();
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "評価設定中にデータベースエラーが発生しました");
                StatusMessage = "評価設定に失敗しました";
                ShowErrorNotificationEvent?.Invoke("評価設定失敗", $"{ex.Message}\n\n再度お試しください。", 5);
            }
            catch (Exception ex)
            {
                // 予期しないエラーの場合もUIを元に戻す
                foreach (var file in selectedFiles.Where(f => originalRatings.ContainsKey(f.Id)))
                {
                    if (originalRatings.TryGetValue(file.Id, out var originalRating))
                    {
                        file.Rating = originalRating;
                    }
                }
                
                _logger.LogError(ex, "評価設定中に予期しないエラーが発生しました");
                StatusMessage = $"評価設定エラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("評価設定エラー", $"{ex.Message}\n\nアプリケーションを再起動してください。", 6);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private async Task ClearRatingAsync()
        {
            var startTime = DateTime.Now;
            var originalRatings = new Dictionary<int, int?>();
            var selectedFiles = new List<MangaFile>();
            
            try
            {
                // 選択されたファイルを取得
                selectedFiles = SelectedMangaFiles.ToList();
                if (selectedFiles.Count == 0 && SelectedMangaFile != null)
                {
                    selectedFiles = new List<MangaFile> { SelectedMangaFile };
                }

                if (selectedFiles.Count == 0)
                {
                    StatusMessage = "評価を解除するファイルが選択されていません";
                    ShowNotificationEvent?.Invoke("ファイル未選択", "評価を解除するファイルを選択してください。", 3);
                    return;
                }

                // 大量ファイル処理の確認
                if (selectedFiles.Count > 100)
                {
                    var result = await ShowConfirmationAsync(
                        "大量ファイル処理の確認",
                        $"{selectedFiles.Count}件のファイルの評価を解除します。\n\n処理に時間がかかる場合があります。続行しますか？");
                    
                    if (!result)
                    {
                        StatusMessage = "評価解除がキャンセルされました";
                        return;
                    }
                }

                // 処理開始
                IsProcessing = true;
                StatusMessage = $"評価を解除中: {selectedFiles.Count}件";
                
                // 元の評価値を保存（エラー時のロールバック用）
                foreach (var file in selectedFiles)
                {
                    originalRatings[file.Id] = file.Rating;
                }

                // UI更新のため先にモデルを更新
                foreach (var file in selectedFiles)
                {
                    file.Rating = null;
                }

                // データベースを一括更新（パフォーマンス向上）
                var fileIds = selectedFiles.Select(f => f.Id).ToList();
                
                try
                {
                    if (fileIds.Count > 1)
                    {
                        // 複数ファイルの場合は一括更新を使用
                        await _repository.UpdateRatingBatchAsync(fileIds, null);
                    }
                    else
                    {
                        // 単一ファイルの場合は通常の更新を使用
                        await _repository.UpdateAsync(selectedFiles.First());
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // データベース更新失敗時はUIを元に戻す
                    foreach (var file in selectedFiles)
                    {
                        if (originalRatings.TryGetValue(file.Id, out var originalRating))
                        {
                            file.Rating = originalRating;
                        }
                    }
                    throw new InvalidOperationException("データベースの更新に失敗しました。", ex);
                }

                var duration = DateTime.Now - startTime;
                StatusMessage = $"評価解除完了: {selectedFiles.Count}件 - {duration.TotalSeconds:F1}秒";
                _logger.LogInformation("評価を解除しました: {Count}件 - 処理時間: {Duration:F1}秒", 
                    selectedFiles.Count, duration.TotalSeconds);
                
                // 視覚的フィードバックを表示
                ShowRatingFeedback?.Invoke(null, selectedFiles.Count, true);
                
                // 大量処理の場合は完了通知（音も遅延）
                if (selectedFiles.Count > 100)
                {
                    await ShowDelayedSuccessNotification("評価解除完了", $"対象ファイル: {selectedFiles.Count}件\n処理時間: {duration.TotalSeconds:F1}秒", 0, 300, true);
                }
                else
                {
                    // 通常処理の場合は即座に音を再生
                    _systemSoundService.PlayCompletionSound();
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "評価解除中にデータベースエラーが発生しました");
                StatusMessage = "評価解除に失敗しました";
                ShowErrorNotificationEvent?.Invoke("評価解除失敗", $"{ex.Message}\n\n再度お試しください。", 5);
            }
            catch (Exception ex)
            {
                // 予期しないエラーの場合もUIを元に戻す
                foreach (var file in selectedFiles.Where(f => originalRatings.ContainsKey(f.Id)))
                {
                    if (originalRatings.TryGetValue(file.Id, out var originalRating))
                    {
                        file.Rating = originalRating;
                    }
                }
                
                _logger.LogError(ex, "評価解除中に予期しないエラーが発生しました");
                StatusMessage = $"評価解除エラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("評価解除エラー", $"{ex.Message}\n\nアプリケーションを再起動してください。", 6);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// 選択されたファイルの現在の評価を取得
        /// </summary>
        public int? GetCurrentRating()
        {
            if (SelectedMangaFile != null)
            {
                return SelectedMangaFile.Rating;
            }
            
            // 複数選択の場合は、全て同じ評価の場合のみ返す
            var selectedFiles = SelectedMangaFiles.ToList();
            if (selectedFiles.Count > 1)
            {
                var firstRating = selectedFiles.First().Rating;
                if (selectedFiles.All(f => f.Rating == firstRating))
                {
                    return firstRating;
                }
            }
            
            return null;
        }

        /// <summary>
        /// 評価設定の可否を判定
        /// </summary>
        public bool CanSetRating => SelectedMangaFiles.Count > 0 || SelectedMangaFile != null;

        /// <summary>
        /// 評価設定エラー時のUI状態復元
        /// </summary>
        private void RestoreRatingState(IEnumerable<MangaFile> files, Dictionary<int, int?> originalRatings)
        {
            foreach (var file in files.Where(f => originalRatings.ContainsKey(f.Id)))
            {
                if (originalRatings.TryGetValue(file.Id, out var originalRating))
                {
                    file.Rating = originalRating;
                }
            }
        }

        /// <summary>
        /// 大量ファイル処理の確認ダイアログ表示
        /// </summary>
        private bool ConfirmBulkOperation(int fileCount, string operation)
        {
            if (fileCount <= 100) return true;

            return _dialogService.ShowConfirmation(
                $"{fileCount}件のファイルに{operation}を実行します。\n\n処理に時間がかかる場合があります。続行しますか？",
                "大量ファイル処理の確認");
        }

        [RelayCommand]
        private async Task DeleteFilesAsync()
        {
            try
            {
                // 選択されたファイルを取得
                var selectedFiles = SelectedMangaFiles.ToList();
                var selectedCount = selectedFiles.Count;

                if (selectedCount == 0)
                {
                    // 選択されたファイルがない場合は、現在選択されている単一ファイルを使用
                    if (SelectedMangaFile != null)
                    {
                        selectedFiles = new List<MangaFile> { SelectedMangaFile };
                        selectedCount = 1;
                    }
                    else
                    {
                        StatusMessage = "削除対象のファイルが選択されていません";
                        ShowNotificationEvent?.Invoke("ファイル未選択", "削除するファイルを選択してください。", 3);
                        return;
                    }
                }

                // 確認ダイアログを表示
                var fileListText = "";
                if (selectedCount <= 10)
                {
                    // 10件以下の場合は全ファイル名を表示
                    fileListText = "\n削除対象ファイル:\n" + string.Join("\n", selectedFiles.Select(f => $"• {f.FileName}"));
                }
                else
                {
                    // 10件を超える場合は最初の10件のみ表示
                    var firstTenFiles = selectedFiles.Take(10).Select(f => $"• {f.FileName}");
                    fileListText = "\n削除対象ファイル:\n" + string.Join("\n", firstTenFiles) + $"\n...他{selectedCount - 10}件";
                }

                var result = await ShowConfirmationAsync(
                    "ファイル削除確認",
                    $"選択された{selectedCount}件のファイルを削除しますか？{fileListText}\n\nこの操作は元に戻せません。");

                if (!result)
                    return;

                IsProcessing = true;
                StatusMessage = $"ファイル削除中: 0/{selectedCount}";

                var successCount = 0;
                var failureCount = 0;
                var errors = new List<string>();
                var filesToDeleteFromDb = new List<int>();

                // 1. 物理ファイルを削除
                for (int i = 0; i < selectedFiles.Count; i++)
                {
                    var file = selectedFiles[i];
                    StatusMessage = $"ファイル削除中: {i + 1}/{selectedCount} - {file.FileName}";

                    try
                    {
                        // 実ファイルを削除
                        if (File.Exists(file.FilePath))
                        {
                            File.Delete(file.FilePath);
                        }

                        // データベース削除対象に追加
                        filesToDeleteFromDb.Add(file.Id);
                        successCount++;
                        _logger.LogInformation("ファイルを削除しました: {FileName}", file.FileName);
                    }
                    catch (Exception ex)
                    {
                        failureCount++;
                        var errorMessage = $"ファイル削除失敗: {file.FileName} - {ex.Message}";
                        errors.Add(errorMessage);
                        _logger.LogError(ex, "ファイル削除に失敗しました: {FileName}", file.FileName);
                    }
                }

                // 2. データベースから一括削除（サムネイルバイナリデータも自動的に削除される）
                if (filesToDeleteFromDb.Count > 0)
                {
                    try
                    {
                        StatusMessage = $"データベース更新中...";
                        await _repository.DeleteBatchAsync(filesToDeleteFromDb);
                        _logger.LogInformation("データベースから一括削除しました: {Count}件", filesToDeleteFromDb.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "データベース一括削除に失敗しました");
                        errors.Add($"データベース削除エラー: {ex.Message}");
                        failureCount += filesToDeleteFromDb.Count;
                        successCount -= filesToDeleteFromDb.Count;
                    }
                }

                // ファイル一覧を更新
                await LoadMangaFilesAsync();

                StatusMessage = $"ファイル削除完了: 成功={successCount}, 失敗={failureCount}";
                _logger.LogInformation("ファイル削除が完了しました: 成功={Success}, 失敗={Failure}", 
                    successCount, failureCount);

                // 結果を表示
                if (failureCount == 0)
                {
                    // 大量処理の場合は完了通知（音も遅延）
                    if (successCount > 100)
                    {
                        await ShowDelayedSuccessNotification("ファイル削除完了", $"削除: {successCount}件", 0, 300, true);
                    }
                    else
                    {
                        // 通常処理の場合は即座に音を再生
                        _systemSoundService.PlayCompletionSound();
                        await ShowDelayedSuccessNotification("ファイル削除完了", $"削除: {successCount}件", 0);
                    }
                }
                else
                {
                    var errorSummary = errors.Take(5).ToList();
                    var errorMessage = $"ファイル削除が完了しました。\n\n成功: {successCount}件\n失敗: {failureCount}件\n\n" +
                        $"最初の5件のエラー:\n{string.Join("\n", errorSummary)}";
                    if (errors.Count > 5)
                    {
                        errorMessage += $"\n...他{errors.Count - 5}件のエラー";
                    }
                    _dialogService.ShowError(errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル削除処理中にエラーが発生しました");
                StatusMessage = $"ファイル削除エラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("ファイル削除エラー", ex.Message, 4);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task PerformSearchAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    await LoadMangaFilesAsync();
                    return;
                }



                var query = new SearchQuery
                {
                    Text = SearchText,
                    UseRegex = false,
                    SearchFields = SearchField.All,
                    SortBy = Services.SortOrder.Relevance
                };

                var result = await _searchEngineService.SearchAsync(query);

                MangaFiles.Clear();
                foreach (var file in result.Results)
                {
                    MangaFiles.Add(file);
                }

                UpdateFileCountMessage();
                StatusMessage = $"検索完了: {result.TotalCount}件見つかりました ({result.Duration.TotalMilliseconds:F0}ms)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "検索中にエラーが発生しました");
                StatusMessage = $"検索エラー: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedFolderAsync(string selectedFolder)
        {
            try
            {
                if (string.IsNullOrEmpty(selectedFolder))
                {
                    ShowNotificationEvent?.Invoke("フォルダ未選択", "削除するフォルダが選択されていません。", 3);
                    return;
                }

                if (_dialogService.ShowConfirmation($"選択したフォルダを削除しますか？\n{selectedFolder}\n\n※このフォルダ内のファイル情報もデータベースから削除されます。"))
                {
                    IsProcessing = true;
                    StatusMessage = $"フォルダを削除中: {selectedFolder}";
                    
                    await Task.Run(async () =>
                    {
                        try
                        {
                            var currentFolders = _config.GetSetting<List<string>>("ScanFolders", new List<string>());
                            
                            // フォルダを設定から削除
                            currentFolders.Remove(selectedFolder);
                            _config.SetSetting("ScanFolders", currentFolders);
                            await _config.SaveAsync();
                            
                            // フォルダ内のファイルを一括削除（直下ファイルのみ）
                            _logger.LogInformation("フォルダ削除開始: {Folder}", selectedFolder);
                            
                            // UIスレッドでステータス更新
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                StatusMessage = $"フォルダ内のファイルを削除中: {selectedFolder}";
                            });
                            
                            // 一括削除実行
                            var deletedCount = await _repository.DeleteByFolderPathAsync(selectedFolder);
                            
                            // UIスレッドでUI更新
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                LoadScanFolders();
                                await LoadMangaFilesAsync();
                                
                                StatusMessage = $"フォルダを削除しました: {selectedFolder} (ファイル {deletedCount}件)";
                                _logger.LogInformation("フォルダを削除しました: {Folder} (ファイル {Count}件)", selectedFolder, deletedCount);
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "フォルダ削除処理中にエラーが発生しました");
                            
                            // UIスレッドでエラー表示
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                _dialogService.ShowError($"フォルダ削除エラー: {ex.Message}");
                            });
                        }
                    });
                    
                    IsProcessing = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フォルダ削除中にエラーが発生しました");
                _dialogService.ShowError($"フォルダ削除エラー: {ex.Message}");
                IsProcessing = false;
            }
        }

        [RelayCommand]
        private void DeleteFolders()
        {
            try
            {
                var currentFolders = _config.GetSetting<List<string>>("ScanFolders", new List<string>());
                
                if (!currentFolders.Any())
                {
                    ShowNotificationEvent?.Invoke("フォルダ未設定", "現在、スキャン対象フォルダは設定されていません。", 3);
                    return;
                }

                // フォルダ削除選択ダイアログを作成
                var dialog = new System.Windows.Window
                {
                    Title = "削除するフォルダを選択",
                    Width = 600,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = System.Windows.Application.Current.MainWindow
                };
                
                // レイアウト
                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                
                // フォルダリスト（チェックボックス付き）
                var scrollViewer = new System.Windows.Controls.ScrollViewer { Margin = new Thickness(10) };
                var stackPanel = new System.Windows.Controls.StackPanel();
                
                var checkBoxes = new List<System.Windows.Controls.CheckBox>();
                // フォルダを昇順でソートしてから表示
                var sortedFolders = currentFolders.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var folder in sortedFolders)
                {
                    var checkBox = new System.Windows.Controls.CheckBox
                    {
                        Content = folder,
                        Margin = new Thickness(0, 2, 0, 2),
                        Tag = folder
                    };
                    stackPanel.Children.Add(checkBox);
                    checkBoxes.Add(checkBox);
                }
                
                scrollViewer.Content = stackPanel;
                
                // ボタンパネル
                var buttonPanel = new System.Windows.Controls.StackPanel 
                { 
                    Orientation = System.Windows.Controls.Orientation.Horizontal, 
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right, 
                    Margin = new Thickness(10) 
                };
                
                var selectAllButton = new System.Windows.Controls.Button { Content = "すべて選択", Width = 80, Margin = new Thickness(0, 0, 5, 0) };
                var deselectAllButton = new System.Windows.Controls.Button { Content = "すべて解除", Width = 80, Margin = new Thickness(0, 0, 5, 0) };
                var deleteButton = new System.Windows.Controls.Button { Content = "削除", Width = 80, Margin = new Thickness(0, 0, 5, 0) };
                var cancelButton = new System.Windows.Controls.Button { Content = "キャンセル", Width = 80 };
                
                buttonPanel.Children.Add(selectAllButton);
                buttonPanel.Children.Add(deselectAllButton);
                buttonPanel.Children.Add(deleteButton);
                buttonPanel.Children.Add(cancelButton);
                
                // グリッドに配置
                grid.Children.Add(scrollViewer);
                grid.Children.Add(buttonPanel);
                
                System.Windows.Controls.Grid.SetRow(scrollViewer, 0);
                System.Windows.Controls.Grid.SetRow(buttonPanel, 1);
                
                dialog.Content = grid;
                
                // イベント
                selectAllButton.Click += (s, e) =>
                {
                    foreach (var cb in checkBoxes)
                    {
                        cb.IsChecked = true;
                    }
                };
                
                deselectAllButton.Click += (s, e) =>
                {
                    foreach (var cb in checkBoxes)
                    {
                        cb.IsChecked = false;
                    }
                };
                
                deleteButton.Click += async (s, e) =>
                {
                    var selectedFolders = checkBoxes
                        .Where(cb => cb.IsChecked == true)
                        .Select(cb => cb.Tag.ToString())
                        .ToList();
                    
                    if (!selectedFolders.Any())
                    {
                        _dialogService.ShowInformation("削除するフォルダを選択してください。");
                        return;
                    }
                    
                    if (_dialogService.ShowConfirmation($"選択した{selectedFolders.Count}個のフォルダを削除しますか？\n\n※フォルダ内のファイル情報もデータベースから削除されます。"))
                    {
                        dialog.Close();
                        
                        // 一括削除実行
                        IsProcessing = true;
                        StatusMessage = $"選択したフォルダを削除中: {selectedFolders.Count}個";
                        
                        await Task.Run(async () =>
                        {
                            try
                            {
                                int totalDeletedFiles = 0;
                                
                                foreach (var folder in selectedFolders)
                                {
                                    // UIスレッドでステータス更新
                                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        StatusMessage = $"フォルダを削除中: {folder}";
                                    });
                                    
                                    // フォルダを設定から削除
                                    if (folder != null)
                                    {
                                        currentFolders.Remove(folder);
                                    }
                                    
                                    // フォルダ内のファイルを一括削除
                                    var deletedCount = await _repository.DeleteByFolderPathAsync(folder ?? "");
                                    totalDeletedFiles += deletedCount;
                                    
                                    _logger.LogInformation("フォルダを削除しました: {Folder} (ファイル {Count}件)", folder, deletedCount);
                                }
                                
                                // 設定を保存
                                _config.SetSetting("ScanFolders", currentFolders);
                                await _config.SaveAsync();
                                
                                // UIスレッドでUI更新
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                                {
                                    LoadScanFolders();
                                    await LoadMangaFilesAsync();
                                    
                                    StatusMessage = $"{selectedFolders.Count}個のフォルダを削除しました (ファイル {totalDeletedFiles}件)";
                                });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "フォルダ一括削除処理中にエラーが発生しました");
                                
                                // UIスレッドでエラー表示
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    _dialogService.ShowError($"フォルダ削除エラー: {ex.Message}");
                                });
                            }
                        });
                        
                        IsProcessing = false;
                    }
                };
                
                cancelButton.Click += (s, e) => { dialog.Close(); };
                
                // ダイアログ表示
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フォルダ削除ダイアログ表示中にエラーが発生しました");
                _dialogService.ShowError($"フォルダ削除ダイアログエラー: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            try
            {
                // 設定画面のViewModelを作成
                var app = System.Windows.Application.Current as App;
                var settingsViewModel = app?.Services.GetRequiredService<SettingsViewModel>();
                
                if (settingsViewModel == null)
                {
                    throw new InvalidOperationException("SettingsViewModel could not be created");
                }
                
                // ステータスメッセージ更新アクションを設定
                settingsViewModel.UpdateStatusMessage = (message) => StatusMessage = message;
                
                // 設定画面を表示
                var settingsWindow = new Views.SettingsWindow(settingsViewModel);
                settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
                
                var result = settingsWindow.ShowDialog();
                
                // 設定が適用されたかどうかでメッセージを表示
                if (settingsViewModel.WasApplied)
                {
                    StatusMessage = "設定を更新しました";
                    _logger.LogInformation("設定ダイアログが閉じられました。設定が適用されました。");
                }
                else
                {
                    StatusMessage = "準備完了";
                    _logger.LogInformation("設定ダイアログが閉じられました。設定の変更はありませんでした。");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定画面の表示中にエラーが発生しました");
                ShowErrorNotificationEvent?.Invoke("設定画面エラー", $"設定画面の表示中にエラーが発生しました: {ex.Message}", 4);
                StatusMessage = "設定画面エラー";
            }
        }

        [RelayCommand]
        private void OpenFile(MangaFile file)
        {
            if (file == null)
            {
                StatusMessage = "ファイルが選択されていません";
                return;
            }

            try
            {
                if (_fileOperationService.OpenFile(file.FilePath))
                {
                    StatusMessage = $"ファイルを開きました: {file.FileName}";
                }
                else
                {
                    StatusMessage = $"ファイルを開けませんでした: {file.FileName}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイルを開く際にエラーが発生しました: {FileName}", file.FileName);
                StatusMessage = $"エラー: {ex.Message}";
            }
        }





        [RelayCommand]
        private async Task ClearAttributesAsync(ClearAttributesParameters parameters)
        {
            try
            {
                // 選択されたファイルがあるか確認
                if (!SelectedMangaFiles.Any())
                {
                    ShowNotificationEvent?.Invoke("ファイル未選択", "クリアするファイルが選択されていません。", 3);
                    return;
                }
                
                IsProcessing = true;
                StatusMessage = $"属性情報をクリア中: {SelectedMangaFiles.Count}件";
                
                int successCount = 0;
                
                foreach (var file in SelectedMangaFiles.ToList())
                {
                    // 選択された属性をクリア
                    if (parameters.ClearTitle) file.Title = null;
                    if (parameters.ClearOriginalAuthor) file.OriginalAuthor = null;
                    if (parameters.ClearArtist) file.Artist = null;
                    if (parameters.ClearAuthorReading) file.AuthorReading = null;
                    if (parameters.ClearVolumeNumber) file.VolumeNumber = null;
                    if (parameters.ClearGenre) file.Genre = null;
                    if (parameters.ClearPublishDate) file.PublishDate = null;
                    if (parameters.ClearPublisher) file.Publisher = null;
                    if (parameters.ClearTags) file.Tags = null;
                    if (parameters.ClearAIProcessed) file.IsAIProcessed = false;
                    
                    // データベースに保存
                    await _repository.UpdateAsync(file);
                    successCount++;
                }
                
                StatusMessage = $"属性情報のクリアが完了しました: {successCount}件";
                _logger.LogInformation("属性情報のクリアが完了しました: {Count}件", successCount);
                
                // 処理結果を表示
                await ShowDelayedSuccessNotification("属性情報クリア完了", $"処理件数: {successCount}件", 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "属性情報のクリア中にエラーが発生しました");
                StatusMessage = $"属性情報のクリアエラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("属性情報クリアエラー", ex.Message, 4);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// ファイル一覧を軽量読み込みで取得します。
        /// サムネイルデータを除外することで、大量ファイル環境でのパフォーマンスを向上させます。
        /// エラー時は既存の完全読み込みメソッドにフォールバックします。
        /// </summary>
        private async Task LoadMangaFilesAsync()
        {
            try
            {
                _logger.LogDebug("軽量ファイル読み込みを開始します");
                
                // 軽量読み込みに変更（サムネイルデータ除外）
                // パフォーマンス最適化: 7万件環境で約2.15GB → 50MBに削減
                _allMangaFiles = await _repository.GetAllWithoutThumbnailsAsync();
                
                _logger.LogDebug("軽量ファイル読み込みが完了しました: {Count}件", _allMangaFiles.Count);
                
                // 選択されたフォルダに基づいてフィルタリング（ステータスメッセージは更新しない）
                FilterMangaFilesByFolder(false);
                
                // フォルダ統計情報を更新
                await UpdateFolderStatisticsAsync();
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "軽量ファイル読み込み中にエラーが発生しました");
                StatusMessage = $"ファイル読み込みエラー: {ex.Message}";
                
                // エラー時のフォールバック（既存メソッドを使用）
                try
                {
                    _logger.LogWarning("軽量読み込みに失敗しました。フォールバックモードで再試行します");
                    _allMangaFiles = await _repository.GetAllAsync();
                    
                    _logger.LogInformation("フォールバックモードでファイル読み込みが完了しました: {Count}件", _allMangaFiles.Count);
                    
                    // 選択されたフォルダに基づいてフィルタリング
                    FilterMangaFilesByFolder(false);
                    
                    // フォルダ統計情報を更新
                    await UpdateFolderStatisticsAsync();
                    
                    StatusMessage = "ファイル一覧を読み込みました（フォールバックモード）";
                    _logger.LogInformation("フォールバックモードでの処理が正常に完了しました");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "フォールバック読み込みも失敗しました。データベース接続またはデータ整合性に問題がある可能性があります");
                    StatusMessage = $"ファイル読み込み失敗: {fallbackEx.Message}";
                    
                    // 重大なエラーの場合は空のリストで初期化して安全な状態を保つ
                    _allMangaFiles = new List<MangaFile>();
                    FilterMangaFilesByFolder(false);
                }
            }
        }
        
        private void FilterMangaFilesByFolder()
        {
            FilterMangaFilesByFolder(true);
        }
        
        private void FilterMangaFilesByFolder(bool updateStatusMessage = true)
        {
            MangaFiles.Clear();
            
            // 選択されたフォルダがない場合はすべてのファイルを表示
            if (string.IsNullOrEmpty(SelectedScanFolder))
            {
                foreach (var file in _allMangaFiles)
                {
                    MangaFiles.Add(file);
                }
                
                if (updateStatusMessage && !IsProcessing)
                {
                    StatusMessage = $"すべてのフォルダのファイルを表示しています: {MangaFiles.Count}件";
                }
            }
            else
            {
                // 選択されたフォルダの直下ファイルのみをフィルタリング（サブフォルダは除外）
                var normalizedSelectedFolder = SelectedScanFolder.TrimEnd('\\', '/');
                var filteredFiles = _allMangaFiles
                    .Where(f => 
                    {
                        var fileDir = Path.GetDirectoryName(f.FilePath)?.TrimEnd('\\', '/');
                        return string.Equals(fileDir, normalizedSelectedFolder, StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();
                
                foreach (var file in filteredFiles)
                {
                    MangaFiles.Add(file);
                }
                
                if (updateStatusMessage && !IsProcessing)
                {
                    StatusMessage = $"フォルダ '{Path.GetFileName(SelectedScanFolder)}' の直下ファイルを表示しています: {MangaFiles.Count}件";
                }
            }
            
            UpdateFileCountMessage();
        }

        private void UpdateFileCountMessage()
        {
            var totalCount = MangaFiles.Count;
            var aiProcessedCount = MangaFiles.Count(f => f.IsAIProcessed);

            // 総ファイルサイズを非同期で取得し、エラーハンドリングでグレースフル劣化を実装
            _ = Task.Run(async () =>
            {
                try
                {
                    var statistics = await _fileSizeService.GetTotalFileSizeAsync();
                    var formattedSize = _fileSizeService.FormatFileSize(statistics.TotalFileSize);
                    
                    // UIスレッドで更新
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        FileCountMessage = $"総数: {totalCount}件 | 総サイズ: {formattedSize} | タグ取得済: {aiProcessedCount}件";
                    });
                }
                catch (Exception ex)
                {
                    // エラー時はログに記録し、既存の表示を維持（グレースフル劣化）
                    _logger.LogWarning(ex, "総ファイルサイズの取得中にエラーが発生しました。既存の表示を維持します。");
                    
                    // UIスレッドで既存形式の表示を設定
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        FileCountMessage = $"総数: {totalCount}件 | タグ取得済: {aiProcessedCount}件";
                    });
                }
            });
        }

        /// <summary>
        /// フォルダ統計情報を更新します
        /// </summary>
        private async Task UpdateFolderStatisticsAsync()
        {
            try
            {
                var statistics = await _fileSizeService.GetFolderStatisticsAsync();
                _logger.LogInformation("フォルダ統計情報を取得しました: {Count}個のフォルダ", statistics.Count);
                FolderStatistics = statistics;
                UpdateFolderDisplayItems();
            }
            catch (Exception ex)
            {
                // エラー時はログに記録し、既存の統計情報を維持（グレースフル劣化）
                _logger.LogWarning(ex, "フォルダ統計情報の取得中にエラーが発生しました。既存の表示を維持します。");
                // FolderStatisticsは既存の値を保持
                UpdateFolderDisplayItems(); // エラー時でも表示を更新
            }
        }

        /// <summary>
        /// フォルダ表示アイテムを更新します
        /// </summary>
        private void UpdateFolderDisplayItems()
        {
            FolderDisplayItems.Clear();
            
            _logger.LogDebug("フォルダ表示アイテム更新開始: ScanFolders={Count}個, FolderStatistics={StatCount}個", 
                ScanFolders.Count, FolderStatistics.Count);
            
            foreach (var folderPath in ScanFolders)
            {
                var folderName = Path.GetFileName(folderPath) ?? folderPath;
                var statisticsText = "(0件, 0 B)";
                

                
                if (FolderStatistics.TryGetValue(folderPath, out var statistics))
                {
                    var formattedSize = _fileSizeService.FormatFileSize(statistics.TotalSize);
                    statisticsText = $"({statistics.FileCount}件, {formattedSize})";

                }
                else
                {
                    _logger.LogDebug("統計情報が見つかりません: {FolderPath}", folderPath);
                }
                
                FolderDisplayItems.Add(new FolderDisplayItem
                {
                    FolderPath = folderPath,
                    FolderName = folderPath, // フルパス表示に戻す
                    StatisticsText = statisticsText,
                    DisplayText = $"{folderPath} {statisticsText}"
                });
            }
        }



        private CancellationTokenSource? _searchCancellationTokenSource;
        
        partial void OnSearchTextChanged(string value)
        {
            // 前の検索をキャンセル
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();
            
            // デバウンス処理（700ms後に検索実行）
            _ = Task.Delay(700, _searchCancellationTokenSource.Token)
                .ContinueWith(async _ =>
                {
                    if (!_searchCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await PerformSearchAsync();
                        });
                    }
                }, _searchCancellationTokenSource.Token);
        }

        partial void OnSelectedMangaFileChanged(MangaFile? value)
        {
            // 選択変更時の処理（必要に応じて実装）
        }

        partial void OnThumbnailDisplayModeChanged(ThumbnailDisplayMode value)
        {
            UpdateThumbnailSize(value);
        }

        private void UpdateThumbnailSize(ThumbnailDisplayMode mode)
        {
            switch (mode)
            {
                case ThumbnailDisplayMode.Standard:
                    ThumbnailMaxWidth = ThumbnailConstants.MaxWidth;
                    ThumbnailMaxHeight = ThumbnailConstants.MaxHeight;
                    break;
                case ThumbnailDisplayMode.Compact:
                    ThumbnailMaxWidth = 90;   // 半分のサイズ
                    ThumbnailMaxHeight = 60;  // 半分のサイズ
                    break;
                case ThumbnailDisplayMode.Hidden:
                    // サイズは関係ないが、一応設定
                    ThumbnailMaxWidth = 0;
                    ThumbnailMaxHeight = 0;
                    break;
            }
        }
        
        partial void OnSelectedScanFolderChanged(string? value)
        {
            // 選択されたフォルダが変更されたら、ファイル一覧をフィルタリング
            FilterMangaFilesByFolder();
        }

        [RelayCommand]
        private void ResetSort()
        {
            // MainWindowのResetDataGridSort()メソッドを呼び出すためのイベントを発火
            ResetSortEvent?.Invoke();
        }
        
        [RelayCommand]
        private void ClearFolderFilter()
        {
            // フォルダ選択をクリア
            SelectedScanFolder = null;
            
            // 検索テキストをクリア
            SearchText = string.Empty;
            
            // フォルダリストの選択をクリア
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.FolderListBox.SelectedItem = null;
                }
            });
            
            // すべてのファイルを表示
            FilterMangaFilesByFolder();
            
            StatusMessage = "すべてのフォルダのファイルを表示しています";
        }

        [RelayCommand]
        private async Task GenerateThumbnailsAsync()
        {
            try
            {
                // 選択されたファイルを取得
                var selectedFiles = SelectedMangaFiles.ToList();
                var selectedCount = selectedFiles.Count;

                if (selectedCount == 0)
                {
                    // 選択されたファイルがない場合は全ファイルを対象にする
                    selectedFiles = _allMangaFiles.ToList();
                    selectedCount = selectedFiles.Count;
                }

                // 生成モードを選択
                var generateMode = await ShowThumbnailGenerationModeDialogAsync(selectedCount);
                if (generateMode == ThumbnailGenerationMode.Cancel)
                {
                    StatusMessage = "サムネイル生成がキャンセルされました";
                    return;
                }

                IsProcessing = true;
                ProgressValue = 0;
                ProgressMaximum = selectedCount;
                StatusMessage = $"サムネイル生成中: 0/{selectedCount}";

                var startTime = DateTime.Now;
                var successCount = 0;
                var failureCount = 0;

                // 進捗報告用（UIスレッドへの負荷軽減のため更新頻度を制限）
                var lastUpdateTime = DateTime.MinValue;
                var progress = new Progress<ThumbnailProgress>(p =>
                {
                    var now = DateTime.Now;
                    // 100ms間隔でのみUI更新（スピナーのカクつき防止）
                    if ((now - lastUpdateTime).TotalMilliseconds >= 100)
                    {
                        ProgressValue = p.CurrentFile;
                        StatusMessage = $"サムネイル生成中: {p.CurrentFile}/{p.TotalFiles}";
                        lastUpdateTime = now;
                    }
                });

                // サムネイル生成を実行
                var skipExisting = generateMode == ThumbnailGenerationMode.OnlyMissing;
                var thumbnailResult = await _thumbnailService.GenerateThumbnailsBatchAsync(selectedFiles, progress, default, skipExisting);

                // 結果を集計
                var actuallyProcessedCount = 0;
                var skippedCount = 0;
                
                if (thumbnailResult.IsSuccess)
                {
                    var results = thumbnailResult.Value!;
                    foreach (var result in results)
                    {
                        successCount++;
                        // 既存のサムネイルをスキップした場合とそうでない場合を区別
                        if (result.WasSkipped)
                        {
                            skippedCount++;
                        }
                        else
                        {
                            actuallyProcessedCount++;
                        }
                        _logger.LogInformation("サムネイル生成成功: {FileName}", result.MangaFile?.FileName);
                    }
                }
                else
                {
                    failureCount = selectedFiles.Count();
                    _logger.LogError("サムネイル生成エラー: {Error}", thumbnailResult.ErrorMessage);
                }

                var duration = DateTime.Now - startTime;
                var modeText = generateMode == ThumbnailGenerationMode.OnlyMissing ? "未生成のみ" : "全て再生成";
                
                if (generateMode == ThumbnailGenerationMode.OnlyMissing && skippedCount > 0)
                {
                    StatusMessage = $"サムネイル生成完了({modeText}): 処理={actuallyProcessedCount}, スキップ={skippedCount}, 失敗={failureCount} (処理時間: {duration.TotalSeconds:F1}秒)";
                }
                else
                {
                    StatusMessage = $"サムネイル生成完了({modeText}): 成功={successCount}, 失敗={failureCount} (処理時間: {duration.TotalSeconds:F1}秒)";
                }
                
                _logger.LogInformation("サムネイル生成が完了しました: 成功={Success}, 失敗={Failure}, スキップ={Skipped}", 
                    successCount, failureCount, skippedCount);
                
                // 結果を表示（音も遅延）
                if (failureCount == 0)
                {
                    if (generateMode == ThumbnailGenerationMode.OnlyMissing && skippedCount > 0)
                    {
                        await ShowDelayedSuccessNotification("サムネイル生成完了", $"処理: {actuallyProcessedCount}件\nスキップ: {skippedCount}件\n処理時間: {duration.TotalSeconds:F1}秒", 0, 300, true);
                    }
                    else
                    {
                        await ShowDelayedSuccessNotification("サムネイル生成完了", $"成功: {successCount}件\n処理時間: {duration.TotalSeconds:F1}秒", 0, 300, true);
                    }
                }
                else
                {
                    if (generateMode == ThumbnailGenerationMode.OnlyMissing && skippedCount > 0)
                    {
                        ShowErrorNotificationEvent?.Invoke("サムネイル生成完了（エラーあり）", $"処理: {actuallyProcessedCount}件\nスキップ: {skippedCount}件\n失敗: {failureCount}件\n処理時間: {duration.TotalSeconds:F1}秒", 5);
                    }
                    else
                    {
                        ShowErrorNotificationEvent?.Invoke("サムネイル生成完了（エラーあり）", $"成功: {successCount}件\n失敗: {failureCount}件\n処理時間: {duration.TotalSeconds:F1}秒", 5);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル生成中にエラーが発生しました");
                StatusMessage = $"サムネイル生成エラー: {ex.Message}";
                _dialogService.ShowError($"サムネイル生成エラー: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                ProgressValue = 0;
            }
        }

        [RelayCommand]
        private async Task GenerateSingleThumbnailAsync()
        {
            try
            {
                if (SelectedMangaFile == null)
                {
                    StatusMessage = "ファイルが選択されていません";
                    return;
                }

                IsProcessing = true;
                StatusMessage = $"サムネイル生成中: {SelectedMangaFile.FileName}";

                var result = await _thumbnailService.GenerateThumbnailAsync(SelectedMangaFile);

                if (result.IsSuccess)
                {
                    StatusMessage = $"サムネイル生成完了: {SelectedMangaFile.FileName}";
                    _logger.LogInformation("サムネイル生成が完了しました: {FileName}", SelectedMangaFile.FileName);
                    
                    // 処理完了音を再生
                    _systemSoundService.PlayCompletionSound();
                }
                else
                {
                    StatusMessage = $"サムネイル生成失敗: {result.ErrorMessage}";
                    _logger.LogWarning("サムネイル生成に失敗しました: {FileName} - {Error}", 
                        SelectedMangaFile.FileName, result.ErrorMessage);
                    _dialogService.ShowError($"サムネイル生成に失敗しました: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル生成中にエラーが発生しました");
                StatusMessage = $"サムネイル生成エラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("サムネイル生成エラー", ex.Message, 4);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// 差分スキャンを実行します（DBクリアなし、変更分のみ処理）
        /// </summary>
        [RelayCommand]
        private async Task IncrementalScanAsync()
        {
            try
            {
                StatusMessage = "差分スキャンを開始しています...";
                _logger.LogInformation("差分スキャンを開始します");

                var allScanFolders = _config.GetSetting<List<string>>("ScanFolders", new List<string>());
                if (!allScanFolders.Any())
                {
                    _dialogService.ShowInformation("スキャン対象フォルダが設定されていません。\n「フォルダ追加」ボタンでフォルダを追加してください。");
                    return;
                }

                // 確認ダイアログを表示
                var result = await ShowConfirmationAsync("差分スキャン確認", "差分スキャンを実行します。\n（変更されたファイルのみ処理します）\n\n実行しますか？");
                if (!result)
                {
                    StatusMessage = "差分スキャンがキャンセルされました";
                    return;
                }

                // 確認ダイアログの非表示アニメーションが完了するまで待機
                await Task.Delay(500);

                IsProcessing = true;

                // 重い処理をバックグラウンドで実行（再スキャンと同じパターン）
                var (scanResult, duration) = await Task.Run(async () =>
                {
                    // 差分スキャンを実行
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        StatusMessage = "差分スキャンを実行中...");
                    
                    var startTime = DateTime.Now;
                    var lastUpdateTime = DateTime.MinValue;
                    var progress = new Progress<ScanProgress>(p =>
                    {
                        // UI更新の頻度を制限（100ms間隔）
                        var now = DateTime.Now;
                        if ((now - lastUpdateTime).TotalMilliseconds < 100)
                            return;
                        
                        lastUpdateTime = now;
                        
                        // 軽量なステータス表示のみ
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (p.TotalFiles > 0)
                            {
                                StatusMessage = $"処理中: {p.CurrentFile}/{p.TotalFiles}";
                            }
                            else
                            {
                                StatusMessage = p.Status;
                            }
                        });
                    });

                    var result = await _fileScannerService.PerformIncrementalScanAsync(progress);
                    var processDuration = DateTime.Now - startTime;
                    
                    return (result, processDuration);
                });

                // ファイル一覧を更新
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await LoadMangaFilesAsync();
                    UpdateFileCountMessage();
                });

                if (scanResult.IsSuccess)
                {
                    var scanData = scanResult.Value!;
                    StatusMessage = $"差分スキャン完了: 追加={scanData.FilesAdded}, 更新={scanData.FilesUpdated}, 削除={scanData.FilesRemoved} (処理時間: {duration.TotalSeconds:F1}秒)";
                    _logger.LogInformation("差分スキャンが完了しました: 追加={Added}, 更新={Updated}, 削除={Removed}", 
                        scanData.FilesAdded, scanData.FilesUpdated, scanData.FilesRemoved);
                    
                    // 変更がない場合の特別なメッセージ
                    if (scanData.FilesAdded == 0 && scanData.FilesUpdated == 0 && scanData.FilesRemoved == 0)
                    {
                        await ShowDelayedSuccessNotification("差分スキャン完了", $"変更されたファイルはありませんでした。\n処理時間: {duration.TotalSeconds:F1}秒", 0, 300, true);
                    }
                    else
                    {
                        await ShowDelayedSuccessNotification("差分スキャン完了", $"追加: {scanData.FilesAdded}件\n更新: {scanData.FilesUpdated}件\n削除: {scanData.FilesRemoved}件\n処理時間: {duration.TotalSeconds:F1}秒", 0, 300, true);
                    }
                }
                else
                {
                    StatusMessage = $"差分スキャンエラー: {scanResult.ErrorMessage}";
                    _logger.LogWarning("差分スキャンでエラーが発生しました: {Error}", scanResult.ErrorMessage);
                    ShowErrorNotificationEvent?.Invoke("差分スキャンエラー", scanResult.ErrorMessage ?? "不明なエラー", 6);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "差分スキャン中にエラーが発生しました");
                StatusMessage = $"差分スキャンエラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("差分スキャンエラー", $"差分スキャン中にエラーが発生しました: {ex.Message}", 4);
            }
            finally
            {
                IsProcessing = false;
                ProgressValue = 0;
            }
        }





        private async Task<ThumbnailGenerationMode> ShowThumbnailGenerationModeDialogAsync(int fileCount)
        {
            if (ShowThumbnailModeSelectionEvent != null)
            {
                return await ShowThumbnailModeSelectionEvent.Invoke("サムネイル生成モード選択", $"サムネイル生成のモードを選択してください。", fileCount);
            }
            
            // フォールバック: キャンセルを返す
            return ThumbnailGenerationMode.Cancel;
        }

        /// <summary>
        /// ファイル移動コマンドを実行します
        /// </summary>
        [RelayCommand]
        private async Task MoveFilesAsync()
        {
            try
            {
                // 選択されたファイルを取得
                var selectedFiles = GetSelectedFilesForMove();
                if (!selectedFiles.Any())
                {
                    StatusMessage = "移動対象のファイルが選択されていません";
                    ShowNotificationEvent?.Invoke("ファイル移動", "移動対象のファイルが選択されていません。", 3);
                    return;
                }

                // フォルダを跨る複数ファイルの制限チェック
                if (!ValidateMultipleFileMove(selectedFiles))
                {
                    return; // 制限に該当する場合は処理終了
                }

                _logger.LogInformation("ファイル移動処理を開始します。選択ファイル数: {Count}", selectedFiles.Count);

                // フォルダ選択ダイアログを表示
                var selectedFolder = await ShowFolderSelectionDialogAsync(selectedFiles);
                if (string.IsNullOrEmpty(selectedFolder))
                {
                    StatusMessage = "ファイル移動がキャンセルされました";
                    _logger.LogInformation("ファイル移動がキャンセルされました");
                    return;
                }

                // 移動処理を実行
                await ExecuteFileMoveAsync(selectedFiles, selectedFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル移動処理中にエラーが発生しました");
                StatusMessage = $"ファイル移動エラー: {ex.Message}";
                ShowErrorNotificationEvent?.Invoke("ファイル移動エラー", ex.Message, 4);
            }
        }

        /// <summary>
        /// 複数ファイル移動の妥当性チェック
        /// </summary>
        private bool ValidateMultipleFileMove(List<MangaFile> selectedFiles)
        {
            if (selectedFiles.Count <= 1)
            {
                return true; // 単一ファイルは制限なし
            }

            // 複数ファイルが異なるフォルダに属しているかチェック
            var distinctFolders = selectedFiles
                .Select(f => Path.GetDirectoryName(f.FilePath))
                .Where(dir => !string.IsNullOrEmpty(dir))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctFolders.Count > 1)
            {
                // フォルダを跨る複数ファイルの移動は制限
                var message = "複数のフォルダに跨るファイルの移動はサポートされていません。";
                
                ShowNotificationEvent?.Invoke("ファイル移動の制限", message, 4);
                StatusMessage = "ファイル移動がキャンセルされました（フォルダ跨ぎ制限）";
                _logger.LogInformation("フォルダを跨る複数ファイル移動が制限されました。フォルダ数: {Count}", distinctFolders.Count);
                
                return false;
            }

            return true;
        }

        /// <summary>
        /// 移動対象のファイルを取得します
        /// </summary>
        private List<MangaFile> GetSelectedFilesForMove()
        {
            // 複数選択されている場合はそれを使用
            if (SelectedMangaFiles.Any())
            {
                return SelectedMangaFiles.ToList();
            }

            // 単一選択の場合
            if (SelectedMangaFile != null)
            {
                return new List<MangaFile> { SelectedMangaFile };
            }

            return new List<MangaFile>();
        }

        /// <summary>
        /// フォルダ選択ダイアログを表示します
        /// </summary>
        private async Task<string?> ShowFolderSelectionDialogAsync(List<MangaFile> selectedFiles)
        {
            try
            {
                // 移動元フォルダを特定
                var sourceFolder = selectedFiles.Count > 0 
                    ? Path.GetDirectoryName(selectedFiles[0].FilePath) 
                    : null;

                // ViewModelを設定（DIコンテナから取得）
                var serviceProvider = System.Windows.Application.Current.Properties["ServiceProvider"] as IServiceProvider;
                if (serviceProvider == null)
                {
                    serviceProvider = App.ServiceProvider;
                }
                
                var viewModel = serviceProvider.GetRequiredService<FolderSelectionViewModel>();
                
                // 移動元フォルダ情報を設定
                viewModel.SetSourceFolder(sourceFolder);
                
                // FolderSelectionDialogを作成
                var dialog = new Views.FolderSelectionDialog(viewModel);

                // ダイアログを表示
                var result = dialog.ShowDialog();
                if (result == true && dialog.DataContext is FolderSelectionViewModel vm && vm.SelectedFolder != null)
                {
                    return vm.SelectedFolder.FolderPath;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フォルダ選択ダイアログの表示中にエラーが発生しました");
                throw;
            }
        }

        /// <summary>
        /// ファイル移動処理を実行します
        /// </summary>
        private async Task ExecuteFileMoveAsync(List<MangaFile> selectedFiles, string destinationFolder)
        {
            try
            {
                IsProcessing = true;
                ProgressValue = 0;
                ProgressMaximum = selectedFiles.Count;
                StatusMessage = $"ファイル移動中: 0/{selectedFiles.Count}";

                // 進捗報告用のプログレス
                var progress = new Progress<FileMoveProgress>(p =>
                {
                    ProgressValue = p.CurrentFile;
                    StatusMessage = p.GetProgressDescription();
                });

                // ファイル移動サービスを使用して移動実行
                var result = await _fileMoveService.MoveFilesAsync(selectedFiles, destinationFolder, progress);

                // 成功した場合はUI更新を先に実行
                if (result.SuccessCount > 0)
                {
                    await RefreshAfterFileMoveAsync();
                }

                // 移動処理結果を表示（UI更新後）
                await ShowMoveResultAsync(result);

                _logger.LogInformation("ファイル移動処理が完了しました。結果: {Summary}", result.GetSummary());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル移動実行中にエラーが発生しました");
                throw;
            }
            finally
            {
                IsProcessing = false;
                ProgressValue = 0;
            }
        }

        /// <summary>
        /// ファイル移動後のUI更新を実行します
        /// 差分スキャン実行後と同様の更新処理を実装
        /// </summary>
        private async Task RefreshAfterFileMoveAsync()
        {
            try
            {
                StatusMessage = "ファイル一覧を更新中...";
                _logger.LogInformation("ファイル移動後のUI更新を開始します");
                
                // 差分スキャン実行後と同様の更新処理を実装
                
                // 1. ファイル一覧を再読み込み
                // - 移動されたファイルの新しい場所への反映処理を実装
                // - サムネイル表示の継続機能を実装（データベースの紐づきにより自動的に維持）
                await LoadMangaFilesAsync();
                _logger.LogDebug("ファイル一覧の再読み込みが完了しました");
                
                // 2. ファイル数メッセージを更新
                UpdateFileCountMessage();
                
                // 3. フォルダ統計情報の再計算とフォルダ表示アイテムの更新
                // LoadMangaFilesAsync内でUpdateFolderStatisticsAsyncが呼ばれ、
                // その中でUpdateFolderDisplayItems()が呼ばれるため、明示的な呼び出しは不要
                // これにより、フォルダ統計情報の再計算処理とUI表示の即座な反映機能が実装される
                
                // 4. 選択状態のクリア処理を実装
                SelectedMangaFile = null;
                SelectedMangaFiles.Clear();
                _logger.LogDebug("選択状態をクリアしました");
                
                StatusMessage = "ファイル移動完了 - 一覧を更新しました";
                _logger.LogInformation("ファイル移動後のUI更新が完了しました。移動されたファイルは新しい場所に反映され、サムネイル表示も継続されます");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル移動後のUI更新中にエラーが発生しました");
                StatusMessage = "UI更新中にエラーが発生しました";
            }
        }

        /// <summary>
        /// ファイル移動処理結果を表示します
        /// </summary>
        private async Task ShowMoveResultAsync(FileMoveResult result)
        {
            try
            {
                var title = "ファイル移動結果";
                var message = BuildMoveResultMessage(result);
                
                if (result.IsCancelled)
                {
                    // キャンセルの場合は遅延付きで情報通知として表示（自動非表示なし）
                    await Task.Delay(300); // 他の通知と同様の遅延を追加
                    ShowNotificationEvent?.Invoke(title, message, 0);
                    _logger.LogInformation("ファイル移動がキャンセルされました");
                }
                else if (result.ErrorCount == 0)
                {
                    // エラーがない場合は成功通知
                    await ShowDelayedSuccessNotification(title, message, 0, 300, true);
                    _logger.LogInformation("ファイル移動結果を表示しました: {Summary}", result.GetSummary());
                }
                else
                {
                    // エラーがある場合はエラー通知
                    ShowErrorNotificationEvent?.Invoke(title, message, 6);
                    _logger.LogWarning("ファイル移動結果（エラーあり）を表示しました: {Summary}", result.GetSummary());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル移動結果の表示中にエラーが発生しました");
            }
        }

        /// <summary>
        /// ファイル移動結果のメッセージを構築します
        /// </summary>
        private string BuildMoveResultMessage(FileMoveResult result)
        {
            // キャンセルされた場合はシンプルなメッセージ
            if (result.IsCancelled)
            {
                return "キャンセルしました";
            }

            var messageBuilder = new System.Text.StringBuilder();
            
            // 基本的な結果情報
            messageBuilder.AppendLine($"成功: {result.SuccessCount}件");
            messageBuilder.AppendLine($"スキップ: {result.SkippedCount}件");
            messageBuilder.AppendLine($"エラー: {result.ErrorCount}件");
            
            // エラーがある場合は詳細を表示
            if (result.ErrorCount > 0 && result.Errors.Any())
            {
                messageBuilder.AppendLine();
                messageBuilder.AppendLine("エラー詳細:");
                
                // 最初の5件のエラーを表示
                var errorsToShow = result.Errors.Take(5).ToList();
                foreach (var error in errorsToShow)
                {
                    messageBuilder.AppendLine($"• {error}");
                }
                
                // 5件を超える場合は省略表示
                if (result.Errors.Count > 5)
                {
                    messageBuilder.AppendLine($"...他{result.Errors.Count - 5}件のエラー");
                }
            }
            
            return messageBuilder.ToString().TrimEnd();
        }


    }
}
