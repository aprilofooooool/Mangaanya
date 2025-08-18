using Mangaanya.Models;

namespace Mangaanya.Services
{
    public interface ISearchEngineService
    {
        Task<SearchResult> SearchAsync(SearchQuery query);
        Task RebuildIndexAsync(IProgress<IndexProgress> progress);
        Task<List<string>> GetSearchSuggestionsAsync(string partialQuery);
        Task<List<RegexPattern>> GetRegexPresetsAsync();
    }

    public class SearchQuery
    {
        public string Text { get; set; } = string.Empty;
        public bool UseRegex { get; set; }
        public SearchField SearchFields { get; set; } = SearchField.All;
        public SortOrder SortBy { get; set; } = SortOrder.Relevance;
    }

    public class SearchResult
    {
        public List<MangaFile> Results { get; set; } = new();
        public int TotalCount { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class IndexProgress
    {
        public int ProcessedFiles { get; set; }
        public int TotalFiles { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class RegexPattern
    {
        public string Name { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    [Flags]
    public enum SearchField
    {
        FileName = 1,
        Title = 2,
        Author = 4,
        Genre = 8,
        Publisher = 16,
        Tags = 32,
        All = FileName | Title | Author | Genre | Publisher | Tags
    }

    public enum SortOrder
    {
        Relevance,
        FileName,
        Title,
        ModifiedDate,
        FileSize
    }
}
