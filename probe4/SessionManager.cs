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
            Environment.SetEnvironmentVariable("DEBUG", "pw:api,pw:browser,pw:network");
            Logger.Log($"[Proxy {proxy.Host}] Начинаем процесс обновления сессии. Прокси: {proxy.Host}:{proxy.Port}, Пользователь: {(string.IsNullOrEmpty(proxy.Username) ? "нет" : proxy.Username)}");
            string userDataDir = Path.Combine(Path.GetTempPath(), "playwright_profile_" + Guid.NewGuid().ToString());
            Logger.Log($"[Proxy {proxy.Host}] Временная директория профиля: {userDataDir}");

            try
            {
                using var playwright = await Playwright.CreateAsync();
                Logger.Log($"[Proxy {proxy.Host}] Playwright инициализирован.");

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
                        "--disable-dev-shm-usage",
                        "--enable-logging",
                        "--v=1",
                        $"--log-net-log=net-log-{proxy.Host}.json"
                    }
                };

                if (!string.IsNullOrEmpty(proxy.Username) && !string.IsNullOrEmpty(proxy.Password))
                {
                    Logger.Log($"[Proxy {proxy.Host}] Установка глобальных HttpCredentials для контекста.");
                    persistentOptions.HttpCredentials = new HttpCredentials
                    {
                        Username = proxy.Username,
                        Password = proxy.Password
                    };
                }

                Logger.Log($"[Proxy {proxy.Host}] Запуск PersistentContext...");
                await using var context = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, persistentOptions);
                Logger.Log($"[Proxy {proxy.Host}] Контекст успешно запущен.");

                // Логирование на уровне контекста
                context.RequestFailed += (_, e) => {
                    Logger.Log($"[NETWORK ERROR] [Proxy {proxy.Host}] Request Failed: {e.Method} {e.Url}. Error: {e.Failure}");
                };

                // Stealth scripts
                await context.AddInitScriptAsync(@"
                    Object.defineProperty(navigator, 'webdriver', { get: () => undefined, configurable: true });
                    delete navigator.__proto__.webdriver;
                    window.chrome = { runtime: {} };
                ");

                var page = await context.NewPageAsync();
                Logger.Log($"[Proxy {proxy.Host}] Новая страница создана.");

                // Pipe browser console logs to C# console
                page.Console += (_, e) => {
                    Logger.Log($"[Browser {e.Type.ToUpper()}] [Proxy {proxy.Host}]: {e.Text}");
                };

                page.Response += (_, e) => {
                    if (e.Status >= 400)
                    {
                        Logger.Log($"[NETWORK WARN] [Proxy {proxy.Host}] HTTP {e.Status}: {e.Request.Method} {e.Url}");
                    }
                };

                Logger.Log($"[Proxy {proxy.Host}] Переход на cs.money...");
                IResponse? response = null;
                bool navSuccess = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        Logger.Log($"[Proxy {proxy.Host}] Попытка навигации {attempt}...");
                        response = await page.GotoAsync("https://cs.money/ru/csgo/trade/", new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = 45000
                        });

                        if (response != null)
                        {
                            Logger.Log($"[Proxy {proxy.Host}] Навигация завершена. Статус: {response.Status}");
                            if (response.Status == 407)
                            {
                                Logger.Log($"[Proxy {proxy.Host}] ОШИБКА: Требуется Proxy Authentication (407). Проверьте логин/пароль.");
                            }
                        }

                        navSuccess = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"[Proxy {proxy.Host}] Попытка навигации {attempt} провалена: {ex.Message}");
                        if (ex.Message.Contains("net::ERR_TUNNEL_CONNECTION_FAILED"))
                        {
                            Logger.Log($"[Proxy {proxy.Host}] ОБНАРУЖЕНО: ERR_TUNNEL_CONNECTION_FAILED. Проблема с соединением через прокси.");
                        }
                        if (attempt < 3) await Task.Delay(2000);
                    }
                }

                if (!navSuccess)
                {
                    Logger.Log($"[Proxy {proxy.Host}] Пропуск из-за постоянных ошибок навигации после 3 попыток.");
                    return null;
                }

                // Wait for Cloudflare challenge to pass
                Logger.Log($"[Proxy {proxy.Host}] Ожидание прохождения Cloudflare challenge...");
                bool challengePassed = false;
                for (int i = 0; i < 30; i++)
                {
                    var title = await page.TitleAsync();
                    if (title.Contains("CS.MONEY") && !title.Contains("Just a moment"))
                    {
                        Logger.Log($"[Proxy {proxy.Host}] Challenge пройден! Title: {title}");
                        challengePassed = true;
                        break;
                    }
                    if (i % 5 == 0) Logger.Log($"[Proxy {proxy.Host}] Текущий заголовок страницы: {title}");
                    await Task.Delay(2000);
                }

                if (!challengePassed)
                {
                    Logger.Log($"[Proxy {proxy.Host}] Challenge НЕ пройден (timeout).");
                }

                // Human Emulation
                Logger.Log($"[Proxy {proxy.Host}] Выполнение эмуляции действий человека...");
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
                Logger.Log($"[Proxy {proxy.Host}] Ожидание куки cf_clearance...");
                bool cookieFound = false;
                for (int i = 0; i < 10; i++)
                {
                    var currentCookies = await context.CookiesAsync();
                    if (currentCookies.Any(c => c.Name == "cf_clearance"))
                    {
                        Logger.Log($"[Proxy {proxy.Host}] Кука cf_clearance найдена через {i + 1} сек.");
                        cookieFound = true;
                        break;
                    }
                    await Task.Delay(1000);
                }

                if (!cookieFound)
                {
                    Logger.Log($"[Proxy {proxy.Host}] ПРЕДУПРЕЖДЕНИЕ: кука cf_clearance НЕ найдена после ожидания.");
                }

                // Session Warm-up: Wait for a successful API call
                Logger.Log($"[Proxy {proxy.Host}] Прогрев сессии (API warm-up)...");

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
                        console.log('Начало проверочного fetch-запроса...');
                        for (let i = 0; i < 10; i++) {
                            try {
                                console.log(`Попытка ${i + 1}: Запрос ${url}`);
                                const res = await fetch(url, { headers });
                                console.log(`Попытка ${i + 1} Статус: ${res.status} ${res.statusText}`);
                                if (res.status === 200) {
                                    console.log('Warm-up fetch УСПЕШНО!');
                                    return true;
                                }
                                if (res.status === 403 || res.status === 401) {
                                    const text = await res.text();
                                    console.error(`Попытка ${i + 1} Доступ запрещен (403/401): ${text.substring(0, 200)}`);
                                }
                            } catch (e) {
                                console.error(`Попытка ${i + 1} Исключение: ${e.message}`);
                            }
                            await new Promise(r => setTimeout(r, 2000));
                        }
                        console.error('Warm-up fetch ПРОВАЛЕН после 10 попыток.');
                        return false;
                    }
                ";

                bool warmedUp = await page.EvaluateAsync<bool>(warmUpScript);
                Logger.Log($"[Proxy {proxy.Host}] Результат прогрева: {warmedUp}");

                if (!warmedUp)
                {
                    Logger.Log($"[Proxy {proxy.Host}] Прогрев ПРОВАЛЕН. Сессия может быть невалидной.");
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
                    Logger.Log($"[Proxy {proxy.Host}] ПРЕДУПРЕЖДЕНИЕ: кука cf_clearance не найдена в итоговом списке!");
                }

                Logger.Log($"[Proxy {proxy.Host}] Сессия успешно обновлена.");
                return session;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Proxy {proxy.Host}] Ошибка при обновлении сессии", ex);
                return null;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(userDataDir))
                    {
                        Logger.Log($"[Proxy {proxy.Host}] Удаление временной директории: {userDataDir}");
                        Directory.Delete(userDataDir, true);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[Proxy {proxy.Host}] Не удалось удалить директорию {userDataDir}: {ex.Message}");
                }
            }
        }
    }
}
