using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Linq;

namespace Mangaanya.Services
{
    public class MangaRepository : IMangaRepository
    {
        private readonly ILogger<MangaRepository> _logger;
        private readonly string _connectionString;

        public MangaRepository(ILogger<MangaRepository> logger)
        {
            _logger = logger;
            
            // 単一ファイル配布時の正しいパス取得
            var baseDirectory = GetApplicationDirectory();
            var dbPath = Path.Combine(baseDirectory, "data", "manga.db");
            var dataDir = Path.GetDirectoryName(dbPath);
            
            if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }

            _connectionString = $"Data Source={dbPath};Version=3;Journal Mode=WAL;Synchronous=NORMAL;Cache Size=10000;Temp Store=MEMORY;";
        }
        
        private static string GetApplicationDirectory()
        {
            // 単一ファイル配布時の対応
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                var directory = Path.GetDirectoryName(processPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    return directory;
                }
            }
            
            // フォールバック
            return AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var createTableSql = @"
                    CREATE TABLE IF NOT EXISTS manga_files (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        file_path TEXT NOT NULL UNIQUE,
                        file_name TEXT NOT NULL,
                        file_size INTEGER NOT NULL,
                        created_date DATETIME NOT NULL,
                        modified_date DATETIME NOT NULL,
                        file_type TEXT NOT NULL,
                        is_corrupted BOOLEAN DEFAULT 0,
                        
                        title TEXT,
                        original_author TEXT,
                        artist TEXT,
                        author_reading TEXT,
                        volume_number INTEGER,
                        volume_string TEXT,
                        
                        genre TEXT,
                        publish_date DATETIME,
                        publisher TEXT,
                        tags TEXT,
                        is_ai_processed BOOLEAN DEFAULT 0,
                        
                        thumbnail_path TEXT,
                        thumbnail_created DATETIME
                    );

                    CREATE INDEX IF NOT EXISTS idx_manga_path ON manga_files(file_path);
                    CREATE INDEX IF NOT EXISTS idx_manga_modified ON manga_files(modified_date);
                    CREATE INDEX IF NOT EXISTS idx_manga_title ON manga_files(title);
                    CREATE INDEX IF NOT EXISTS idx_manga_author ON manga_files(original_author);
                ";

                using var command = new SQLiteCommand(createTableSql, connection);
                try
                {
                    await command.ExecuteNonQueryAsync();
                    
                    // データベースマイグレーション: volume_stringカラムが存在しない場合は追加
                    await MigrateVolumeStringColumnAsync(connection);
                }
                catch (Exception ex) when (ex.Message.Contains("duplicate column name"))
                {
                    // author_reading カラムが既に存在する場合は無視
                    
                }

                // カラムの存在確認と追加
                await EnsureColumnExistsAsync(connection, "author_reading", "TEXT");

