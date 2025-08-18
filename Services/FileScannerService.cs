using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using Mangaanya.Utilities;
using Mangaanya.Common;
using System.IO;
using System.Text.RegularExpressions;

namespace Mangaanya.Services
{
    public class FileScannerService : IFileScannerService
    {
        private readonly ILogger<FileScannerService> _logger;
        private readonly IMangaRepository _repository;
        private readonly IConfigurationManager _config;
        private readonly string[] _supportedExtensions = FileUtilities.GetSupportedExtensions();
        
        // コンパイル済み正規表現でパフォーマンス向上
        private static readonly Regex FileNamePattern = new(
            @"\[一般コミック\]\s*\[([あ-んア-ンーA-Za-z]+)\]\s*\[([^×\]]+)(?:×([^\]]+))?\]\s*(.+?)(?:\s+第(\d+(?:-\d+)?)巻|\s+第0*(\d+(?:-\d+)?)巻)(?:\s*.*?)?(?:\.[^\.]+)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public FileScannerService(ILogger<FileScannerService> logger, IMangaRepository repository, IConfigurationManager config)
        {
            _logger = logger;
            _repository = repository;
            _config = config;
        }

        /// <summary>
        /// 増分スキャンを実行します（既存ファイルとの差分を検出）
        /// </summary>
        /// <param name="progress">進捗報告用のプログレス</param>
        /// <returns>スキャン結果</returns>
        public async Task<Result<ScanResult>> PerformIncrementalScanAsync(IProgress<ScanProgress> progress)
        {
            var startTime = DateTime.Now;
            var result = new ScanResult();

            try
            {
                var scanFolders = _config.GetSetting<List<string>>("ScanFolders", new List<string>());
                if (!scanFolders.Any())
                {
                    return Result<ScanResult>.Failure("スキャン対象フォルダが設定されていません");
                }

                var existingFiles = await _repository.GetAllAsync();
                var existingFilePaths = existingFiles.ToDictionary(f => f.FilePath, f => f);

                var currentFiles = new HashSet<string>();
                var processedFiles = 0;

                // 全フォルダのファイル数をカウント
                progress?.Report(new ScanProgress
                {
                    CurrentFile = 0,
                    TotalFiles = 0,
                    CurrentFileName = "",
                    Status = "ファイル数をカウント中..."
                });
                
                // 重複を排除してファイルリストを作成
                var allFiles = new HashSet<string>();
                
                foreach (var folder in scanFolders)
                {
                    if (!Directory.Exists(folder))
                    {
                        _logger.LogWarning("フォルダが見つかりません: {Folder}", folder);
                        continue;
                    }
                    
                    progress?.Report(new ScanProgress
                    {
                        CurrentFile = 0,
                        TotalFiles = 0,
                        CurrentFileName = Path.GetFileName(folder),
                        Status = $"フォルダをスキャン中: {folder}"
                    });
                    
                    var files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()));
                    
                    foreach (var file in files)
                    {
                        allFiles.Add(file);
                    }
                }
                
                // 総ファイル数を設定
                var totalFiles = allFiles.Count;
                _logger.LogInformation("スキャン対象ファイル数: {Count}件", totalFiles);
                
                // 各ファイルを処理
                foreach (var filePath in allFiles)
                {
                    processedFiles++;
                    progress?.Report(new ScanProgress
                    {
                        CurrentFile = processedFiles,
                        TotalFiles = totalFiles,
                        CurrentFileName = Path.GetFileName(filePath),
                        Status = "ファイルを処理中..."
                    });

                        currentFiles.Add(filePath);

                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            
                            if (existingFilePaths.TryGetValue(filePath, out var existingFile))
                            {
                                // 既存ファイルの更新チェック
                                if (existingFile.ModifiedDate != fileInfo.LastWriteTime)
                                {
                                    await UpdateExistingFile(existingFile, fileInfo);
                                    result.FilesUpdated++;
                                }
                            }
                            else
                            {
                                // 新規ファイルの追加
                                await AddNewFile(filePath, fileInfo);
                                result.FilesAdded++;
                            }
                        }
                        catch (Exception ex)
                        {
                            var fileProcessingEx = new Mangaanya.Exceptions.FileProcessingException(filePath, "ファイル処理中にエラーが発生しました", ex);
                            _logger.LogError(fileProcessingEx, "ファイル処理中にエラーが発生しました: {FilePath}", filePath);
                            // 個別ファイルのエラーは処理を続行
                        }
                    }

                // 削除されたファイルの検出
                var deletedFiles = existingFilePaths.Keys.Except(currentFiles).ToList();
                
