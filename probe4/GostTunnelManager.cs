using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Probe4
{
    public static class GostTunnelManager
    {
        private static Process? _gostProcess;

        public static void Start(List<ProxyInfo> proxies)
        {
            if (_gostProcess != null && !_gostProcess.HasExited)
            {
                Logger.Log("[GostTunnelManager] GOST is already running.");
                return;
            }

            var argsBuilder = new StringBuilder();
            // Local listener on port 18080 without auth
            argsBuilder.Append("-L=http://:18080 ");

            // Add each proxy as an upstream SOCKS5 target
            foreach (var proxy in proxies)
            {
                // Format: -F=socks5://username:password@proxy_ip:proxy_port
                argsBuilder.Append($"-F=socks5://{proxy.Username}:{proxy.Password}@{proxy.Host}:{proxy.Port} ");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "gost.exe",
                Arguments = argsBuilder.ToString().Trim(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                Logger.Log($"[GostTunnelManager] Starting GOST with args: {startInfo.Arguments}");
                _gostProcess = Process.Start(startInfo);

                if (_gostProcess == null)
                {
                    Logger.Log("[GostTunnelManager] CRITICAL: Failed to start GOST process.");
                    return;
                }

                _gostProcess.OutputDataReceived += (s, e) => { if (e.Data != null) Logger.Log($"[GOST] {e.Data}"); };
                _gostProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) Logger.Log($"[GOST ERROR] {e.Data}"); };

                _gostProcess.BeginOutputReadLine();
                _gostProcess.BeginErrorReadLine();

                Logger.Log("[GostTunnelManager] GOST process started successfully on 127.0.0.1:18080");
            }
            catch (Exception ex)
            {
                Logger.LogError("[GostTunnelManager] Error starting GOST process", ex);
            }
        }

        public static void Stop()
        {
            if (_gostProcess != null && !_gostProcess.HasExited)
            {
                Logger.Log("[GostTunnelManager] Stopping GOST process...");
                try
                {
                    _gostProcess.Kill(true); // Kill entire process tree
                    _gostProcess.Dispose();
                    _gostProcess = null;
                    Logger.Log("[GostTunnelManager] GOST process stopped.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("[GostTunnelManager] Error stopping GOST process", ex);
                }
            }
        }
    }
}
