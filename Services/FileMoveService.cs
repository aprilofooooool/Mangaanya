using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using Mangaanya.Services.Interfaces;
using Mangaanya.Views;
using System.IO;

namespace Mangaanya.Services
{
    /// <summary>
    /// ファイル移動サービスの実装クラス
    /// </summary>
    public class FileMoveService : IFileMoveService
    {
        private readonly ILogger<FileMoveService> _logger;
        private readonly IMangaRepository _mangaRepository;
        private readonly IDialogService _dialogService;
        private readonly IThumbnailService _thumbnailService;
        
        // バッチ処理用の競合解決設定
        private ConflictResolution? _batchFileExistsResolution;

        public FileMoveService(
            ILogger<FileMoveService> logger,
            IMangaRepository mangaRepository,
            IDialogService dialogService,
            IThumbnailService thumbnailService)
        {
            _logger = logger;
            _mangaRepository = mangaRepository;
            _dialogService = dialogService;
            _thumbnailService = thumbnailService;
        }



        /// <summary>
        /// 指定されたファイルを移動先フォルダに移動する
        /// </summary>
        public async Task<FileMoveResult> MoveFilesAsync(
            IEnumerable<MangaFile> sourceFiles, 
            string destinationFolder, 
            IProgress<FileMoveProgress>? progress = null)
        {
            var result = new FileMoveResult();
            var fileList = sourceFiles.ToList();
            
            if (!fileList.Any())
            {
                _logger.LogWarning("移動対象のファイルが指定されていません");
                return result;
            }

            _logger.LogInformation("ファイル移動処理を開始します。対象ファイル数: {Count}, 移動先: {Destination}", 
                fileList.Count, destinationFolder);

            // バッチ処理用の競合解決設定をリセット
            _batchFileExistsResolution = null;

            // 成功したファイル移動の記録（データベース同期用）
            var successfulMoves = new List<(MangaFile OriginalFile, string NewPath, MangaFile? OverwrittenFile)>();

            try
            {
                // 移動先フォルダの存在確認
                if (!Directory.Exists(destinationFolder))
                {
                    result.Errors.Add($"移動先フォルダが存在しません: {destinationFolder}");
                    result.ErrorCount = fileList.Count;
                    return result;
                }

                // 事前に競合を検出し、複数ファイルの場合はまとめて処理
                await PreProcessConflictsAsync(fileList, destinationFolder);

                // 各ファイルを順次処理
                for (int i = 0; i < fileList.Count; i++)
                {
                    var file = fileList[i];
                    var progressInfo = new FileMoveProgress
                    {
                        CurrentFile = i + 1,
                        TotalFiles = fileList.Count,
                        CurrentFileName = file.FileName,
                        Operation = FileMoveOperation.Validating
                    };
                    progress?.Report(progressInfo);

                    try
                    {
                        var moveResult = await ProcessSingleFileAsync(file, destinationFolder, result, progress, progressInfo);
                        if (!string.IsNullOrEmpty(moveResult.newPath))
                        {
                            successfulMoves.Add((file, moveResult.newPath, moveResult.overwrittenFile));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // キャンセルの場合は処理を中断
                        _logger.LogInformation("ファイル移動処理がキャンセルされました");
                        result.IsCancelled = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ファイル移動中にエラーが発生しました: {FilePath}", file.FilePath);
                        result.Errors.Add($"{file.FileName}: {ex.Message}");
                        result.ErrorCount++;
                    }
                }

                // データベース同期処理
                if (successfulMoves.Any())
                {
                    await SynchronizeDatabaseAsync(successfulMoves, progress);
                }

                _logger.LogInformation("ファイル移動処理が完了しました。{Summary}", result.GetSummary());
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル移動処理中に予期しないエラーが発生しました");
                result.Errors.Add($"予期しないエラー: {ex.Message}");
                result.ErrorCount = fileList.Count;
                return result;
            }
        }

        /// <summary>
        /// ファイル移動操作の妥当性を検証する
        /// </summary>
        public async Task<bool> ValidateMoveOperationAsync(
            IEnumerable<MangaFile> sourceFiles, 
            string destinationFolder)
        {
            try
            {
                // 移動先フォルダの存在確認
                if (!Directory.Exists(destinationFolder))
                {
                    _logger.LogWarning("移動先フォルダが存在しません: {Destination}", destinationFolder);
                    return false;
                }

                // 各ファイルの存在確認
                foreach (var file in sourceFiles)
                {
                    if (!File.Exists(file.FilePath))
                    {
                        _logger.LogWarning("移動対象ファイルが存在しません: {FilePath}", file.FilePath);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "移動操作の妥当性検証中にエラーが発生しました");
                return false;
            }
        }

        /// <summary>
        /// 移動先フォルダでの競合を検出する
        /// </summary>
        public async Task<FileMoveConflictType> DetectConflictAsync(
            MangaFile sourceFile, 
            string destinationFolder)
        {
            try
            {
                // パスの正規化
                var sourceFolder = Path.GetDirectoryName(sourceFile.FilePath);
                if (string.IsNullOrEmpty(sourceFolder))
                {
                    _logger.LogWarning("移動元ファイルのフォルダパスが取得できません: {FilePath}", sourceFile.FilePath);
                    return FileMoveConflictType.None;
                }

                // 移動先に同名ファイルが存在するかチェック（同一フォルダも含む）
                var destinationFilePath = Path.Combine(destinationFolder, sourceFile.FullFileName);
                if (File.Exists(destinationFilePath))
                {
                    // 同じファイルかどうかをチェック（同一ファイルの場合は競合ではない）
                    var sourceFullPath = Path.GetFullPath(sourceFile.FilePath);
                    var destinationFullPath = Path.GetFullPath(destinationFilePath);
                    
                    if (string.Equals(sourceFullPath, destinationFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // 同一ファイルの場合は競合なし
                        return FileMoveConflictType.None;
                    }

                    _logger.LogDebug("移動先に同名ファイルが存在: {DestinationPath}", destinationFilePath);
                    return FileMoveConflictType.FileExists;
                }

                return FileMoveConflictType.None;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "競合検出中にエラーが発生しました: {FilePath}", sourceFile.FilePath);
                return FileMoveConflictType.None;
            }
        }

        /// <summary>
        /// 競合解決方法を取得する（ユーザーに確認ダイアログを表示）
        /// </summary>
        public async Task<ConflictResolution> ResolveConflictAsync(
            FileMoveConflictType conflictType,
            MangaFile sourceFile,
            string destinationFolder)
        {
            try
            {
                string message = conflictType switch
                {
                    FileMoveConflictType.SameFolder => 
                        $"ファイル '{sourceFile.FileName}' は既に同じフォルダにあります。\n\n" +
                        $"移動元: {Path.GetDirectoryName(sourceFile.FilePath)}\n" +
                        $"移動先: {destinationFolder}\n\n" +
                        "このファイルをスキップしますか？",
                    FileMoveConflictType.FileExists => 
                        $"移動先に同名のファイル '{sourceFile.FileName}' が既に存在します。\n\n" +
                        $"移動先: {destinationFolder}\n\n" +
                        "既存のファイルを上書きしますか？",
                    _ => "予期しない競合が発生しました。"
                };

                // カスタムダイアログで選択肢を表示
                var dialogResult = await ShowConflictResolutionDialogAsync(message, conflictType);
                
                _logger.LogInformation("競合解決結果: {ConflictType} -> {Resolution} (ファイル: {FileName})", 
                    conflictType, dialogResult, sourceFile.FileName);
                
                return dialogResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "競合解決ダイアログ表示中にエラーが発生しました");
                return ConflictResolution.Cancel;
            }
        }

        /// <summary>
        /// 複数ファイル用の競合解決方法を取得する
        /// </summary>
        public async Task<ConflictResolution> ResolveConflictForMultipleFilesAsync(
            FileMoveConflictType conflictType,
            int conflictFileCount,
            string destinationFolder)
        {
            try
            {
                string message = conflictType switch
                {
                    FileMoveConflictType.SameFolder => 
                        $"{conflictFileCount}件のファイルが既に同じフォルダにあります。\n\n" +
                        $"移動先: {destinationFolder}\n\n" +
                        "これらのファイルをスキップしますか？",
                    FileMoveConflictType.FileExists => 
                        $"移動先に同名のファイルが{conflictFileCount}件存在します。\n\n" +
                        $"移動先: {destinationFolder}\n\n" +
                        "既存のファイルを上書きしますか？",
                    _ => "予期しない競合が発生しました。"
                };

                // カスタムダイアログで選択肢を表示
                var dialogResult = await ShowConflictResolutionDialogAsync(message, conflictType);
                
                _logger.LogInformation("複数ファイル競合解決結果: {ConflictType} -> {Resolution} ({Count}件)", 
                    conflictType, dialogResult, conflictFileCount);
                
                return dialogResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "複数ファイル競合解決ダイアログ表示中にエラーが発生しました");
                return ConflictResolution.Cancel;
            }
        }

