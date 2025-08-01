# Mangaanya API リファレンス

## 概要

このドキュメントは、Mangaanya アプリケーションの主要なクラス、インターフェース、メソッドの詳細な API リファレンスです。

## Models

### MangaFile クラス

漫画ファイルの情報を格納するメインモデルクラス。

```csharp
public class MangaFile : INotifyPropertyChanged
```

#### プロパティ

| プロパティ名 | 型 | 説明 |
|-------------|---|------|
| `Id` | `int` | データベースの主キー |
| `FilePath` | `string` | ファイルの完全パス |
| `FileName` | `string` | ファイル名 |
| `FolderPath` | `string` | 親フォルダのパス（読み取り専用） |
| `FileSize` | `long` | ファイルサイズ（バイト） |
| `FileSizeFormatted` | `string` | フォーマット済みファイルサイズ（読み取り専用） |
| `CreatedDate` | `DateTime` | ファイル作成日時 |
| `ModifiedDate` | `DateTime` | ファイル更新日時 |
| `FileType` | `string` | ファイル種類（拡張子） |
| `IsCorrupted` | `bool` | ファイル破損フラグ |
| `Title` | `string?` | 作品タイトル |
| `OriginalAuthor` | `string?` | 原作者 |
| `Artist` | `string?` | 作画者 |
| `AuthorReading` | `string?` | 作者名のよみがな |
| `VolumeNumber` | `int?` | 巻数（数値） |
| `VolumeString` | `string?` | 巻数（文字列） |
| `VolumeDisplay` | `string` | 表示用巻数（読み書き可能） |
| `Genre` | `string?` | ジャンル |
| `PublishDate` | `DateTime?` | 発行日 |
| `Publisher` | `string?` | 出版社 |
| `Tags` | `string?` | タグ（カンマ区切り） |
| `IsAIProcessed` | `bool` | AI処理済みフラグ |
| `ThumbnailPath` | `string?` | サムネイル画像のパス |
| `ThumbnailCreated` | `DateTime?` | サムネイル作成日時 |

#### メソッド

```csharp
// INotifyPropertyChanged の実装
public event PropertyChangedEventHandler? PropertyChanged;
protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null);
protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null);

// ヘルパーメソッド
private static string FormatFileSize(long bytes);
```

## Services

### IFileScannerService インターフェース

ファイルスキャン機能を提供するサービス。

```csharp
public interface IFileScannerService
{
    Task<ScanResult> PerformIncrementalScanAsync(IProgress<ScanProgress> progress);
    Task<ScanResult> PerformFullScanAsync(string folderPath, IProgress<ScanProgress> progress);
    Task<ParsedFileInfo> ParseFileNameAsync(string fileName);
}
```

#### メソッド

##### PerformIncrementalScanAsync
```csharp
Task<ScanResult> PerformIncrementalScanAsync(IProgress<ScanProgress> progress)
```
設定されたフォルダに対して増分スキャンを実行します。

**パラメータ:**
- `progress`: 進捗報告用のインターフェース

**戻り値:**
- `ScanResult`: スキャン結果

##### PerformFullScanAsync
```csharp
Task<ScanResult> PerformFullScanAsync(string folderPath, IProgress<ScanProgress> progress)
```
指定されたフォルダに対して全体スキャンを実行します。

**パラメータ:**
- `folderPath`: スキャン対象フォルダのパス
- `progress`: 進捗報告用のインターフェース

**戻り値:**
- `ScanResult`: スキャン結果

##### ParseFileNameAsync
```csharp
Task<ParsedFileInfo> ParseFileNameAsync(string fileName)
```
ファイル名から作品情報を解析します。

**パラメータ:**
- `fileName`: 解析対象のファイル名

**戻り値:**
- `ParsedFileInfo`: 解析結果

#### 関連クラス

##### ScanResult
```csharp
public class ScanResult
{
    public bool Success { get; set; }
    public int FilesProcessed { get; set; }
    public int FilesAdded { get; set; }
    public int FilesUpdated { get; set; }
    public int FilesRemoved { get; set; }
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}
```

