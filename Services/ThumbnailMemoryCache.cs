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

        public ThumbnailMemoryCache(int maxSize = 100)
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
                    return true;
                }
                
                image = null;
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
        /// キャッシュアイテムの内部クラス
        /// </summary>
        private class CacheItem
        {
            public BitmapImage Image { get; set; } = null!;
            public LinkedListNode<string> Node { get; set; } = null!;
        }
    }
}
