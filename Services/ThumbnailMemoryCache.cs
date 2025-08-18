using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace Mangaanya.Services
{
    /// <summary>
    /// LRU方式のサムネイル用メモリキャッシュ
    /// </summary>
    public class ThumbnailMemoryCache
    {
        private readonly Dictionary<string, CacheItem> _cache = new();
        private readonly LinkedList<string> _lruList = new();
        private readonly int _maxSize;
        private readonly object _lock = new();

        public ThumbnailMemoryCache(int maxSize = 300)
        {
            _maxSize = maxSize;
        }

        /// <summary>
        /// キャッシュから画像を取得します
        /// </summary>
        /// <param name="key">キャッシュキー</param>
        /// <param name="image">取得された画像</param>
        /// <returns>取得に成功した場合はtrue</returns>
        public bool TryGet(string key, out BitmapImage? image)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var item))
                {
                    // LRUリストの先頭に移動（最近使用されたことを記録）
                    _lruList.Remove(item.Node);
                    _lruList.AddFirst(item.Node);
                    
                    image = item.Image;
                    UpdateHitRate(true);
                    return true;
                }
                
                image = null;
                UpdateHitRate(false);
                return false;
            }
        }

        /// <summary>
        /// キャッシュに画像を追加します
        /// </summary>
        /// <param name="key">キャッシュキー</param>
        /// <param name="image">追加する画像</param>
        public void Add(string key, BitmapImage image)
        {
            lock (_lock)
            {
                // 既に存在する場合は更新
                if (_cache.ContainsKey(key))
                {
                    var existingItem = _cache[key];
                    _lruList.Remove(existingItem.Node);
                    _lruList.AddFirst(existingItem.Node);
                    existingItem.Image = image;
                    return;
                }

                // キャッシュサイズ制限をチェック
                while (_cache.Count >= _maxSize && _lruList.Last != null)
                {
                    var lastKey = _lruList.Last.Value;
                    _cache.Remove(lastKey);
                    _lruList.RemoveLast();
                }

                // 新しいアイテムを追加
                var node = _lruList.AddFirst(key);
                _cache[key] = new CacheItem { Image = image, Node = node };
            }
        }

        /// <summary>
        /// キャッシュをクリアします
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
                _lruList.Clear();
                
                // ヒット率統計をリセット
                _totalRequests = 0;
                _hitCount = 0;
                HitRate = 0.0;
            }
        }

        /// <summary>
        /// 現在のキャッシュサイズを取得します
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// 最大キャッシュサイズを取得します
        /// </summary>
        public int MaxSize => _maxSize;

        /// <summary>
        /// 指定されたキーがキャッシュに存在するかチェックします
        /// </summary>
        /// <param name="key">チェックするキー</param>
        /// <returns>存在する場合はtrue</returns>
        public bool ContainsKey(string key)
        {
            lock (_lock)
            {
                return _cache.ContainsKey(key);
            }
        }

        /// <summary>
        /// 指定されたキーのアイテムをキャッシュから削除します
        /// </summary>
        /// <param name="key">削除するキー</param>
        /// <returns>削除に成功した場合はtrue</returns>
        public bool Remove(string key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var item))
                {
                    _cache.Remove(key);
                    _lruList.Remove(item.Node);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// IDベースのキャッシュキーを生成します（バイナリデータ用に最適化）
        /// </summary>
        /// <param name="mangaFileId">MangaFileのID</param>
        /// <returns>最適化されたキャッシュキー</returns>
        public static string GenerateIdBasedKey(int mangaFileId)
        {
            return $"thumbnail_{mangaFileId}";
        }

        /// <summary>
        /// メモリ使用量を最適化するため、キャッシュサイズを動的に調整します
        /// </summary>
        /// <param name="targetSize">目標キャッシュサイズ</param>
        public void OptimizeMemoryUsage(int? targetSize = null)
        {
            lock (_lock)
            {
                var target = targetSize ?? (_maxSize * 3 / 4); // デフォルトで75%に削減
                
                while (_cache.Count > target && _lruList.Last != null)
                {
                    var lastKey = _lruList.Last.Value;
                    _cache.Remove(lastKey);
                    _lruList.RemoveLast();
                }
            }
        }

        /// <summary>
        /// キャッシュヒット率を取得します（パフォーマンス監視用）
        /// </summary>
        public double HitRate { get; private set; }

        private int _totalRequests = 0;
        private int _hitCount = 0;

        /// <summary>
        /// ヒット率を更新します（内部使用）
        /// </summary>
        private void UpdateHitRate(bool isHit)
        {
            _totalRequests++;
            if (isHit) _hitCount++;
            
            if (_totalRequests > 0)
            {
                HitRate = (double)_hitCount / _totalRequests;
            }
        }

        /// <summary>
        /// 推定メモリ使用量を取得します（300件環境用の監視機能）
        /// </summary>
        /// <returns>推定メモリ使用量（バイト）</returns>
        public long GetEstimatedMemoryUsage()
        {
            lock (_lock)
            {
                // 平均的なサムネイル画像サイズを30KBと仮定（480x320ピクセル、圧縮済み）
                const long averageThumbnailSize = 30 * 1024;
                return _cache.Count * averageThumbnailSize;
            }
        }

        /// <summary>
        /// 詳細なキャッシュ統計情報を取得します（300件環境用の拡張監視）
        /// </summary>
        /// <returns>キャッシュ統計情報</returns>
        public CacheStatistics GetDetailedStatistics()
        {
            lock (_lock)
            {
                return new CacheStatistics
                {
                    CurrentSize = _cache.Count,
                    MaxSize = _maxSize,
                    HitRate = HitRate,
                    TotalRequests = _totalRequests,
                    HitCount = _hitCount,
                    MissCount = _totalRequests - _hitCount,
                    EstimatedMemoryUsage = GetEstimatedMemoryUsage(),
                    MemoryEfficiency = _cache.Count > 0 ? (double)_hitCount / _cache.Count : 0.0
                };
            }
        }

        /// <summary>
        /// メモリ使用量が閾値を超えた場合の自動最適化（300件環境用）
        /// </summary>
        /// <param name="maxMemoryMB">最大メモリ使用量（MB）</param>
        /// <returns>最適化が実行された場合はtrue</returns>
        public bool AutoOptimizeIfNeeded(int maxMemoryMB = 10)
        {
            lock (_lock)
            {
                var currentMemoryMB = GetEstimatedMemoryUsage() / (1024 * 1024);
                
                if (currentMemoryMB > maxMemoryMB)
                {
                    // メモリ使用量が閾値を超えた場合、キャッシュサイズを75%に削減
                    var targetSize = _maxSize * 3 / 4;
                    OptimizeMemoryUsage(targetSize);
                    return true;
                }
                
                return false;
            }
        }

        /// <summary>
        /// キャッシュアイテムの内部クラス
        /// </summary>
        private class CacheItem
        {
            public BitmapImage Image { get; set; } = null!;
            public LinkedListNode<string> Node { get; set; } = null!;
        }
    }

    /// <summary>
    /// キャッシュ統計情報クラス（300件環境用の拡張監視）
    /// </summary>
    public class CacheStatistics
    {
        public int CurrentSize { get; set; }
        public int MaxSize { get; set; }
        public double HitRate { get; set; }
        public int TotalRequests { get; set; }
        public int HitCount { get; set; }
        public int MissCount { get; set; }
        public long EstimatedMemoryUsage { get; set; }
        public double MemoryEfficiency { get; set; }

        public override string ToString()
        {
            return $"Cache: {CurrentSize}/{MaxSize} items, Hit Rate: {HitRate:P2}, " +
                   $"Memory: {EstimatedMemoryUsage / 1024 / 1024:F1}MB, Efficiency: {MemoryEfficiency:F2}";
        }
    }
}
