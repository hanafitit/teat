using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Probe4
{
    public static class SessionManager
    {
        public static async Task<SessionData?> RefreshSessionAsync(ProxyInfo proxy)
        {
            Logger.Log($"Refreshing session using proxy {proxy.Host}...");
            string userDataDir = Path.Combine(Path.GetTempPath(), "playwright_profile_" + Guid.NewGuid().ToString());
            try
            {
                using var playwright = await Playwright.CreateAsync();

                var persistentOptions = new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = true,
                    Proxy = new Proxy {
                        Server = $"http://{proxy.Host}:{proxy.Port}"
                    },
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
                    IgnoreHTTPSErrors = true,
                    Args = new[] {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-blink-features=AutomationControlled",
                        "--disable-gpu",
                        "--disable-dev-shm-usage"
                    }
                };

                if (!string.IsNullOrEmpty(proxy.Username) && !string.IsNullOrEmpty(proxy.Password))
                {
                    persistentOptions.HttpCredentials = new HttpCredentials
                    {
                        Username = proxy.Username,
                        Password = proxy.Password
                    };
                }

                await using var context = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, persistentOptions);

                // Stealth scripts
                await context.AddInitScriptAsync(@"
                    Object.defineProperty(navigator, 'webdriver', { get: () => undefined, configurable: true });
                    delete navigator.__proto__.webdriver;
                    window.chrome = { runtime: {} };
                ");

                var page = await context.NewPageAsync();

                // Pipe browser console logs to C# console
                page.Console += (_, e) => {
                    var color = e.Type == "error" ? "RED" : "YELLOW";
                    Logger.Log($"[Browser {e.Type.ToUpper()}] [Proxy {proxy.Host}]: {e.Text}");
                };

                Logger.Log($"[Proxy {proxy.Host}] Navigating to cs.money...");
                IResponse? response = null;
                bool navSuccess = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        response = await page.GotoAsync("https://cs.money/ru/csgo/trade/", new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = 45000
                        });
                        navSuccess = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[Proxy {proxy.Host}] Navigation attempt {attempt} failed: {ex.Message}");
                        if (attempt < 3) await Task.Delay(2000);
                    }
                }

                if (!navSuccess)
                {
                    Logger.Log($"[Proxy {proxy.Host}] Skipped due to persistent timeouts after 3 attempts.");
                    return null;
                }

                // Wait for Cloudflare challenge to pass
                Logger.Log($"[Proxy {proxy.Host}] Waiting for Cloudflare challenge...");
                bool challengePassed = false;
                for (int i = 0; i < 30; i++)
                {
                    var title = await page.TitleAsync();
                    if (title.Contains("CS.MONEY") && !title.Contains("Just a moment"))
                    {
                        Logger.Log($"[Proxy {proxy.Host}] Challenge passed! Title: {title}");
                        challengePassed = true;
                        break;
                    }
                    await Task.Delay(2000);
                }

                if (!challengePassed)
                {
                    Logger.Log($"[Proxy {proxy.Host}] Challenge NOT passed (timeout).");
                }

                // Human Emulation
                Logger.Log($"[Proxy {proxy.Host}] Performing human emulation...");
                var random = new Random();
                await page.Mouse.MoveAsync(random.Next(100, 1000), random.Next(100, 600));
                await Task.Delay(random.Next(200, 500));
                await page.Mouse.MoveAsync(random.Next(100, 1000), random.Next(100, 600));
                await Task.Delay(random.Next(200, 500));

                await page.EvaluateAsync("window.scrollBy(0, 400)");
                await Task.Delay(500);
                await page.EvaluateAsync("window.scrollBy(0, -400)");
                await Task.Delay(500);

                await page.ClickAsync("body", new PageClickOptions { Position = new Position { X = random.Next(10, 100), Y = random.Next(10, 100) } });
                await Task.Delay(1000);

                // Hard Cookie Wait
                Logger.Log($"[Proxy {proxy.Host}] Waiting for cf_clearance cookie...");
                bool cookieFound = false;
                for (int i = 0; i < 10; i++)
                {
                    var currentCookies = await context.CookiesAsync();
                    if (currentCookies.Any(c => c.Name == "cf_clearance"))
                    {
                        Logger.Log($"[Proxy {proxy.Host}] cf_clearance cookie found after {i + 1} seconds.");
                        cookieFound = true;
                        break;
                    }
                    await Task.Delay(1000);
                }

                if (!cookieFound)
                {
                    Logger.Log($"[Proxy {proxy.Host}] WARNING: cf_clearance cookie NOT found after wait.");
                }

                // Session Warm-up: Wait for a successful API call
                Logger.Log($"[Proxy {proxy.Host}] Waiting for session warm-up...");

                var warmUpScript = @"
                    async () => {
                        const url = '/5.0/load_bots_inventory/730?limit=1&offset=0&order=asc&sort=price';
                        const headers = {
                            'x-client-app': 'web',
                            'X-Kl-Ajax-Request': 'Ajax_Request',
                            'Accept': 'application/json, text/plain, */*',
                            'sec-fetch-dest': 'empty',
                            'sec-fetch-mode': 'cors',
                            'sec-fetch-site': 'same-origin'
                        };
                        console.log('Starting warm-up fetch test...');
                        for (let i = 0; i < 10; i++) {
                            try {
                                console.log(`Attempt ${i + 1}: Fetching ${url}`);
                                const res = await fetch(url, { headers });
                                console.log(`Attempt ${i + 1} Status: ${res.status} ${res.statusText}`);
                                if (res.status === 200) {
                                    console.log('Warm-up fetch SUCCESS!');
                                    return true;
                                }
                                if (res.status === 403 || res.status === 401) {
                                    const text = await res.text();
                                    console.error(`Attempt ${i + 1} Access Denied: ${text.substring(0, 200)}`);
                                }
                            } catch (e) {
                                console.error(`Attempt ${i + 1} Exception: ${e.message}`);
                            }
                            await new Promise(r => setTimeout(r, 2000));
                        }
                        console.error('Warm-up fetch FAILED after 10 attempts.');
                        return false;
                    }
                ";

                bool warmedUp = await page.EvaluateAsync<bool>(warmUpScript);
                Logger.Log($"[Proxy {proxy.Host}] Warm-up result: {warmedUp}");

                if (!warmedUp)
                {
                    Logger.Log($"[Proxy {proxy.Host}] Warm-up FAILED. Session might be invalid.");
                    // According to requirements, failure in warm-up means we should return null or handle retry.
                    // For now, we continue but the warning is logged. The user said:
                    // "It should be considered a Failure if the warmUpScript fails, even if the cookie is present."
                    return null;
                }

                var cookies = await context.CookiesAsync();
                var userAgent = await page.EvaluateAsync<string>("navigator.userAgent");

                var session = new SessionData
                {
                    UserAgent = userAgent,
                    Cookies = cookies.Select(c => new System.Net.Cookie(c.Name, c.Value, c.Path, c.Domain)).ToList(),
                    CookieString = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"))
                };

                if (!cookies.Any(c => c.Name == "cf_clearance"))
                {
                    Logger.Log($"[Proxy {proxy.Host}] WARNING: cf_clearance cookie not found!");
                }

                Logger.Log($"[Proxy {proxy.Host}] Session refreshed successfully.");
                return session;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Proxy {proxy.Host}] Failed to refresh session", ex);
                return null;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(userDataDir))
                    {
                        Directory.Delete(userDataDir, true);
                    }
                }
                catch {}
            }
        }
    }
}
