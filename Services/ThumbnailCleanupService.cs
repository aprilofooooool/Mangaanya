using Microsoft.Extensions.Logging;
using System.IO;
using System.Linq;
using Mangaanya.Models;

namespace Mangaanya.Services
{
    /// <summary>
    /// サムネイルファイルのクリーンアップ機能を提供するサービス
    /// </summary>
    public class ThumbnailCleanupService : IThumbnailCleanupService
    {
        private readonly IMangaRepository _repository;
        private readonly ILogger<ThumbnailCleanupService> _logger;
        private readonly string _thumbnailDirectory;

        public ThumbnailCleanupService(
            IMangaRepository repository,
            ILogger<ThumbnailCleanupService> logger,
            IConfigurationManager config)
        {
            _repository = repository;
            _logger = logger;

            // サムネイル保存ディレクトリを取得（ThumbnailServiceOptimizedと同じ方法）
            _thumbnailDirectory = GetThumbnailDirectory();
        }

        /// <summary>
        /// サムネイルディレクトリのパスを取得します（ThumbnailServiceOptimizedと同じ実装）
        /// </summary>
        private string GetThumbnailDirectory()
        {
            var appDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var thumbnailDir = Path.Combine(appDirectory!, "thumbnails");

            try
            {
                Directory.CreateDirectory(thumbnailDir);
                return thumbnailDir;
            }
            catch
            {
                var fallbackDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mangaanya", "thumbnails");
                Directory.CreateDirectory(fallbackDir);
                return fallbackDir;
            }
        }

