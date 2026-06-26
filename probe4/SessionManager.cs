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
        private static IPlaywright? _playwright;
        private static IBrowser? _browser;

        public static async Task InitializePlaywrightAsync()
        {
            if (_playwright == null)
            {
                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
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
                });
            }
        }

        public static async Task<SessionData?> SetupProxyPageAsync(ProxyInfo proxy)
        {
            await InitializePlaywrightAsync();
            Logger.Log($"Setting up page for proxy {proxy.Host}...");

            try
            {
                if (proxy.Context != null) await proxy.Context.CloseAsync();

                var contextOptions = new BrowserNewContextOptions
                {
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                    Proxy = new Proxy
                    {
                        Server = $"http://{proxy.Host}:{proxy.Port}",
                        Username = proxy.Username,
                        Password = proxy.Password
                    },
                    ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
                };

                proxy.Context = await _browser!.NewContextAsync(contextOptions);

                await proxy.Context.AddInitScriptAsync(@"
                    Object.defineProperty(navigator, 'webdriver', { get: () => undefined, configurable: true });
                    delete navigator.__proto__.webdriver;
                    window.chrome = { runtime: {} };
                ");

                proxy.Page = await proxy.Context.NewPageAsync();

                Logger.Log($"Navigating to cs.money on proxy {proxy.Host}...");
                await proxy.Page.GotoAsync("https://cs.money/ru/csgo/trade/", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.NetworkIdle,
                    Timeout = 60000
                });

                var cookies = await proxy.Context.CookiesAsync();
                var userAgent = await proxy.Page.EvaluateAsync<string>("navigator.userAgent");

                Logger.Log($"Page for {proxy.Host} is ready.");
                return new SessionData
                {
                    UserAgent = userAgent,
                    Cookies = cookies.Select(c => new System.Net.Cookie(c.Name, c.Value, c.Path, c.Domain)).ToList()
                };
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to setup page for proxy {proxy.Host}", ex);
                return null;
            }
        }

        public static async Task CleanupAsync()
        {
            if (_browser != null) await _browser.CloseAsync();
            if (_playwright != null) _playwright.Dispose();
        }
    }
}
