// CS.MONEY load_bots_inventory probe v4 — Playwright + Promise.all + прокси
//
// Использование:
//   dotnet run -- <интервал_мс> <прокси_url>
// Пример:
//   dotnet run -- 500 "http://user:pass@p.webshare.io:80"

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
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

    static async Task Main(string[] args)
    {
        int intervalMs = args.Length > 0 ? int.Parse(args[0]) : 500;
        string? proxyUrl = args.Length > 1 ? args[1] : null;

        // Парсим user:pass из URL если есть
        string? proxyUser = null, proxyPass = null, proxyServer = null;
        if (proxyUrl != null)
        {
            var uri = new Uri(proxyUrl);
            proxyServer = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                proxyUser = Uri.UnescapeDataString(parts[0]);
                proxyPass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : null;
            }
        }

        Console.WriteLine($"Probe v4 Promise.all — {SkinNames.Length} скинов параллельно, интервал={intervalMs}мс");
        if (proxyUrl != null) Console.WriteLine($"Прокси: {proxyServer} user={proxyUser}");

        using var playwright = await Playwright.CreateAsync();

        // Прокси нужен и на уровне Launch, и на уровне Context
        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = false,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-blink-features=AutomationControlled" }
        };
        if (proxyServer != null)
            launchOptions.Proxy = new Proxy { Server = proxyServer, Username = proxyUser, Password = proxyPass };

        await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);

        var contextOptions = new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            Locale = "ru-RU",
        };
        if (proxyServer != null)
            contextOptions.Proxy = new Proxy { Server = proxyServer, Username = proxyUser, Password = proxyPass };

        var context = await browser.NewContextAsync(contextOptions);

        await context.AddInitScriptAsync(@"
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined, configurable: true });
            delete navigator.__proto__.webdriver;
            window.chrome = { runtime: {} };
        ");

        var page = await context.NewPageAsync();

        Console.WriteLine("Загружаем cs.money...");
        try
        {
            await page.GotoAsync("https://cs.money/ru/csgo/trade/", new PageGotoOptions
            {
                Timeout = 60_000,
                WaitUntil = WaitUntilState.NetworkIdle
            });
        }
        catch (Exception ex) { Console.WriteLine($"Предупреждение: {ex.Message}"); }

        await Task.Delay(3000);
        Console.WriteLine("Страница загружена. Начинаем параллельный опрос...\n");

        string logPath = "inventory_probe_log_v4.csv";
        bool isNew = !File.Exists(logPath);
        using var log = new StreamWriter(logPath, append: true);
        if (isNew) { log.WriteLine("local_time_iso,cycle_ms,ok_count,blocked_count,error_count,changed_skins"); log.Flush(); }

        var lastHashes = new Dictionary<string, string>();
        int cycleCount = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Console.CancelKeyPress += (s, e) => Console.WriteLine($"\n--- Остановлено после {cycleCount} циклов ---");

        while (true)
        {
            cycleCount++;
            var t0 = sw.ElapsedMilliseconds;
            string namesJson = JsonSerializer.Serialize(SkinNames);
            string script = $@"
async () => {{
    const names = {namesJson};
    const results = await Promise.all(names.map(async (name) => {{
        const encoded = encodeURIComponent(name);
        const url = `/5.0/load_bots_inventory/730?limit=60&offset=0&order=asc&sort=price&name=${{encoded}}`;
        try {{
            const res = await fetch(url, {{
                credentials: 'include',
                headers: {{ 'Accept': 'application/json, text/plain, */*', 'X-Client-App': 'web_mobile', 'X-Kl-Ajax-Request': 'Ajax_Request' }}
            }});
            const body = await res.text();
            return {{ name, status: res.status, body }};
        }} catch(e) {{
            return {{ name, status: -2, body: '', error: e.message }};
        }}
    }}));
    return JSON.stringify(results);
}}";

            try
            {
                var raw = await page.EvaluateAsync<string>(script);
                long cycleMs = sw.ElapsedMilliseconds - t0;
                var results = JsonSerializer.Deserialize<List<SkinResult>>(raw ?? "[]") ?? new();

                int okCount = 0, blockedCount = 0, errorCount = 0;
                var changedSkins = new List<string>();

                foreach (var r in results)
                {
                    if (r.Status == 200)
                    {
                        okCount++;
                        string hash = ComputeHash(r.Body ?? "");
                        if (lastHashes.TryGetValue(r.Name, out var prev) && prev != hash) changedSkins.Add(r.Name);
                        lastHashes[r.Name] = hash;
                    }
                    else if (r.Status == 403 || r.Status == 429) blockedCount++;
                    else errorCount++;
                }

                string changedStr = changedSkins.Count > 0 ? $" | ИЗМЕНИЛИСЬ: {string.Join(", ", changedSkins)}" : "";
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Цикл #{cycleCount} — {cycleMs}мс | OK:{okCount} БЛОК:{blockedCount} ERR:{errorCount}{changedStr}");
                if (blockedCount > SkinNames.Length * 0.2) Console.WriteLine($"  ⚠ Много блоков ({blockedCount}/{SkinNames.Length})!");

                log.WriteLine($"{DateTime.Now:O},{cycleMs},{okCount},{blockedCount},{errorCount},{string.Join(";", changedSkins).Replace(',', '|')}");
                log.Flush();
            }
            catch (Exception ex) { Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Ошибка цикла: {ex.Message}"); }

            long elapsed = sw.ElapsedMilliseconds - t0;
            int delay = Math.Max(0, intervalMs - (int)elapsed);
            if (delay > 0) await Task.Delay(delay);
        }
    }

    static string ComputeHash(string input)
    {
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }
}

class SkinResult
{
    [System.Text.Json.Serialization.JsonPropertyName("name")] public string Name { get; set; } = "";
    [System.Text.Json.Serialization.JsonPropertyName("status")] public int Status { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("body")] public string? Body { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("error")] public string? Error { get; set; }
}
