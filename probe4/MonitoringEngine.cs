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
        public static HttpClient CreateHttpClient(ProxyInfo proxy)
        {
            var handler = new SocketsHttpHandler
            {
                Proxy = new WebProxy($"{proxy.Host}:{proxy.Port}")
                {
                    Credentials = new NetworkCredential(proxy.Username, proxy.Password)
                },
                UseProxy = true,
                UseCookies = false, // We handle cookies manually via headers
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };

            var client = new HttpClient(handler);

            // Disable default .NET telemetry headers
            // These are usually controlled via Activity.DefaultIdFormat or environmental variables,
            // but we can also ensure they aren't added by being careful with the client.
            // HttpClient doesn't add traceparent by default unless DiagnosticSource is enabled.

            return client;
        }

        public static async Task<SkinResult> FetchSkinAsync(ProxyInfo proxy, string skinName, SessionData session)
        {
            try
            {
                var encodedName = Uri.EscapeDataString(skinName);
                var url = $"https://cs.money/5.0/load_bots_inventory/730?limit=60&offset=0&order=asc&sort=price&name={encodedName}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Set headers
                request.Headers.TryAddWithoutValidation("User-Agent", session.UserAgent);
                request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
                request.Headers.TryAddWithoutValidation("Referer", "https://cs.money/ru/csgo/trade/");
                request.Headers.TryAddWithoutValidation("Origin", "https://cs.money");
                request.Headers.TryAddWithoutValidation("X-Client-App", "web_mobile");

                // Add cookies
                var cookieHeader = string.Join("; ", session.Cookies.Select(c => $"{c.Name}={c.Value}"));
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

                // Ensure no traceparent or other X- headers are sent except X-Client-App
                // HttpRequestMessage by default doesn't have them.
                // We'll just make sure we don't add them.

                var response = await proxy.Client!.SendAsync(request);

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    Logger.Log($"[Proxy {proxy.Host}] 429 Too Many Requests. Banning for 60s.");
                    proxy.BannedUntil = DateTime.UtcNow.AddSeconds(60);
                    return new SkinResult { Name = skinName, Status = 429 };
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    Logger.Log($"[Proxy {proxy.Host}] 403 Forbidden.");
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

                // For this task, we don't need to fully parse the items, just check status and body length
                return new SkinResult
                {
                    Name = skinName,
                    Status = (long)response.StatusCode,
                    BodyLength = content.Length
                };
            }
            catch (Exception ex)
            {
                // Logger.LogError($"Exception fetching {skinName} on {proxy.Host}", ex);
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
