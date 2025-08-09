using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using Mangaanya.Models;

namespace Mangaanya.Services
{
    public class ConfigurationManager : IConfigurationManager, ISettingsChangedNotifier
    {
        private readonly ILogger<ConfigurationManager> _logger;
        private readonly string _configFilePath;
        private Dictionary<string, object> _settings;

        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public ConfigurationManager(ILogger<ConfigurationManager> logger)
        {
            _logger = logger;
            
            // 単一ファイル配布時の正しいパス取得
            var baseDirectory = GetApplicationDirectory();
            _configFilePath = Path.Combine(baseDirectory, "config", "settings.json");
            _settings = new Dictionary<string, object>();
            
            // 設定フォルダの作成
            var configDir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
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

        public T GetSetting<T>(string key, T defaultValue = default!)
        {
            try
            {
                if (_settings.TryGetValue(key, out var value))
                {
                    if (value is T directValue)
                        return directValue;

                    // JSON文字列からの変換を試行
                    if (value is string jsonString)
                    {
                        try
                        {
                            var deserializedValue = JsonConvert.DeserializeObject<T>(jsonString);
                            return deserializedValue ?? defaultValue;
                        }
                        catch
                        {
                            // JSON変換に失敗した場合は型変換を試行
                            return (T)Convert.ChangeType(value, typeof(T));
                        }
                    }

                    // 型変換を試行
                    return (T)Convert.ChangeType(value, typeof(T));
                }

                return defaultValue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "設定値の取得に失敗しました: {Key}", key);
                return defaultValue;
            }
        }

        public void SetSetting<T>(string key, T value)
        {
            try
            {
                if (value == null)
                {
                    _settings.Remove(key);
                }
                else
                {
                    _settings[key] = value;
                }

                // 設定変更通知を発行
                NotifySettingsChanged(key);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定値の設定に失敗しました: {Key}", key);
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                // 設定ディレクトリの存在確認
                var configDir = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir!);
                    _logger.LogInformation("設定ディレクトリを作成しました: {Path}", configDir);
                }
                
                // バックアップを作成（既存ファイルがある場合）
                if (File.Exists(_configFilePath))
                {
                    try
                    {
                        var backupPath = _configFilePath + ".bak";
                        File.Copy(_configFilePath, backupPath, true);
                        
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "設定ファイルのバックアップ作成に失敗しました");
                    }
                }
                
