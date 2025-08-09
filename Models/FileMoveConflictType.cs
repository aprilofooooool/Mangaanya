namespace Mangaanya.Models
{
    /// <summary>
    /// ファイル移動時の競合タイプを表す列挙型
    /// </summary>
    public enum FileMoveConflictType
    {
        /// <summary>
        /// 競合なし
        /// </summary>
        None,
        
        /// <summary>
        /// 同一フォルダへの移動
        /// </summary>
        SameFolder,
        
        /// <summary>
        /// 移動先に同名ファイルが存在
        /// </summary>
        FileExists
    }

    /// <summary>
    /// 競合解決方法を表す列挙型
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>
        /// スキップ
        /// </summary>
        Skip,
        
        /// <summary>
        /// 上書き
        /// </summary>
        Overwrite,
        
        /// <summary>
        /// キャンセル
        /// </summary>
        Cancel
    }

    /// <summary>
    /// ファイル移動操作の種類を表す列挙型
    /// </summary>
    public enum FileMoveOperation
    {
        /// <summary>
        /// 検証中
        /// </summary>
        Validating,
        
        /// <summary>
        /// 移動中
        /// </summary>
        Moving,
        
        /// <summary>
        /// データベース更新中
        /// </summary>
        UpdatingDatabase,
        
        /// <summary>
        /// 完了
        /// </summary>
        Complete
    }
}