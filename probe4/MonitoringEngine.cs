using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Probe4
{
    public class MonitoringEngine
    {
        private readonly HttpClient _httpClient;
        private readonly CookieContainer _cookieContainer;
        private readonly ProxyInfo _proxy;

        public MonitoringEngine(ProxyInfo proxy, string baseCookies)
        {
            _proxy = proxy;
            _cookieContainer = new CookieContainer();

            if (!string.IsNullOrEmpty(baseCookies))
            {
                UpdateCookies(baseCookies);
            }

            var handler = new SocketsHttpHandler
            {
                Proxy = new WebProxy("http://127.0.0.1:18080"),
                UseProxy = true,
                UseCookies = true,
                CookieContainer = _cookieContainer,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };

            // Note: Zstd might require .NET 9 or a specific library if not natively in SocketsHttpHandler for this version,
            // but .NET 8 supports it in some contexts. Sticking to standard ones to be safe,
            // but the header will still claim we support it.

            _httpClient = new HttpClient(handler);

            // Hardcoded browser fingerprint headers for Chrome 131
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-client-app", "web");
        }

        public void UpdateCookies(string cookieString)
        {
            if (string.IsNullOrEmpty(cookieString)) return;

            var cookies = cookieString.Split(';');
            foreach (var cookie in cookies)
            {
                var parts = cookie.Split('=', 2);
                if (parts.Length == 2)
                {
                    var name = parts[0].Trim();
                    var value = parts[1].Trim();
                    // Add for both .cs.money and cs.money to ensure Cloudflare cookies are picked up
                    _cookieContainer.Add(new Cookie(name, value, "/", ".cs.money"));
                    _cookieContainer.Add(new Cookie(name, value, "/", "cs.money"));
                }
            }
        }

        public async Task<SkinResult> FetchSkinAsync(string skinName)
        {
            try
            {
                var encodedName = Uri.EscapeDataString(skinName);
                var url = $"https://cs.money/5.0/load_bots_inventory/730?limit=60&offset=0&order=asc&sort=price&name={encodedName}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Add required headers for each request
                request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br, zstd");
                request.Headers.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9");
                request.Headers.TryAddWithoutValidation("Referer", "https://cs.money/ru/csgo/trade/");
                request.Headers.TryAddWithoutValidation("Origin", "https://cs.money");
                request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
                request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
                request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
                request.Headers.TryAddWithoutValidation("X-Kl-Ajax-Request", "Ajax_Request");

                var response = await _httpClient.SendAsync(request);

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    Logger.Log($"[Proxy {_proxy.Host}] 429 Too Many Requests. Banning for 60s.");
                    _proxy.BannedUntil = DateTime.UtcNow.AddSeconds(60);
                    return new SkinResult { Name = skinName, Status = 429 };
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Logger.Log($"[Proxy {_proxy.Host}] 403 Forbidden.");
                    return new SkinResult { Name = skinName, Status = 403 };
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new SkinResult { Name = skinName, Status = (long)response.StatusCode };
                }

                var content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content) || (!content.TrimStart().StartsWith("{") && !content.TrimStart().StartsWith("[")))
                {
                    return new SkinResult { Name = skinName, Status = -1, Error = "Invalid JSON response" };
                }

                return new SkinResult
                {
                    Name = skinName,
                    Status = (long)response.StatusCode,
                    BodyLength = content.Length
                };
            }
            catch (Exception ex)
            {
                return new SkinResult { Name = skinName, Status = -2, Error = ex.Message };
            }
        }
    }

    public class SkinResult
    {
        public string Name { get; set; } = "";
        public long Status { get; set; }
        public int BodyLength { get; set; }
        public string? Error { get; set; }
    }
}