                // JsonSerializerSettingsを使用して適切な型変換を設定
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented,
                    Converters = { 
                        new ListStringJsonConverter(),
                        new DataGridColumnSettingsJsonConverter()
                    }
                };
                
                var json = JsonConvert.SerializeObject(_settings, settings);
                
                // 一時ファイルに書き込んでから移動（書き込み中のクラッシュ対策）
                var tempPath = _configFilePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, _configFilePath, true);
                
                _logger.LogInformation("設定ファイルを保存しました: {Path}", _configFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定ファイルの保存に失敗しました: {Path}", _configFilePath);
                throw;
            }
        }

        public async Task LoadAsync()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = await File.ReadAllTextAsync(_configFilePath);
                    
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        _logger.LogWarning("設定ファイルが空です。デフォルト設定を使用します: {Path}", _configFilePath);
                        InitializeDefaultSettings();
                        await SaveAsync();
                        return;
                    }
                    
                    try
                    {
                        // JsonSerializerSettingsを使用して適切な型変換を設定
                        var settings = new JsonSerializerSettings
                        {
                            TypeNameHandling = TypeNameHandling.None,
                            NullValueHandling = NullValueHandling.Ignore,
                            Converters = { 
                                new ListStringJsonConverter(),
                                new DataGridColumnSettingsJsonConverter()
                            }
                        };
                        
                        var loadedSettings = JsonConvert.DeserializeObject<Dictionary<string, object>>(json, settings);
                        
                        if (loadedSettings != null)
                        {
                            _settings = loadedSettings;
                            _logger.LogInformation("設定ファイルを読み込みました: {Path}", _configFilePath);
                            
                            // 特定の設定値の型を修正
                            FixSettingsTypes();
                            
                            // ColumnVisibilityからDataGridSettingsへの移行処理
                            await MigrateColumnVisibilityToDataGridSettingsAsync();
                        }
                        else
                        {
                            _logger.LogWarning("設定ファイルの解析に失敗しました。デフォルト設定を使用します: {Path}", _configFilePath);
                            InitializeDefaultSettings();
                            await SaveAsync();
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "設定ファイルのJSON解析に失敗しました: {Path}", _configFilePath);
                        
                        // バックアップを作成
                        var backupPath = _configFilePath + ".bak";
                        try
                        {
                            File.Copy(_configFilePath, backupPath, true);
                            _logger.LogInformation("破損した設定ファイルのバックアップを作成しました: {BackupPath}", backupPath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "設定ファイルのバックアップ作成に失敗しました");
                        }
                        
                        // デフォルト設定を使用
                        InitializeDefaultSettings();
                        await SaveAsync();
                    }
                }
                else
                {
                    // デフォルト設定の初期化
                    InitializeDefaultSettings();
                    await SaveAsync();
                    _logger.LogInformation("デフォルト設定ファイルを作成しました: {Path}", _configFilePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "設定ファイルの読み込みに失敗しました: {Path}", _configFilePath);
                InitializeDefaultSettings();
            }
        }
        
        private async Task MigrateColumnVisibilityToDataGridSettingsAsync()
        {
            try
            {
                // ColumnVisibilityが存在する場合は移行処理を実行
                if (_settings.TryGetValue("ColumnVisibility", out var columnVisibilityObj))
                {
                    _logger.LogInformation("ColumnVisibilityが検出されました。移行処理を開始します");
                    
                    var dataGridSettings = GetSetting<DataGridSettings>("DataGridSettings", new DataGridSettings());
                    bool needsMigration = false;
                    
                    // DataGridSettingsが空の場合は移行が必要
                    if (dataGridSettings.Columns.Count == 0)
                    {
                        needsMigration = true;
                        _logger.LogInformation("DataGridSettingsが空のため、ColumnVisibilityから移行します");
                        
                        // デフォルトのDataGridSettingsを作成
                        dataGridSettings = GetDefaultDataGridSettingsForMigration();
                        
                        // ColumnVisibilityの値をDataGridSettingsに反映
                        if (columnVisibilityObj is Dictionary<string, object> columnVisibilityDict)
                        {
                            foreach (var columnSetting in dataGridSettings.Columns)
                            {
                                if (columnVisibilityDict.TryGetValue(columnSetting.Header, out var visibilityObj) &&
                                    visibilityObj is bool isVisible)
                                {
                                    columnSetting.IsVisible = isVisible;
                                    _logger.LogInformation("移行: {Column} = {Visible}", columnSetting.Header, isVisible);
                                }
                            }
                        }
                        
                        // DataGridSettingsを保存
                        SetSetting("DataGridSettings", dataGridSettings);
                    }
                    else
                    {
                        _logger.LogInformation("DataGridSettingsが既に存在するため、ColumnVisibilityのみ削除します");
                    }
                    
                    // ColumnVisibilityを削除（DataGridSettingsの有無に関係なく）
                    _settings.Remove("ColumnVisibility");
                    
                    // 設定を保存
                    await SaveAsync();
                    
                    _logger.LogInformation("ColumnVisibility移行処理が完了しました（移行: {NeedsMigration}）", needsMigration);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ColumnVisibilityからDataGridSettingsへの移行中にエラーが発生しました");
            }
        }

        public void NotifySettingsChanged(string settingName)
        {
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(settingName));
        }
        
        private DataGridSettings GetDefaultDataGridSettingsForMigration()
        {
            return new DataGridSettings
            {
                Columns = new List<DataGridColumnSettings>
                {
                    new DataGridColumnSettings { Header = "サムネイル", Width = 190, DisplayIndex = 0, IsVisible = true },
                    new DataGridColumnSettings { Header = "パス", Width = 300, DisplayIndex = 1, IsVisible = true },
                    new DataGridColumnSettings { Header = "ファイル名", Width = 200, DisplayIndex = 2, IsVisible = true },
                    new DataGridColumnSettings { Header = "サイズ", Width = 80, DisplayIndex = 3, IsVisible = true },
                    new DataGridColumnSettings { Header = "種類", Width = 60, DisplayIndex = 4, IsVisible = true },
                    new DataGridColumnSettings { Header = "作成日時", Width = 120, DisplayIndex = 5, IsVisible = true },
                    new DataGridColumnSettings { Header = "更新日時", Width = 120, DisplayIndex = 6, IsVisible = true },
                    new DataGridColumnSettings { Header = "かな", Width = 100, DisplayIndex = 7, IsVisible = true },
                    new DataGridColumnSettings { Header = "原作者", Width = 100, DisplayIndex = 8, IsVisible = true },
                    new DataGridColumnSettings { Header = "作画者", Width = 100, DisplayIndex = 9, IsVisible = true },
                    new DataGridColumnSettings { Header = "タイトル", Width = 150, DisplayIndex = 10, IsVisible = true },
                    new DataGridColumnSettings { Header = "巻数", Width = 60, DisplayIndex = 11, IsVisible = true },
                    new DataGridColumnSettings { Header = "タグ", Width = 200, DisplayIndex = 12, IsVisible = false },
                    new DataGridColumnSettings { Header = "ジャンル", Width = 100, DisplayIndex = 13, IsVisible = false },
                    new DataGridColumnSettings { Header = "出版社", Width = 100, DisplayIndex = 14, IsVisible = false },
                    new DataGridColumnSettings { Header = "発行日", Width = 100, DisplayIndex = 15, IsVisible = false },
                    new DataGridColumnSettings { Header = "評価", Width = 80, DisplayIndex = 16, IsVisible = true },
                    new DataGridColumnSettings { Header = "タグ取得済", Width = 80, DisplayIndex = 17, IsVisible = false }
                }
            };
        }
        
        private void FixSettingsTypes()
        {
            // ScanFoldersがJArrayとして読み込まれた場合、List<string>に変換
            if (_settings.TryGetValue("ScanFolders", out var scanFoldersObj) && 
                scanFoldersObj is Newtonsoft.Json.Linq.JArray jArray)
            {
                var folderList = new List<string>();
                foreach (var item in jArray)
                {
                    if (item.ToString() is string folder)
                    {
                        folderList.Add(folder);
                    }
                }
                _settings["ScanFolders"] = folderList;
                
            }
            
            // DataGridSettingsがJObjectとして読み込まれた場合、DataGridSettingsに変換
            if (_settings.TryGetValue("DataGridSettings", out var dataGridSettingsObj) && 
                dataGridSettingsObj is Newtonsoft.Json.Linq.JObject jObject)
            {
                try
                {
                    var settings = new Models.DataGridSettings();
                    var columns = jObject["Columns"] as Newtonsoft.Json.Linq.JArray;
                    
                    if (columns != null)
                    {
                        foreach (var column in columns)
                        {
                            var columnSetting = new Models.DataGridColumnSettings
                            {
                                Header = column["Header"]?.ToString() ?? string.Empty,
                                Width = column["Width"] != null ? (double)column["Width"]! : 100,
                                DisplayIndex = column["DisplayIndex"] != null ? (int)column["DisplayIndex"]! : 0,
                                IsVisible = column["IsVisible"] != null ? (bool)column["IsVisible"]! : true
                            };
                            
                            settings.Columns.Add(columnSetting);
                        }
                    }
                    
                    _settings["DataGridSettings"] = settings;
                    
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DataGridSettingsの変換に失敗しました");
                }
            }
        }

        private void InitializeDefaultSettings()
        {
            var defaultSettings = new AppSettings();
            
            SetSetting("MaxMemoryUsage", defaultSettings.MaxMemoryUsage);
            SetSetting("CacheSize", defaultSettings.CacheSize);
            SetSetting("MaxConcurrentAIRequests", defaultSettings.MaxConcurrentAIRequests);
            SetSetting("ShowThumbnails", defaultSettings.ShowThumbnails);
            SetSetting("ScanFolders", defaultSettings.ScanFolders);
            SetSetting("GeminiApiKey", defaultSettings.GeminiApiKey);
            SetSetting("DataGridSettings", defaultSettings.DataGridSettings);

            _logger.LogInformation("デフォルト設定を初期化しました");
        }
    }
}
