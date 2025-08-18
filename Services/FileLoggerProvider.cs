using Microsoft.Extensions.Logging;
using System.IO;

namespace Mangaanya.Services
{
    /// <summary>
    /// シンプルなファイルログプロバイダー
    /// </summary>
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logFilePath;

        public FileLoggerProvider()
        {
            // 実行ファイルと同じフォルダの\logサブフォルダにログを出力
            var executablePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var executableDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory;
            var logDirectory = Path.Combine(executableDirectory, "log");
            
            // logフォルダが存在しない場合は作成
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            
            _logFilePath = Path.Combine(logDirectory, $"mangaanya_{DateTime.Now:yyyyMMdd}.log");
            
            // 起動時にログファイルを初期化（既存のログを削除）
            InitializeLogFile();
        }
        
        /// <summary>
        /// ログファイルを初期化（既存のログを削除してファイルサイズの肥大化を防ぐ）
        /// </summary>
        private void InitializeLogFile()
        {
            try
            {
                if (File.Exists(_logFilePath))
                {
                    File.Delete(_logFilePath);
                }
                
                // 初期化メッセージを書き込み
                var initMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Information] [Mangaanya] ログファイルを初期化しました。アプリケーション開始。";
                File.WriteAllText(_logFilePath, initMessage + Environment.NewLine);
            }
            catch
            {
                // ログファイルの初期化でエラーが発生しても、アプリケーションを停止させない
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _logFilePath);
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// シンプルなファイルログ実装
    /// </summary>
    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logFilePath;
        private readonly object _lock = new object();

        public FileLogger(string categoryName, string logFilePath)
        {
            _categoryName = categoryName;
            _logFilePath = logFilePath;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";
            
            if (exception != null)
            {
                logEntry += Environment.NewLine + exception.ToString();
            }

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // ログ出力でエラーが発生しても、アプリケーションを停止させない
                }
            }
        }
    }
}