##### ScanProgress
```csharp
public class ScanProgress
{
    public int CurrentFile { get; set; }
    public int TotalFiles { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
```

##### ParsedFileInfo
```csharp
public class ParsedFileInfo
{
    public string? Title { get; set; }
    public string? OriginalAuthor { get; set; }
    public string? Artist { get; set; }
    public string? AuthorReading { get; set; }
    public int? VolumeNumber { get; set; }
    public string? VolumeString { get; set; }
    public bool ParseSuccess { get; set; }
}
```

### IAIService インターフェース

AI による情報取得機能を提供するサービス。

```csharp
public interface IAIService : IDisposable
{
    Task<AIResult> GetMangaInfoAsync(string title, string author);
    Task<List<AIResult>> GetMangaInfoBatchAsync(List<MangaFile> files, int maxConcurrency = 30);
    bool IsApiAvailable();
    void ClearCache();
    void CleanupCache();
}
```

#### メソッド

##### GetMangaInfoAsync
```csharp
Task<AIResult> GetMangaInfoAsync(string title, string author)
```
単一の作品情報を AI から取得します。

**パラメータ:**
- `title`: 作品タイトル
- `author`: 作者名

**戻り値:**
- `AIResult`: AI取得結果

##### GetMangaInfoBatchAsync
```csharp
Task<List<AIResult>> GetMangaInfoBatchAsync(List<MangaFile> files, int maxConcurrency = 30)
```
複数の作品情報を一括で AI から取得します。

**パラメータ:**
- `files`: 対象ファイルのリスト
- `maxConcurrency`: 最大同時処理数（デフォルト: 30）

**戻り値:**
- `List<AIResult>`: AI取得結果のリスト

##### IsApiAvailable
```csharp
bool IsApiAvailable()
```
AI API が利用可能かどうかを確認します。

**戻り値:**
- `bool`: 利用可能な場合は `true`

##### ClearCache / CleanupCache
```csharp
void ClearCache()
void CleanupCache()
```
AI サービスのキャッシュをクリア/クリーンアップします。

#### 関連クラス