                if (deletedFiles.Any())
                {
                    progress?.Report(new ScanProgress
                    {
                        CurrentFile = processedFiles,
                        TotalFiles = totalFiles,
                        CurrentFileName = "",
                        Status = "削除されたファイルを処理中..."
                    });
                    
                    foreach (var deletedFile in deletedFiles)
                    {
                        await _repository.DeleteAsync(existingFilePaths[deletedFile].Id);
                        result.FilesRemoved++;
                        
                        // 10件ごとに進捗を更新
                        if (result.FilesRemoved % 10 == 0)
                        {
                            progress?.Report(new ScanProgress
                            {
                                CurrentFile = processedFiles,
                                TotalFiles = totalFiles,
                                CurrentFileName = Path.GetFileName(deletedFile),
                                Status = $"削除されたファイルを処理中... ({result.FilesRemoved}件)"
                            });
                        }
                    }
                }

                result.FilesProcessed = processedFiles;
                result.Duration = DateTime.Now - startTime;
                
                // スキャン完了の進捗を報告
                progress?.Report(new ScanProgress
                {
                    CurrentFile = processedFiles,
                    TotalFiles = totalFiles,
                    CurrentFileName = "",
                    Status = $"スキャン完了: 処理={result.FilesProcessed}, 追加={result.FilesAdded}, 更新={result.FilesUpdated}, 削除={result.FilesRemoved}"
                });

                _logger.LogInformation("増分スキャン完了: 処理={0}, 追加={1}, 更新={2}, 削除={3}", 
                    result.FilesProcessed, result.FilesAdded, result.FilesUpdated, result.FilesRemoved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "増分スキャン中にエラーが発生しました");
                return Result<ScanResult>.Failure($"スキャンエラー: {ex.Message}", ex);
            }

