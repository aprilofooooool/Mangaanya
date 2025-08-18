using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mangaanya.Services;
using Mangaanya.Services.Interfaces;
using Mangaanya.ViewModels;
using Mangaanya.Views;

namespace Mangaanya
{
    public partial class App : System.Windows.Application
    {
        private ServiceProvider? _serviceProvider;
        
        // ServiceProviderを公開するプロパティ
        public IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("ServiceProvider is not initialized");
        
        // 静的プロパティとしてもアクセス可能にする
        public static IServiceProvider ServiceProvider => ((App)Current).Services;

        protected override void OnStartup(StartupEventArgs e)
        {
            // グローバル例外ハンドラーを設定
            DispatcherUnhandledException += (sender, args) =>
            {
                Mangaanya.Views.CustomMessageBox.Show($"未処理の例外が発生しました:\n{args.Exception.Message}\n\nスタックトレース:\n{args.Exception.StackTrace}", 
                    "エラー", Mangaanya.Views.CustomMessageBoxButton.OK);
                args.Handled = true;
            };

            try
            {
                base.OnStartup(e);

                var services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                // MainViewModelを取得
                var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                
                // メインウィンドウを作成（まだ表示しない）
                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };

                // 初期化完了を待機してからウィンドウを表示
                mainViewModel.InitializationCompleted += (sender, args) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        mainWindow.Show();
                    });
                };

                // 初期化を開始
                mainViewModel.StartInitialization();
            }
            catch (Exception ex)
            {
                Mangaanya.Views.CustomMessageBox.Show($"アプリケーション起動エラー:\n{ex.Message}\n\nスタックトレース:\n{ex.StackTrace}", 
                    "起動エラー", Mangaanya.Views.CustomMessageBoxButton.OK);
                Shutdown(1);
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // ログ設定
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Warning);
                
                // シンプルなファイルログプロバイダーを追加
                builder.AddProvider(new FileLoggerProvider());
            });

            // サービス登録
            services.AddSingleton<IFileScannerService, FileScannerService>();
            services.AddSingleton<IAIService, AIService>();
            services.AddSingleton<ISearchEngineService, SearchEngineService>();
            services.AddSingleton<IMangaRepository, MangaRepository>();
            services.AddSingleton<ConfigurationManager>();
            services.AddSingleton<IConfigurationManager>(provider => provider.GetRequiredService<ConfigurationManager>());
            services.AddSingleton<ISettingsChangedNotifier>(provider => provider.GetRequiredService<ConfigurationManager>());
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IFileOperationService, FileOperationService>();
            services.AddSingleton<IThumbnailService, ThumbnailServiceOptimized>();
            services.AddSingleton<ISystemSoundService, SystemSoundService>();
            services.AddSingleton<IFileSizeService, FileSizeService>();
            services.AddSingleton<IFileMoveService, FileMoveService>();

            // ViewModel登録
            services.AddTransient<MainViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<FolderSelectionViewModel>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }
    }
}
