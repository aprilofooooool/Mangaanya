using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Mangaanya.Models;
using Mangaanya.Services;
using Mangaanya.Views;
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
        private readonly IThumbnailCleanupService _thumbnailCleanupService;
        private readonly ISystemSoundService _systemSoundService;

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
        private int _progressMaximum = 100;
        
        [ObservableProperty]
        private ObservableCollection<string> _scanFolders = new();
        
        [ObservableProperty]
        private string? _selectedScanFolder;
        
        [ObservableProperty]
        private bool _includeSubfolders = true;
        
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
            IThumbnailCleanupService thumbnailCleanupService,
            ISystemSoundService systemSoundService)
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
            _thumbnailCleanupService = thumbnailCleanupService;
            _systemSoundService = systemSoundService;
            
            // サムネイル表示設定を初期化
            ShowThumbnails = _config.GetSetting<bool>("ShowThumbnails", true);
        }

        // 初期化完了イベント
        public event EventHandler? InitializationCompleted;

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
                
                // 起動時サムネイルクリーンアップチェック
                StatusMessage = "サムネイルクリーンアップチェック中...";
                await PerformStartupThumbnailCleanup();
                
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

        // 外部から初期化を開始するメソッド
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
                    _dialogService.ShowInformation("選択されたフォルダは既に追加されています。");
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
                _dialogService.ShowError($"フォルダ選択エラー: {ex.Message}");
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
                    _dialogService.ShowInformation("スキャン対象フォルダが設定されていません。\n「フォルダ追加」ボタンでフォルダを追加してください。");
                    return;
                }

                // スキャン対象フォルダをそのまま使用（各フォルダの直下のみスキャンするため重複なし）
                var scanFolders = allScanFolders;

                // 確認ダイアログを表示
                var result = _dialogService.ShowConfirmation("全スキャン対象フォルダを再スキャンします。\n\n実行しますか？");
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
                    var supportedExtensions = new[] { ".zip", ".rar" };
                    
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
                                var folderSupportedExtensions = new[] { ".zip", ".rar" };
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
                            
                            if (scanResult.Success)
                            {
                                totalFilesAdded += scanResult.FilesAdded;
                                totalFilesUpdated += scanResult.FilesUpdated;
                                totalFilesRemoved += scanResult.FilesRemoved;
                                // このフォルダで実際に処理されたファイル数を累積
                                globalProcessedCount += folderFileCount;
                            }
                            else
                            {
                                allErrors.AddRange(scanResult.Errors);
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
                    
                    // 処理完了音を再生
                    _systemSoundService.PlayCompletionSound();
                    
                    _dialogService.ShowInformation($"再スキャンが完了しました。\n\n追加: {totalFilesAdded}件\n更新: {totalFilesUpdated}件\n削除: {totalFilesRemoved}件\n\n処理時間: {duration.TotalSeconds:F1}秒");
                }
                else
                {
                    StatusMessage = $"スキャン完了（エラー{allErrors.Count}件）: {totalFilesAdded}件追加";
                    _logger.LogWarning("フォルダ再スキャンでエラーが発生しました: {Errors}", allErrors);
                    _dialogService.ShowError($"再スキャンが完了しましたが、エラーがありました:\n\n追加: {totalFilesAdded}件\nエラー: {allErrors.Count}件\n\n最初の5件のエラー:\n{string.Join("\n", allErrors.Take(5))}" + 
                        (allErrors.Count > 5 ? $"\n...他{allErrors.Count - 5}件のエラー" : ""));
                }
            }
            catch (Exception ex)
            {
                // エラー時もスピナーを停止
                IsProcessing = false;
                
                _logger.LogError(ex, "フォルダ再スキャン中にエラーが発生しました");
                StatusMessage = $"再スキャンエラー: {ex.Message}";
                _dialogService.ShowError($"再スキャンエラー: {ex.Message}");
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

                                var result = await _fileScannerService.PerformFullScanAsync(folder, progress);
                                totalFilesAdded += result.FilesAdded;
                                allErrors.AddRange(result.Errors);
                                
                                _logger.LogInformation("フォルダスキャン完了: {Folder}, 追加={Added}件", folder, result.FilesAdded);
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
                            
                            // 処理完了音を再生
                            _systemSoundService.PlayCompletionSound();
                        }
                        else
                        {
                            StatusMessage = $"スキャン完了（エラー{allErrors.Count}件）: {totalFilesAdded}件追加";
                            _logger.LogWarning("新しいフォルダのスキャンでエラーが発生しました: {Errors}", allErrors);
                            _dialogService.ShowError($"スキャンが完了しましたが、エラーがありました:\n\n追加: {totalFilesAdded}件\nエラー: {allErrors.Count}件\n\n最初の3件のエラー:\n{string.Join("\n", allErrors.Take(3))}" + 
                                (allErrors.Count > 3 ? $"\n...他{allErrors.Count - 3}件のエラー" : ""));
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "新しいフォルダのスキャン中にエラーが発生しました");
                StatusMessage = $"スキャンエラー: {ex.Message}";
                _dialogService.ShowError($"スキャンエラー: {ex.Message}");
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

                var result = _dialogService.ShowConfirmation(
                    $"選択された{selectedCount}件のファイルに対してタグ情報取得を実行しますか？",
                    "一括タグ情報取得");

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
                _dialogService.ShowInformation($"タグ情報取得が完了しました。\n\n成功: {successCount}件\n失敗: {failureCount}件");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "一括編集中にエラーが発生しました");
                StatusMessage = $"一括編集エラー: {ex.Message}";
                _dialogService.ShowError($"一括編集エラー: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                ProgressValue = 0;
            }
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
                        _dialogService.ShowInformation("削除するファイルを選択してください。");
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

                var result = _dialogService.ShowConfirmation(
                    $"選択された{selectedCount}件のファイルを削除しますか？{fileListText}\n\nこの操作は元に戻せません。",
                    "ファイル削除");

                if (!result)
                    return;

                IsProcessing = true;
                StatusMessage = $"ファイル削除中: 0/{selectedCount}";

                var successCount = 0;
                var failureCount = 0;
                var errors = new List<string>();

                for (int i = 0; i < selectedFiles.Count; i++)
                {
                    var file = selectedFiles[i];
                    StatusMessage = $"ファイル削除中: {i + 1}/{selectedCount} - {file.FileName}";

                    try
                    {
                        // 1. サムネイルファイルを先に削除
                        if (!string.IsNullOrEmpty(file.ThumbnailPath))
                        {
                            await _thumbnailService.DeleteThumbnailAsync(file.ThumbnailPath);
                        }

                        // 2. 実ファイルを削除
                        if (File.Exists(file.FilePath))
                        {
                            File.Delete(file.FilePath);
                        }

                        // 3. データベースから削除
                        await _repository.DeleteAsync(file.Id);

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

                // ファイル一覧を更新
                await LoadMangaFilesAsync();

                StatusMessage = $"ファイル削除完了: 成功={successCount}, 失敗={failureCount}";
                _logger.LogInformation("ファイル削除が完了しました: 成功={Success}, 失敗={Failure}", 
                    successCount, failureCount);

                // 結果を表示
                if (failureCount == 0)
                {
                    _dialogService.ShowInformation($"ファイル削除が完了しました。\n\n削除: {successCount}件");
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
                _dialogService.ShowError($"ファイル削除エラー: {ex.Message}");
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
                    _dialogService.ShowInformation("削除するフォルダが選択されていません。");
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
                    _dialogService.ShowInformation("現在、スキャン対象フォルダは設定されていません。");
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
                _dialogService.ShowError($"設定画面の表示中にエラーが発生しました: {ex.Message}");
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
                    _dialogService.ShowInformation("クリアするファイルが選択されていません。");
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
                _dialogService.ShowInformation($"属性情報のクリアが完了しました。\n\n処理件数: {successCount}件");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "属性情報のクリア中にエラーが発生しました");
                StatusMessage = $"属性情報のクリアエラー: {ex.Message}";
                _dialogService.ShowError($"属性情報のクリアエラー: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task LoadMangaFilesAsync()
        {
            try
            {
                // すべてのファイルを取得してキャッシュ
                _allMangaFiles = await _repository.GetAllAsync();
                
                // 選択されたフォルダに基づいてフィルタリング（ステータスメッセージは更新しない）
                FilterMangaFilesByFolder(false);
                
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "漫画ファイル一覧の読み込み中にエラーが発生しました");
                StatusMessage = $"ファイル読み込みエラー: {ex.Message}";
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

            FileCountMessage = $"総数: {totalCount}件 | タグ取得済: {aiProcessedCount}件";
        }

        private CancellationTokenSource? _searchCancellationTokenSource;
        
        partial void OnSearchTextChanged(string value)
        {
            // 前の検索をキャンセル
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();
            
            // デバウンス処理（300ms後に検索実行）
            _ = Task.Delay(300, _searchCancellationTokenSource.Token)
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
        
        partial void OnSelectedScanFolderChanged(string? value)
        {
            // 選択されたフォルダが変更されたら、ファイル一覧をフィルタリング
            FilterMangaFilesByFolder();
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
                    // 選択されたファイルがない場合は全ファイルを対象にするか確認
                    var allFilesResult = _dialogService.ShowConfirmation(
                        "ファイルが選択されていません。\n全ファイルのサムネイルを生成しますか？\n\n※この処理には時間がかかる場合があります。");
                    
                    if (allFilesResult)
                    {
                        selectedFiles = _allMangaFiles.ToList();
                        selectedCount = selectedFiles.Count;
                    }
                    else
                    {
                        StatusMessage = "サムネイル生成がキャンセルされました";
                        return;
                    }
                }

                // 生成モードを選択
                var generateMode = ShowThumbnailGenerationModeDialog(selectedCount);
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
                var results = await _thumbnailService.GenerateThumbnailsBatchAsync(selectedFiles, progress, default, skipExisting);

                // 結果を集計
                var actuallyProcessedCount = 0;
                var skippedCount = 0;
                
                foreach (var result in results)
                {
                    if (result.Success)
                    {
                        successCount++;
                        // 既存のサムネイルをスキップした場合とそうでない場合を区別
                        if (generateMode == ThumbnailGenerationMode.OnlyMissing && 
                            _thumbnailService.ThumbnailExists(result.ThumbnailPath))
                        {
                            skippedCount++;
                        }
                        else
                        {
                            actuallyProcessedCount++;
                        }
                        _logger.LogInformation("サムネイル生成成功: {FileName}", result.MangaFile?.FileName);
                    }
                    else
                    {
                        failureCount++;
                        actuallyProcessedCount++;
                        _logger.LogWarning("サムネイル生成失敗: {FileName} - {Error}", 
                            result.MangaFile?.FileName, result.ErrorMessage);
                    }
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
                
                // 処理完了音を再生
                _systemSoundService.PlayCompletionSound();

                // 結果を表示
                if (failureCount == 0)
                {
                    if (generateMode == ThumbnailGenerationMode.OnlyMissing && skippedCount > 0)
                    {
                        _dialogService.ShowInformation($"サムネイル生成が完了しました。\n\n処理: {actuallyProcessedCount}件\nスキップ: {skippedCount}件\n\n処理時間: {duration.TotalSeconds:F1}秒");
                    }
                    else
                    {
                        _dialogService.ShowInformation($"サムネイル生成が完了しました。\n\n成功: {successCount}件\n\n処理時間: {duration.TotalSeconds:F1}秒");
                    }
                }
                else
                {
                    if (generateMode == ThumbnailGenerationMode.OnlyMissing && skippedCount > 0)
                    {
                        _dialogService.ShowError($"サムネイル生成が完了しました。\n\n処理: {actuallyProcessedCount}件\nスキップ: {skippedCount}件\n失敗: {failureCount}件\n\n処理時間: {duration.TotalSeconds:F1}秒");
                    }
                    else
                    {
                        _dialogService.ShowError($"サムネイル生成が完了しました。\n\n成功: {successCount}件\n失敗: {failureCount}件\n\n処理時間: {duration.TotalSeconds:F1}秒");
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

                if (result.Success)
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
                _dialogService.ShowError($"サムネイル生成エラー: {ex.Message}");
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
                var result = _dialogService.ShowConfirmation("差分スキャンを実行します。\n（変更されたファイルのみ処理します）\n\n実行しますか？");
                if (!result)
                {
                    StatusMessage = "差分スキャンがキャンセルされました";
                    return;
                }

                IsProcessing = true;

                // 差分スキャンを実行
                var startTime = DateTime.Now;
                var progress = new Progress<ScanProgress>(p =>
                {
                    StatusMessage = $"{p.Status} ({p.CurrentFile}/{p.TotalFiles})";
                    if (p.TotalFiles > 0)
                    {
                        ProgressValue = (int)((double)p.CurrentFile / p.TotalFiles * 100);
                    }
                });

                var scanResult = await _fileScannerService.PerformIncrementalScanAsync(progress);
                var duration = DateTime.Now - startTime;

                // ファイル一覧を更新
                await LoadMangaFilesAsync();
                UpdateFileCountMessage();

                if (scanResult.Success)
                {
                    StatusMessage = $"差分スキャン完了: 追加={scanResult.FilesAdded}, 更新={scanResult.FilesUpdated}, 削除={scanResult.FilesRemoved} (処理時間: {duration.TotalSeconds:F1}秒)";
                    _logger.LogInformation("差分スキャンが完了しました: 追加={Added}, 更新={Updated}, 削除={Removed}", 
                        scanResult.FilesAdded, scanResult.FilesUpdated, scanResult.FilesRemoved);
                    
                    // 処理完了音を再生
                    _systemSoundService.PlayCompletionSound();
                    
                    _dialogService.ShowInformation($"差分スキャンが完了しました。\n\n追加: {scanResult.FilesAdded}件\n更新: {scanResult.FilesUpdated}件\n削除: {scanResult.FilesRemoved}件\n\n処理時間: {duration.TotalSeconds:F1}秒");
                }
                else
                {
                    StatusMessage = $"差分スキャン完了（エラー{scanResult.Errors.Count}件）";
                    _logger.LogWarning("差分スキャンでエラーが発生しました: {Errors}", scanResult.Errors);
                    _dialogService.ShowError($"差分スキャンが完了しましたが、エラーがありました:\n\n追加: {scanResult.FilesAdded}件\n更新: {scanResult.FilesUpdated}件\n削除: {scanResult.FilesRemoved}件\nエラー: {scanResult.Errors.Count}件\n\n最初の5件のエラー:\n{string.Join("\n", scanResult.Errors.Take(5))}" + 
                        (scanResult.Errors.Count > 5 ? $"\n...他{scanResult.Errors.Count - 5}件のエラー" : ""));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "差分スキャン中にエラーが発生しました");
                StatusMessage = $"差分スキャンエラー: {ex.Message}";
                _dialogService.ShowError($"差分スキャン中にエラーが発生しました: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                ProgressValue = 0;
            }
        }

        /// <summary>
        /// 孤立したサムネイルファイルのみを削除します（DBにひも付かないファイル）
        /// </summary>
        [RelayCommand]
        private async Task CleanupOrphanedThumbnailsAsync()
        {
            try
            {
                StatusMessage = "サムネイル掃除を開始しています...";
                _logger.LogInformation("孤立サムネイルクリーンアップを開始します");

                // 確認ダイアログを表示
                var result = _dialogService.ShowConfirmation("データベースにひも付かない孤立したサムネイルファイルを削除します。\n\n実行しますか？");
                if (!result)
                {
                    StatusMessage = "サムネイル掃除がキャンセルされました";
                    return;
                }

                IsProcessing = true;

                // 孤立ファイルのクリーンアップのみ実行
                StatusMessage = "孤立したサムネイルファイルを検索中...";
                var cleanupResult = await _thumbnailCleanupService.CleanupOrphanedThumbnailsAsync();
                
                if (cleanupResult.Success)
                {
                    StatusMessage = $"サムネイル掃除完了: {cleanupResult.DeletedCount}件削除";
                    _logger.LogInformation("孤立サムネイルクリーンアップ完了: {DeletedCount}件削除", cleanupResult.DeletedCount);
                    
                    // 処理完了音を再生
                    _systemSoundService.PlayCompletionSound();
                    
                    if (cleanupResult.DeletedCount > 0)
                    {
                        _dialogService.ShowInformation($"サムネイル掃除が完了しました。\n\n削除されたファイル: {cleanupResult.DeletedCount}件", "掃除完了");
                    }
                    else
                    {
                        _dialogService.ShowInformation("削除対象の孤立ファイルはありませんでした。", "掃除完了");
                    }
                }
                else
                {
                    StatusMessage = $"サムネイル掃除エラー: {cleanupResult.ErrorMessage}";
                    _logger.LogError("孤立サムネイルクリーンアップでエラーが発生しました: {Error}", cleanupResult.ErrorMessage);
                    _dialogService.ShowError($"サムネイル掃除中にエラーが発生しました: {cleanupResult.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル掃除中にエラーが発生しました");
                StatusMessage = $"サムネイル掃除エラー: {ex.Message}";
                _dialogService.ShowError($"サムネイル掃除中にエラーが発生しました: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// サムネイル状況の詳細診断を実行します
        /// </summary>
        [RelayCommand]
        private async Task DiagnoseThumbnailStatusAsync()
        {
            try
            {
                StatusMessage = "サムネイル状況を診断中...";
                _logger.LogInformation("サムネイル診断を開始します");

                IsProcessing = true;

                var diagnosticResult = await _thumbnailCleanupService.DiagnoseThumbnailStatusAsync();
                
                StatusMessage = "サムネイル診断完了";
                _dialogService.ShowInformation(diagnosticResult, "サムネイル診断結果");

                _logger.LogInformation("サムネイル診断が完了しました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル診断中にエラーが発生しました");
                StatusMessage = $"サムネイル診断エラー: {ex.Message}";
                _dialogService.ShowError($"サムネイル診断中にエラーが発生しました: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// アプリケーション起動時に古いサムネイルファイルのクリーンアップチェックを実行します
        /// </summary>
        private async Task PerformStartupThumbnailCleanup()
        {
            try
            {
                // 自動削除設定をチェック
                var autoCleanupEnabled = _config.GetSetting<bool>("ThumbnailAutoCleanupEnabled", true);
                
                if (!autoCleanupEnabled)
                {
                    
                    return;
                }
                
                // 設定から保持期間と件数閾値を取得
                var retentionDays = _config.GetSetting<int>("ThumbnailRetentionDays", 30);
                var maxFileCount = _config.GetSetting<int>("ThumbnailMaxFileCount", 1000);
                
                _logger.LogInformation("起動時サムネイルクリーンアップチェックを開始します（保持期間: {RetentionDays}日, 件数閾値: {MaxFileCount}件）", retentionDays, maxFileCount);
                
                // 件数条件付きで古いサムネイルファイルを削除
                var result = await _thumbnailCleanupService.CleanupOldThumbnailsIfExceedsCountAsync(retentionDays, maxFileCount);
                
                if (result.Success)
                {
                    if (result.DeletedCount > 0)
                    {
                        _logger.LogInformation("起動時サムネイルクリーンアップ完了: {DeletedCount}件削除", result.DeletedCount);
                    }
                    else
                    {
                        
                    }
                }
                else
                {
                    _logger.LogWarning("起動時サムネイルクリーンアップでエラー: {ErrorMessage}", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "起動時サムネイルクリーンアップ中にエラーが発生しました");
            }
        }

        private ThumbnailGenerationMode ShowThumbnailGenerationModeDialog(int fileCount)
        {
            var dialog = new ThumbnailModeSelectionWindow(fileCount)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            var result = dialog.ShowDialog();
            return result == true ? dialog.SelectedMode : ThumbnailGenerationMode.Cancel;
        }


    }
}
