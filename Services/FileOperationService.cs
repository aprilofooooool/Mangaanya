using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using System.Windows.Forms;

namespace Mangaanya.Services
{
    public class FileOperationService : IFileOperationService
    {
        private readonly ILogger<FileOperationService> _logger;
        
        public FileOperationService(ILogger<FileOperationService> logger)
        {
            _logger = logger;
        }
        
        public bool OpenFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("ファイルが存在しません: {FilePath}", filePath);
                    return false;
                }
                
                // ファイルを既定のアプリケーションで開く
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(filePath)
                    {
                        UseShellExecute = true
                    }
                };
                
                process.Start();
                _logger.LogInformation("ファイルを開きました: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ファイルを開く際にエラーが発生しました: {FilePath}", filePath);
                return false;
            }
        }
        
        public bool OpenContainingFolder(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("ファイルが存在しません: {FilePath}", filePath);
                    return false;
                }
                
                // /selectパラメータを使用してファイルを選択した状態でエクスプローラーを開く
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                _logger.LogInformation("ファイルのあるフォルダを開きました: {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "フォルダを開く際にエラーが発生しました: {FilePath}", filePath);
                return false;
            }
        }

    }
}
