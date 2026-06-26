using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Probe4
{
    public class SessionData
    {
        public List<Cookie> Cookies { get; set; } = new();
        public string UserAgent { get; set; } = "";
    }

    public class ProxyInfo
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public Microsoft.Playwright.IPage? Page { get; set; }
        public Microsoft.Playwright.IBrowserContext? Context { get; set; }
        public DateTime BannedUntil { get; set; } = DateTime.MinValue;
        public bool IsBanned => DateTime.UtcNow < BannedUntil;

        public ProxyInfo(string host, int port, string user, string pass)
        {
            Host = host;
            Port = port;
            Username = user;
            Password = pass;
        }
    }

    public static class Logger
    {
        private static readonly string LogFile = "error_log.txt";
        private static readonly object _lock = new();

        public static void Log(string message)
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            Console.WriteLine(logLine);
            lock (_lock)
            {
                File.AppendAllLines(LogFile, new[] { logLine });
            }
        }

        public static void LogError(string message, Exception ex)
        {
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}. Exception: {ex.Message}\n{ex.StackTrace}";
            Console.WriteLine(logLine);
            lock (_lock)
            {
                File.AppendAllLines(LogFile, new[] { logLine });
            }
        }
    }
}
