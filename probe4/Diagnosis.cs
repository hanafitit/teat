using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Playwright;
using System.Text.Json;

namespace Probe4
{
    public class Diagnosis
    {
        public static async Task RunAsync()
        {
            var proxies = new List<ProxyInfo>
            {
                new ProxyInfo("142.111.67.146", 5611, "ycmhblvu", "htols81cakkl"),
                new ProxyInfo("64.137.96.74", 6641, "ycmhblvu", "htols81cakkl")
            };

            foreach (var proxy in proxies)
            {
                await TestProxyAsync(proxy);
            }
        }

        private static async Task TestProxyAsync(ProxyInfo proxy)
        {
            var skinName = "★ Bayonet | Freehand";
            var relativeUrl = $"/5.0/load_bots_inventory/730?limit=60&offset=0&order=asc&sort=price&name={Uri.EscapeDataString(skinName)}";
            var fullUrl = $"https://cs.money{relativeUrl}";

            Console.WriteLine($"\n[Diagnosis] Testing with proxy: {proxy.Host}");

            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions {
                Headless = true,
                Proxy = new Proxy { Server = "http://per-context" },
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            });

            try {
                var context = await browser.NewContextAsync(new BrowserNewContextOptions {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                    Proxy = new Proxy { Server = $"http://{proxy.Host}:{proxy.Port}", Username = proxy.Username, Password = proxy.Password },
                    HttpCredentials = new HttpCredentials { Username = proxy.Username, Password = proxy.Password },
                    ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
                });

                var page = await context.NewPageAsync();

                // Intercept and log headers of load_bots_inventory
                page.Request += (_, request) => {
                    if (request.Url.Contains("load_bots_inventory")) {
                        Console.WriteLine($"\n[Diagnosis] Intercepted Request: {request.Url}");
                        foreach(var header in request.Headers) {
                            Console.WriteLine($"  Header: {header.Key}: {header.Value}");
                        }
                    }
                };

                // Extra stealth
                await page.EvaluateAsync(@"() => {
                    Object.defineProperty(navigator, 'webdriver', { get: () => false });
                }");

                Console.WriteLine("[Diagnosis] Navigating to trade page...");
                var response = await page.GotoAsync("https://cs.money/ru/csgo/trade/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
                Console.WriteLine($"[Diagnosis] Goto Status: {response?.Status}");

                // Wait for challenge
                for (int i = 0; i < 15; i++)
                {
                    var title = await page.TitleAsync();
                    Console.WriteLine($"[Diagnosis] Title: {title}");
                    if (title.Contains("CS.MONEY") && !title.Contains("Just a moment")) break;

                    // Human-like activity
                    await page.Mouse.MoveAsync(i * 10, i * 10);
                    await Task.Delay(2000);
                }

                var cookies = await context.CookiesAsync();
                var cfCookie = cookies.FirstOrDefault(c => c.Name == "cf_clearance");
                Console.WriteLine($"[Diagnosis] cf_clearance: {(cfCookie != null ? "FOUND" : "NOT FOUND")}");

                if (cfCookie != null)
                {
                    // Human interaction: click somewhere
                    await page.ClickAsync("body", new PageClickOptions { Position = new Position { X = 100, Y = 100 } });
                    await Task.Delay(2000);

                    // Test 1: Native Fetch
                    var res1 = await page.EvaluateAsync<string>($@"
                        async () => {{
                            try {{
                                const res = await fetch('{relativeUrl}', {{
                                    headers: {{ 'X-Client-App': 'web_mobile', 'X-Kl-Ajax-Request': 'Ajax_Request' }}
                                }});
                                return JSON.stringify({{ status: res.status, bodyLength: (await res.text()).length }});
                            }} catch (e) {{ return JSON.stringify({{ status: -2, error: e.message }}); }}
                        }}
                    ");
                    Console.WriteLine($"[Diagnosis] Native Fetch (web_mobile): {res1}");

                    // Test 2: Native Fetch (web)
                    var res2 = await page.EvaluateAsync<string>($@"
                        async () => {{
                            try {{
                                const res = await fetch('{relativeUrl}', {{
                                    headers: {{ 'X-Client-App': 'web', 'X-Kl-Ajax-Request': 'Ajax_Request' }}
                                }});
                                return JSON.stringify({{ status: res.status, bodyLength: (await res.text()).length }});
                            }} catch (e) {{ return JSON.stringify({{ status: -2, error: e.message }}); }}
                        }}
                    ");
                    Console.WriteLine($"[Diagnosis] Native Fetch (web): {res2}");

                    // Test 3: MonitoringEngine with direct Cookie objects
                    var session = new SessionData {
                        UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                        Cookies = cookies.Select(c => new System.Net.Cookie(c.Name, c.Value, c.Path, c.Domain)).ToList(),
                        CookieString = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"))
                    };

                    var engine = new MonitoringEngine(proxy, session.CookieString);
                    var httpResult = await engine.FetchSkinAsync(skinName);
                    Console.WriteLine($"[Diagnosis] MonitoringEngine (web) Result: Status={httpResult.Status}, BodyLength={httpResult.BodyLength}");

                    // Try web_mobile in engine
                    // I'll manually modify MonitoringEngine for a second to test or just assume it's one of them.
                }

                await page.ScreenshotAsync(new PageScreenshotOptions { Path = $"diag_{proxy.Host}.png" });

            } finally {
                await browser.CloseAsync();
            }
        }
    }
}
