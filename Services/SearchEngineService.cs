using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using System.Text.RegularExpressions;

namespace Mangaanya.Services
{
    public class SearchEngineService : ISearchEngineService
    {
        private readonly ILogger<SearchEngineService> _logger;
        private readonly IMangaRepository _repository;

        public SearchEngineService(ILogger<SearchEngineService> logger, IMangaRepository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        public async Task<SearchResult> SearchAsync(SearchQuery query)
        {
            var startTime = DateTime.Now;
            var result = new SearchResult();

            try
            {
                var criteria = new SearchCriteria
                {
                    SearchText = query.Text
                };

                // 評価による検索かどうかをチェック
                if (IsRatingSearch(query.Text))
                {
                    var rating = ParseRatingFromSearchText(query.Text);
                    criteria.Rating = rating;
                    
                    // 評価による検索の場合は、評価専用の検索を実行
                    var ratingResults = await _repository.SearchByRatingAsync(rating);
                    
                    result.Results = ratingResults;
                    result.TotalCount = ratingResults.Count;
                    result.Duration = DateTime.Now - startTime;
                    
                    _logger.LogDebug("評価検索完了: クエリ={Query}, 評価={Rating}, 結果数={Count}, 実行時間={Duration}ms", 
                        query.Text, rating?.ToString() ?? "なし", result.TotalCount, result.Duration.TotalMilliseconds);
                    
                    return result;
                }

                var searchResults = await _repository.SearchAsync(criteria);
                
                // フィルタリング
                if (query.UseRegex && !string.IsNullOrEmpty(query.Text))
                {
                    try
                    {
                        var regex = new Regex(query.Text, RegexOptions.IgnoreCase);
                        searchResults = searchResults.Where(f => 
                            (query.SearchFields.HasFlag(SearchField.FileName) && regex.IsMatch(f.FileName)) ||
                            (query.SearchFields.HasFlag(SearchField.Title) && !string.IsNullOrEmpty(f.Title) && regex.IsMatch(f.Title)) ||
                            (query.SearchFields.HasFlag(SearchField.Author) && !string.IsNullOrEmpty(f.OriginalAuthor) && regex.IsMatch(f.OriginalAuthor)) ||
                            (query.SearchFields.HasFlag(SearchField.Genre) && !string.IsNullOrEmpty(f.Genre) && regex.IsMatch(f.Genre)) ||
                            (query.SearchFields.HasFlag(SearchField.Publisher) && !string.IsNullOrEmpty(f.Publisher) && regex.IsMatch(f.Publisher)) ||
                            (query.SearchFields.HasFlag(SearchField.Tags) && !string.IsNullOrEmpty(f.Tags) && regex.IsMatch(f.Tags))
                        ).ToList();
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogWarning("無効な正規表現: {Pattern} - {Error}", query.Text, ex.Message);
                        // 正規表現が無効な場合は通常の検索にフォールバック
                    }
                }

                // ソート
                searchResults = query.SortBy switch
                {
                    SortOrder.FileName => searchResults.OrderBy(f => f.FileName).ToList(),
                    SortOrder.Title => searchResults.OrderBy(f => f.Title ?? f.FileName).ToList(),
                    SortOrder.ModifiedDate => searchResults.OrderByDescending(f => f.ModifiedDate).ToList(),
                    SortOrder.FileSize => searchResults.OrderByDescending(f => f.FileSize).ToList(),
                    _ => searchResults // Relevance (デフォルト)
                };

                result.Results = searchResults;
                result.TotalCount = searchResults.Count;
                result.Duration = DateTime.Now - startTime;

                _logger.LogDebug("検索完了: クエリ={Query}, 結果数={Count}, 実行時間={Duration}ms", 
                    query.Text, result.TotalCount, result.Duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "検索中にエラーが発生しました: {Query}", query.Text);
                result.Results = new List<MangaFile>();
                result.TotalCount = 0;
            }

            return result;
        }

        public async Task RebuildIndexAsync(IProgress<IndexProgress> progress)
        {
            try
            {
                var allFiles = await _repository.GetAllAsync();
                var totalFiles = allFiles.Count;
                var processedFiles = 0;

                progress?.Report(new IndexProgress
                {
                    ProcessedFiles = 0,
                    TotalFiles = totalFiles,
                    Status = "インデックス再構築を開始しています..."
                });

                // 実際のインデックス再構築処理はここに実装
                // 現在はプレースホルダー
                foreach (var file in allFiles)
                {
                    processedFiles++;
                    progress?.Report(new IndexProgress
                    {
                        ProcessedFiles = processedFiles,
                        TotalFiles = totalFiles,
                        Status = $"インデックス処理中: {file.FileName}"
                    });

                    // 少し待機してプログレスを表示
                    await Task.Delay(1);
                }

                _logger.LogInformation("インデックス再構築完了: {Count}件のファイルを処理しました", totalFiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "インデックス再構築中にエラーが発生しました");
                throw;
            }
        }

        public async Task<List<string>> GetSearchSuggestionsAsync(string partialQuery)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partialQuery) || partialQuery.Length < 2)
                {
                    return new List<string>();
                }

