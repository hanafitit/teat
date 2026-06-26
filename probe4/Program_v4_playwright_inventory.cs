// CS.MONEY load_bots_inventory probe v4.1 — 10x10 Proxy Scheme
// 100 requests in <1s using 10 proxies

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.Playwright;

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

    static readonly (string ip, int port)[] ProxyList = new[]
    {
        ("31.59.20.176", 6754),
        ("31.56.127.193", 7684),
        ("45.38.107.97", 6014),
        ("38.154.203.95", 5863),
        ("198.105.121.200", 6462),
        ("64.137.96.74", 6641),
        ("198.23.243.226", 6361),
        ("38.154.185.97", 6370),
        ("142.111.67.146", 5611),
        ("191.96.254.138", 6185),
    };

    const string ProxyUser = "ycmhblvu";
    const string ProxyPass = "htols81cakkl";

    static async Task Main(string[] args)
    {
        Console.WriteLine("Запуск схемы 10x10...");
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

        var contexts = new IBrowserContext[ProxyList.Length];
        var pages = new IPage[ProxyList.Length];
        var skinGroups = new string[ProxyList.Length][];

        Console.WriteLine("Создание 10 контекстов с прокси...");

        for (int i = 0; i < ProxyList.Length; i++)
        {
            var proxy = ProxyList[i];
            var contextOptions = new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
                Proxy = new Proxy { Server = $"http://{proxy.ip}:{proxy.port}", Username = ProxyUser, Password = ProxyPass },
                ViewportSize = new ViewportSize { Width = 800, Height = 600 },
                HttpCredentials = new HttpCredentials { Username = ProxyUser, Password = ProxyPass }
            };
            contexts[i] = await browser.NewContextAsync(contextOptions);

            await contexts[i].AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', { get: () => undefined, configurable: true });
                delete navigator.__proto__.webdriver;
                window.chrome = { runtime: {} };
            ");

            pages[i] = await contexts[i].NewPageAsync();
            skinGroups[i] = SkinNames.Skip(i * 10).Take(10).ToArray();
        }

        Console.WriteLine("Переход на cs.money для инициализации сессий...");
        for (int i = 0; i < pages.Length; i++)
        {
            try
            {
                Console.WriteLine($"Загрузка страницы {i}...");
                await pages[i].GotoAsync("https://cs.money/ru/csgo/trade/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60000 });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке страницы {i}: {ex.Message}");
            }
        }

        Console.WriteLine("Готовность 100%. Начинаем опрос.\n");

        int cycleCount = 0;
        var sw = Stopwatch.StartNew();

        while (true)
        {
            cycleCount++;
            var cycleT0 = sw.ElapsedMilliseconds;

            var cycleTasks = new Task<string>[ProxyList.Length];
            for (int i = 0; i < ProxyList.Length; i++)
            {
                int index = i;
                string namesJson = JsonSerializer.Serialize(skinGroups[index]);
                string script = $@"
async () => {{
    const names = {namesJson};
    const cycleIndex = {cycleCount};
    const results = await Promise.all(names.map(async (name) => {{
        const encoded = encodeURIComponent(name);
        const url = `/5.0/load_bots_inventory/730?limit=60&offset=0&order=asc&sort=price&name=${{encoded}}`;
        const start = performance.now();
        try {{
            const res = await fetch(url, {{
                headers: {{
                    'Accept': 'application/json, text/plain, */*',
                    'X-Client-App': 'web_mobile',
                    'X-Kl-Ajax-Request': 'Ajax_Request'
                }}
            }});
            const body = await res.text();
            const end = performance.now();
            return {{ name, status: res.status, bodyLength: body.length, duration: end - start }};
        }} catch(e) {{
            const end = performance.now();
            return {{ name, status: -2, bodyLength: 0, error: e.message, duration: end - start }};
        }}
    }}));
    return JSON.stringify(results);
}}";
                cycleTasks[i] = pages[i].EvaluateAsync<string>(script);
            }

            string[] allRawResults;
            try
            {
                allRawResults = await Task.WhenAll(cycleTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Цикл {cycleCount}] Ошибка выполнения батчей: {ex.Message}");
                break;
            }

            long cycleDuration = sw.ElapsedMilliseconds - cycleT0;

            int okCount = 0;
            int blockedCount = 0;
            int errorCount = 0;
            bool stopEverything = false;

            foreach (var raw in allRawResults)
            {
                var batchResults = JsonSerializer.Deserialize<List<SkinResult>>(raw) ?? new();
                foreach (var r in batchResults)
                {
                    if (r.Status == 200) okCount++;
                    else if (r.Status == 429) { blockedCount++; stopEverything = true; }
                    else if (r.Status == 403) { errorCount++; stopEverything = true; }
                    else { errorCount++; }
                }
            }

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Цикл {cycleCount} | Время: {cycleDuration}мс | OK: {okCount} | 429: {blockedCount} | 403+: {errorCount}");

            if (stopEverything)
            {
                Console.WriteLine("\n!!! ОБНАРУЖЕНА БЛОКИРОВКА (403/429). ОСТАНОВКА ЦИКЛА !!!");
                break;
            }

            // Условие задачи: <1с. Если работаем быстрее, задержка не нужна (по желанию пользователя).
            // Но чтобы не спамить бесконечно быстро и не перегружать CPU, можно добавить микро-паузу,
            // если пользователь не против. Однако он сказал "нет" на вопрос о ожидании интервала.
            // Поэтому идем на следующий круг сразу.
        }

        Console.WriteLine("Завершение работы...");
        await browser.CloseAsync();
    }
}

class SkinResult
{
    [System.Text.Json.Serialization.JsonPropertyName("name")] public string Name { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("status")] public long Status { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("bodyLength")] public int BodyLength { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("duration")] public double Duration { get; set; }
}
