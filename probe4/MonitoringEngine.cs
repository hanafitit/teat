using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace Probe4
{
    public class MonitoringEngine
    {
        public static async Task<SkinResult> FetchSkinAsync(ProxyInfo proxy, string skinName)
        {
            if (proxy.Page == null)
            {
                return new SkinResult { Name = skinName, Status = -3, Error = "Page not initialized" };
            }

            try
            {
                var encodedName = Uri.EscapeDataString(skinName);
                var url = $"/5.0/load_bots_inventory/730?limit=60&offset=0&order=asc&sort=price&name={encodedName}";

                // Выполняем запрос через браузерный fetch для обхода Cloudflare
                string script = $@"
async () => {{
    try {{
        const res = await fetch('{url}', {{
            headers: {{
                'Accept': 'application/json, text/plain, */*',
                'X-Client-App': 'web_mobile',
                'X-Kl-Ajax-Request': 'Ajax_Request'
            }}
        }});
        const body = await res.text();
        return JSON.stringify({{ status: res.status, body: body }});
    }} catch(e) {{
        return JSON.stringify({{ status: -2, error: e.message }});
    }}
}}";
                var resultJson = await proxy.Page.EvaluateAsync<string>(script);
                var resultData = JsonSerializer.Deserialize<FetchResult>(resultJson);

                if (resultData == null)
                {
                    return new SkinResult { Name = skinName, Status = -1, Error = "Failed to parse fetch result" };
                }

                if (resultData.Status == 429)
                {
                    Logger.Log($"[Proxy {proxy.Host}] 429 Too Many Requests. Banning for 60s.");
                    proxy.BannedUntil = DateTime.UtcNow.AddSeconds(60);
                    return new SkinResult { Name = skinName, Status = 429 };
                }

                if (resultData.Status == 403)
                {
                    Logger.Log($"[Proxy {proxy.Host}] 403 Forbidden.");
                    return new SkinResult { Name = skinName, Status = 403 };
                }

                if (resultData.Status != 200)
                {
                    return new SkinResult { Name = skinName, Status = resultData.Status, Error = resultData.Error };
                }

                if (string.IsNullOrWhiteSpace(resultData.Body) || (!resultData.Body.TrimStart().StartsWith("{") && !resultData.Body.TrimStart().StartsWith("[")))
                {
                    return new SkinResult { Name = skinName, Status = -1, Error = "Invalid JSON response" };
                }

                return new SkinResult
                {
                    Name = skinName,
                    Status = resultData.Status,
                    BodyLength = resultData.Body.Length
                };
            }
            catch (Exception ex)
            {
                return new SkinResult { Name = skinName, Status = -2, Error = ex.Message };
            }
        }

        private class FetchResult
        {
            public int Status { get; set; }
            public string? Body { get; set; }
            public string? Error { get; set; }
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
