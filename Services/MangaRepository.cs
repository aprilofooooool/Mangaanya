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
                        
                        thumbnail_data BLOB,
                        thumbnail_generated DATETIME,
                        rating INTEGER
                    );

                    CREATE INDEX IF NOT EXISTS idx_manga_path ON manga_files(file_path);
                    CREATE INDEX IF NOT EXISTS idx_manga_modified ON manga_files(modified_date);
                    CREATE INDEX IF NOT EXISTS idx_manga_title ON manga_files(title);
                    CREATE INDEX IF NOT EXISTS idx_manga_author ON manga_files(original_author);
                    CREATE INDEX IF NOT EXISTS idx_manga_rating ON manga_files(rating);
                ";

                using var command = new SQLiteCommand(createTableSql, connection);
                try
                {
                    await command.ExecuteNonQueryAsync();
                    
                    // データベースマイグレーション: volume_stringカラムが存在しない場合は追加
                    await MigrateVolumeStringColumnAsync(connection);
                    
                    // データベースマイグレーション: ratingカラムが存在しない場合は追加
                    await MigrateRatingColumnAsync(connection);
                    
                    // データベースマイグレーション: サムネイルスキーマをバイナリ保存に移行
                    await MigrateThumbnailSchemaAsync(connection);
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
                var dbException = new Mangaanya.Exceptions.DatabaseException("データベース初期化中にエラーが発生しました", ex);
                _logger.LogError(dbException, "データベース初期化中にエラーが発生しました");
                throw dbException;
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

        private async Task MigrateRatingColumnAsync(SQLiteConnection connection)
        {
            try
            {
                // ratingカラムが存在するかチェック
                var checkColumnSql = "PRAGMA table_info(manga_files)";
                using var checkCommand = new SQLiteCommand(checkColumnSql, connection);
                using var reader = await checkCommand.ExecuteReaderAsync();
                
                bool ratingExists = false;
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString("name");
                    if (columnName == "rating")
                    {
                        ratingExists = true;
                        break;
                    }
                }
                reader.Close();
                
                // ratingカラムが存在しない場合は追加
                if (!ratingExists)
                {
                    var addColumnSql = "ALTER TABLE manga_files ADD COLUMN rating INTEGER";
                    using var addCommand = new SQLiteCommand(addColumnSql, connection);
                    await addCommand.ExecuteNonQueryAsync();
                    
                    // 評価による検索を高速化するためのインデックス作成
                    var createIndexSql = "CREATE INDEX IF NOT EXISTS idx_manga_rating ON manga_files(rating)";
                    using var indexCommand = new SQLiteCommand(createIndexSql, connection);
                    await indexCommand.ExecuteNonQueryAsync();
                    
                    _logger.LogInformation("ratingカラムとインデックスを追加しました");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ratingカラムのマイグレーション中にエラーが発生しました");
            }
        }

        private async Task MigrateThumbnailSchemaAsync(SQLiteConnection connection)
        {
            try
            {
                // 現在のテーブル構造を確認
                var checkColumnSql = "PRAGMA table_info(manga_files)";
                using var checkCommand = new SQLiteCommand(checkColumnSql, connection);
                using var reader = await checkCommand.ExecuteReaderAsync();
                
                bool thumbnailPathExists = false;
                bool thumbnailCreatedExists = false;
                bool thumbnailDataExists = false;
                bool thumbnailGeneratedExists = false;
                
                while (await reader.ReadAsync())
                {
                    var columnName = reader.GetString("name");
                    switch (columnName)
                    {
                        case "thumbnail_path":
                            thumbnailPathExists = true;
                            break;
                        case "thumbnail_created":
                            thumbnailCreatedExists = true;
                            break;
                        case "thumbnail_data":
                            thumbnailDataExists = true;
                            break;
                        case "thumbnail_generated":
                            thumbnailGeneratedExists = true;
                            break;
                    }
                }
                reader.Close();
                
                // 新しいカラムを追加
                if (!thumbnailDataExists)
                {
                    var addThumbnailDataSql = "ALTER TABLE manga_files ADD COLUMN thumbnail_data BLOB";
                    using var addThumbnailDataCommand = new SQLiteCommand(addThumbnailDataSql, connection);
                    await addThumbnailDataCommand.ExecuteNonQueryAsync();
                    _logger.LogInformation("thumbnail_dataカラムを追加しました");
                }
                
                if (!thumbnailGeneratedExists)
                {
                    var addThumbnailGeneratedSql = "ALTER TABLE manga_files ADD COLUMN thumbnail_generated DATETIME";
                    using var addThumbnailGeneratedCommand = new SQLiteCommand(addThumbnailGeneratedSql, connection);
                    await addThumbnailGeneratedCommand.ExecuteNonQueryAsync();
                    _logger.LogInformation("thumbnail_generatedカラムを追加しました");
                }
                
                // 古いカラムが存在する場合は削除（SQLiteでは直接削除できないため、テーブル再作成が必要）
                if (thumbnailPathExists || thumbnailCreatedExists)
                {
                    await RecreateTableWithoutOldThumbnailColumnsAsync(connection);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "サムネイルスキーマのマイグレーション中にエラーが発生しました");
            }
        }

        private async Task RecreateTableWithoutOldThumbnailColumnsAsync(SQLiteConnection connection)
        {
            try
            {
                using var transaction = connection.BeginTransaction();
                
                // 1. 新しいテーブル構造でテンポラリテーブルを作成
                var createTempTableSql = @"
                    CREATE TABLE manga_files_temp (
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
                        
                        thumbnail_data BLOB,
                        thumbnail_generated DATETIME,
                        rating INTEGER
                    )";
                using var createTempCommand = new SQLiteCommand(createTempTableSql, connection, transaction);
                await createTempCommand.ExecuteNonQueryAsync();
                
                // 2. 既存データを新しいテーブルにコピー（古いサムネイルカラムは除外）
                var copyDataSql = @"
                    INSERT INTO manga_files_temp (
                        id, file_path, file_name, file_size, created_date, modified_date, file_type, is_corrupted,
                        title, original_author, artist, author_reading, volume_number, volume_string,
                        genre, publish_date, publisher, tags, is_ai_processed, rating
                    )
                    SELECT 
                        id, file_path, file_name, file_size, created_date, modified_date, file_type, is_corrupted,
                        title, original_author, artist, author_reading, volume_number, volume_string,
                        genre, publish_date, publisher, tags, is_ai_processed, rating
                    FROM manga_files";
                using var copyDataCommand = new SQLiteCommand(copyDataSql, connection, transaction);
                await copyDataCommand.ExecuteNonQueryAsync();
                
                // 3. 古いテーブルを削除
                var dropOldTableSql = "DROP TABLE manga_files";
                using var dropOldCommand = new SQLiteCommand(dropOldTableSql, connection, transaction);
                await dropOldCommand.ExecuteNonQueryAsync();
                
                // 4. 新しいテーブルの名前を変更
                var renameTableSql = "ALTER TABLE manga_files_temp RENAME TO manga_files";
                using var renameCommand = new SQLiteCommand(renameTableSql, connection, transaction);
                await renameCommand.ExecuteNonQueryAsync();
                
                // 5. インデックスを再作成
                var recreateIndexesSql = @"
                    CREATE INDEX IF NOT EXISTS idx_manga_path ON manga_files(file_path);
                    CREATE INDEX IF NOT EXISTS idx_manga_modified ON manga_files(modified_date);
                    CREATE INDEX IF NOT EXISTS idx_manga_title ON manga_files(title);
                    CREATE INDEX IF NOT EXISTS idx_manga_author ON manga_files(original_author);
                    CREATE INDEX IF NOT EXISTS idx_manga_rating ON manga_files(rating);
                ";
                using var recreateIndexesCommand = new SQLiteCommand(recreateIndexesSql, connection, transaction);
                await recreateIndexesCommand.ExecuteNonQueryAsync();
                
                await transaction.CommitAsync();
                _logger.LogInformation("サムネイルスキーマの移行が完了しました（thumbnail_path, thumbnail_createdカラムを削除）");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "テーブル再作成中にエラーが発生しました");
                throw;
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

        /// <summary>
        /// サムネイルデータを除外した軽量なファイル一覧を取得します。
        /// パフォーマンス最適化のため、thumbnail_dataフィールドを除外してデータ転送量を削減します。
        /// </summary>
        /// <returns>サムネイルデータを除外したMangaFileのリスト</returns>
        public async Task<List<MangaFile>> GetAllWithoutThumbnailsAsync()
        {
            var files = new List<MangaFile>();

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                // thumbnail_dataフィールドを除外したSELECTクエリ
                // パフォーマンス最適化: 7万件環境で約2.15GB → 50MBに削減
                var sql = @"SELECT 
                    id, file_path, file_name, file_size, created_date, modified_date, file_type, is_corrupted,
                    title, original_author, artist, author_reading, volume_number, volume_string,
                    genre, publish_date, publisher, tags, is_ai_processed, rating,
                    thumbnail_generated
                    FROM manga_files ORDER BY file_name";
                    
                using var command = new SQLiteCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    files.Add(MapFromReaderWithoutThumbnail(reader));
                }

                _logger.LogDebug("軽量ファイル取得が完了しました: {Count}件", files.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "軽量ファイル取得中にエラーが発生しました");
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

                if (criteria.Rating.HasValue)
                {
                    sql += " AND rating = @rating";
                    parameters.Add(new SQLiteParameter("@rating", criteria.Rating.Value));
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
                        thumbnail_data, thumbnail_generated, rating
                    ) VALUES (
                        @filePath, @fileName, @fileSize, @createdDate, @modifiedDate, @fileType, @isCorrupted,
                        @title, @originalAuthor, @artist, @authorReading, @volumeNumber, @volumeString,
                        @genre, @publishDate, @publisher, @tags, @isAIProcessed,
                        @thumbnailData, @thumbnailGenerated, @rating
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
                var dbException = new Mangaanya.Exceptions.DatabaseException($"ファイル挿入中にエラーが発生しました: {manga.FilePath}", ex);
                _logger.LogError(dbException, "ファイル挿入中にエラーが発生しました: {FilePath}", manga.FilePath);
                throw dbException;
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
                        thumbnail_data = @thumbnailData, thumbnail_generated = @thumbnailGenerated, rating = @rating
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
                            thumbnail_data = @thumbnailData, thumbnail_generated = @thumbnailGenerated, rating = @rating
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

        public async Task UpdateFilePathsBatchAsync(IEnumerable<(int Id, string NewFilePath)> filePathUpdates)
        {
            var updates = filePathUpdates.ToList();
            if (updates.Count == 0) return;

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();
                try
                {
                    foreach (var (id, newFilePath) in updates)
                    {
                        var fullFileName = Path.GetFileName(newFilePath);
                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullFileName);
                        var fileExtension = Path.GetExtension(fullFileName).TrimStart('.').ToUpper();

                        // 1. 移動先パスの既存レコードを削除（移動元レコード以外）
                        var deleteSql = @"DELETE FROM manga_files WHERE file_path = @newFilePath AND id != @id";
                        using var deleteCommand = new SQLiteCommand(deleteSql, connection, transaction);
                        deleteCommand.Parameters.AddWithValue("@newFilePath", newFilePath);
                        deleteCommand.Parameters.AddWithValue("@id", id);
                        
                        var deletedRows = await deleteCommand.ExecuteNonQueryAsync();
                        if (deletedRows > 0)
                        {
                            _logger.LogInformation("移動先の既存レコードを削除しました: {FilePath} ({DeletedRows}件)", newFilePath, deletedRows);
                        }

                        // 2. 移動元レコードを更新
                        var updateSql = @"
                            UPDATE manga_files SET
                                file_path = @newFilePath,
                                file_name = @fileName,
                                file_type = @fileType
                            WHERE id = @id
                        ";
                        using var updateCommand = new SQLiteCommand(updateSql, connection, transaction);
                        updateCommand.Parameters.AddWithValue("@id", id);
                        updateCommand.Parameters.AddWithValue("@newFilePath", newFilePath);
                        updateCommand.Parameters.AddWithValue("@fileName", fileNameWithoutExtension);
                        updateCommand.Parameters.AddWithValue("@fileType", fileExtension);
                        
                        var rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                        if (rowsAffected == 0)
                        {
                            _logger.LogWarning("ファイルパス更新対象のレコードが見つかりません: ID={Id}", id);
                        }
                    }

                    transaction.Commit();
                    _logger.LogInformation("ファイルパスの一括更新が完了しました: {Count}件", updates.Count);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイルパス一括更新中にエラーが発生しました: {Count}件", updates.Count);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                // データベースからファイル情報を削除（サムネイルデータも自動的に削除される）
                var deleteSql = "DELETE FROM manga_files WHERE id = @id";
                using var deleteCommand = new SQLiteCommand(deleteSql, connection);
                deleteCommand.Parameters.AddWithValue("@id", id);
                
                var rowsAffected = await deleteCommand.ExecuteNonQueryAsync();

                _logger.LogInformation("ファイルを削除しました: ID={Id}, 削除行数={RowsAffected}", id, rowsAffected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル削除中にエラーが発生しました: {Id}", id);
                throw;
            }
        }

        public async Task<int> DeleteBatchAsync(IEnumerable<int> ids)
        {
            var idList = ids.ToList();
            if (idList.Count == 0) return 0;

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();
                try
                {
                    // バッチ削除用のSQL（サムネイルバイナリデータも自動的に削除される）
                    var placeholders = string.Join(",", idList.Select((_, index) => $"@id{index}"));
                    var deleteSql = $"DELETE FROM manga_files WHERE id IN ({placeholders})";
                    
                    using var deleteCommand = new SQLiteCommand(deleteSql, connection, transaction);
                    
                    // パラメータを追加
                    for (int i = 0; i < idList.Count; i++)
                    {
                        deleteCommand.Parameters.AddWithValue($"@id{i}", idList[i]);
                    }
                    
                    var deletedCount = await deleteCommand.ExecuteNonQueryAsync();
                    await transaction.CommitAsync();
                    
                    _logger.LogInformation("ファイルを一括削除しました: 削除件数={Count}", deletedCount);
                    return deletedCount;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイル一括削除中にエラーが発生しました: {Count}件", idList.Count);
                throw;
            }
        }

        public async Task<int> DeleteByFolderPathAsync(string folderPath)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                // データベースからファイル情報を削除（サムネイルデータも自動的に削除される）
                var normalizedFolderPath = folderPath.TrimEnd('\\', '/');
                var deleteSql = @"DELETE FROM manga_files 
                                 WHERE file_path LIKE @folderPath || @separator || '%' 
                                 AND file_path NOT LIKE @folderPath || @separator || '%' || @separator || '%'";
                
                using var deleteCommand = new SQLiteCommand(deleteSql, connection);
                deleteCommand.Parameters.AddWithValue("@folderPath", normalizedFolderPath);
                deleteCommand.Parameters.AddWithValue("@separator", Path.DirectorySeparatorChar.ToString());
                
                var deletedCount = await deleteCommand.ExecuteNonQueryAsync();
                _logger.LogInformation("フォルダ内のファイルを一括削除しました: {FolderPath}, DB削除件数: {Count}", 
                    folderPath, deletedCount);
                
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
                
                ThumbnailData = reader.IsDBNull(reader.GetOrdinal("thumbnail_data")) ? null : (byte[])reader["thumbnail_data"],
                ThumbnailGenerated = reader.IsDBNull(reader.GetOrdinal("thumbnail_generated")) ? null : reader.GetDateTime(reader.GetOrdinal("thumbnail_generated")),
                Rating = reader.IsDBNull(reader.GetOrdinal("rating")) ? null : reader.GetInt32(reader.GetOrdinal("rating"))
            };
        }

        /// <summary>
        /// サムネイルデータを除外したMangaFileオブジェクトをDataReaderからマッピングします。
        /// ThumbnailDataは明示的にnullに設定され、既存のサムネイル遅延読み込み機能と連携します。
        /// </summary>
        /// <param name="reader">SQLiteDataReader</param>
        /// <returns>サムネイルデータを除外したMangaFileオブジェクト</returns>
        private MangaFile MapFromReaderWithoutThumbnail(IDataReader reader)
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
                
                // サムネイル関連
                ThumbnailData = null, // 明示的にnullを設定（既存のLazyThumbnailConverterOptimizedと連携）
                ThumbnailGenerated = reader.IsDBNull(reader.GetOrdinal("thumbnail_generated")) ? null : reader.GetDateTime(reader.GetOrdinal("thumbnail_generated")),
                Rating = reader.IsDBNull(reader.GetOrdinal("rating")) ? null : reader.GetInt32(reader.GetOrdinal("rating"))
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
            
            command.Parameters.AddWithValue("@thumbnailData", (object?)manga.ThumbnailData ?? DBNull.Value);
            command.Parameters.AddWithValue("@thumbnailGenerated", (object?)manga.ThumbnailGenerated ?? DBNull.Value);
            command.Parameters.AddWithValue("@rating", (object?)manga.Rating ?? DBNull.Value);
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
                            thumbnail_data, thumbnail_generated, rating
                        ) VALUES (
                            @filePath, @fileName, @fileSize, @createdDate, @modifiedDate, @fileType, @isCorrupted,
                            @title, @originalAuthor, @artist, @authorReading, @volumeNumber, @volumeString,
                            @genre, @publishDate, @publisher, @tags, @isAIProcessed,
                            @thumbnailData, @thumbnailGenerated, @rating
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
            // バイナリ保存に移行したため、サムネイルパスは存在しない
            // 後方互換性のためメソッドは残すが、空のリストを返す
            await Task.CompletedTask;
            _logger.LogDebug("サムネイルパス取得: バイナリ保存のため空のリストを返します");
            return new List<string>();
        }

        public async Task ClearAllAsync()
        {
            try
            {
                // データベースをクリア（サムネイルデータも自動的に削除される）
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "DELETE FROM manga_files";
                using var command = new SQLiteCommand(sql, connection);
                
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("すべてのファイルとサムネイルデータを削除しました");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "すべてのファイル削除中にエラーが発生しました");
                throw;
            }
        }

        /// <summary>
        /// 評価による検索をサポート（パフォーマンス最適化版）
        /// </summary>
        public async Task<List<MangaFile>> SearchByRatingAsync(int? rating)
        {
            var files = new List<MangaFile>();
            var startTime = DateTime.Now;

            try
            {
                // 入力検証
                if (rating.HasValue && (rating.Value < 1 || rating.Value > 5))
                {
                    throw new ArgumentOutOfRangeException(nameof(rating), "評価は1-5の範囲で指定してください");
                }

                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                // データベース接続の健全性チェック
                if (!await ValidateDatabaseConnectionAsync(connection))
                {
                    throw new InvalidOperationException("データベース接続が無効です。");
                }

                string sql;
                SQLiteCommand command;

                if (rating.HasValue)
                {
                    // インデックスを活用した高速検索
                    sql = "SELECT * FROM manga_files WHERE rating = @rating ORDER BY file_name";
                    command = new SQLiteCommand(sql, connection);
                    command.Parameters.AddWithValue("@rating", rating.Value);
                }
                else
                {
                    sql = "SELECT * FROM manga_files WHERE rating IS NULL ORDER BY file_name";
                    command = new SQLiteCommand(sql, connection);
                }

                using (command)
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        files.Add(MapFromReader(reader));
                    }
                }

                var duration = DateTime.Now - startTime;
                _logger.LogDebug("評価による検索完了: Rating={Rating}, 結果={Count}件, 処理時間={Duration:F3}秒", 
                    rating, files.Count, duration.TotalSeconds);

                // パフォーマンス要件チェック（500ms以内）
                if (duration.TotalMilliseconds > 500)
                {
                    _logger.LogWarning("評価検索のパフォーマンス要件を満たしていません。処理時間: {Duration:F3}秒", 
                        duration.TotalSeconds);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                throw; // 入力検証エラーはそのまま再スロー
            }
            catch (InvalidOperationException)
            {
                throw; // データベース接続エラーはそのまま再スロー
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "評価による検索中にエラーが発生しました: Rating={Rating}", rating);
                throw new InvalidOperationException($"評価による検索に失敗しました。Rating={rating}", ex);
            }

            return files;
        }

        /// <summary>
        /// 複数ファイルの評価を一括更新（パフォーマンス最適化版）
        /// </summary>
        public async Task UpdateRatingBatchAsync(IEnumerable<int> fileIds, int? rating)
        {
            var ids = fileIds.ToList();
            if (ids.Count == 0) return;

            const int batchSize = 1000; // バッチサイズを制限してメモリ使用量を最適化
            var totalProcessed = 0;
            var startTime = DateTime.Now;

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                // データベース接続の健全性チェック
                if (!await ValidateDatabaseConnectionAsync(connection))
                {
                    throw new InvalidOperationException("データベース接続が無効です。");
                }

                // 大量データの場合はバッチに分割して処理
                for (int i = 0; i < ids.Count; i += batchSize)
                {
                    var batch = ids.Skip(i).Take(batchSize).ToList();
                    
                    var affectedRows = await ExecuteInTransactionAsync(connection, async (transaction) =>
                    {
                        // IN句を使用した一括更新でパフォーマンス向上
                        var placeholders = string.Join(",", batch.Select((_, index) => $"@id{index}"));
                        var sql = $"UPDATE manga_files SET rating = @rating WHERE id IN ({placeholders})";
                        
                        using var command = new SQLiteCommand(sql, connection, transaction);
                        command.Parameters.AddWithValue("@rating", (object?)rating ?? DBNull.Value);
                        
                        for (int j = 0; j < batch.Count; j++)
                        {
                            command.Parameters.AddWithValue($"@id{j}", batch[j]);
                        }
                        
                        return await command.ExecuteNonQueryAsync();
                    });
                    
                    // 更新された行数をチェック
                    if (affectedRows != batch.Count)
                    {
                        _logger.LogWarning("一部のファイルの評価更新に失敗しました。期待値: {Expected}, 実際: {Actual}", 
                            batch.Count, affectedRows);
                        
                        // 部分的な失敗でも処理を継続するが、警告を記録
                        if (affectedRows == 0)
                        {
                            throw new InvalidOperationException(
                                $"バッチ更新で対象レコードが見つかりませんでした。バッチ範囲: {i}-{Math.Min(i + batchSize - 1, ids.Count - 1)}");
                        }
                    }

                    totalProcessed += batch.Count;
                    _logger.LogDebug("評価バッチ更新完了: {Processed}/{Total}件", totalProcessed, ids.Count);
                }

                var duration = DateTime.Now - startTime;
                _logger.LogInformation("評価を一括更新しました: {Count}件, Rating={Rating}, 処理時間: {Duration:F2}秒", 
                    ids.Count, rating, duration.TotalSeconds);
                
                // パフォーマンス要件チェック（5秒以内）
                if (duration.TotalSeconds > 5.0 && ids.Count >= 1000)
                {
                    _logger.LogWarning("大量ファイル処理のパフォーマンス要件を満たしていません。処理時間: {Duration:F2}秒, ファイル数: {Count}件", 
                        duration.TotalSeconds, ids.Count);
                }
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                _logger.LogError(ex, "評価一括更新中にエラーが発生しました: {Count}件, Rating={Rating}", ids.Count, rating);
                throw new InvalidOperationException(
                    $"評価の一括更新に失敗しました。対象ファイル数: {ids.Count}件", ex);
            }
        }

        /// <summary>
        /// データベース接続の健全性チェック
        /// </summary>
        private async Task<bool> ValidateDatabaseConnectionAsync(SQLiteConnection connection)
        {
            try
            {
                var sql = "SELECT COUNT(*) FROM manga_files LIMIT 1";
                using var command = new SQLiteCommand(sql, connection);
                await command.ExecuteScalarAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "データベース接続の検証に失敗しました");
                return false;
            }
        }

        /// <summary>
        /// トランザクション実行のヘルパーメソッド
        /// </summary>
        private async Task<T> ExecuteInTransactionAsync<T>(SQLiteConnection connection, Func<SQLiteTransaction, Task<T>> operation)
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                var result = await operation(transaction);
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<FileSizeStatistics> GetFileSizeStatisticsAsync()
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = @"
                    SELECT 
                        COUNT(*) as TotalFileCount,
                        COALESCE(SUM(file_size), 0) as TotalFileSize,
                        COUNT(CASE WHEN is_ai_processed = 1 THEN 1 END) as AIProcessedCount
                    FROM manga_files";

                using var command = new SQLiteCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new FileSizeStatistics
                    {
                        TotalFileCount = reader.GetInt32("TotalFileCount"),
                        TotalFileSize = reader.GetInt64("TotalFileSize"),
                        AIProcessedCount = reader.GetInt32("AIProcessedCount")
                    };
                }

                return new FileSizeStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイルサイズ統計取得中にエラーが発生しました");
                throw;
            }
        }

        public async Task<Dictionary<string, FolderStatistics>> GetFolderStatisticsAsync()
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                // すべてのファイルを取得してC#でフォルダパスを抽出
                var sql = @"SELECT file_path, file_size FROM manga_files";

                using var command = new SQLiteCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                var folderStats = new Dictionary<string, FolderStatistics>();
                
                while (await reader.ReadAsync())
                {
                    var filePath = reader.GetString("file_path");
                    var fileSize = reader.IsDBNull("file_size") ? 0L : reader.GetInt64("file_size");
                    
                    // C#でフォルダパスを抽出
                    var folderPath = Path.GetDirectoryName(filePath);
                    if (string.IsNullOrEmpty(folderPath))
                        continue;
                    
                    // 統計情報を更新
                    if (!folderStats.ContainsKey(folderPath))
                    {
                        folderStats[folderPath] = new FolderStatistics
                        {
                            FolderPath = folderPath,
                            FileCount = 0,
                            TotalSize = 0
                        };
                    }
                    
                    folderStats[folderPath].FileCount++;
                    folderStats[folderPath].TotalSize += fileSize;
                }

                _logger.LogDebug("フォルダ統計を取得しました: {Count}個のフォルダ", folderStats.Count);

                return folderStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フォルダ統計取得中にエラーが発生しました");
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

        /// <summary>
        /// データベースを最適化し、削除されたデータの領域を回収します
        /// </summary>
        /// <returns>最適化前後のファイルサイズ情報</returns>
        public async Task<(long BeforeSize, long AfterSize)> OptimizeDatabaseAsync()
        {
            long beforeSize = 0;
            long afterSize = 0;
            
            try
            {
                // データベースファイルのパスを取得
                var connectionStringBuilder = new SQLiteConnectionStringBuilder(_connectionString);
                var dbPath = connectionStringBuilder.DataSource;
                
                // 最適化前のファイルサイズを取得
                if (File.Exists(dbPath))
                {
                    beforeSize = new FileInfo(dbPath).Length;
                }
                
                _logger.LogInformation("データベース最適化を開始します。最適化前サイズ: {BeforeSize:N0} bytes", beforeSize);
                
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();
                
                // VACUUM実行（時間がかかる可能性があるため、タイムアウトを長めに設定）
                using var vacuumCommand = new SQLiteCommand("VACUUM", connection);
                vacuumCommand.CommandTimeout = 300; // 5分のタイムアウト
                await vacuumCommand.ExecuteNonQueryAsync();
                
                // 最適化後のファイルサイズを取得
                if (File.Exists(dbPath))
                {
                    afterSize = new FileInfo(dbPath).Length;
                }
                
                var savedSize = beforeSize - afterSize;
                var savedPercentage = beforeSize > 0 ? (double)savedSize / beforeSize * 100 : 0;
                
                _logger.LogInformation("データベース最適化が完了しました。最適化後サイズ: {AfterSize:N0} bytes, 削減サイズ: {SavedSize:N0} bytes ({SavedPercentage:F1}%)", 
                    afterSize, savedSize, savedPercentage);
                
                return (beforeSize, afterSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "データベース最適化中にエラーが発生しました");
                throw;
            }
        }

        /// <summary>
        /// 指定されたIDのサムネイルデータを取得します。
        /// 遅延読み込み機能で使用され、軽量読み込み後にサムネイル表示が必要な場合に呼び出されます。
        /// </summary>
        /// <param name="id">取得対象のファイルID</param>
        /// <returns>サムネイルデータ（存在しない場合はnull）</returns>
        public async Task<byte[]?> GetThumbnailDataByIdAsync(int id)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT thumbnail_data FROM manga_files WHERE id = @id";
                using var command = new SQLiteCommand(sql, connection);
                command.Parameters.AddWithValue("@id", id);
                
                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    if (reader.IsDBNull("thumbnail_data"))
                    {
                        _logger.LogDebug("サムネイルデータが存在しません: ID={Id}", id);
                        return null;
                    }
                    
                    var thumbnailData = (byte[])reader["thumbnail_data"];
                    _logger.LogDebug("サムネイルデータを取得しました: ID={Id}, Size={Size} bytes", id, thumbnailData.Length);
                    return thumbnailData;
                }
                
                _logger.LogDebug("指定されたIDのレコードが見つかりません: ID={Id}", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "サムネイルデータ取得中にエラーが発生しました: ID={Id}", id);
                throw;
            }
        }

        /// <summary>
        /// 複数のIDのサムネイルデータを一括取得します。
        /// パフォーマンス最適化のため、最大100件ずつ処理します。
        /// </summary>
        /// <param name="ids">取得対象のファイルIDリスト</param>
        /// <returns>IDとサムネイルデータのディクショナリ</returns>
        public async Task<Dictionary<int, byte[]>> GetThumbnailDataBatchAsync(IEnumerable<int> ids)
        {
            var result = new Dictionary<int, byte[]>();
            var idList = ids.ToList();
            
            if (idList.Count == 0)
            {
                _logger.LogDebug("バッチサムネイル取得: IDリストが空です");
                return result;
            }

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                await connection.OpenAsync();

                // 最大100件ずつ処理してパフォーマンスを最適化
                const int batchSize = 100;
                for (int i = 0; i < idList.Count; i += batchSize)
                {
                    var batchIds = idList.Skip(i).Take(batchSize).ToList();
                    var placeholders = string.Join(",", batchIds.Select((_, index) => $"@id{index}"));
                    
                    var sql = $"SELECT id, thumbnail_data FROM manga_files WHERE id IN ({placeholders}) AND thumbnail_data IS NOT NULL";
                    using var command = new SQLiteCommand(sql, connection);
                    
                    // パラメータを追加
                    for (int j = 0; j < batchIds.Count; j++)
                    {
                        command.Parameters.AddWithValue($"@id{j}", batchIds[j]);
                    }
                    
                    using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var id = reader.GetInt32("id");
                        var thumbnailData = (byte[])reader["thumbnail_data"];
                        result[id] = thumbnailData;
                    }
                    
                    _logger.LogDebug("バッチサムネイル取得: {BatchStart}-{BatchEnd} ({BatchCount}件中{ResultCount}件取得)", 
                        i + 1, Math.Min(i + batchSize, idList.Count), batchIds.Count, result.Count - (result.Count - batchIds.Count));
                }

                _logger.LogDebug("バッチサムネイル取得完了: 要求{RequestCount}件中{ResultCount}件取得", idList.Count, result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "バッチサムネイルデータ取得中にエラーが発生しました: 要求件数={Count}", idList.Count);
                throw;
            }
        }
    }
}
