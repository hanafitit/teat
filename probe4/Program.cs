using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Probe4
{
    class Program
    {
        static readonly string[] SkinNames = new[]
        {
            "★ Bayonet | Freehand", "★ Bayonet | Doppler", "★ Bayonet | Marble Fade", "★ Bayonet | Tiger Tooth", "★ Bayonet | Fade",
            "★ Karambit | Freehand", "★ Karambit | Doppler", "★ Karambit | Marble Fade", "★ Karambit | Tiger Tooth", "★ Karambit | Fade",
            "★ M9 Bayonet | Freehand", "★ M9 Bayonet | Doppler", "★ M9 Bayonet | Marble Fade", "★ M9 Bayonet | Tiger Tooth", "★ M9 Bayonet | Fade",
            "★ Butterfly Knife | Freehand", "★ Butterfly Knife | Doppler", "★ Butterfly Knife | Marble Fade", "★ Butterfly Knife | Tiger Tooth", "★ Butterfly Knife | Fade",
            "★ Flip Knife | Freehand", "★ Flip Knife | Doppler", "★ Flip Knife | Marble Fade", "★ Flip Knife | Tiger Tooth", "★ Flip Knife | Fade",
            "★ Gut Knife | Freehand", "★ Gut Knife | Doppler", "★ Gut Knife | Marble Fade", "★ Gut Knife | Tiger Tooth", "★ Gut Knife | Fade",
            "★ Huntsman Knife | Freehand", "★ Huntsman Knife | Doppler", "★ Huntsman Knife | Marble Fade", "★ Huntsman Knife | Tiger Tooth", "★ Huntsman Knife | Fade",
            "★ Falchion Knife | Freehand", "★ Falchion Knife | Doppler", "★ Falchion Knife | Marble Fade", "★ Falchion Knife | Tiger Tooth", "★ Falchion Knife | Fade",
            "★ Bowie Knife | Freehand", "★ Bowie Knife | Doppler", "★ Bowie Knife | Marble Fade", "★ Bowie Knife | Tiger Tooth", "★ Bowie Knife | Fade",
            "★ Shadow Daggers | Freehand", "★ Shadow Daggers | Doppler", "★ Shadow Daggers | Marble Fade", "★ Shadow Daggers | Tiger Tooth", "★ Shadow Daggers | Fade",
            "★ Navaja Knife | Freehand", "★ Navaja Knife | Doppler", "★ Navaja Knife | Marble Fade", "★ Navaja Knife | Tiger Tooth", "★ Navaja Knife | Fade",
            "★ Stiletto Knife | Freehand", "★ Stiletto Knife | Doppler", "★ Stiletto Knife | Marble Fade", "★ Stiletto Knife | Tiger Tooth", "★ Stiletto Knife | Fade",
            "★ Talon Knife | Freehand", "★ Talon Knife | Doppler", "★ Talon Knife | Marble Fade", "★ Talon Knife | Tiger Tooth", "★ Talon Knife | Fade",
            "★ Ursus Knife | Freehand", "★ Ursus Knife | Doppler", "★ Ursus Knife | Marble Fade", "★ Ursus Knife | Tiger Tooth", "★ Ursus Knife | Fade",
            "★ Paracord Knife | Freehand", "★ Paracord Knife | Doppler", "★ Paracord Knife | Marble Fade", "★ Paracord Knife | Tiger Tooth", "★ Paracord Knife | Fade",
            "★ Survival Knife | Freehand", "★ Survival Knife | Doppler", "★ Survival Knife | Marble Fade", "★ Survival Knife | Tiger Tooth", "★ Survival Knife | Fade",
            "★ Nomad Knife | Freehand", "★ Nomad Knife | Doppler", "★ Nomad Knife | Marble Fade", "★ Nomad Knife | Tiger Tooth", "★ Nomad Knife | Fade",
            "★ Skeleton Knife | Freehand", "★ Skeleton Knife | Doppler", "★ Skeleton Knife | Marble Fade", "★ Skeleton Knife | Tiger Tooth", "★ Skeleton Knife | Fade",
            "★ Classic Knife | Freehand", "★ Classic Knife | Doppler", "★ Classic Knife | Marble Fade", "★ Classic Knife | Tiger Tooth", "★ Classic Knife | Fade",
            "AWP | Dragon Lore", "AWP | Medusa", "AWP | Asiimov", "AK-47 | Wild Lotus", "AK-47 | Gold Arabesque",
        };

        static readonly List<ProxyInfo> ProxyList = new List<ProxyInfo>
        {
            new ProxyInfo("127.0.0.1", 18080, "", "")
        };

        static int _poolFailures = 0;

        static async Task Main(string[] args)
        {
            // Critical: Disable default .NET telemetry headers (traceparent)
            AppContext.SetSwitch("System.Net.Http.EnableActivityPropagation", false);

            if (args.Contains("--diagnosis"))
            {
                await Diagnosis.RunAsync();
                return;
            }

            Logger.Log("Starting Monitoring Engine v5.1 (Per-Proxy Sessions)...");

            var skinSet = new HashSet<string>(SkinNames);
            var random = new Random();
            var sw = Stopwatch.StartNew();

            while (true)
            {
                // Refresh sessions for proxies that need it
                var refreshTasks = new List<Task>();
                foreach (var p in ProxyList)
                {
                    if (p.Session == null || (DateTime.UtcNow - p.LastRefresh).TotalMinutes >= 15)
                    {
                        refreshTasks.Add(Task.Run(async () => {
                            var session = await SessionManager.RefreshSessionAsync(p);
                            if (session != null)
                            {
                                p.Session = session;
                                p.LastRefresh = DateTime.UtcNow;
                                if (p.Engine == null)
                                    p.Engine = new MonitoringEngine(p, session.CookieString);
                                else
                                    p.Engine.UpdateCookies(session.CookieString);
                            }
                        }));
                    }
                }

                if (refreshTasks.Any())
                {
                    await Task.WhenAll(refreshTasks);
                }

                var activeProxies = ProxyList.Where(p => !p.IsBanned && p.Engine != null).ToList();
                if (activeProxies.Count == 0)
                {
                    Logger.Log("No active proxies with sessions available. Waiting 10 seconds...");
                    _poolFailures++;
                    await Task.Delay(10000);
                    continue;
                }

                var items = skinSet.ToList();
                var itemsPerProxy = (int)Math.Ceiling((double)items.Count / activeProxies.Count);

                var cycleTasks = new List<Task<SkinResult[]>>();
                var startTime = sw.ElapsedMilliseconds;

                for (int i = 0; i < activeProxies.Count; i++)
                {
                    var proxy = activeProxies[i];
                    var proxyItems = items.Skip(i * itemsPerProxy).Take(itemsPerProxy).ToList();

                    cycleTasks.Add(Task.Run(async () => {
                        var tasks = proxyItems.Select(item => proxy.Engine!.FetchSkinAsync(item)).ToList();
                        var results = await Task.WhenAll(tasks);
                        return results;
                    }));
                }

                SkinResult[][] allResults;
                try
                {
                    allResults = await Task.WhenAll(cycleTasks);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Cycle execution failed", ex);
                    _poolFailures++;
                    continue;
                }

                var flatResults = allResults.SelectMany(r => r).ToList();
                int okCount = flatResults.Count(r => r.Status == 200);
                int rateLimitCount = flatResults.Count(r => r.Status == 429);
                int forbiddenCount = flatResults.Count(r => r.Status == 403);
                int errorCount = flatResults.Count(r => r.Status != 200 && r.Status != 429 && r.Status != 403);

                long duration = sw.ElapsedMilliseconds - startTime;
                Logger.Log($"Cycle complete in {duration}ms | OK: {okCount} | 403: {forbiddenCount} | 429: {rateLimitCount} | Errors: {errorCount}");

                if (okCount == 0 && items.Count > 0)
                {
                    _poolFailures++;
                    if (forbiddenCount > 0)
                    {
                        // Immediate refresh if forbidden
                        foreach(var p in ProxyList) p.Session = null;
                    }
                }
                else
                {
                    _poolFailures = 0;
                }

                if (_poolFailures >= 3)
                {
                    Logger.Log("3 consecutive whole-pool failures. Forcing all sessions refresh...");
                    foreach(var p in ProxyList) p.Session = null;
                    _poolFailures = 0;
                }

                // Jitter 0-500ms
                await Task.Delay(random.Next(0, 501));
            }
        }
    }
}