        /// <summary>
        /// データベースに登録されていない孤立したサムネイルファイルを削除します
        /// </summary>
        /// <returns>クリーンアップ結果</returns>
        public async Task<CleanupResult> CleanupOrphanedThumbnailsAsync()
        {
            try
            {
                if (!Directory.Exists(_thumbnailDirectory))
                {

                    return CleanupResult.CreateSuccess(0);
                }

                _logger.LogInformation("孤立サムネイルクリーンアップを開始します: {Directory}", _thumbnailDirectory);

                // DBに登録されているサムネイルパスを取得
                var dbThumbnailPaths = await _repository.GetAllThumbnailPathsAsync();
                _logger.LogInformation("DB登録サムネイル総数: {Count}件", dbThumbnailPaths.Count);

                var dbThumbnailFiles = dbThumbnailPaths
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => Path.GetFileName(p))
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToHashSet();

                _logger.LogInformation("DB登録サムネイルファイル名（有効）: {Count}件", dbThumbnailFiles.Count);

                // ディスク上のサムネイルファイルを取得（default_thumbnail.jpgを除外）
                var diskThumbnailFiles = Directory.GetFiles(_thumbnailDirectory, "*.jpg")
                    .Select(f => Path.GetFileName(f))
                    .Where(f => !string.Equals(f, "default_thumbnail.jpg", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                _logger.LogInformation("ディスク上のサムネイルファイル: {Count}件", diskThumbnailFiles.Count);

                // デバッグ用：最初の10件のファイル名を比較
                _logger.LogDebug("DB登録ファイル名（最初の10件）: {Files}",
                    string.Join(", ", dbThumbnailFiles.Take(10)));
                _logger.LogDebug("ディスクファイル名（最初の10件）: {Files}",
                    string.Join(", ", diskThumbnailFiles.Take(10)));

                // 孤立ファイルを削除
                var orphanedCount = 0;
                var checkedCount = 0;

                foreach (var diskFile in diskThumbnailFiles)
                {
                    checkedCount++;
                    if (!dbThumbnailFiles.Contains(diskFile))
                    {
                        var fullPath = Path.Combine(_thumbnailDirectory, diskFile);
                        try
                        {
                            File.Delete(fullPath);
                            orphanedCount++;

                            // 最初の10件の削除ファイルをログ出力
                            if (orphanedCount <= 10)
                            {
                                _logger.LogInformation("孤立サムネイルを削除しました: {File}", diskFile);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "孤立サムネイル削除に失敗: {File}", diskFile);
                        }
                    }

                    // 進捗を1000件ごとに報告
                    if (checkedCount % 1000 == 0)
                    {
                        _logger.LogDebug("チェック進捗: {Checked}/{Total}件, 削除済み: {Deleted}件",
                            checkedCount, diskThumbnailFiles.Count, orphanedCount);
                    }
                }

                _logger.LogInformation("孤立サムネイルクリーンアップ完了: チェック={Checked}件, 削除={Deleted}件",
                    checkedCount, orphanedCount);
                return CleanupResult.CreateSuccess(orphanedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "孤立サムネイルクリーンアップ中にエラーが発生しました");
                return CleanupResult.CreateError(ex.Message);
            }
        }

        /// <summary>
        /// サムネイル状況の詳細診断を実行します
        /// </summary>
        /// <returns>診断結果</returns>
        public async Task<string> DiagnoseThumbnailStatusAsync()
        {
            try
            {
                if (!Directory.Exists(_thumbnailDirectory))
                {
                    return $"サムネイルディレクトリが存在しません: {_thumbnailDirectory}";
                }

                // DBに登録されているサムネイルパスを取得
                var dbThumbnailPaths = await _repository.GetAllThumbnailPathsAsync();
                var dbThumbnailFiles = dbThumbnailPaths
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => Path.GetFileName(p))
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToHashSet();

                // ディスク上のサムネイルファイルを取得（default_thumbnail.jpgを除外）
                var diskThumbnailFiles = Directory.GetFiles(_thumbnailDirectory, "*.jpg")
                    .Select(f => Path.GetFileName(f))
                    .Where(f => !string.Equals(f, "default_thumbnail.jpg", StringComparison.OrdinalIgnoreCase))
                    .ToHashSet();

                // 統計情報を計算
                var orphanedFiles = diskThumbnailFiles.Except(dbThumbnailFiles).ToList();
                var missingFiles = dbThumbnailFiles.Except(diskThumbnailFiles).ToList();

                var result = $@"
=== サムネイル診断結果 ===
サムネイルディレクトリ: {_thumbnailDirectory}
ディレクトリ存在: {Directory.Exists(_thumbnailDirectory)}

DB登録サムネイル総数: {dbThumbnailPaths.Count}件
DB登録サムネイル（有効）: {dbThumbnailFiles.Count}件
ディスク上のサムネイル: {diskThumbnailFiles.Count}件
孤立ファイル（ディスクにあるがDBにない）: {orphanedFiles.Count}件
欠損ファイル（DBにあるがディスクにない）: {missingFiles.Count}件

孤立ファイル例（最初の10件）:
{string.Join("\n", orphanedFiles.Take(10))}

欠損ファイル例（最初の10件）:
{string.Join("\n", missingFiles.Take(10))}
";

                _logger.LogInformation("サムネイル診断完了: 孤立={Orphaned}件, 欠損={Missing}件",
                    orphanedFiles.Count, missingFiles.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイル診断中にエラーが発生しました");
                return $"診断エラー: {ex.Message}";
            }
        }

        /// <summary>
        /// 孤立したサムネイルファイルの数を取得します
        /// </summary>
        /// <returns>孤立ファイル数</returns>
        public async Task<int> GetOrphanedThumbnailCountAsync()
        {
            try
            {
                if (!Directory.Exists(_thumbnailDirectory))
                    return 0;

                // DBに登録されているサムネイルパスを取得
                var dbThumbnailPaths = await _repository.GetAllThumbnailPathsAsync();
                var dbThumbnailFiles = dbThumbnailPaths
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => Path.GetFileName(p))
                    .Where(f => !string.IsNullOrEmpty(f))
                    .ToHashSet();

                // ディスク上のサムネイルファイルを取得（default_thumbnail.jpgを除外）
                var diskThumbnailFiles = Directory.GetFiles(_thumbnailDirectory, "*.jpg")
                    .Where(f => !string.Equals(Path.GetFileName(f), "default_thumbnail.jpg", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                var orphanedCount = diskThumbnailFiles.Count(f => !dbThumbnailFiles.Contains(Path.GetFileName(f)));


                return orphanedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "孤立サムネイル数取得中にエラーが発生しました");
                return 0;
            }
        }
    }
}