            return Result<ScanResult>.Success(result);
        }

        /// <summary>
        /// 指定されたフォルダの完全スキャンを実行します
        /// </summary>
        /// <param name="folderPath">スキャン対象のフォルダパス</param>
        /// <param name="progress">進捗報告用のプログレス</param>
        /// <returns>スキャン結果</returns>
        public async Task<Result<ScanResult>> PerformFullScanAsync(string folderPath, IProgress<ScanProgress> progress)
        {
            var startTime = DateTime.Now;
            var result = new ScanResult();

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    return Result<ScanResult>.Failure($"フォルダが見つかりません: {folderPath}");
                }

                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();

                var totalFiles = files.Count;
                var processedFiles = 0;
                const int BATCH_SIZE = 200; // バッチサイズをさらに増加
                
                // ログ出力を削除してパフォーマンス向上

                // ファイルをバッチに分割
                var batches = files.Chunk(BATCH_SIZE);
                
                foreach (var batch in batches)
                {
                    var mangaFiles = new List<MangaFile>();
                    
                    // バッチ内のファイルを並列処理（制限なし）
                    var tasks = batch.Select(async filePath =>
                    {
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            return await CreateMangaFile(filePath, fileInfo);
                        }
                        catch (Exception ex)
                        {
                            var fileProcessingEx = new Mangaanya.Exceptions.FileProcessingException(filePath, "ファイル処理中にエラーが発生しました", ex);
                            _logger.LogError(fileProcessingEx, "ファイル処理中にエラーが発生しました: {FilePath}", filePath);
                            // 個別ファイルのエラーは処理を続行
                            return null;
                        }
                    });

                    var batchResults = await Task.WhenAll(tasks);
                    
                    // 成功したファイルのみを収集
                    foreach (var mangaFile in batchResults.Where(f => f != null))
                    {
                        mangaFiles.Add(mangaFile!);
                    }

                    // バッチでデータベースに挿入
                    if (mangaFiles.Any())
                    {
                        try
                        {
                            var insertedCount = await _repository.InsertBatchAsync(mangaFiles);
                            result.FilesAdded += insertedCount;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "バッチ挿入中にエラーが発生しました");
                            // バッチ挿入エラーは重大なエラーとして処理を中断
                            return Result<ScanResult>.Failure($"バッチ挿入エラー: {ex.Message}", ex);
                        }
                    }

                    processedFiles += batch.Count();
                    
                    // バッチ完了後に進捗報告
                    progress?.Report(new ScanProgress
                    {
                        CurrentFile = processedFiles,
                        TotalFiles = totalFiles,
                        CurrentFileName = $"バッチ {processedFiles / BATCH_SIZE + 1}",
                        Status = $"{processedFiles}/{totalFiles} 処理完了"
                    });
                }

                result.FilesProcessed = processedFiles;
                result.Duration = DateTime.Now - startTime;

                _logger.LogInformation("フルスキャン完了: 処理={0}, 追加={1}", result.FilesProcessed, result.FilesAdded);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フルスキャン中にエラーが発生しました");
                return Result<ScanResult>.Failure($"スキャンエラー: {ex.Message}", ex);
            }

            return Result<ScanResult>.Success(result);
        }



        /// <summary>
        /// ファイル名を解析してマンガ情報を抽出します
        /// </summary>
        /// <param name="fileName">解析対象のファイル名</param>
        /// <returns>解析されたファイル情報</returns>
        public Task<Result<ParsedFileInfo>> ParseFileNameAsync(string fileName)
        {
            var result = new ParsedFileInfo();

            try
            {
                // 基本パターン: [一般コミック] [よみがな] [原作者×作画者] タイトル 第XX巻 [任意の文字列]
                // よみがなは日本語文字またはアルファベット1文字を許可
                // 第xx-yy巻の形式にも対応
                var match = FileNamePattern.Match(fileName);

                if (match.Success)
                {
                    
                    // グループ1: 作者読み（必須）
                    result.AuthorReading = match.Groups[1].Value.Trim();
                    
                    // グループ2: 原作者（必須）
                    result.OriginalAuthor = match.Groups[2].Value.Trim();
                    
                    // グループ3: 作画者（オプション）
                    if (match.Groups[3].Success && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
                    {
                        result.Artist = match.Groups[3].Value.Trim();
                    }
                    
                    // グループ4: タイトル（必須）
                    result.Title = match.Groups[4].Value.Trim();
                    
                    // グループ5または6: 巻数（オプション）
                    // 第xx-yy巻の形式にも対応
                    if (match.Groups[5].Success)
                    {
                        var volumeText = match.Groups[5].Value;
                        if (volumeText.Contains("-"))
                        {
                            // 第xx-yy巻の場合、文字列として保存
                            result.VolumeNumber = null; // 数値としては保存しない
                            result.VolumeString = volumeText; // 文字列として保存
                        }
                        else if (int.TryParse(volumeText, out var volume))
                        {
                            result.VolumeNumber = volume;
                        }
                    }
                    else if (match.Groups[6].Success)
                    {
                        var volumeText = match.Groups[6].Value;
                        if (volumeText.Contains("-"))
                        {
                            // 第xx-yy巻の場合、文字列として保存
                            result.VolumeNumber = null; // 数値としては保存しない
                            result.VolumeString = volumeText; // 文字列として保存
                        }
                        else if (int.TryParse(volumeText, out var volume2))
                        {
                            // 0埋めされた巻数（例: 01, 02など）
                            result.VolumeNumber = volume2;
                        }
                    }
                }
                else
                {
                    // フォールバック1: より柔軟なパターン（よみがなを任意の文字に）
                    // 第xx-yy巻の形式にも対応
                    var flexiblePattern = @"\[一般コミック\]\s*\[([^\]]+)\]\s*\[([^×\]]+)(?:×([^\]]+))?\]\s*(.+?)(?:\s+第(\d+(?:-\d+)?)巻|\s+第0*(\d+(?:-\d+)?)巻)(?:\s*.*?)?(?:\.[^\.]+)?$";
                    var flexibleMatch = Regex.Match(fileName, flexiblePattern);
                    
                    if (flexibleMatch.Success)
                    {
                        // グループ1: 作者読み
                        result.AuthorReading = flexibleMatch.Groups[1].Value.Trim();
                        
                        result.OriginalAuthor = flexibleMatch.Groups[2].Value.Trim();
                        
                        if (flexibleMatch.Groups[3].Success && !string.IsNullOrWhiteSpace(flexibleMatch.Groups[3].Value))
                        {
                            result.Artist = flexibleMatch.Groups[3].Value.Trim();
                        }
                        
                        result.Title = flexibleMatch.Groups[4].Value.Trim();
                        
                        // 第xx-yy巻の形式にも対応
                        if (flexibleMatch.Groups[5].Success)
                        {
                            var volumeText = flexibleMatch.Groups[5].Value;
                            if (volumeText.Contains("-"))
                            {
                                // 第xx-yy巻の場合、文字列として保存
                                result.VolumeNumber = null; // 数値としては保存しない
                                result.VolumeString = volumeText; // 文字列として保存
                            }
                            else if (int.TryParse(volumeText, out var volume))
                            {
                                result.VolumeNumber = volume;
                            }
                        }
                        else if (flexibleMatch.Groups[6].Success)
                        {
                            var volumeText = flexibleMatch.Groups[6].Value;
                            if (volumeText.Contains("-"))
                            {
                                // 第xx-yy巻の場合、文字列として保存
                                result.VolumeNumber = null; // 数値としては保存しない
                                result.VolumeString = volumeText; // 文字列として保存
                            }
                            else if (int.TryParse(volumeText, out var volume2))
                            {
                                // 0埋めされた巻数（例: 01, 02など）
                                result.VolumeNumber = volume2;
                            }
                        }
                    }
                    else
                    {
                        // フォールバック2: タイトル内に巻数がある場合の抽出
                        var titleWithVolumePattern = @"\[一般コミック\]\s*\[([^\]]+)\]\s*\[([^×\]]+)(?:×([^\]]+))?\]\s*(.+?)(?:\.[^\.]+)?$";
                        var titleWithVolumeMatch = Regex.Match(fileName, titleWithVolumePattern);
                        
                        if (titleWithVolumeMatch.Success)
                        {
                            // グループ1: 作者読み
                            result.AuthorReading = titleWithVolumeMatch.Groups[1].Value.Trim();
                            
                            result.OriginalAuthor = titleWithVolumeMatch.Groups[2].Value.Trim();
                            
                            if (titleWithVolumeMatch.Groups[3].Success && !string.IsNullOrWhiteSpace(titleWithVolumeMatch.Groups[3].Value))
                            {
                                result.Artist = titleWithVolumeMatch.Groups[3].Value.Trim();
                            }
                            
                            var title = titleWithVolumeMatch.Groups[4].Value.Trim();
                            
                            // タイトル内から巻数を抽出（0埋めされた巻数と第xx-yy巻にも対応）
                            var volumeMatch = Regex.Match(title, @"第(0*\d+(?:-\d+)?)巻");
                            if (volumeMatch.Success)
                            {
                                var volumeText = volumeMatch.Groups[1].Value;
                                if (volumeText.Contains("-"))
                                {
                                    // 第xx-yy巻の場合、文字列として保存
                                    result.VolumeNumber = null;
                                    result.VolumeString = volumeText;
                                    // 巻数表記を除去してタイトルをクリーンに
                                    result.Title = Regex.Replace(title, @"\s*第0*\d+(?:-\d+)?巻.*?$", "").Trim();
                                }
                                else if (int.TryParse(volumeText, out var volume))
                                {
                                    result.VolumeNumber = volume;
                                    // 巻数表記を除去してタイトルをクリーンに
                                    result.Title = Regex.Replace(title, @"\s*第0*\d+巻.*?$", "").Trim();
                                }
                                else
                                {
                                    result.Title = title;
                                }
                            }
                            else
                            {
                                result.Title = title;
                            }
                        }
                        else
                        {
                            // フォールバック3: 基本的なタイトル抽出
                            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                            result.Title = fileNameWithoutExtension;
                        }
                    }
                }

                _logger.LogDebug("ファイル名解析結果: {FileName} -> タイトル={Title}, 原作者={Author}, 作画者={Artist}, 巻数={Volume}", 
                    fileName, result.Title, result.OriginalAuthor, result.Artist, result.VolumeNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル名解析中にエラーが発生しました: {FileName}", fileName);
                return Task.FromResult(Result<ParsedFileInfo>.Failure($"ファイル名解析エラー: {ex.Message}", ex));
            }

            return Task.FromResult(Result<ParsedFileInfo>.Success(result));
        }

        private async Task<MangaFile> CreateMangaFile(string filePath, FileInfo fileInfo)
        {
            var mangaFile = new MangaFile
            {
                FilePath = filePath,
                FileName = Path.GetFileNameWithoutExtension(fileInfo.Name),
                FileSize = fileInfo.Length,
                CreatedDate = fileInfo.CreationTime,
                ModifiedDate = fileInfo.LastWriteTime,
                FileType = Path.GetExtension(filePath).ToUpper().TrimStart('.'),
                IsCorrupted = false // 整合性チェック不要
            };

            // ファイル名解析
            var parsedResult = await ParseFileNameAsync(fileInfo.Name);
            if (parsedResult.IsSuccess)
            {
                var parsedInfo = parsedResult.Value!;
                mangaFile.Title = parsedInfo.Title;
                mangaFile.OriginalAuthor = parsedInfo.OriginalAuthor;
                mangaFile.Artist = parsedInfo.Artist;
                mangaFile.AuthorReading = parsedInfo.AuthorReading;
                mangaFile.VolumeNumber = parsedInfo.VolumeNumber;
                mangaFile.VolumeString = parsedInfo.VolumeString;
            }
            else
            {
                // 解析に失敗した場合はファイル名をそのままタイトルとして使用
                mangaFile.Title = Path.GetFileNameWithoutExtension(fileInfo.Name);
            }

            return mangaFile;
        }

        private async Task AddNewFile(string filePath, FileInfo fileInfo)
        {
            
            
            var mangaFile = await CreateMangaFile(filePath, fileInfo);

            
            await _repository.InsertAsync(mangaFile);
            
        }

        private async Task UpdateExistingFile(MangaFile existingFile, FileInfo fileInfo)
        {
            existingFile.FileSize = fileInfo.Length;
            existingFile.ModifiedDate = fileInfo.LastWriteTime;
            existingFile.IsCorrupted = false; // 整合性チェック不要

            await _repository.UpdateAsync(existingFile);
        }
    }
}
