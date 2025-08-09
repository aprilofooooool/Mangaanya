using Mangaanya.Models;

namespace Mangaanya.Services
{
    public interface IFileOperationService
    {
        /// <summary>
        /// ファイルを既定のアプリケーションで開きます
        /// </summary>
        /// <param name="filePath">開くファイルのパス</param>
        /// <returns>成功した場合はtrue、失敗した場合はfalse</returns>
        bool OpenFile(string filePath);
        
        /// <summary>
        /// ファイルのあるフォルダをエクスプローラーで開きます
        /// </summary>
        /// <param name="filePath">対象ファイルのパス</param>
        /// <returns>成功した場合はtrue、失敗した場合はfalse</returns>
        bool OpenContainingFolder(string filePath);
        

    }
}
