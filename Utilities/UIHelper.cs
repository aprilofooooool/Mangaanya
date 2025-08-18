using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Mangaanya.Views;

namespace Mangaanya.Utilities
{
    /// <summary>
    /// UI操作に関する共通ユーティリティクラス
    /// </summary>
    public static class UIHelper
    {
        /// <summary>
        /// 確認ダイアログを表示します
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="title">タイトル（デフォルト: "確認"）</param>
        /// <returns>ユーザーが「はい」を選択した場合はtrue、そうでなければfalse</returns>
        public static bool ShowConfirmationDialog(string message, string title = "確認")
        {
            var result = CustomMessageBox.Show(message, title, CustomMessageBoxButton.YesNo);
            return result == CustomMessageBoxResult.Yes;
        }

        /// <summary>
        /// 情報ダイアログを表示します
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="title">タイトル（デフォルト: "情報"）</param>
        public static void ShowInformationDialog(string message, string title = "情報")
        {
            CustomMessageBox.Show(message, title, CustomMessageBoxButton.OK);
        }

        /// <summary>
        /// 警告ダイアログを表示します
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="title">タイトル（デフォルト: "警告"）</param>
        public static void ShowWarningDialog(string message, string title = "警告")
        {
            CustomMessageBox.Show(message, title, CustomMessageBoxButton.OK);
        }

        /// <summary>
        /// エラーダイアログを表示します
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="title">タイトル（デフォルト: "エラー"）</param>
        public static void ShowErrorDialog(string message, string title = "エラー")
        {
            CustomMessageBox.Show(message, title, CustomMessageBoxButton.OK);
        }

        /// <summary>
        /// 例外情報を含むエラーダイアログを表示します
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="exception">例外オブジェクト</param>
        /// <param name="title">タイトル（デフォルト: "エラー"）</param>
        public static void ShowErrorDialog(string message, Exception exception, string title = "エラー")
        {
            var fullMessage = $"{message}\n\n詳細: {exception.Message}";
            CustomMessageBox.Show(fullMessage, title, CustomMessageBoxButton.OK);
        }

        /// <summary>
        /// ファイル操作の確認ダイアログを表示します（上書き/スキップ/キャンセル）
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="title">タイトル（デフォルト: "ファイル操作の確認"）</param>
        /// <returns>ユーザーの選択結果</returns>
        public static CustomMessageBoxResult ShowFileOperationDialog(string message, string title = "ファイル操作の確認")
        {
            return CustomMessageBox.Show(message, title, CustomMessageBoxButton.OverwriteSkipCancel);
        }

        /// <summary>
        /// UIスレッドで処理を実行します
        /// </summary>
        /// <param name="action">実行するアクション</param>
        public static void InvokeOnUIThread(Action action)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(action);
                }
            }
        }

        /// <summary>
        /// UIスレッドで非同期処理を実行します
        /// </summary>
        /// <param name="action">実行するアクション</param>
        /// <returns>Task</returns>
        public static Task InvokeOnUIThreadAsync(Action action)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    action();
                    return Task.CompletedTask;
                }
                else
                {
                    return System.Windows.Application.Current.Dispatcher.InvokeAsync(action).Task;
                }
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// UIスレッドで非同期処理を実行し、結果を返します
        /// </summary>
        /// <typeparam name="T">戻り値の型</typeparam>
        /// <param name="func">実行する関数</param>
        /// <returns>関数の実行結果</returns>
        public static Task<T> InvokeOnUIThreadAsync<T>(Func<T> func)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    return Task.FromResult(func());
                }
                else
                {
                    return System.Windows.Application.Current.Dispatcher.InvokeAsync(func).Task;
                }
            }
            
            return Task.FromResult(default(T)!);
        }

        /// <summary>
        /// 大量処理の確認ダイアログを表示します
        /// </summary>
        /// <param name="itemCount">処理対象のアイテム数</param>
        /// <param name="operationName">操作名</param>
        /// <param name="title">タイトル（デフォルト: "大量処理の確認"）</param>
        /// <returns>ユーザーが「はい」を選択した場合はtrue、そうでなければfalse</returns>
        public static bool ShowBulkOperationConfirmation(int itemCount, string operationName, string title = "大量処理の確認")
        {
            var message = $"{itemCount}件のファイルに対して{operationName}を実行します。\n\n処理に時間がかかる場合があります。続行しますか？";
            return ShowConfirmationDialog(message, title);
        }

        /// <summary>
        /// 設定変更の確認ダイアログを表示します
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="title">タイトル（デフォルト: "設定変更の確認"）</param>
        /// <returns>ユーザーの選択結果</returns>
        public static CustomMessageBoxResult ShowSettingsChangeConfirmation(string message, string title = "設定変更の確認")
        {
            return CustomMessageBox.Show(message, title, CustomMessageBoxButton.YesNoCancel);
        }

        /// <summary>
        /// 処理完了の通知を表示します
        /// </summary>
        /// <param name="operationName">操作名</param>
        /// <param name="itemCount">処理したアイテム数</param>
        /// <param name="duration">処理時間</param>
        /// <param name="title">タイトル（デフォルト: "処理完了"）</param>
        public static void ShowCompletionNotification(string operationName, int itemCount, TimeSpan duration, string title = "処理完了")
        {
            var message = $"{operationName}が完了しました。\n\n対象ファイル: {itemCount}件\n処理時間: {duration.TotalSeconds:F1}秒";
            ShowInformationDialog(message, title);
        }
    }
}