                _logger.LogInformation("データベースを初期化しました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "データベース初期化中にエラーが発生しました");
                throw;
            }
        }

        private async Task MigrateVolumeStringColumnAsync(SQLiteConnection connection)
        {
            try
            {
                // volume_stringカラムが存在するかチェック
                var checkColumnSql = "PRAGMA table_info(manga_files)";
                using var checkCommand = new SQLiteCommand(checkColumnSql, connection);
                using var reader = await checkCommand.ExecuteReaderAsync();
                
                bool volumeStringExists = false;
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString("name");
                    if (columnName == "volume_string")
                    {
                        volumeStringExists = true;
                        break;
                    }
                }
                reader.Close();
                
                // volume_stringカラムが存在しない場合は追加
                if (!volumeStringExists)
                {
                    var addColumnSql = "ALTER TABLE manga_files ADD COLUMN volume_string TEXT";
                    using var addCommand = new SQLiteCommand(addColumnSql, connection);
                    await addCommand.ExecuteNonQueryAsync();
                    _logger.LogInformation("volume_stringカラムを追加しました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "volume_stringカラムのマイグレーション中にエラーが発生しました");
            }
        }

        public async Task<List<MangaFile>> GetAllAsync()
        {
            var files = new List<MangaFile>();

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT * FROM manga_files ORDER BY file_name";
                using var command = new SQLiteCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    files.Add(MapFromReader(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "全ファイル取得中にエラーが発生しました");
                throw;
            }

            return files;
        }

        public async Task<MangaFile?> GetByIdAsync(int id)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT * FROM manga_files WHERE id = @id";
                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@id", id);
                
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return MapFromReader(reader);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ID指定ファイル取得中にエラーが発生しました: {Id}", id);
                throw;
            }

            return null;
        }

        public async Task<List<MangaFile>> SearchAsync(SearchCriteria criteria)
        {
            var files = new List<MangaFile>();

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT * FROM manga_files WHERE 1=1";
                var parameters = new List<SQLiteParameter>();

                if (!string.IsNullOrEmpty(criteria.SearchText))
                {
                    sql += @" AND (
                        file_name LIKE @searchText OR 
                        title LIKE @searchText OR 
                        original_author LIKE @searchText OR 
                        artist LIKE @searchText OR 
                        genre LIKE @searchText OR 
                        publisher LIKE @searchText OR 
                        tags LIKE @searchText
                    )";
                    parameters.Add(new SQLiteParameter("@searchText", $"%{criteria.SearchText}%"));
                }

                if (criteria.IsAIProcessed.HasValue)
                {
                    sql += " AND is_ai_processed = @isAIProcessed";
                    parameters.Add(new SQLiteParameter("@isAIProcessed", criteria.IsAIProcessed.Value));
                }

                if (criteria.IsCorrupted.HasValue)
                {
                    sql += " AND is_corrupted = @isCorrupted";
                    parameters.Add(new SQLiteParameter("@isCorrupted", criteria.IsCorrupted.Value));
                }

                if (criteria.ModifiedAfter.HasValue)
                {
                    sql += " AND modified_date >= @modifiedAfter";
                    parameters.Add(new SQLiteParameter("@modifiedAfter", criteria.ModifiedAfter.Value));
                }

                if (criteria.ModifiedBefore.HasValue)
                {
                    sql += " AND modified_date <= @modifiedBefore";
                    parameters.Add(new SQLiteParameter("@modifiedBefore", criteria.ModifiedBefore.Value));
                }

                if (!string.IsNullOrEmpty(criteria.Genre))
                {
                    sql += " AND genre LIKE @genre";
                    parameters.Add(new SQLiteParameter("@genre", $"%{criteria.Genre}%"));
                }

                if (!string.IsNullOrEmpty(criteria.Publisher))
                {
                    sql += " AND publisher LIKE @publisher";
                    parameters.Add(new SQLiteParameter("@publisher", $"%{criteria.Publisher}%"));
                }

                sql += " ORDER BY file_name";

                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddRange(parameters.ToArray());
                
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    files.Add(MapFromReader(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "検索中にエラーが発生しました");
                throw;
            }

            return files;
        }

        public async Task<int> InsertAsync(MangaFile manga)
        {
            try
            {
                
                
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    INSERT INTO manga_files (
                        file_path, file_name, file_size, created_date, modified_date, file_type, is_corrupted,
                        title, original_author, artist, author_reading, volume_number, volume_string,
                        genre, publish_date, publisher, tags, is_ai_processed,
                        thumbnail_path, thumbnail_created
                    ) VALUES (
                        @filePath, @fileName, @fileSize, @createdDate, @modifiedDate, @fileType, @isCorrupted,
                        @title, @originalAuthor, @artist, @authorReading, @volumeNumber, @volumeString,
                        @genre, @publishDate, @publisher, @tags, @isAIProcessed,
                        @thumbnailPath, @thumbnailCreated
                    );
                    SELECT last_insert_rowid();
                ";

                using var command = new SQLiteCommand(sql, connection);
                AddParameters(command, manga);
                
                var result = await command.ExecuteScalarAsync();
                var id = Convert.ToInt32(result);
                manga.Id = id;
                
                
                return id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル挿入中にエラーが発生しました: {FilePath}", manga.FilePath);
                throw;
            }
        }

        public async Task UpdateAsync(MangaFile manga)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    UPDATE manga_files SET
                        file_path = @filePath, file_name = @fileName, file_size = @fileSize,
                        created_date = @createdDate, modified_date = @modifiedDate, file_type = @fileType, is_corrupted = @isCorrupted,
                        title = @title, original_author = @originalAuthor, artist = @artist, author_reading = @authorReading, volume_number = @volumeNumber, volume_string = @volumeString,
                        genre = @genre, publish_date = @publishDate, publisher = @publisher, tags = @tags, is_ai_processed = @isAIProcessed,
                        thumbnail_path = @thumbnailPath, thumbnail_created = @thumbnailCreated
                    WHERE id = @id
                ";

                using var command = new SQLiteCommand(sql, connection);
                AddParameters(command, manga);
                command.Parameters.AddWithValue("@id", manga.Id);
                
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル更新中にエラーが発生しました: {Id}", manga.Id);
                throw;
            }
        }

        public async Task UpdateBatchAsync(IEnumerable<MangaFile> mangaFiles)
        {
            var files = mangaFiles.ToList();
            if (files.Count == 0) return;

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();
                try
                {
                    var sql = @"
                        UPDATE manga_files SET
                            file_path = @filePath, file_name = @fileName, file_size = @fileSize,
                            created_date = @createdDate, modified_date = @modifiedDate, file_type = @fileType, is_corrupted = @isCorrupted,
                            title = @title, original_author = @originalAuthor, artist = @artist, author_reading = @authorReading, volume_number = @volumeNumber, volume_string = @volumeString,
                            genre = @genre, publish_date = @publishDate, publisher = @publisher, tags = @tags, is_ai_processed = @isAIProcessed,
                            thumbnail_path = @thumbnailPath, thumbnail_created = @thumbnailCreated
                        WHERE id = @id
                    ";

                    using var command = new SQLiteCommand(sql, connection, transaction);
                    
                    foreach (var manga in files)
                    {
                        command.Parameters.Clear();
                        AddParameters(command, manga);
                        command.Parameters.AddWithValue("@id", manga.Id);
                        await command.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();
                    
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "バッチ更新中にエラーが発生しました: {Count}件", files.Count);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "DELETE FROM manga_files WHERE id = @id";
                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@id", id);
                
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル削除中にエラーが発生しました: {Id}", id);
                throw;
            }
        }

        public async Task<int> DeleteByFolderPathAsync(string folderPath)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                // 1. 削除対象のファイルのサムネイルパスを事前に取得
                var normalizedFolderPath = folderPath.TrimEnd('\\', '/');
                var selectSql = @"SELECT thumbnail_path FROM manga_files 
                                 WHERE file_path LIKE @folderPath || @separator || '%' 
                                 AND file_path NOT LIKE @folderPath || @separator || '%' || @separator || '%'
                                 AND thumbnail_path IS NOT NULL AND thumbnail_path != ''";
                
                var thumbnailPaths = new List<string>();
                using (var selectCommand = new SQLiteCommand(selectSql, connection))
                {
                    selectCommand.Parameters.AddWithValue("@folderPath", normalizedFolderPath);
                    selectCommand.Parameters.AddWithValue("@separator", Path.DirectorySeparatorChar.ToString());
                    
                    using var reader = await selectCommand.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var thumbnailPath = reader.GetString("thumbnail_path");
                        if (!string.IsNullOrEmpty(thumbnailPath))
                        {
                            thumbnailPaths.Add(thumbnailPath);
                        }
                    }
                }

                // 2. サムネイルファイルを削除
                var deletedThumbnailCount = 0;
                foreach (var thumbnailPath in thumbnailPaths)
                {
                    try
                    {
                        if (File.Exists(thumbnailPath))
                        {
                            File.Delete(thumbnailPath);
                            deletedThumbnailCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "サムネイル削除に失敗: {Path}", thumbnailPath);
                    }
                }

                // 3. データベースからファイル情報を削除
                var deleteSql = @"DELETE FROM manga_files 
                                 WHERE file_path LIKE @folderPath || @separator || '%' 
                                 AND file_path NOT LIKE @folderPath || @separator || '%' || @separator || '%'";
                
                using var deleteCommand = new SQLiteCommand(deleteSql, connection);
                deleteCommand.Parameters.AddWithValue("@folderPath", normalizedFolderPath);
                deleteCommand.Parameters.AddWithValue("@separator", Path.DirectorySeparatorChar.ToString());
                
                var deletedCount = await deleteCommand.ExecuteNonQueryAsync();
                _logger.LogInformation("フォルダ内のファイルを一括削除しました: {FolderPath}, DB削除件数: {Count}, サムネイル削除件数: {ThumbnailCount}", 
                    folderPath, deletedCount, deletedThumbnailCount);
                
                return deletedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フォルダ内ファイル一括削除中にエラーが発生しました: {FolderPath}", folderPath);
                throw;
            }
        }

        public async Task<int> GetTotalCountAsync()
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT COUNT(*) FROM manga_files";
                using var command = new SQLiteCommand(sql, connection);
                
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "総件数取得中にエラーが発生しました");
                throw;
            }
        }

        private MangaFile MapFromReader(IDataReader reader)
        {
            return new MangaFile
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                FilePath = reader.GetString(reader.GetOrdinal("file_path")),
                FileName = reader.GetString(reader.GetOrdinal("file_name")),
                FileSize = reader.GetInt64(reader.GetOrdinal("file_size")),
                CreatedDate = reader.GetDateTime(reader.GetOrdinal("created_date")),
                ModifiedDate = reader.GetDateTime(reader.GetOrdinal("modified_date")),
                FileType = reader.GetString(reader.GetOrdinal("file_type")),
                IsCorrupted = reader.GetBoolean(reader.GetOrdinal("is_corrupted")),
                
                Title = reader.IsDBNull(reader.GetOrdinal("title")) ? null : reader.GetString(reader.GetOrdinal("title")),
                OriginalAuthor = reader.IsDBNull(reader.GetOrdinal("original_author")) ? null : reader.GetString(reader.GetOrdinal("original_author")),
                Artist = reader.IsDBNull(reader.GetOrdinal("artist")) ? null : reader.GetString(reader.GetOrdinal("artist")),
                AuthorReading = reader.IsDBNull(reader.GetOrdinal("author_reading")) ? null : reader.GetString(reader.GetOrdinal("author_reading")),
                VolumeNumber = reader.IsDBNull(reader.GetOrdinal("volume_number")) ? null : reader.GetInt32(reader.GetOrdinal("volume_number")),
                VolumeString = reader.IsDBNull(reader.GetOrdinal("volume_string")) ? null : reader.GetString(reader.GetOrdinal("volume_string")),
                
                Genre = reader.IsDBNull(reader.GetOrdinal("genre")) ? null : reader.GetString(reader.GetOrdinal("genre")),
                PublishDate = reader.IsDBNull(reader.GetOrdinal("publish_date")) ? null : reader.GetDateTime(reader.GetOrdinal("publish_date")),
                Publisher = reader.IsDBNull(reader.GetOrdinal("publisher")) ? null : reader.GetString(reader.GetOrdinal("publisher")),
                Tags = reader.IsDBNull(reader.GetOrdinal("tags")) ? null : reader.GetString(reader.GetOrdinal("tags")),
                IsAIProcessed = reader.GetBoolean(reader.GetOrdinal("is_ai_processed")),
                
                ThumbnailPath = reader.IsDBNull(reader.GetOrdinal("thumbnail_path")) ? null : reader.GetString(reader.GetOrdinal("thumbnail_path")),
                ThumbnailCreated = reader.IsDBNull(reader.GetOrdinal("thumbnail_created")) ? null : reader.GetDateTime(reader.GetOrdinal("thumbnail_created"))
            };
        }

        private void AddParameters(SQLiteCommand command, MangaFile manga)
        {
            command.Parameters.AddWithValue("@filePath", manga.FilePath);
            command.Parameters.AddWithValue("@fileName", manga.FileName);
            command.Parameters.AddWithValue("@fileSize", manga.FileSize);
            command.Parameters.AddWithValue("@createdDate", manga.CreatedDate);
            command.Parameters.AddWithValue("@modifiedDate", manga.ModifiedDate);
            command.Parameters.AddWithValue("@fileType", manga.FileType);
            command.Parameters.AddWithValue("@isCorrupted", manga.IsCorrupted);
            
            command.Parameters.AddWithValue("@title", (object?)manga.Title ?? DBNull.Value);
            command.Parameters.AddWithValue("@originalAuthor", (object?)manga.OriginalAuthor ?? DBNull.Value);
            command.Parameters.AddWithValue("@artist", (object?)manga.Artist ?? DBNull.Value);
            command.Parameters.AddWithValue("@authorReading", (object?)manga.AuthorReading ?? DBNull.Value);
            command.Parameters.AddWithValue("@volumeNumber", (object?)manga.VolumeNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@volumeString", (object?)manga.VolumeString ?? DBNull.Value);
            
            command.Parameters.AddWithValue("@genre", (object?)manga.Genre ?? DBNull.Value);
            command.Parameters.AddWithValue("@publishDate", (object?)manga.PublishDate ?? DBNull.Value);
            command.Parameters.AddWithValue("@publisher", (object?)manga.Publisher ?? DBNull.Value);
            command.Parameters.AddWithValue("@tags", (object?)manga.Tags ?? DBNull.Value);
            command.Parameters.AddWithValue("@isAIProcessed", manga.IsAIProcessed);
            
            command.Parameters.AddWithValue("@thumbnailPath", (object?)manga.ThumbnailPath ?? DBNull.Value);
            command.Parameters.AddWithValue("@thumbnailCreated", (object?)manga.ThumbnailCreated ?? DBNull.Value);
        }

        public async Task<int> InsertBatchAsync(IEnumerable<MangaFile> mangaFiles)
        {
            try
            {
                var files = mangaFiles.ToList();
                if (!files.Any()) return 0;

                
                
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();
                try
                {
                    var sql = @"
                        INSERT INTO manga_files (
                            file_path, file_name, file_size, created_date, modified_date, file_type, is_corrupted,
                            title, original_author, artist, author_reading, volume_number, volume_string,
                            genre, publish_date, publisher, tags, is_ai_processed,
                            thumbnail_path, thumbnail_created
                        ) VALUES (
                            @filePath, @fileName, @fileSize, @createdDate, @modifiedDate, @fileType, @isCorrupted,
                            @title, @originalAuthor, @artist, @authorReading, @volumeNumber, @volumeString,
                            @genre, @publishDate, @publisher, @tags, @isAIProcessed,
                            @thumbnailPath, @thumbnailCreated
                        )";

                    using var command = new SQLiteCommand(sql, connection, transaction);
                    
                    var insertedCount = 0;
                    foreach (var manga in files)
                    {
                        command.Parameters.Clear();
                        AddParameters(command, manga);
                        await command.ExecuteNonQueryAsync();
                        insertedCount++;
                    }

                    transaction.Commit();
                    
                    return insertedCount;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "バッチファイル挿入中にエラーが発生しました");
                throw;
            }
        }

        public async Task<List<string>> GetAllThumbnailPathsAsync()
        {
            var thumbnailPaths = new List<string>();
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();
                
                var sql = "SELECT thumbnail_path FROM manga_files WHERE thumbnail_path IS NOT NULL AND thumbnail_path != ''";
                using var command = new SQLiteCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var path = reader.GetString("thumbnail_path");
                    if (!string.IsNullOrEmpty(path))
                    {
                        thumbnailPaths.Add(path);
                    }
                }
                
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイルパス取得中にエラーが発生しました");
                throw;
            }
            
            return thumbnailPaths;
        }

        public async Task ClearAllAsync()
        {
            try
            {
                // 1. 全サムネイルパスを取得
                var allThumbnailPaths = await GetAllThumbnailPathsAsync();
                
                // 2. サムネイルファイルを削除
                var deletedThumbnailCount = 0;
                foreach (var thumbnailPath in allThumbnailPaths.Where(p => !string.IsNullOrEmpty(p)))
                {
                    try
                    {
                        if (File.Exists(thumbnailPath))
                        {
                            File.Delete(thumbnailPath);
                            deletedThumbnailCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "サムネイル削除に失敗: {Path}", thumbnailPath);
                    }
                }

                // 3. データベースをクリア
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "DELETE FROM manga_files";
                using var command = new SQLiteCommand(sql, connection);
                
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("すべてのファイルとサムネイルを削除しました: DB削除完了, サムネイル削除={ThumbnailCount}件", deletedThumbnailCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "すべてのファイル削除中にエラーが発生しました");
                throw;
            }
        }

        private async Task EnsureColumnExistsAsync(SQLiteConnection connection, string columnName, string columnType)
        {
            try
            {
                // カラムの存在確認
                var checkSql = "PRAGMA table_info(manga_files)";
                using var checkCommand = new SQLiteCommand(checkSql, connection);
                using var reader = await checkCommand.ExecuteReaderAsync();
                
                var columnExists = false;
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString("name");
                    if (name == columnName)
                    {
                        columnExists = true;
                        break;
                    }
                }

                // カラムが存在しない場合は追加
                if (!columnExists)
                {
                    var alterSql = $"ALTER TABLE manga_files ADD COLUMN {columnName} {columnType}";
                    using var alterCommand = new SQLiteCommand(alterSql, connection);
                    await alterCommand.ExecuteNonQueryAsync();
                    _logger.LogInformation("カラム {ColumnName} を追加しました", columnName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "カラム {ColumnName} の追加中にエラーが発生しました", columnName);
            }
        }
    }
}
