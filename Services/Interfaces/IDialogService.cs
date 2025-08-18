using System.Threading.Tasks;
using Mangaanya.Models;

namespace Mangaanya.Services
{
    public interface IDialogService
    {
        (string? SelectedFolder, bool IncludeSubfolders) SelectFolderWithOptions(string title = "フォルダを選択してください");
        string? SelectFolder(string title = "フォルダを選択してください");
        string? SelectFile(string title = "ファイルを選択してください", string filter = "すべてのファイル (*.*)|*.*");
        bool ShowConfirmation(string message, string title = "確認");
        void ShowInformation(string message, string title = "情報");
        void ShowError(string message, string title = "エラー");
        
        /// <summary>
        /// 入力ダイアログを表示します
        /// </summary>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="defaultValue">デフォルト値</param>
        /// <returns>入力された文字列、キャンセルされた場合はnull</returns>
        Task<string?> ShowInputDialogAsync(string title, string message, string defaultValue = "");
        
        /// <summary>
        /// 競合解決ダイアログを表示します
        /// </summary>
        /// <param name="title">ダイアログのタイトル</param>
        /// <param name="message">表示するメッセージ</param>
        /// <param name="conflictType">競合の種類</param>
        /// <returns>ユーザーの選択結果</returns>
        Task<ConflictResolution> ShowConflictResolutionDialogAsync(string title, string message, FileMoveConflictType conflictType);
    }
}