        /// <summary>
        /// バッチ処理を考慮した競合解決方法を取得する
        /// </summary>
        private async Task<ConflictResolution> GetConflictResolutionAsync(
            FileMoveConflictType conflictType,
            MangaFile sourceFile,
            string destinationFolder)
        {
            // バッチ処理用の設定が既にある場合はそれを使用
            if (conflictType == FileMoveConflictType.FileExists && _batchFileExistsResolution.HasValue)
            {
                _logger.LogDebug("バッチ処理により同名ファイル競合を自動処理: {FilePath} -> {Resolution}", 
                    sourceFile.FilePath, _batchFileExistsResolution.Value);
                return _batchFileExistsResolution.Value;
            }

            // ユーザーに確認
            var resolution = await ResolveConflictAsync(conflictType, sourceFile, destinationFolder);

            // バッチ処理用の設定として保存（スキップまたは上書きの場合のみ）
            if (resolution == ConflictResolution.Skip || resolution == ConflictResolution.Overwrite)
            {
                if (conflictType == FileMoveConflictType.FileExists)
                {
                    _batchFileExistsResolution = resolution;
                }
            }

            return resolution;
        }

        /// <summary>
        /// 単一ファイルの移動処理
        /// </summary>
        private async Task<(string? newPath, MangaFile? overwrittenFile)> ProcessSingleFileAsync(
            MangaFile file, 
            string destinationFolder, 
            FileMoveResult result, 
            IProgress<FileMoveProgress>? progress,
            FileMoveProgress progressInfo)
        {
            string? originalFilePath = null;
            string? destinationFilePath = null;
            bool fileMovedSuccessfully = false;

            try
            {
                // ファイルの存在確認
                if (!File.Exists(file.FilePath))
                {
                    throw new FileNotFoundException($"移動対象ファイルが見つかりません: {file.FilePath}");
                }

                originalFilePath = file.FilePath;
                destinationFilePath = Path.Combine(destinationFolder, file.FullFileName);

                // 移動先の既存ファイル情報を取得（上書きされる場合の記録用）
                MangaFile? overwrittenFile = null;
                if (File.Exists(destinationFilePath))
                {
                    try
                    {
                        // 移動先の既存ファイルのレコードを直接検索
                        // SearchCriteriaは部分一致検索なので、GetAllAsyncを使用して完全一致を探す
                        var allFiles = await _mangaRepository.GetAllAsync();
                        overwrittenFile = allFiles.FirstOrDefault(f => 
                            string.Equals(f.FilePath, destinationFilePath, StringComparison.OrdinalIgnoreCase));
                        
                        if (overwrittenFile != null)
                        {
                            _logger.LogInformation("移動先の既存ファイルが上書きされます: {FilePath} (ID: {Id})", 
                                destinationFilePath, overwrittenFile.Id);
                        }
                        else
                        {
                            _logger.LogWarning("移動先の既存ファイル情報が見つかりませんでした: {FilePath}", destinationFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "移動先の既存ファイル情報の取得に失敗しました: {FilePath}", destinationFilePath);
                    }
                }

                // 競合検出
                var conflictType = await DetectConflictAsync(file, destinationFolder);
                _logger.LogDebug("競合検出結果: {FilePath} -> {ConflictType}", file.FilePath, conflictType);
                
                if (conflictType != FileMoveConflictType.None)
                {
                    var resolution = await GetConflictResolutionAsync(conflictType, file, destinationFolder);
                    
                    switch (resolution)
                    {
                        case ConflictResolution.Skip:
                            result.SkippedCount++;
                            _logger.LogInformation("ファイルをスキップしました: {FilePath}", file.FilePath);
                            return (null, null);
                        case ConflictResolution.Cancel:
                            throw new OperationCanceledException("ユーザーによって操作がキャンセルされました");
                        case ConflictResolution.Overwrite:
                            // 上書きで続行
                            break;
                    }
                }

                // ファイル移動実行
                progressInfo.Operation = FileMoveOperation.Moving;
                progress?.Report(progressInfo);

                // ファイルアクセス権限の確認
                await ValidateFileAccessAsync(originalFilePath, destinationFilePath);

                // 実際のファイル移動
                await MoveFileWithRetryAsync(originalFilePath, destinationFilePath);
                fileMovedSuccessfully = true;

                // サムネイルデータはデータベース内にあるため、ファイル移動時に自動的に保持される
                // 上書きされるファイルのサムネイルデータも、データベース更新時に自動的に処理される

                result.SuccessCount++;
                _logger.LogInformation("ファイル移動が完了しました: {OldPath} -> {NewPath}", originalFilePath, destinationFilePath);
                
                return (destinationFilePath, overwrittenFile);
            }
            catch (OperationCanceledException)
            {
                // キャンセルの場合は再スロー
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル移動処理中にエラーが発生しました: {FilePath}", file.FilePath);
                
                // ファイル移動が成功していた場合はロールバック
                if (fileMovedSuccessfully && !string.IsNullOrEmpty(originalFilePath) && !string.IsNullOrEmpty(destinationFilePath))
                {
                    await RollbackFileMove(originalFilePath, destinationFilePath);
                }

                result.Errors.Add($"{file.FileName}: {ex.Message}");
                result.ErrorCount++;
                return (null, null);
            }
        }

        /// <summary>
        /// ファイルアクセス権限の検証
        /// </summary>
        private async Task ValidateFileAccessAsync(string sourcePath, string destinationPath)
        {
            try
            {
                // 移動元ファイルの読み取り権限確認
                using (var sourceStream = File.OpenRead(sourcePath))
                {
                    // ファイルが読み取り可能であることを確認
                }

                // 移動先ディレクトリの書き込み権限確認
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir))
                {
                    var testFile = Path.Combine(destinationDir, $"test_{Guid.NewGuid()}.tmp");
                    try
                    {
                        await File.WriteAllTextAsync(testFile, "test");
                        File.Delete(testFile);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        throw new UnauthorizedAccessException($"移動先ディレクトリへの書き込み権限がありません: {destinationDir}");
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"ファイルアクセス権限の検証に失敗しました: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// リトライ機能付きファイル移動
        /// </summary>
        private async Task MoveFileWithRetryAsync(string sourcePath, string destinationPath, int maxRetries = 3)
        {
            var retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    // ファイル移動実行
                    File.Move(sourcePath, destinationPath, true);
                    return;
                }
                catch (IOException ex) when (retryCount < maxRetries - 1)
                {
                    // I/Oエラーの場合はリトライ
                    retryCount++;
                    _logger.LogWarning("ファイル移動でI/Oエラーが発生しました。リトライします ({Retry}/{MaxRetries}): {Error}", 
                        retryCount, maxRetries, ex.Message);
                    
                    // 短時間待機してリトライ
                    await Task.Delay(1000 * retryCount);
                }
                catch (Exception)
                {
                    // その他のエラーは即座に再スロー
                    throw;
                }
            }
            
            // 最大リトライ回数に達した場合
            throw new IOException($"ファイル移動に{maxRetries}回失敗しました: {sourcePath} -> {destinationPath}");
        }



        /// <summary>
        /// ファイル移動のロールバック
        /// </summary>
        private async Task RollbackFileMove(string originalPath, string destinationPath)
        {
            try
            {
                if (File.Exists(destinationPath) && !File.Exists(originalPath))
                {
                    _logger.LogInformation("ファイル移動をロールバックします: {Destination} -> {Original}", destinationPath, originalPath);
                    File.Move(destinationPath, originalPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル移動のロールバックに失敗しました: {Destination} -> {Original}", destinationPath, originalPath);
            }
        }

        /// <summary>
        /// データベース同期処理（バッチ更新）
        /// サムネイルデータはデータベース内にあるため、ファイルパス更新時に自動的に保持される
        /// </summary>
        private async Task SynchronizeDatabaseAsync(
            List<(MangaFile OriginalFile, string NewPath, MangaFile? OverwrittenFile)> successfulMoves,
            IProgress<FileMoveProgress>? progress)
        {
            if (!successfulMoves.Any()) return;

            try
            {
                _logger.LogInformation("データベース同期処理を開始します: {Count}件", successfulMoves.Count);

                // 進捗報告
                var progressInfo = new FileMoveProgress
                {
                    CurrentFile = successfulMoves.Count,
                    TotalFiles = successfulMoves.Count,
                    CurrentFileName = "データベース同期中...",
                    Operation = FileMoveOperation.UpdatingDatabase
                };
                progress?.Report(progressInfo);

                // バッチ更新用のデータを準備
                var filePathUpdates = successfulMoves.Select(move => 
                    (move.OriginalFile.Id, move.NewPath)).ToList();

                // バッチでデータベースを更新（トランザクション処理は内部で実行される）
                // サムネイルデータ（thumbnail_data）は自動的に保持される
                await _mangaRepository.UpdateFilePathsBatchAsync(filePathUpdates);

                _logger.LogInformation("データベース同期処理が完了しました: {Count}件（サムネイルデータも自動的に保持されました）", successfulMoves.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "データベース同期処理中にエラーが発生しました");
                
                // データベース更新に失敗した場合、ファイル移動をロールバック
                await RollbackSuccessfulMoves(successfulMoves);
                throw new InvalidOperationException($"データベース同期に失敗しました。ファイル移動をロールバックしました: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 成功したファイル移動のロールバック
        /// </summary>
        private async Task RollbackSuccessfulMoves(List<(MangaFile OriginalFile, string NewPath, MangaFile? OverwrittenFile)> successfulMoves)
        {
            _logger.LogWarning("データベース更新失敗のため、ファイル移動をロールバックします: {Count}件", successfulMoves.Count);

            var rollbackTasks = successfulMoves.Select(async move =>
            {
                try
                {
                    if (File.Exists(move.NewPath) && !File.Exists(move.OriginalFile.FilePath))
                    {
                        File.Move(move.NewPath, move.OriginalFile.FilePath);
                        _logger.LogDebug("ファイルをロールバックしました: {NewPath} -> {OriginalPath}", 
                            move.NewPath, move.OriginalFile.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ファイルロールバックに失敗しました: {NewPath} -> {OriginalPath}", 
                        move.NewPath, move.OriginalFile.FilePath);
                }
            });

            await Task.WhenAll(rollbackTasks);
        }

        /// <summary>
        /// 事前に競合を検出し、複数ファイルの場合はまとめて処理する
        /// </summary>
        private async Task PreProcessConflictsAsync(List<MangaFile> fileList, string destinationFolder)
        {
            try
            {
                // 各競合タイプごとにファイルをグループ化
                var fileExistsFiles = new List<MangaFile>();

                foreach (var file in fileList)
                {
                    var conflictType = await DetectConflictAsync(file, destinationFolder);
                    if (conflictType == FileMoveConflictType.FileExists)
                    {
                        fileExistsFiles.Add(file);
                    }
                }

                // 同名ファイル存在の処理
                if (fileExistsFiles.Count > 1)
                {
                    var resolution = await ResolveConflictForMultipleFilesAsync(
                        FileMoveConflictType.FileExists, fileExistsFiles.Count, destinationFolder);
                    _batchFileExistsResolution = resolution;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "事前競合検出中にエラーが発生しました");
            }
        }

        /// <summary>
        /// 競合解決ダイアログを表示
        /// </summary>
        private async Task<ConflictResolution> ShowConflictResolutionDialogAsync(string message, FileMoveConflictType conflictType)
        {
            try
            {
                string title = conflictType switch
                {
                    FileMoveConflictType.FileExists => "ファイル競合の確認",
                    _ => "ファイル移動の競合"
                };

                // DialogServiceを使用して統一されたダイアログを表示
                return await _dialogService.ShowConflictResolutionDialogAsync(title, message, conflictType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "競合解決ダイアログの表示中にエラーが発生しました");
                return ConflictResolution.Cancel;
            }
        }
    }
}