##### AIResult
```csharp
public class AIResult
{
    public bool Success { get; set; }
    public string? Genre { get; set; }
    public DateTime? PublishDate { get; set; }
    public string? Publisher { get; set; }
    public string? Tags { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### IMangaRepository インターフェース

データベースアクセス機能を提供するリポジトリ。

```csharp
public interface IMangaRepository
{
    Task<List<MangaFile>> GetAllAsync();
    Task<MangaFile?> GetByIdAsync(int id);
    Task<List<MangaFile>> SearchAsync(SearchCriteria criteria);
    Task<int> InsertAsync(MangaFile manga);
    Task<int> InsertBatchAsync(IEnumerable<MangaFile> mangaFiles);
    Task UpdateAsync(MangaFile manga);
    Task DeleteAsync(int id);
    Task<int> DeleteByFolderPathAsync(string folderPath);
    Task<int> GetTotalCountAsync();
    Task InitializeDatabaseAsync();
    Task ClearAllAsync();
}
```

#### メソッド

##### GetAllAsync
```csharp
Task<List<MangaFile>> GetAllAsync()
```
すべての漫画ファイル情報を取得します。

**戻り値:**
- `List<MangaFile>`: 漫画ファイルのリスト

##### GetByIdAsync
```csharp
Task<MangaFile?> GetByIdAsync(int id)
```
指定されたIDの漫画ファイル情報を取得します。

**パラメータ:**
- `id`: 取得対象のID

**戻り値:**
- `MangaFile?`: 漫画ファイル情報（見つからない場合は `null`）

##### SearchAsync
```csharp
Task<List<MangaFile>> SearchAsync(SearchCriteria criteria)
```
検索条件に基づいて漫画ファイルを検索します。

**パラメータ:**
- `criteria`: 検索条件

**戻り値:**
- `List<MangaFile>`: 検索結果のリスト

##### InsertAsync
```csharp
Task<int> InsertAsync(MangaFile manga)
```
新しい漫画ファイル情報を挿入します。

**パラメータ:**
- `manga`: 挿入する漫画ファイル情報

**戻り値:**
- `int`: 挿入されたレコードのID

##### InsertBatchAsync
```csharp
Task<int> InsertBatchAsync(IEnumerable<MangaFile> mangaFiles)
```
複数の漫画ファイル情報を一括挿入します。

**パラメータ:**
- `mangaFiles`: 挿入する漫画ファイル情報のコレクション

**戻り値:**
- `int`: 挿入されたレコード数

##### UpdateAsync
```csharp
Task UpdateAsync(MangaFile manga)
```
漫画ファイル情報を更新します。

**パラメータ:**
- `manga`: 更新する漫画ファイル情報

##### DeleteAsync
```csharp
Task DeleteAsync(int id)
```
指定されたIDの漫画ファイル情報を削除します。

**パラメータ:**
- `id`: 削除対象のID

##### DeleteByFolderPathAsync
```csharp
Task<int> DeleteByFolderPathAsync(string folderPath)
```
指定されたフォルダパス内のすべての漫画ファイル情報を削除します。

**パラメータ:**
- `folderPath`: 削除対象のフォルダパス

**戻り値:**
- `int`: 削除されたレコード数

##### GetTotalCountAsync
```csharp
Task<int> GetTotalCountAsync()
```
データベース内の総レコード数を取得します。

**戻り値:**
- `int`: 総レコード数

##### InitializeDatabaseAsync
```csharp
Task InitializeDatabaseAsync()
```
データベースを初期化します。

##### ClearAllAsync
```csharp
Task ClearAllAsync()
```
データベース内のすべてのレコードを削除します。

#### 関連クラス

##### SearchCriteria
```csharp
public class SearchCriteria
{
    public string? SearchText { get; set; }
    public bool? IsAIProcessed { get; set; }
    public bool? IsCorrupted { get; set; }
    public DateTime? ModifiedAfter { get; set; }
    public DateTime? ModifiedBefore { get; set; }
    public string? Genre { get; set; }
    public string? Publisher { get; set; }
}
```

### IThumbnailService インターフェース

サムネイル生成機能を提供するサービス。

```csharp
public interface IThumbnailService
{
    Task<ThumbnailGenerationResult> GenerateThumbnailAsync(MangaFile mangaFile);
    Task<List<ThumbnailGenerationResult>> GenerateThumbnailsBatchAsync(
        IEnumerable<MangaFile> mangaFiles, 
        IProgress<ThumbnailProgress>? progress = null,
        CancellationToken cancellationToken = default,
        bool skipExisting = true);
    bool ThumbnailExists(string? thumbnailPath);
    Task<bool> DeleteThumbnailAsync(string? thumbnailPath);
    string GetDefaultThumbnailPath();
}
```

#### メソッド

##### GenerateThumbnailAsync
```csharp
Task<ThumbnailGenerationResult> GenerateThumbnailAsync(MangaFile mangaFile)
```
指定されたファイルのサムネイルを生成します。

**パラメータ:**
- `mangaFile`: サムネイルを生成する漫画ファイル

**戻り値:**
- `ThumbnailGenerationResult`: サムネイル生成結果

##### GenerateThumbnailsBatchAsync
```csharp
Task<List<ThumbnailGenerationResult>> GenerateThumbnailsBatchAsync(
    IEnumerable<MangaFile> mangaFiles, 
    IProgress<ThumbnailProgress>? progress = null,
    CancellationToken cancellationToken = default,
    bool skipExisting = true)