                var criteria = new SearchCriteria { SearchText = partialQuery };
                var results = await _repository.SearchAsync(criteria);

                var suggestions = new HashSet<string>();

                foreach (var file in results.Take(10))
                {
                    if (!string.IsNullOrEmpty(file.Title))
                        suggestions.Add(file.Title);
                    if (!string.IsNullOrEmpty(file.OriginalAuthor))
                        suggestions.Add(file.OriginalAuthor);
                    if (!string.IsNullOrEmpty(file.Artist))
                        suggestions.Add(file.Artist);
                    if (!string.IsNullOrEmpty(file.Genre))
                        suggestions.Add(file.Genre);
                    if (!string.IsNullOrEmpty(file.Publisher))
                        suggestions.Add(file.Publisher);
                }

                return suggestions.Where(s => s.Contains(partialQuery, StringComparison.OrdinalIgnoreCase))
                                 .OrderBy(s => s)
                                 .Take(10)
                                 .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "検索候補取得中にエラーが発生しました: {Query}", partialQuery);
                return new List<string>();
            }
        }

        public async Task<List<RegexPattern>> GetRegexPresetsAsync()
        {
            // よく使用される正規表現パターンのプリセット
            return await Task.FromResult(new List<RegexPattern>
            {
                new RegexPattern
                {
                    Name = "巻数検索",
                    Pattern = @"第(\d+)巻",
                    Description = "特定の巻数を検索します"
                },
                new RegexPattern
                {
                    Name = "作者名検索",
                    Pattern = @"\[([^×\]]+)(?:×([^\]]+))?\]",
                    Description = "作者名を検索します"
                },
                new RegexPattern
                {
                    Name = "タイトル部分一致",
                    Pattern = @".*{keyword}.*",
                    Description = "タイトルの部分一致検索（{keyword}を置き換えてください）"
                },
                new RegexPattern
                {
                    Name = "ファイルサイズ範囲",
                    Pattern = @".*",
                    Description = "ファイルサイズでの絞り込み（別途実装が必要）"
                }
            });
        }

        /// <summary>
        /// 検索テキストが評価による検索かどうかを判定します
        /// </summary>
        /// <param name="searchText">検索テキスト</param>
        /// <returns>評価検索の場合はtrue</returns>
        private bool IsRatingSearch(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return false;

            var trimmedText = searchText.Trim();

            // ★記号による検索（★、★★、★★★、★★★★、★★★★★）
            if (Regex.IsMatch(trimmedText, @"^★{1,5}$"))
                return true;

            // 数値による検索（★1、★2、★3、★4、★5）
            if (Regex.IsMatch(trimmedText, @"^★[1-5]$"))
                return true;

            // 「評価なし」キーワード
            if (trimmedText.Equals("評価なし", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// 検索テキストから評価値を解析します
        /// </summary>
        /// <param name="searchText">検索テキスト</param>
        /// <returns>評価値（1-5）、または未評価の場合はnull</returns>
        private int? ParseRatingFromSearchText(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return null;

            var trimmedText = searchText.Trim();

            // 「評価なし」キーワードの場合
            if (trimmedText.Equals("評価なし", StringComparison.OrdinalIgnoreCase))
                return null;

            // ★記号の数をカウント（★、★★、★★★、★★★★、★★★★★）
            var starMatch = Regex.Match(trimmedText, @"^★{1,5}$");
            if (starMatch.Success)
            {
                return starMatch.Value.Length; // ★の数が評価値
            }

            // 数値による検索（★1、★2、★3、★4、★5）
            var numberMatch = Regex.Match(trimmedText, @"^★([1-5])$");
            if (numberMatch.Success)
            {
                return int.Parse(numberMatch.Groups[1].Value);
            }

            return null;
        }
    }
}
