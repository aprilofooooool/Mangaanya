using Mangaanya.Models;

namespace Mangaanya.Services.Interfaces
{
    /// <summary>
    /// ファイル移動サービスのインターフェース
    /// </summary>
    public interface IFileMoveService
    {
        /// <summary>
        /// 指定されたファイルを移動先フォルダに移動する
        /// </summary>
        /// <param name="sourceFiles">移動対象のファイル一覧</param>
        /// <param name="destinationFolder">移動先フォルダパス</param>
        /// <param name="progress">進捗報告用のプログレスレポーター（オプション）</param>
        /// <returns>移動処理の結果</returns>
        Task<FileMoveResult> MoveFilesAsync(
            IEnumerable<MangaFile> sourceFiles, 
            string destinationFolder, 
            IProgress<FileMoveProgress>? progress = null);

        /// <summary>
        /// ファイル移動操作の妥当性を検証する
        /// </summary>
        /// <param name="sourceFiles">移動対象のファイル一覧</param>
        /// <param name="destinationFolder">移動先フォルダパス</param>
        /// <returns>移動操作が有効な場合true</returns>
        Task<bool> ValidateMoveOperationAsync(
            IEnumerable<MangaFile> sourceFiles, 
            string destinationFolder);

        /// <summary>
        /// 移動先フォルダでの競合を検出する
        /// </summary>
        /// <param name="sourceFile">移動対象のファイル</param>
        /// <param name="destinationFolder">移動先フォルダパス</param>
        /// <returns>競合タイプ</returns>
        Task<FileMoveConflictType> DetectConflictAsync(
            MangaFile sourceFile, 
            string destinationFolder);

        /// <summary>
        /// 競合解決方法を取得する（ユーザーに確認ダイアログを表示）
        /// </summary>
        /// <param name="conflictType">競合タイプ</param>
        /// <param name="sourceFile">移動対象のファイル</param>
        /// <param name="destinationFolder">移動先フォルダパス</param>
        /// <returns>ユーザーが選択した解決方法</returns>
        Task<ConflictResolution> ResolveConflictAsync(
            FileMoveConflictType conflictType,
            MangaFile sourceFile,
            string destinationFolder);
    }
}