using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Controller.Subtitles;
using Emby.Subtitle.OneOneFiveMaster.Utils;

namespace Emby.Subtitle.OneOneFiveMaster.Providers
{
    public class SubtitleCatProvider : ISubtitleProvider
    {
        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private const string Domain = "https://subtitlecat.com";

        public SubtitleCatProvider(ILogger logger, IHttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _logger.Info("[SubtitleCat] Constructor called. Provider loaded.");
        }

        public string Name => "SubtitleCat";

        public IEnumerable<MediaBrowser.Controller.Providers.VideoContentType> SupportedMediaTypes => 
            new[] { MediaBrowser.Controller.Providers.VideoContentType.Movie, MediaBrowser.Controller.Providers.VideoContentType.Episode };

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            var keyword = request.Name;
            var language = request.Language ?? "zh-CN"; // Default to Chinese
            _logger.Info($"[SubtitleCat] Search called. Original keyword: '{keyword}', Language: '{language}'");
            
            // Extract番号 (JAV code) from full title - patterns like ABC-123, LULU-421, 390JAC-177
            var extractedCode = ExtractCode(keyword);
            if (!string.IsNullOrEmpty(extractedCode))
            {
                _logger.Info($"[SubtitleCat] Extracted code: '{extractedCode}' from '{keyword}'");
                keyword = extractedCode;
            }
            
            try
            {
                bool enabled = true;
                try
                {
                    var config = Plugin.Instance?.Configuration;
                    enabled = config?.EnableSubtitleCat ?? true;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[SubtitleCat] Config access failed, defaulting to enabled: {ex.Message}");
                }

                if (!enabled)
                {
                    _logger.Info("[SubtitleCat] Disabled by config.");
                    return Enumerable.Empty<RemoteSubtitleInfo>();
                }

                if (string.IsNullOrWhiteSpace(keyword))
                {
                    _logger.Info("[SubtitleCat] Keyword is empty.");
                    return Enumerable.Empty<RemoteSubtitleInfo>();
                }

                int maxRetries = 2;
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        var url = $"{Domain}/index.php?search={Uri.EscapeDataString(keyword)}";
                        _logger.Info($"[SubtitleCat] Fetching (Attempt {attempt}): {url}");
                        
                        var options = new HttpRequestOptions
                        {
                            Url = url,
                            CancellationToken = cancellationToken,
                            TimeoutMs = 30000 // 30 seconds timeout
                        };
                        options.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

                        using var response = await _httpClient.GetResponse(options).ConfigureAwait(false);
                        using var reader = new StreamReader(response.Content);
                        var html = await reader.ReadToEndAsync().ConfigureAwait(false);
                        
                        _logger.Info($"[SubtitleCat] Got response, length: {html.Length}");

                        var results = new List<RemoteSubtitleInfo>();
                        
                        // Match table rows with subtitle links
                        var rowPattern = new Regex(@"<tr[^>]*>.*?<td[^>]*>.*?<a\s+href=""([^""]+)""[^>]*>([^<]+)</a>.*?</tr>", 
                            RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        
                        var matches = rowPattern.Matches(html);
                        _logger.Info($"[SubtitleCat] Found {matches.Count} matches");
                        
                        foreach (Match match in matches.Cast<Match>().Take(10))
                        {
                            var href = match.Groups[1].Value;
                            var title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim());
                            
                            if (string.IsNullOrEmpty(href) || href.StartsWith("#")) continue;
                            
                            var similarity = Similarity.GetJaccardSimilarity(request.Name, title);
                            var score = (int)(similarity * 100);

                            // Encode both URL and language in ID (format: url|language)
                            var fullHref = href.StartsWith("http") ? href : $"{Domain}/{href.TrimStart('/')}";
                            var combinedId = $"{fullHref}|{language}";
                            var encodedId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(combinedId));

                            results.Add(new RemoteSubtitleInfo
                            {
                                Id = encodedId,
                                Name = title,
                                ProviderName = Name,
                                Format = "srt",
                                CommunityRating = (float)score
                            });
                            
                            _logger.Info($"[SubtitleCat] Added result: {title}");
                        }

