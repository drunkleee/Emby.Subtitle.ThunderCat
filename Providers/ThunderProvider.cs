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

namespace Emby.Subtitle.OneOneFiveMaster.Providers
{
    public class ThunderProvider : ISubtitleProvider
    {
        private readonly ILogger _logger;
        private readonly IHttpClient _httpClient;
        private const string ApiDomain = "https://api-shoulei-ssl.xunlei.com";

        public ThunderProvider(ILogger logger, IHttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _logger.Info("[Thunder] Constructor called. Provider loaded.");
        }

        public string Name => "Thunder";

        public IEnumerable<MediaBrowser.Controller.Providers.VideoContentType> SupportedMediaTypes => 
            new[] { MediaBrowser.Controller.Providers.VideoContentType.Movie, MediaBrowser.Controller.Providers.VideoContentType.Episode };

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
        {
            var keyword = request.Name;
            _logger.Info($"[Thunder] Search called. Original keyword: '{keyword}'");
            
            // Extract番号 (JAV code) from full title
            var extractedCode = ExtractCode(keyword);
            if (!string.IsNullOrEmpty(extractedCode))
            {
                _logger.Info($"[Thunder] Extracted code: '{extractedCode}' from '{keyword}'");
                keyword = extractedCode;
            }
            
            bool enabled = true;
            try
            {
                var config = Plugin.Instance?.Configuration;
                enabled = config?.EnableThunder ?? true;
                _logger.Info($"[Thunder] Config check: Enabled={enabled}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[Thunder] Config access failed, defaulting to enabled: {ex.Message}");
            }

            if (!enabled)
            {
                _logger.Info("[Thunder] Disabled by config.");
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                _logger.Info("[Thunder] Keyword is empty.");
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }

            var url = $"{ApiDomain}/oracle/subtitle?name={Uri.EscapeDataString(keyword)}";
            _logger.Info($"[Thunder] Fetching: {url}");

            try
            {
                var options = new HttpRequestOptions
                {
                    Url = url,
                    CancellationToken = cancellationToken
                };
                options.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

                using var response = await _httpClient.GetResponse(options).ConfigureAwait(false);
                using var reader = new StreamReader(response.Content);
                var json = await reader.ReadToEndAsync().ConfigureAwait(false);
                
                _logger.Info($"[Thunder] Got response, length: {json.Length}");

                // Check if result is "ok"
                var resultMatch = Regex.Match(json, @"""result""\s*:\s*""(\w+)""");
                if (!resultMatch.Success || resultMatch.Groups[1].Value != "ok")
                {
                    _logger.Info("[Thunder] Result not ok or not found");
                    return Enumerable.Empty<RemoteSubtitleInfo>();
                }

                var subtitles = new List<RemoteSubtitleInfo>();
                
                var itemPattern = new Regex(@"\{[^{}]*""name""\s*:\s*""([^""]*)""\s*[^{}]*""url""\s*:\s*""([^""]*)""\s*[^{}]*""ext""\s*:\s*""([^""]*)""\s*[^{}]*""score""\s*:\s*(\d+)[^{}]*\}");
                var matches = itemPattern.Matches(json);
                
                _logger.Info($"[Thunder] Found {matches.Count} subtitle matches");
                
                foreach (Match match in matches.Cast<Match>().Take(10))
                {
                    var name = match.Groups[1].Value;
                    var downloadUrl = match.Groups[2].Value.Replace("\\/", "/");
                    var ext = match.Groups[3].Value;
                    var scoreStr = match.Groups[4].Value;
                    int.TryParse(scoreStr, out var score);
                    
                    // Base64 encode URL to avoid path separator issues
                    var encodedId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(downloadUrl));
                    
                    subtitles.Add(new RemoteSubtitleInfo
                    {
                        Id = encodedId,
                        Name = name,
                        ProviderName = Name,
                        Format = ext,
                        CommunityRating = score
                    });
                    
                    _logger.Info($"[Thunder] Added: {name}");
                }
                
                _logger.Info($"[Thunder] Returning {subtitles.Count} results");
                return subtitles;
            }
            catch (Exception ex)
            {
                _logger.Error($"[Thunder] Error searching: {ex}");
            }

            return Enumerable.Empty<RemoteSubtitleInfo>();
        }

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            _logger.Info($"[Thunder] GetSubtitles called with id: {id}");
            
            // Decode Base64 ID back to URL
            string url;
            try
            {
                url = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(id));
            }
            catch
            {
                url = id; // Fallback if not base64
            }
            _logger.Info($"[Thunder] Decoded URL: {url}");
            
            try
            {
                var options = new HttpRequestOptions
                {
                    Url = url,
                    CancellationToken = cancellationToken
                };
                
                var response = await _httpClient.GetResponse(options).ConfigureAwait(false);
                
                var format = "srt";
                if (url.EndsWith(".ass")) format = "ass";
                else if (url.EndsWith(".vtt")) format = "vtt";

                _logger.Info($"[Thunder] Downloaded subtitle, format: {format}");
                
                return new SubtitleResponse
                {
                    Format = format,
                    Language = "chi",
                    Stream = response.Content
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"[Thunder] Error downloading: {ex.Message}");
                return new SubtitleResponse();
            }
        }
        
        /// <summary>
        /// Extract番号 (JAV code) from full title
        /// </summary>
        private string ExtractCode(string fullTitle)
        {
            if (string.IsNullOrWhiteSpace(fullTitle)) return null;
            
            var codePattern = new Regex(@"(\d*[A-Za-z]{2,10}-\d{2,7})", RegexOptions.IgnoreCase);
            var match = codePattern.Match(fullTitle);
            
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpperInvariant();
            }
            
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
