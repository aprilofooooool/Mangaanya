using Microsoft.Extensions.Logging;
using Mangaanya.Models;
using System.Text.Json;
using System.Text;
using System.Net.Http;

namespace Mangaanya.Services
{
    public class AIService : IAIService
    {
        private readonly ILogger<AIService> _logger;
        private readonly IConfigurationManager _config;
        private readonly HttpClient _httpClient;
        
        // キャッシュ（キー: "タイトル|作者", 値: AIResultとキャッシュ時間のタプル）
        private readonly Dictionary<string, (AIResult Result, DateTime CacheTime)> _cache = new();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromHours(24); // キャッシュの有効期間
        private readonly object _cacheLock = new object();

        public AIService(ILogger<AIService> logger, IConfigurationManager config)
        {
            _logger = logger;
            _config = config;
            _httpClient = new HttpClient();
        }

        public async Task<AIResult> GetMangaInfoAsync(string title, string author)
        {
            // 入力値の正規化
            title = title?.Trim() ?? string.Empty;
            author = author?.Trim() ?? string.Empty;
            
            // キャッシュキーの生成
            string cacheKey = $"{title}|{author}";
            
            // キャッシュをチェック
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out var cachedResult))
                {
                    // キャッシュが有効期間内かチェック
                    if (DateTime.Now - cachedResult.CacheTime < _cacheDuration)
                    {
                        
                        return cachedResult.Result;
                    }
                    else
                    {
                        // 期限切れのキャッシュを削除
                        _cache.Remove(cacheKey);
                    }
                }
            }
            
            const int maxRetries = 3;
            int retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                try
                {
                    var apiKey = _config.GetSetting<string>("GeminiApiKey");
                    if (string.IsNullOrEmpty(apiKey))
                    {
                        _logger.LogWarning("GEMINI APIキーが設定されていません");
                        return new AIResult 
                        { 
                            Success = false, 
                            ErrorMessage = "APIキーが設定されていません" 
                        };
                    }

                    // GEMINI-2.5-FLASH APIへのリクエスト構築
                    var prompt = BuildPrompt(title, author);
                    var response = await CallGeminiApiAsync(apiKey, prompt);
                    
                    if (response != null)
                    {
                        var result = ParseGeminiResponse(response);
                        
                        // 結果が成功している場合はキャッシュに保存
                        if (result.Success)
                        {
                            lock (_cacheLock)
                            {
                                _cache[cacheKey] = (result, DateTime.Now);
                            }
                            return result;
                        }
                        
                        // 失敗した場合で、最大リトライ回数に達した場合は結果を返す
                        if (retryCount >= maxRetries - 1)
                        {
                            return result;
                        }
                        
                        // 失敗した場合はリトライ
                        _logger.LogWarning("AI応答の解析に失敗しました。リトライします ({RetryCount}/{MaxRetries}): {Title} - {Author}", 
                            retryCount + 1, maxRetries, title, author);
                        retryCount++;
                        
                        // リトライ前に少し待機
                        await Task.Delay(1000 * (retryCount + 1));
                    }
                    else
                    {
                        // API呼び出しが失敗した場合
                        if (retryCount >= maxRetries - 1)
                        {
                            return new AIResult 
                            { 
                                Success = false, 
                                ErrorMessage = "API応答が無効です" 
                            };
                        }
                        
                        _logger.LogWarning("API呼び出しに失敗しました。リトライします ({RetryCount}/{MaxRetries}): {Title} - {Author}", 
                            retryCount + 1, maxRetries, title, author);
                        retryCount++;
                        
                        // リトライ前に少し待機（指数バックオフ）
                        await Task.Delay(1000 * (int)Math.Pow(2, retryCount));
                    }
                }
                catch (HttpRequestException ex)
                {
                    if (retryCount >= maxRetries - 1)
                    {
                        _logger.LogError(ex, "ネットワークエラーが発生しました: {Title} - {Author}", title, author);
                        return new AIResult 
                        { 
                            Success = false, 
                            ErrorMessage = $"ネットワークエラー: {ex.Message}" 
                        };
                    }
                    
                    _logger.LogWarning(ex, "ネットワークエラーが発生しました。リトライします ({RetryCount}/{MaxRetries}): {Title} - {Author}", 
                        retryCount + 1, maxRetries, title, author);
                    retryCount++;
                    
                    // リトライ前に少し待機（指数バックオフ）
                    await Task.Delay(1000 * (int)Math.Pow(2, retryCount));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI情報取得中にエラーが発生しました: {Title} - {Author}", title, author);
                    return new AIResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"処理エラー: {ex.Message}" 
                    };
                }
            }
            
            // すべてのリトライが失敗した場合
            return new AIResult 
            { 
                Success = false, 
                ErrorMessage = "最大リトライ回数に達しました" 
            };
        }

        public async Task<List<AIResult>> GetMangaInfoBatchAsync(List<MangaFile> files, int maxConcurrency = 30)
        {
            var results = new List<AIResult>();
            var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var tasks = new List<Task<AIResult>>();

            foreach (var file in files)
            {
                tasks.Add(ProcessFileWithSemaphore(file, semaphore));
            }

            var completedResults = await Task.WhenAll(tasks);
            results.AddRange(completedResults);

            return results;
        }

        public bool IsApiAvailable()
        {
            var apiKey = _config.GetSetting<string>("GeminiApiKey");
            return !string.IsNullOrEmpty(apiKey);
        }

        private async Task<AIResult> ProcessFileWithSemaphore(MangaFile file, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            try
            {
                var title = file.Title ?? file.FileName;
                var author = file.OriginalAuthor ?? file.Artist ?? "";
                return await GetMangaInfoAsync(title, author);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private string BuildPrompt(string title, string author)
        {
            return "あなたは漫画情報の専門家です。以下の漫画作品について、正確な情報を提供してください。\n\n" +
                   $"タイトル: {title}\n" +
                   $"作者: {author}\n\n" +
                   "以下の形式でJSONで回答してください：\n" +
                   "{\n" +
                   "  \"publishDate\": \"発売日（YYYY-MM-DD形式、不明な場合はnull）\",\n" +
                   "  \"tags\": \"この漫画を表す特徴的なタグを5つ（カンマ区切り）\"\n" +
                   "}\n\n" +
                   "特に「tags」フィールドには、以下のような情報を含めてください：\n" +
                   "- 物語の舞台（学園、異世界、現代日本、SF、ファンタジーなど）\n" +
                   "- 主要なテーマ（恋愛、バトル、冒険、日常、ホラー、ミステリーなど）\n" +
                   "- 特徴的な要素（ロボット、魔法、スポーツ、料理、音楽、歴史など）\n" +
                   "- ターゲット層（子供向け、大人向け、全年齢など）\n" +
                   "- 作風（コメディ、シリアス、ギャグ、ダーク、ほのぼのなど）\n\n" +
                   "必ず5つのタグを提供してください。情報が不明な場合は、該当フィールドをnullにしてください。説明文や追加コメントは不要です。";
        }

        private async Task<string?> CallGeminiApiAsync(string apiKey, string prompt)
        {
            try
            {
                // GEMINI API エンドポイント
                var endpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";
                
                // 生成パラメータを調整して、より決定論的な応答を得る
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.1,           // 低い温度で決定論的な応答を得る
                        maxOutputTokens = 1024,      // 出力トークン数の制限
                        topP = 0.95,                 // 確率の高い選択肢のみを考慮
                        topK = 40                    // 上位40個の選択肢のみを考慮
                    },
                    safetySettings = new[]
                    {
                        new
                        {
                            category = "HARM_CATEGORY_DANGEROUS_CONTENT",
                            threshold = "BLOCK_NONE"
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
                
                // タイムアウト設定
                var timeoutCancellationTokenSource = new CancellationTokenSource();
                timeoutCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30));

                var response = await _httpClient.PostAsync(endpoint, content, timeoutCancellationTokenSource.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    
                    return responseContent;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("GEMINI API エラー: {StatusCode} - {ReasonPhrase} - {ErrorContent}", 
                        response.StatusCode, response.ReasonPhrase, errorContent);
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GEMINI API呼び出しがタイムアウトしました");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GEMINI API呼び出し中にエラーが発生しました");
                return null;
            }
        }

        private AIResult ParseGeminiResponse(string response)
        {
            try
            {
                using var document = JsonDocument.Parse(response);
                var root = document.RootElement;
                
                // GEMINI APIのレスポンス構造に合わせて解析
                if (root.TryGetProperty("candidates", out var candidates) && 
                    candidates.GetArrayLength() > 0)
                {
                    var firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out var content) &&
                        content.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        var text = parts[0].GetProperty("text").GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            return ParseMangaInfo(text);
                        }
                    }
                }

                return new AIResult 
                { 
                    Success = false, 
                    ErrorMessage = "レスポンスの解析に失敗しました" 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GEMINI レスポンスの解析中にエラーが発生しました");
                return new AIResult 
                { 
                    Success = false, 
                    ErrorMessage = $"解析エラー: {ex.Message}" 
                };
            }
        }

        private AIResult ParseMangaInfo(string jsonText)
        {
            try
            {
                
                
                // コードブロックとJSONを抽出するための正規表現
                var jsonMatch = System.Text.RegularExpressions.Regex.Match(jsonText, @"```(?:json)?\s*({[\s\S]*?})\s*```");
                string jsonPart;
                
                if (jsonMatch.Success)
                {
                    // コードブロック内のJSONを使用
                    jsonPart = jsonMatch.Groups[1].Value;
                    
                }
                else
                {
                    // 従来の方法でJSONを抽出
                    var startIndex = jsonText.IndexOf('{');
                    var endIndex = jsonText.LastIndexOf('}');
                    
                    if (startIndex >= 0 && endIndex > startIndex)
                    {
                        jsonPart = jsonText.Substring(startIndex, endIndex - startIndex + 1);
                        
                    }
                    else
                    {
                        _logger.LogWarning("JSONが見つかりませんでした: {Text}", jsonText);
                        return new AIResult 
                        { 
                            Success = false, 
                            ErrorMessage = "JSON形式の情報が見つかりませんでした" 
                        };
                    }
                }
                
                // 不要な文字を削除して整形
                jsonPart = jsonPart.Trim();
                
                // JSONの検証
                using var document = JsonDocument.Parse(jsonPart);
                var root = document.RootElement;
                
                var result = new AIResult { Success = true };
                
                // ジャンルと出版社はAI情報取得の対象から除外
                result.Genre = null;
                result.Publisher = null;
                
                // 発行日の処理
                if (root.TryGetProperty("publishDate", out var publishDate))
                {
                    if (publishDate.ValueKind != JsonValueKind.Null)
                    {
                        var dateStr = publishDate.GetString();
                        if (!string.IsNullOrEmpty(dateStr) && 
                            !dateStr.Contains("不明") && 
                            !dateStr.Contains("情報がありません"))
                        {
                            // 日付形式の正規化
                            dateStr = dateStr.Replace("年", "-").Replace("月", "-").Replace("日", "");
                            
                            // 様々な日付形式に対応
                            if (DateTime.TryParse(dateStr, out var date))
                            {
                                // 明らかに不適切な日付をフィルタリング
                                if (date.Year >= 1900 && date.Year <= DateTime.Now.Year + 1)
                                {
                                    result.PublishDate = date;
                                }
                            }
                        }
                    }
                }
                
                // タグの処理
                if (root.TryGetProperty("tags", out var tags))
                {
                    if (tags.ValueKind != JsonValueKind.Null)
                    {
                        var tagsStr = tags.GetString();
                        if (!string.IsNullOrEmpty(tagsStr) && 
                            !tagsStr.Contains("不明") && 
                            !tagsStr.Contains("情報がありません"))
                        {
                            // タグの正規化
                            result.Tags = NormalizeTags(tagsStr);
                        }
                    }
                }
                
                // 結果の検証（すべてのフィールドがnullの場合は失敗とみなす）
                if (result.PublishDate == null && 
                    string.IsNullOrEmpty(result.Tags))
                {
                    _logger.LogWarning("有効な情報が含まれていません: {Json}", jsonPart);
                    return new AIResult 
                    { 
                        Success = false, 
                        ErrorMessage = "有効な情報が取得できませんでした" 
                    };
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "漫画情報の解析中にエラーが発生しました");
                return new AIResult 
                { 
                    Success = false, 
                    ErrorMessage = $"情報解析エラー: {ex.Message}" 
                };
            }
        }

        /// <summary>
        /// 漫画ジャンルを正規化するヘルパーメソッド
        /// </summary>
        private string? NormalizeGenre(string? genre)
        {
            if (string.IsNullOrEmpty(genre))
                return null;
                
            // 一般的なジャンル名に正規化
            var normalizedGenre = genre.ToLower();
            
            if (normalizedGenre.Contains("少年") || normalizedGenre.Contains("しょうねん"))
                return "少年漫画";
                
            if (normalizedGenre.Contains("少女") || normalizedGenre.Contains("しょうじょ"))
                return "少女漫画";
                
            if (normalizedGenre.Contains("青年") || normalizedGenre.Contains("せいねん"))
                return "青年漫画";
                
            if (normalizedGenre.Contains("女性") || normalizedGenre.Contains("じょせい"))
                return "女性漫画";
                
            if (normalizedGenre.Contains("成人") || normalizedGenre.Contains("アダルト") || 
                normalizedGenre.Contains("18禁") || normalizedGenre.Contains("r-18"))
                return "成人向け";
                
            // その他のジャンルはそのまま返す
            return genre;
        }
        
        /// <summary>
        /// 出版社名を正規化するヘルパーメソッド
        /// </summary>
        private string? NormalizePublisher(string? publisher)
        {
            if (string.IsNullOrEmpty(publisher))
                return null;
                
            // 一般的な出版社名の表記ゆれを修正
            var normalizedPublisher = publisher
                .Replace("株式会社", "")
                .Replace("出版社", "")
                .Replace("出版", "")
                .Trim();
                
            // 主要な出版社の表記を統一
            if (normalizedPublisher.Contains("講談社"))
                return "講談社";
                
            if (normalizedPublisher.Contains("集英社"))
                return "集英社";
                
            if (normalizedPublisher.Contains("小学館"))
                return "小学館";
                
            if (normalizedPublisher.Contains("角川") || normalizedPublisher.Contains("KADOKAWA"))
                return "KADOKAWA";
                
            if (normalizedPublisher.Contains("秋田書店"))
                return "秋田書店";
                
            // その他の出版社はそのまま返す
            return normalizedPublisher;
        }

        /// <summary>
        /// タグを正規化するヘルパーメソッド
        /// </summary>
        private string? NormalizeTags(string? tags)
        {
            if (string.IsNullOrEmpty(tags))
                return null;
                
            // タグを分割して正規化
            var tagList = tags.Split(',', '、', '・', '/', '|', ';')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t) && 
                       !t.Contains("不明") && 
                       !t.Contains("情報がありません") &&
                       !t.Equals("null", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .ToList();
            
            // タグの優先順位付け（より具体的なタグを優先）
            var prioritizedTags = new List<string>();
            
            // 物語の舞台に関するタグを優先
            var settingTags = new[] { "学園", "異世界", "現代", "SF", "ファンタジー", "歴史", "未来", "宇宙" };
            foreach (var keyword in settingTags)
            {
                var matchingTag = tagList.FirstOrDefault(t => t.Contains(keyword));
                if (matchingTag != null)
                {
                    prioritizedTags.Add(matchingTag);
                    tagList.Remove(matchingTag);
                }
            }
            
            // テーマに関するタグを優先
            var themesTags = new[] { "恋愛", "バトル", "冒険", "日常", "ホラー", "ミステリー", "スポーツ", "料理", "音楽" };
            foreach (var keyword in themesTags)
            {
                var matchingTag = tagList.FirstOrDefault(t => t.Contains(keyword));
                if (matchingTag != null)
                {
                    prioritizedTags.Add(matchingTag);
                    tagList.Remove(matchingTag);
                }
            }
            
            // 作風に関するタグを優先
            var styleTags = new[] { "コメディ", "シリアス", "ギャグ", "ダーク", "ほのぼの", "感動", "アクション" };
            foreach (var keyword in styleTags)
            {
                var matchingTag = tagList.FirstOrDefault(t => t.Contains(keyword));
                if (matchingTag != null)
                {
                    prioritizedTags.Add(matchingTag);
                    tagList.Remove(matchingTag);
                }
            }
            
            // 残りのタグを追加
            prioritizedTags.AddRange(tagList);
            
            // タグが5つに満たない場合、デフォルトタグを追加
            if (prioritizedTags.Count < 5)
            {
                var defaultTags = new List<string> { "漫画", "日本の漫画", "コミック", "エンターテイメント", "読み物" };
                foreach (var tag in defaultTags)
                {
                    if (prioritizedTags.Count < 5 && !prioritizedTags.Contains(tag))
                    {
                        prioritizedTags.Add(tag);
                    }
                }
            }
            
            // 最大5つまでに制限
            prioritizedTags = prioritizedTags.Take(5).ToList();
                
            return string.Join(", ", prioritizedTags);
        }

        /// <summary>
        /// キャッシュをクリアする
        /// </summary>
        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
                _logger.LogInformation("AIキャッシュをクリアしました");
            }
        }
        
        /// <summary>
        /// 期限切れのキャッシュエントリを削除する
        /// </summary>
        public void CleanupCache()
        {
            lock (_cacheLock)
            {
                var expiredKeys = _cache
                    .Where(kv => DateTime.Now - kv.Value.CacheTime > _cacheDuration)
                    .Select(kv => kv.Key)
                    .ToList();
                
                foreach (var key in expiredKeys)
                {
                    _cache.Remove(key);
                }
                
                if (expiredKeys.Count > 0)
                {
                    
                }
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