```
複数のファイルのサムネイルを一括生成します。

**パラメータ:**
- `mangaFiles`: サムネイルを生成する漫画ファイルのリスト
- `progress`: 進捗報告用（オプション）
- `cancellationToken`: キャンセルトークン
- `skipExisting`: 既存のサムネイルをスキップするかどうか

**戻り値:**
- `List<ThumbnailGenerationResult>`: サムネイル生成結果のリスト

##### ThumbnailExists
```csharp
bool ThumbnailExists(string? thumbnailPath)
```
サムネイルファイルが存在するかチェックします。

**パラメータ:**
- `thumbnailPath`: サムネイルファイルのパス

**戻り値:**
- `bool`: 存在する場合は `true`

##### DeleteThumbnailAsync
```csharp
Task<bool> DeleteThumbnailAsync(string? thumbnailPath)
```
サムネイルファイルを削除します。

**パラメータ:**
- `thumbnailPath`: 削除するサムネイルファイルのパス

**戻り値:**
- `bool`: 削除に成功した場合は `true`

##### GetDefaultThumbnailPath
```csharp
string GetDefaultThumbnailPath()
```
デフォルトのダミー画像パスを取得します。

**戻り値:**
- `string`: ダミー画像のパス

#### 関連クラス

##### ThumbnailGenerationResult
```csharp
public class ThumbnailGenerationResult
{
    public bool Success { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? ErrorMessage { get; set; }
    public MangaFile? MangaFile { get; set; }
}
```

##### ThumbnailProgress
```csharp
public class ThumbnailProgress
{
    public int CurrentFile { get; set; }
    public int TotalFiles { get; set; }
    public string CurrentFileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
```

### IConfigurationManager インターフェース

設定管理機能を提供するサービス。

```csharp
public interface IConfigurationManager
{
    T GetSetting<T>(string key, T defaultValue = default!);
    void SetSetting<T>(string key, T value);
    Task SaveAsync();
    Task LoadAsync();
}
```

#### メソッド

##### GetSetting<T>
```csharp
T GetSetting<T>(string key, T defaultValue = default!)
```
指定されたキーの設定値を取得します。

**パラメータ:**
- `key`: 設定キー
- `defaultValue`: デフォルト値

**戻り値:**
- `T`: 設定値

##### SetSetting<T>
```csharp
void SetSetting<T>(string key, T value)
```
指定されたキーに設定値を保存します。

**パラメータ:**
- `key`: 設定キー
- `value`: 設定値

##### SaveAsync
```csharp
Task SaveAsync()
```
設定をファイルに保存します。

##### LoadAsync
```csharp
Task LoadAsync()
```
設定をファイルから読み込みます。

## ViewModels

### MainViewModel クラス

メイン画面のビジネスロジックを担当するビューモデル。

```csharp
public partial class MainViewModel : ObservableObject
```

#### 主要プロパティ

| プロパティ名 | 型 | 説明 |
|-------------|---|------|
| `MangaFiles` | `ObservableCollection<MangaFile>` | 表示中の漫画ファイルリスト |
| `SelectedMangaFile` | `MangaFile?` | 選択中の漫画ファイル |
| `SelectedMangaFiles` | `ObservableCollection<MangaFile>` | 複数選択中の漫画ファイル |
| `SearchText` | `string` | 検索テキスト |
| `StatusMessage` | `string` | ステータスメッセージ |
| `FileCountMessage` | `string` | ファイル数メッセージ |
| `IsProcessing` | `bool` | 処理中フラグ |
| `ShowThumbnails` | `bool` | サムネイル表示フラグ |
| `ScanFolders` | `ObservableCollection<string>` | スキャン対象フォルダリスト |
| `SelectedScanFolder` | `string?` | 選択中のスキャンフォルダ |
| `IncludeSubfolders` | `bool` | サブフォルダ含有フラグ |

#### 主要コマンド

| コマンド名 | 説明 |
|-----------|------|
| `SelectFolderCommand` | フォルダ選択ダイアログを表示 |
| `ScanFolderCommand` | フォルダの再スキャンを実行 |
| `BulkEditCommand` | 一括タグ取得を実行 |
| `DeleteFilesCommand` | 選択ファイルの削除を実行 |
| `GenerateThumbnailsCommand` | サムネイル生成を実行 |
| `OpenSettingsCommand` | 設定画面を開く |
| `DeleteFoldersCommand` | フォルダ削除を実行 |
| `ClearFolderFilterCommand` | フォルダフィルタをクリア |

#### 主要メソッド

##### StartInitialization
```csharp
public void StartInitialization()
```
ビューモデルの初期化を開始します。

##### LoadMangaFilesAsync
```csharp
private async Task LoadMangaFilesAsync()
```
データベースから漫画ファイル情報を読み込みます。

##### UpdateFileCountMessage
```csharp
private void UpdateFileCountMessage()
```
ファイル数メッセージを更新します。

#### イベント

##### InitializationCompleted
```csharp
public event EventHandler? InitializationCompleted;
```
初期化完了時に発生するイベント。

### SettingsViewModel クラス

設定画面のビジネスロジックを担当するビューモデル。

```csharp
public partial class SettingsViewModel : ObservableObject
```

#### 主要プロパティ

| プロパティ名 | 型 | 説明 |
|-------------|---|------|
| `MaxMemoryUsage` | `long` | 最大メモリ使用量 |
| `CacheSize` | `long` | キャッシュサイズ |
| `MaxConcurrentAIRequests` | `int` | AI同時処理数 |
| `ShowThumbnails` | `bool` | サムネイル表示設定 |
| `GeminiApiKey` | `string` | Gemini APIキー |
| `FileNameRegexPattern` | `string` | ファイル名解析用正規表現 |
| `MangaViewerPath` | `string` | 漫画ビューアのパス |
| `ColumnSettings` | `ObservableCollection<ColumnVisibilityItem>` | 列表示設定 |
| `IsModified` | `bool` | 変更フラグ |

#### 主要コマンド

| コマンド名 | 説明 |
|-----------|------|
| `SaveSettingsCommand` | 設定を保存 |
| `ResetSettingsCommand` | 設定をデフォルトに戻す |
| `CancelCommand` | 設定画面を閉じる |
| `ResetRegexCommand` | 正規表現をデフォルトに戻す |
| `TestRegexCommand` | 正規表現をテスト |
| `SelectMangaViewerCommand` | 漫画ビューアを選択 |

#### ヘルパーメソッド

##### FormatMemorySize
```csharp
public static string FormatMemorySize(long bytes)
```
バイト数を人間が読みやすい形式に変換します。

##### ParseMemorySize
```csharp
public static long ParseMemorySize(string formattedSize)
```
フォーマット済みサイズをバイト数に変換します。

## Converters

### MemorySizeConverter クラス

メモリサイズの表示変換を行うコンバーター。

```csharp
public class MemorySizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
}
```

### ThumbnailPathConverter クラス

サムネイルパスの表示変換を行うコンバーター。

```csharp
public class ThumbnailPathConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
}
```

## 使用例

### 基本的な使用パターン

#### ファイルスキャンの実行
```csharp
var progress = new Progress<ScanProgress>(p => 
{
    Console.WriteLine($"進捗: {p.CurrentFile}/{p.TotalFiles} - {p.CurrentFileName}");
});

