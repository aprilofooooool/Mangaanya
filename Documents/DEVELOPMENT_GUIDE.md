# Mangaanya 開発ガイド

## 開発環境セットアップ

### 必要な環境
- **Visual Studio 2022** (Community以上)
- **.NET 9.0 SDK**
- **Windows 10/11** (WPF アプリケーションのため)

### プロジェクト構成

```
Mangaanya/
├── App.xaml                    # アプリケーション定義
├── App.xaml.cs                 # アプリケーション起動処理
├── MainWindow.xaml             # メインウィンドウ UI
├── MainWindow.xaml.cs          # メインウィンドウ コードビハインド
├── Program.cs                  # 参考用（WPFでは未使用）
├── Mangaanya.csproj           # プロジェクトファイル
├── Mangaanya.ico              # アプリケーションアイコン
├── Converters/                # 値変換器
│   ├── MemorySizeConverter.cs
│   └── ThumbnailPathConverter.cs
├── Models/                    # データモデル
│   ├── MangaFile.cs
│   ├── DataGridColumnSettings.cs
│   └── ClearAttributesParameters.cs
├── Services/                  # サービス層
│   ├── Interfaces/           # サービスインターフェース
│   ├── AIService.cs
│   ├── ConfigurationManager.cs
│   ├── FileScannerService.cs
│   ├── MangaRepository.cs
│   ├── ThumbnailService.cs
│   └── その他サービス...
├── ViewModels/               # ビューモデル
│   ├── MainViewModel.cs
│   ├── SettingsViewModel.cs
│   └── ColumnVisibilitySettings.cs
└── Views/                    # ビュー
    ├── SettingsWindow.xaml
    ├── ClearAttributesWindow.xaml
    ├── CustomMessageBox.xaml
    └── ThumbnailModeSelectionWindow.xaml
```

## 開発パターンとベストプラクティス

### 1. MVVM パターンの実装

#### ViewModel の作成
```csharp
public partial class ExampleViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;
    
    [ObservableProperty]
    private bool _isProcessing;
    
    [RelayCommand]
    private async Task ProcessAsync()
    {
        IsProcessing = true;
        try
        {
            // 処理実装
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
```

#### View との バインディング
```xml
<TextBox Text="{Binding Title, Mode=TwoWay}" />
<Button Content="処理実行" Command="{Binding ProcessCommand}" />
<ProgressBar IsIndeterminate="{Binding IsProcessing}" />
```

### 2. 依存性注入の活用

#### サービス登録 (App.xaml.cs)
```csharp
private void ConfigureServices(IServiceCollection services)
{
    // サービス登録
    services.AddSingleton<IFileScannerService, FileScannerService>();
    services.AddSingleton<IAIService, AIService>();
    
    // ViewModel登録
    services.AddTransient<MainViewModel>();
}
```

#### サービス利用
```csharp
public class ExampleViewModel
{
    private readonly IFileScannerService _scannerService;
    
    public ExampleViewModel(IFileScannerService scannerService)
    {
        _scannerService = scannerService;
    }
}
```

### 3. 非同期処理のパターン

#### 長時間処理の実装
```csharp
[RelayCommand]
private async Task LongRunningTaskAsync()
{
    IsProcessing = true;
    StatusMessage = "処理中...";
    
    try
    {
        await Task.Run(async () =>
        {
            // 重い処理をバックグラウンドで実行
            var result = await ProcessDataAsync();
            
            // UI更新はDispatcherで
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdateUI(result);
            });
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "処理中にエラーが発生しました");
        StatusMessage = $"エラー: {ex.Message}";
    }
    finally
    {
        IsProcessing = false;
    }
}
```

### 4. エラーハンドリング

#### サービス層でのエラー処理
```csharp
public class ServiceResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();
}

public async Task<ServiceResult<List<MangaFile>>> GetFilesAsync()
{
    try
    {
        var files = await _repository.GetAllAsync();
        return new ServiceResult<List<MangaFile>>
        {
            Success = true,
            Data = files
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "ファイル取得中にエラーが発生しました");
        return new ServiceResult<List<MangaFile>>
        {
            Success = false,
            ErrorMessage = ex.Message
        };
    }
}
```

### 5. 設定管理

#### 設定の読み書き
```csharp
// 設定の取得
var maxMemory = _config.GetSetting<long>("MaxMemoryUsage", 8L * 1024 * 1024 * 1024);

// 設定の保存
_config.SetSetting("MaxMemoryUsage", newValue);
await _config.SaveAsync();
```

#### 設定変更の通知
```csharp
// 設定変更を通知
_settingsChangedNotifier.NotifySettingsChanged("ColumnVisibility");

// 設定変更を受信
_settingsChangedNotifier.SettingsChanged += OnSettingsChanged;
```

## データベース操作

### 1. Repository パターン

#### 基本的な CRUD 操作
```csharp
public interface IMangaRepository
{
    Task<List<MangaFile>> GetAllAsync();
    Task<MangaFile?> GetByIdAsync(int id);
    Task<int> InsertAsync(MangaFile manga);
    Task UpdateAsync(MangaFile manga);
    Task DeleteAsync(int id);
}
```

