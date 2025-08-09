using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading.Tasks;

namespace Mangaanya.Services
{
    public class FileScannerService : IFileScannerService
    {
        private readonly ILogger<FileScannerService> _logger;
        private readonly IMangaRepository _repository;
        private readonly IConfigurationManager _config;
        private readonly string[] _supportedExtensions = { ".zip", ".rar" };

        private static readonly Regex FileNamePattern = new(
            @"\[一般コミック\]\s*\[([あ-んア-ンーA-Za-z]+)\]\s*\[([^×\]]+)(?:×([^\]]+))?\]\s*(.+?)(?:\s+第(\d+(?:-\d+)?)巻|\s+第0*(\d+(?:-\d+)?)巻)(?:\s*.*?)?(?:\.[^\.]+)?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public FileScannerService(ILogger<FileScannerService> logger, IMangaRepository repository, IConfigurationManager config)
        {
            _logger = logger;
            _repository = repository;
            _config = config;
        }

        public async Task<ScanResult> PerformIncrementalScanAsync(IProgress<ScanProgress> progress)
        {
            var startTime = DateTime.Now;
            var result = new ScanResult { Success = true };

            try
            {
                var scanFolders = _config.GetSetting<List<string>>("ScanFolders", new List<string>());
                if (!scanFolders.Any())
                {
                    result.Success = false;
                    result.Errors.Add("スキャン対象フォルダが設定されていません");
                    return result;
                }

                var existingFiles = await _repository.GetAllAsync();
                var existingFilePaths = existingFiles.ToDictionary(f => f.FilePath, f => f);

                var currentFiles = new HashSet<string>();
                var allFiles = new HashSet<string>();

                foreach (var folder in scanFolders)
                {
                    if (!Directory.Exists(folder)) continue;
                    var files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower()));
                    foreach (var file in files) allFiles.Add(file);
                }

                var totalFiles = allFiles.Count;
                var processedFiles = 0;

                foreach (var filePath in allFiles)
                {
                    processedFiles++;
                    progress?.Report(new ScanProgress { CurrentFile = processedFiles, TotalFiles = totalFiles, CurrentFileName = Path.GetFileName(filePath), Status = "ファイルを処理中..." });
                    currentFiles.Add(filePath);

                    try
                    {
                        var fileInfo = new FileInfo(filePath);

                        if (existingFilePaths.TryGetValue(filePath, out var existingFile))
                        {
                            if (existingFile.ModifiedDate != fileInfo.LastWriteTime)
                            {
                                await UpdateExistingFile(existingFile, fileInfo);
                                result.FilesUpdated++;
                            }
                            // --- Thumbnail Migration Logic ---
                            await MigrateThumbnailIfNeeded(existingFile);
                        }
                        else
                        {
                            await AddNewFile(filePath, fileInfo);
                            result.FilesAdded++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ファイル処理中にエラーが発生しました: {FilePath}", filePath);
                        result.Errors.Add($"ファイル処理エラー: {Path.GetFileName(filePath)} - {ex.Message}");
                    }
                }

                var deletedFiles = existingFilePaths.Keys.Except(currentFiles).ToList();
                if (deletedFiles.Any())
                {
                    foreach (var deletedFile in deletedFiles)
                    {
                        var fileToDelete = existingFilePaths[deletedFile];
                        if (!string.IsNullOrEmpty(fileToDelete.ThumbnailPath) && File.Exists(fileToDelete.ThumbnailPath))
                        {
                            try
                            {
                                File.Delete(fileToDelete.ThumbnailPath);
                            }
                            catch(Exception ex)
                            {
                                _logger.LogWarning(ex, "スキャン中のサムネイル削除に失敗: {Path}", fileToDelete.ThumbnailPath);
                            }
                        }
                        await _repository.DeleteAsync(fileToDelete.Id);
                        result.FilesRemoved++;
                    }
                }

                result.FilesProcessed = processedFiles;
                result.Duration = DateTime.Now - startTime;
                progress?.Report(new ScanProgress { CurrentFile = processedFiles, TotalFiles = totalFiles, Status = $"スキャン完了" });
                _logger.LogInformation("増分スキャン完了: 処理={0}, 追加={1}, 更新={2}, 削除={3}", result.FilesProcessed, result.FilesAdded, result.FilesUpdated, result.FilesRemoved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "増分スキャン中にエラーが発生しました");
                result.Success = false;
                result.Errors.Add($"スキャンエラー: {ex.Message}");
            }

            return result;
        }