var result = await _fileScannerService.PerformFullScanAsync(@"C:\Manga", progress);

if (result.Success)
{
    Console.WriteLine($"スキャン完了: {result.FilesAdded}件追加");
}
else
{
    Console.WriteLine($"エラー: {string.Join(", ", result.Errors)}");
}
```

#### AI情報取得の実行
```csharp
if (_aiService.IsApiAvailable())
{
    var result = await _aiService.GetMangaInfoAsync("ワンピース", "尾田栄一郎");
    
    if (result.Success)
    {
        Console.WriteLine($"タグ: {result.Tags}");
        Console.WriteLine($"発行日: {result.PublishDate}");
    }
}
```

#### データベース操作
```csharp
// 全件取得
var allFiles = await _repository.GetAllAsync();

// 検索
var criteria = new SearchCriteria
{
    SearchText = "ワンピース",
    IsAIProcessed = true
};
var searchResults = await _repository.SearchAsync(criteria);

// 挿入
var newFile = new MangaFile
{
    FilePath = @"C:\Manga\onepiece_01.zip",
    FileName = "onepiece_01.zip",
    Title = "ワンピース"
};
var id = await _repository.InsertAsync(newFile);
```

#### 設定の読み書き
```csharp
// 設定の取得
var maxMemory = _config.GetSetting<long>("MaxMemoryUsage", 8L * 1024 * 1024 * 1024);

// 設定の変更
_config.SetSetting("MaxMemoryUsage", 16L * 1024 * 1024 * 1024);
await _config.SaveAsync();
```

---

*このAPIリファレンスは、Mangaanya v1.0 の実装を基に作成されています。*