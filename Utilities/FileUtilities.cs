using System;
using System.IO;
using System.Linq;

namespace Mangaanya.Utilities
{
    /// <summary>
    /// ファイル操作に関する共通ユーティリティクラス
    /// </summary>
    public static class FileUtilities
    {
        /// <summary>
        /// サポートされているマンガファイルの拡張子
        /// </summary>
        private static readonly string[] SupportedExtensions = { ".zip", ".rar" };

        /// <summary>
        /// ファイルサイズを人間が読みやすい形式にフォーマットします（TB対応）
        /// </summary>
        /// <param name="bytes">バイト数</param>
        /// <returns>フォーマットされたファイルサイズ文字列</returns>
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 0)
                return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 指定されたファイルパスが有効なマンガファイルかどうかを判定します
        /// </summary>
        /// <param name="filePath">ファイルパス</param>
        /// <returns>有効なマンガファイルの場合はtrue、そうでなければfalse</returns>
        public static bool IsValidMangaFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return SupportedExtensions.Contains(extension);
        }

        /// <summary>
        /// ファイル名から無効な文字を除去し、安全なファイル名を生成します
        /// </summary>
        /// <param name="fileName">元のファイル名</param>
        /// <returns>安全なファイル名</returns>
        public static string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "unnamed";

            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// サポートされているマンガファイルの拡張子一覧を取得します
        /// </summary>
        /// <returns>サポートされている拡張子の配列</returns>
        public static string[] GetSupportedExtensions()
        {
            return (string[])SupportedExtensions.Clone();
        }
    }
}