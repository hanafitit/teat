using System;
using System.Collections.Generic;
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
            try
            {
                using var playwright = await Playwright.CreateAsync();
                var launchOptions = new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Proxy = new Proxy { Server = "http://per-context" },
                    Args = new[] {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-blink-features=AutomationControlled",
                        "--disable-gpu",
                        "--disable-dev-shm-usage"
                    }
                };

                await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);
                var contextOptions = new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
                    Proxy = new Proxy {
                        Server = $"http://{proxy.Host}:{proxy.Port}",
                        Username = proxy.Username,
                        Password = proxy.Password
                    },
                    HttpCredentials = new HttpCredentials {
                        Username = proxy.Username,
                        Password = proxy.Password
                    },
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
                };

                var context = await browser.NewContextAsync(contextOptions);

                // Stealth scripts
                await context.AddInitScriptAsync(@"
                    Object.defineProperty(navigator, 'webdriver', { get: () => undefined, configurable: true });
                    delete navigator.__proto__.webdriver;
                    window.chrome = { runtime: {} };
                ");

                var page = await context.NewPageAsync();

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
                        for (let i = 0; i < 10; i++) {
                            try {
                                const res = await fetch(url, { headers: { 'X-Client-App': 'web_mobile' } });
                                if (res.status === 200) return true;
                            } catch (e) {}
                            await new Promise(r => setTimeout(r, 2000));
                        }
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
        }
    }
}