        private async Task MigrateThumbnailIfNeeded(MangaFile existingFile)
        {
            if (existingFile.Id > 0 && !string.IsNullOrEmpty(existingFile.ThumbnailPath) && File.Exists(existingFile.ThumbnailPath))
            {
                var thumbFileName = Path.GetFileNameWithoutExtension(existingFile.ThumbnailPath);
                if (thumbFileName != existingFile.Id.ToString())
                {
                    var thumbDir = Path.GetDirectoryName(existingFile.ThumbnailPath);
                    if (thumbDir != null)
                    {
                        var newThumbFileName = $"{existingFile.Id}.jpg";
                        var newThumbFullPath = Path.Combine(thumbDir, newThumbFileName);
                        try
                        {
                            if (File.Exists(newThumbFullPath))
                            {
                                // A correct, ID-based thumbnail already exists. The old one is an orphan.
                                File.Delete(existingFile.ThumbnailPath);
                            }
                            else
                            {
                                File.Move(existingFile.ThumbnailPath, newThumbFullPath);
                            }

                            existingFile.ThumbnailPath = newThumbFullPath;
                            existingFile.ThumbnailCreated = DateTime.Now;
                            await _repository.UpdateAsync(existingFile);
                            _logger.LogInformation("古いサムネイルを新しいIDベースの形式に移行しました: {Old} -> {New}", thumbFileName, newThumbFileName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "サムネイルの移行に失敗しました: {Path}", existingFile.ThumbnailPath);
                        }
                    }
                }
            }
        }

        public async Task<ScanResult> PerformFullScanAsync(string folderPath, IProgress<ScanProgress> progress)
        {
            // This method is unchanged
            var startTime = DateTime.Now;
            var result = new ScanResult { Success = true };
            try
            {
                var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly).Where(f => _supportedExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();
                var totalFiles = files.Count;
                var processedFiles = 0;
                const int BATCH_SIZE = 200;
                var batches = files.Chunk(BATCH_SIZE);
                foreach (var batch in batches)
                {
                    var mangaFiles = new List<MangaFile>();
                    var tasks = batch.Select(async filePath => {
                        try {
                            var fileInfo = new FileInfo(filePath);
                            return await CreateMangaFile(filePath, fileInfo);
                        } catch { return null; }
                    });
                    var batchResults = await Task.WhenAll(tasks);
                    foreach (var mangaFile in batchResults.Where(f => f != null)) mangaFiles.Add(mangaFile!);
                    if (mangaFiles.Any())
                    {
                        var insertedCount = await _repository.InsertBatchAsync(mangaFiles);
                        result.FilesAdded += insertedCount;
                    }
                    processedFiles += batch.Count();
                    progress?.Report(new ScanProgress { CurrentFile = processedFiles, TotalFiles = totalFiles, Status = $"{processedFiles}/{totalFiles} 処理完了" });
                }
                result.FilesProcessed = processedFiles;
                result.Duration = DateTime.Now - startTime;
            } catch (Exception ex) { result.Success = false; result.Errors.Add(ex.Message); }
            return result;
        }

        public Task<ParsedFileInfo> ParseFileNameAsync(string fileName)
        {
            // This method is unchanged
            var result = new ParsedFileInfo();
            try {
                var match = FileNamePattern.Match(fileName);
                if (match.Success)
                {
                    result.ParseSuccess = true;
                    result.AuthorReading = match.Groups[1].Value.Trim();
                    result.OriginalAuthor = match.Groups[2].Value.Trim();
                    if (match.Groups[3].Success && !string.IsNullOrWhiteSpace(match.Groups[3].Value)) result.Artist = match.Groups[3].Value.Trim();
                    result.Title = match.Groups[4].Value.Trim();
                    if (match.Groups[5].Success) {
                        if (int.TryParse(match.Groups[5].Value, out var vol)) result.VolumeNumber = vol;
                        else result.VolumeString = match.Groups[5].Value;
                    } else if (match.Groups[6].Success) {
                        if (int.TryParse(match.Groups[6].Value, out var vol)) result.VolumeNumber = vol;
                        else result.VolumeString = match.Groups[6].Value;
                    }
                } else { result.Title = Path.GetFileNameWithoutExtension(fileName); }
            } catch { result.Title = Path.GetFileNameWithoutExtension(fileName); }
            return Task.FromResult(result);
        }

        private async Task<MangaFile> CreateMangaFile(string filePath, FileInfo fileInfo)
        {
            // This method is unchanged
            var mangaFile = new MangaFile {
                FilePath = filePath, FileName = Path.GetFileNameWithoutExtension(fileInfo.Name), FileSize = fileInfo.Length,
                CreatedDate = fileInfo.CreationTime, ModifiedDate = fileInfo.LastWriteTime, FileType = Path.GetExtension(filePath).ToUpper().TrimStart('.'),
            };
            var parsedInfo = await ParseFileNameAsync(fileInfo.Name);
            mangaFile.Title = parsedInfo.Title;
            mangaFile.OriginalAuthor = parsedInfo.OriginalAuthor;
            mangaFile.Artist = parsedInfo.Artist;
            mangaFile.AuthorReading = parsedInfo.AuthorReading;
            mangaFile.VolumeNumber = parsedInfo.VolumeNumber;
            mangaFile.VolumeString = parsedInfo.VolumeString;
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
            await _repository.UpdateAsync(existingFile);
        }
    }
}