                        _logger.Info($"[SubtitleCat] Returning {results.Count} results");
                        return results;
                    }
                    catch (Exception ex)
                    {
                         _logger.Warn($"[SubtitleCat] Error searching (Attempt {attempt}/{maxRetries}): {ex.Message}");
                         if (attempt == maxRetries)
                         {
                             _logger.Error($"[SubtitleCat] Max retries reached. Giving up.");
                         }
                    }
                }
                
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }
            catch (Exception ex)
            {
                _logger.Error($"[SubtitleCat] Error searching: {ex}");
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            _logger.Info($"[SubtitleCat] GetSubtitles called with id: {id}");
            
            // Decode Base64 ID back to URL|language
            string url;
            string language = "zh-CN"; // Default
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(id));
                var parts = decoded.Split('|');
                url = parts[0];
                if (parts.Length > 1) language = parts[1];
            }
            catch
            {
                url = id; // Fallback if not base64
            }
            _logger.Info($"[SubtitleCat] Decoded URL: {url}, Language: {language}");
             
            try 
            {
                // First, fetch the subtitle detail page
                var options = new HttpRequestOptions
                {
                    Url = url,
                    CancellationToken = cancellationToken
                };
                options.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

                using var response = await _httpClient.GetResponse(options).ConfigureAwait(false);
                using var reader = new StreamReader(response.Content);
                var html = await reader.ReadToEndAsync().ConfigureAwait(false);

                // Find language-specific download link using pattern from 115master
                // Looking for: <a id="download_zh-CN" href="...">
                var downloadPattern = new Regex($@"<a[^>]+id=""download_{Regex.Escape(language)}""[^>]+href=""([^""]+)""", 
                    RegexOptions.IgnoreCase);
                var match = downloadPattern.Match(html);
                
                if (!match.Success)
                {
                    _logger.Info($"[SubtitleCat] No download link for language {language}, trying fallback patterns");
                    
                    // Try Chinese variants if zh-CN not found
                    if (language.StartsWith("zh"))
                    {
                        // Try other Chinese variants
                        var chinesePattern = new Regex(@"<a[^>]+id=""download_(zh-CN|zho|chi|zh-TW|zh-Hans|zh-Hant)""[^>]+href=""([^""]+)""", 
                            RegexOptions.IgnoreCase);
                        match = chinesePattern.Match(html);
                        if (match.Success)
                        {
                            _logger.Info($"[SubtitleCat] Found Chinese variant download");
                        }
                    }
                    
                    // Last resort: any download link
                    if (!match.Success)
                    {
                        downloadPattern = new Regex(@"<a[^>]+id=""download_[^""]*""[^>]+href=""([^""]+)""", 
                            RegexOptions.IgnoreCase);
                        match = downloadPattern.Match(html);
                    }
                }

                if (match.Success)
                {
                    var downloadHref = match.Groups[match.Groups.Count > 2 ? 2 : 1].Value;
                    if (!string.IsNullOrEmpty(downloadHref)) 
                    {
                        var downloadUrl = downloadHref.StartsWith("http") ? downloadHref : $"{Domain}{downloadHref}";
                        _logger.Info($"[SubtitleCat] Downloading from: {downloadUrl}");
                         
                        var dlOptions = new HttpRequestOptions
                        {
                            Url = downloadUrl,
                            CancellationToken = cancellationToken
                        };
                        dlOptions.UserAgent = options.UserAgent;
                         
                        var dlResponse = await _httpClient.GetResponse(dlOptions).ConfigureAwait(false);
                         
                        return new SubtitleResponse
                        {
                            Language = language,
                            Stream = dlResponse.Content,
                            Format = "srt" 
                        };
                    }
                }
                
                _logger.Warn("[SubtitleCat] No download link found");
            }
            catch(Exception ex)
            {
                _logger.Error($"[SubtitleCat] Error downloading: {ex.Message}");
            }

            return new SubtitleResponse();
        }
        
        /// <summary>
        /// Extract番号 (JAV code) from full title
        /// Matches patterns like: ABC-123, LULU-421, 390JAC-177, SONE-342
        /// </summary>
        private string ExtractCode(string fullTitle)
        {
            if (string.IsNullOrWhiteSpace(fullTitle)) return null;
            
            // Pattern matches: optional digits + letters + dash + digits
            // Examples: LULU-421, ABC-123, 390JAC-177, SONE-342, FC2-PPV-1234567
            var codePattern = new Regex(@"(\d*[A-Za-z]{2,10}-\d{2,7})", RegexOptions.IgnoreCase);
            var match = codePattern.Match(fullTitle);
            
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpperInvariant();
            }
            
            // Also try pattern without dash for codes like "FC2PPV1234567"
            var noDashPattern = new Regex(@"([A-Za-z]{2,6}\d{5,8})", RegexOptions.IgnoreCase);
            match = noDashPattern.Match(fullTitle);
            
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpperInvariant();
            }
            
            return null;
        }
    }
}
