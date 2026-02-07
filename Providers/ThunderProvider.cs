using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
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
            bool isCodeExtraction = false;
            if (!string.IsNullOrEmpty(extractedCode))
            {
                _logger.Info($"[Thunder] Extracted code: '{extractedCode}' from '{keyword}'");
                keyword = extractedCode;
                isCodeExtraction = true;
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

            // Calculate CID for smart matching
            string localCid = null;
            if (!string.IsNullOrEmpty(request.MediaPath) && File.Exists(request.MediaPath))
            {
                try 
                {
                    localCid = await GetCidByFileAsync(request.MediaPath).ConfigureAwait(false);
                    _logger.Info($"[Thunder] Calculated local CID: {localCid}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[Thunder] Failed to calculate CID: {ex.Message}");
                }
            }

            if (string.IsNullOrWhiteSpace(keyword))
            {
                _logger.Info("[Thunder] Keyword is empty.");
                return Enumerable.Empty<RemoteSubtitleInfo>();
            }

            var url = $"{ApiDomain}/oracle/subtitle?name={Uri.EscapeDataString(keyword)}";
            _logger.Info($"[Thunder] Fetching: {url}");

            int maxRetries = 2;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var options = new HttpRequestOptions
                    {
                        Url = url,
                        CancellationToken = cancellationToken,
                        TimeoutMs = 30000 // 30 seconds timeout
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
                    
                    // Robust parsing: Find all {...} blocks and extract fields from them
                    var itemRegex = new Regex(@"\{[^{}]+\}"); 
                    var fieldRegex = new Regex(@"""(\w+)""\s*:\s*(?:""([^""]*)""|(\d+|true|false))");
                    
                    var matches = itemRegex.Matches(json);
                    _logger.Info($"[Thunder] Found {matches.Count} item matches (raw objects)");

                    foreach (Match match in matches)
                    {
                        var itemJson = match.Value;
                        
                        string name = null;
                        string downloadUrl = null;
                        string ext = null;
                        string cid = null;
                        int score = 0;

                        // Parse fields within this item
                        var fields = fieldRegex.Matches(itemJson);
                        foreach (Match field in fields)
                        {
                            var key = field.Groups[1].Value;
                            var strValue = field.Groups[2].Success ? field.Groups[2].Value : field.Groups[3].Value;

                            switch (key)
                            {
                                case "name": name = strValue; break;
                                case "url": downloadUrl = strValue; break;
                                case "ext": ext = strValue; break;
                                case "cid": cid = strValue; break;
                                case "score": int.TryParse(strValue, out score); break;
                            }
                        }

                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(downloadUrl)) continue;

                        downloadUrl = downloadUrl.Replace("\\/", "/");
                        
                        // Base64 encode URL to avoid path separator issues
                        var encodedId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(downloadUrl));

                        var isHashMatch = !string.IsNullOrEmpty(localCid) && 
                                          !string.IsNullOrEmpty(cid) && 
                                          string.Equals(localCid, cid, StringComparison.OrdinalIgnoreCase);

                        if (isHashMatch)
                        {
                             name = "[⚡完美匹配] " + name;
                             score += 1000; // Boost score significantly
                        }
                        else if (isCodeExtraction)
                        {
                            // If we matched by Code, gives a small boost
                            score += 100;
                        }

                        subtitles.Add(new RemoteSubtitleInfo
                        {
                            Id = encodedId,
                            Name = name,
                            ProviderName = Name,
                            Format = ext,
                            CommunityRating = score,
                            IsHashMatch = isHashMatch
                        });
                    }
                    
                    _logger.Info($"[Thunder] Returning {subtitles.Count} results");
                    return subtitles.OrderByDescending(s => s.IsHashMatch).ThenByDescending(s => s.CommunityRating);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[Thunder] Error searching (Attempt {attempt}/{maxRetries}): {ex.Message}");
                    if (attempt == maxRetries)
                    {
                        _logger.Error($"[Thunder] Max retries reached. Giving up.");
                    }
                }
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

        /// <summary>
        /// Calculate Thunder CID (SHA1 of specific file parts)
        /// Logic ported from MeiamSubtitles
        /// </summary>
        private async Task<string> GetCidByFileAsync(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous))
            {
                var fileSize = new FileInfo(filePath).Length;
                using (var sha1 = SHA1.Create())
                {
                    var buffer = new byte[0xf000]; // 61440 bytes
                    if (fileSize < 0xf000)
                    {
                        await stream.ReadAsync(buffer, 0, (int)fileSize);
                        buffer = sha1.ComputeHash(buffer, 0, (int)fileSize);
                    }
                    else
                    {
                        // First 0x5000 bytes
                        await stream.ReadAsync(buffer, 0, 0x5000);
                        
                        // Middle 0x5000 bytes
                        stream.Seek(fileSize / 3, SeekOrigin.Begin);
                        await stream.ReadAsync(buffer, 0x5000, 0x5000);
                        
                        // Last 0x5000 bytes
                        stream.Seek(fileSize - 0x5000, SeekOrigin.Begin);
                        await stream.ReadAsync(buffer, 0xa000, 0x5000); // offset 0xa000 is correct (0x5000 * 2)

                        buffer = sha1.ComputeHash(buffer, 0, 0xf000);
                    }
                    
                    var sb = new StringBuilder();
                    foreach (var i in buffer)
                    {
                        sb.Append(i.ToString("X2"));
                    }
                    return sb.ToString();
                }
            }
        }
    }
}