### 2. バッチ処理
```csharp
// 大量データの一括挿入
public async Task<int> InsertBatchAsync(IEnumerable<MangaFile> mangaFiles)
{
    using var transaction = _connection.BeginTransaction();
    try
    {
        var insertedCount = 0;
        foreach (var batch in mangaFiles.Chunk(1000))
        {
            // 1000件ずつ処理
            insertedCount += await InsertBatchInternalAsync(batch);
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
```

## UI 開発のガイドライン

### 1. DataGrid のカスタマイズ

#### 列の動的制御
```csharp
// 列の表示/非表示
column.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

// 列幅の設定
column.Width = new DataGridLength(width);

// 列順序の設定
column.DisplayIndex = displayIndex;
```

#### セルのカスタマイズ
```xml
<DataGridTemplateColumn Header="サムネイル">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <Image Source="{Binding ThumbnailPath, Converter={StaticResource ThumbnailPathConverter}}" 
                   MaxWidth="120" MaxHeight="80" />
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

### 2. 進捗表示

#### スピナーの実装
```xml
<Grid Visibility="{Binding IsProcessing, Converter={StaticResource BooleanToVisibilityConverter}}">
    <Viewbox Width="80" Height="80">
        <Canvas Width="120" Height="120">
            <Canvas.RenderTransform>
                <RotateTransform x:Name="SpinnerRotate" CenterX="60" CenterY="60"/>
            </Canvas.RenderTransform>
            <!-- アニメーション定義 -->
        </Canvas>
    </Viewbox>
</Grid>
```

### 3. カスタムコントロール

#### メッセージボックス
```csharp
public static class CustomMessageBox
{
    public static CustomMessageBoxResult Show(string message, string title, CustomMessageBoxButton button)
    {
        var messageBox = new CustomMessageBox
        {
            MessageText = message,
            Title = title,
            ButtonType = button
        };
        
        messageBox.ShowDialog();
        return messageBox.Result;
    }
}
```

## テストとデバッグ

### 1. ログ出力

#### ログレベルの使い分け
```csharp
_logger.LogDebug("デバッグ情報: {Value}", debugValue);
_logger.LogInformation("処理完了: {Count}件", processedCount);
_logger.LogWarning("警告: {Message}", warningMessage);
_logger.LogError(ex, "エラーが発生しました: {Context}", context);
```

### 2. デバッグ用設定

#### 開発時の設定
```json
{
  "LogLevel": "Debug",
  "MaxConcurrentAIRequests": 5,
  "TestMode": true
}
```

## パフォーマンス最適化

### 1. メモリ管理

#### 大量データの処理
```csharp
// チャンク処理による メモリ使用量制御
await foreach (var batch in GetDataInBatches(batchSize: 1000))
{
    await ProcessBatch(batch);
    
    // 定期的なガベージコレクション
    if (processedCount % 10000 == 0)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
```

### 2. UI の最適化

#### 仮想化の活用
```xml
<DataGrid EnableRowVirtualization="True"
          EnableColumnVirtualization="True"
          VirtualizingPanel.VirtualizationMode="Recycling" />
```

#### 非同期読み込み
```csharp
// UI をブロックしない読み込み
await Task.Run(async () =>
{
    var data = await LoadDataAsync();
    
    await Dispatcher.InvokeAsync(() =>
    {
        DataSource = data;
    });
});
```

## デプロイメント

### 1. ビルド設定

#### プロジェクトファイルの設定
```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <SelfContained>false</SelfContained>
    <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

### 2. 配布用ビルド

#### コマンドライン
```bash
# Release ビルド
dotnet build -c Release

# 発行
dotnet publish -c Release -r win-x64 --self-contained false
```

## トラブルシューティング

### よくある問題と解決方法

#### 1. DataGrid の列設定が保存されない
- `SaveDataGridSettings()` が適切に呼ばれているか確認
- JSON シリアライゼーションの設定を確認

#### 2. AI API の呼び出しが失敗する
- API キーの設定を確認
- ネットワーク接続を確認
- レート制限に引っかかっていないか確認

#### 3. サムネイル生成が失敗する
- ファイルの破損チェック
- 一時ディレクトリの書き込み権限確認
- メモリ不足の確認

#### 4. 設定ファイルが読み込めない
- ファイルパスの確認
- JSON 形式の妥当性確認
- バックアップファイルからの復旧

## コーディング規約

### 1. 命名規則
- **クラス**: PascalCase (`MangaFile`)
- **メソッド**: PascalCase (`GetAllAsync`)
- **プロパティ**: PascalCase (`FileName`)
- **フィールド**: camelCase with underscore (`_logger`)
- **定数**: PascalCase (`MaxRetryCount`)

### 2. ファイル構成
- 1ファイル1クラスを基本とする
- インターフェースは `Interfaces` フォルダに配置
- 関連するクラスは同じフォルダにまとめる

### 3. コメント
- パブリックメンバーには XML ドキュメントコメントを記述
- 複雑なロジックには説明コメントを追加
- TODO コメントは課題管理システムと連携

---

*このガイドは継続的に更新され、プロジェクトの成長とともに拡張されます。*