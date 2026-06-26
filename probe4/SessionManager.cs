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
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
                };

                var context = await browser.NewContextAsync(contextOptions);
                var page = await context.NewPageAsync();

                Logger.Log("Navigating to cs.money...");
                await page.GotoAsync("https://cs.money/ru/csgo/trade/", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 60000
                });

                var cookies = await context.CookiesAsync();
                var userAgent = await page.EvaluateAsync<string>("navigator.userAgent");

                var session = new SessionData
                {
                    UserAgent = userAgent,
                    Cookies = cookies.Select(c => new System.Net.Cookie(c.Name, c.Value, c.Path, c.Domain)).ToList(),
                    CookieString = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"))
                };

                Logger.Log("Session refreshed successfully.");
                return session;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to refresh session", ex);
                return null;
            }
        }
    }
}
