using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;
using System.Timers;

namespace FocusApp
{
    class Program
    {
        static ProxyServer proxy;
        static List<BlockEntry> blockedList;
        static List<string> messages;
        static Random random = new();
        static Timer focusTimer;
        static DateTime focusEndTime;
        static bool focusActive = false;

        // Statistics & logs
        static int blockCount = 0;
        static List<RequestLogEntry> requestLogs = new();

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            LoadConfig();
            Console.WriteLine("FocusApp v1.3");
            CommandLoop();
        }

        static void LoadConfig()
        {
            // 10 motivational messages
            var defaultMessages = new List<string>
            {
                "Stay focused! You’ve got this.",
                "Keep going—every minute counts.",
                "Focus now, succeed later.",
                "Don’t give up—you’re almost there.",
                "Your goals are waiting—push forward!",
                "Distraction is the enemy of progress.",
                "You are building your future right now.",
                "Stay in the zone—success is near.",
                "Every second focused is a step ahead.",
                "Your best work happens when you focus."
            };

            // Default blocked list
            var defaultBlocked = new List<BlockEntry>
            {
                new BlockEntry { Name = "X", Host = "x.com" },
                new BlockEntry { Name = "Facebook", Host = "facebook.com" },
                new BlockEntry { Name = "Facebook WWW", Host = "www.facebook.com" },
                new BlockEntry { Name = "Instagram", Host = "instagram.com" },
                new BlockEntry { Name = "Instagram WWW", Host = "www.instagram.com" }
            };

            // Blocked sites
            const string sitesFile = "blockedSites.json";
            if (!File.Exists(sitesFile))
            {
                File.WriteAllText(sitesFile, JsonSerializer.Serialize(defaultBlocked, new JsonSerializerOptions { WriteIndented = true }));
                blockedList = defaultBlocked;
            }
            else
            {
                try
                {
                    blockedList = JsonSerializer.Deserialize<List<BlockEntry>>(File.ReadAllText(sitesFile))
                                  ?? defaultBlocked;
                }
                catch
                {
                    blockedList = defaultBlocked;
                }
            }

            // Motivational messages
            const string msgFile = "messages.json";
            if (!File.Exists(msgFile))
            {
                File.WriteAllText(msgFile, JsonSerializer.Serialize(defaultMessages, new JsonSerializerOptions { WriteIndented = true }));
                messages = defaultMessages;
            }
            else
            {
                try
                {
                    messages = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(msgFile))
                               ?? defaultMessages;
                }
                catch
                {
                    messages = defaultMessages;
                }
            }
        }

        static void CommandLoop()
        {
            while (true)
            {
                Console.Write("> ");
                var input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input)) continue;
                var parts = input.Split(' ', 3);
                switch (parts[0].ToLower())
                {
                    case "add":
                        if (parts.Length < 3)
                        {
                            Console.WriteLine("Usage: add [name] [host]");
                            break;
                        }
                        if (blockedList.Any(b => b.Name.Equals(parts[1], StringComparison.OrdinalIgnoreCase)))
                        {
                            Console.WriteLine($"Name '{parts[1]}' already exists.");
                            break;
                        }
                        blockedList.Add(new BlockEntry { Name = parts[1], Host = parts[2] });
                        File.WriteAllText("blockedSites.json", JsonSerializer.Serialize(blockedList, new JsonSerializerOptions { WriteIndented = true }));
                        Console.WriteLine($"Added {parts[1]} → {parts[2]}");
                        break;

                    case "remove":
                        if (parts.Length < 2)
                        {
                            Console.WriteLine("Usage: remove [name]");
                            break;
                        }
                        var removed = blockedList.RemoveAll(b => b.Name.Equals(parts[1], StringComparison.OrdinalIgnoreCase));
                        Console.WriteLine(removed > 0 ? "Removed." : "Not found.");
                        File.WriteAllText("blockedSites.json", JsonSerializer.Serialize(blockedList, new JsonSerializerOptions { WriteIndented = true }));
                        break;

                    case "list":
                        foreach (var b in blockedList)
                            Console.WriteLine($"• {b.Name} = {b.Host}");
                        break;

                    case "focus":
                        if (focusActive)
                        {
                            Console.WriteLine("Focus mode is already active.");
                            break;
                        }
                        if (parts.Length < 2 || !int.TryParse(parts[1], out var mins) || mins < 1)
                        {
                            Console.WriteLine("Usage: focus [minutes]");
                            break;
                        }
                        focusEndTime = DateTime.Now.AddMinutes(mins);
                        ProxyConfigurator.EnableProxy("127.0.0.1:8888");
                        if (!StartProxy())
                        {
                            ProxyConfigurator.DisableProxy();
                            break;
                        }
                        StartTimer(mins);
                        focusActive = true;
                        blockCount = 0;
                        requestLogs.Clear();
                        Console.WriteLine($"🔒 Focus mode ON for {mins} minutes. Set browser proxy to 127.0.0.1:8888");
                        break;

                    case "stop":
                        if (!focusActive)
                        {
                            Console.WriteLine("Focus mode is not active.");
                            break;
                        }
                        StopFocusMode();
                        Console.WriteLine("Focus mode stopped.");
                        break;

                    case "stats":
                        ShowStats();
                        break;

                    case "log":
                        ShowLog();
                        break;

                    case "exit":
                        CleanExit();
                        return;

                    default:
                        Console.WriteLine("Commands: add, remove, list, focus, stop, stats, log, exit");
                        break;
                }
            }
        }

        static bool StartProxy()
        {
            try
            {
                if (proxy != null)
                {
                    proxy.Stop();
                    proxy.Dispose();
                }
                proxy = new ProxyServer();
                var ep = new ExplicitProxyEndPoint(IPAddress.Loopback, 8888, true);
                proxy.BeforeRequest += OnRequestAsync;
                proxy.AddEndPoint(ep);
                proxy.Start();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start proxy: {ex.Message}");
                proxy = null;
                return false;
            }
        }

        static void StartTimer(int minutes)
        {
            focusTimer?.Stop();
            focusTimer?.Dispose();

            focusTimer = new Timer(minutes * 60_000) { AutoReset = false };
            focusTimer.Elapsed += (s, e) =>
            {
                StopFocusMode();
                Console.WriteLine("\n🔔 Focus complete – proxy turned off.");
            };
            focusTimer.Start();
        }

        static void StopFocusMode()
        {
            try
            {
                focusTimer?.Stop();
                focusTimer?.Dispose();
                focusTimer = null;
                proxy?.Stop();
                proxy?.Dispose();
                proxy = null;
                focusActive = false;
                ProxyConfigurator.DisableProxy();
            }
            catch { }
        }

        static void CleanExit()
        {
            try
            {
                StopFocusMode();
            }
            catch { }
            Console.WriteLine("Goodbye.");
        }

        private static Task OnRequestAsync(object sender, SessionEventArgs e)
        {
            var host = e.HttpClient.Request.RequestUri.Host;
            bool isBlocked = IsBlocked(host);
            string msg = "";
            string statusIcon = "";
            ConsoleColor color = Console.ForegroundColor;

            if (isBlocked)
            {
                msg = messages[random.Next(messages.Count)];
                blockCount++;
                statusIcon = "🔒";
            }
            else
            {
                statusIcon = "✅";
            }

            // Add to log (always)
            requestLogs.Add(new RequestLogEntry
            {
                Time = DateTime.Now,
                Host = host,
                Status = isBlocked ? "Blocked" : "Allowed",
                StatusIcon = statusIcon,
                Message = msg
            });
            if (requestLogs.Count > 100)
                requestLogs.RemoveAt(0);

            if (isBlocked)
            {
                var now = DateTime.Now;
                var timeLeft = focusEndTime > now ? focusEndTime - now : TimeSpan.Zero;
                var minutesLeft = Math.Max(0, (int)timeLeft.TotalMinutes);
                var secondsLeft = Math.Max(0, timeLeft.Seconds);

                var gifUrl = "https://media.giphy.com/media/v1.Y2lkPTc5MGI3NjExbTliaXBreXI0MnpudnFteGlncTJic3ZmOXRnemY3bGhxdm9kNWs0eSZlcD12MV9pbnRlcm5hbF9naWZfYnlfaWQmY3Q9Zw/PRgs2sn03T1xpCSWKe/giphy.gif";

                var html = $@"
<html>
<head>
<title>Focus Mode</title>
</head>
<body style='font-family:sans-serif; text-align:center; background:#fafafa; margin-top:10%;'>
    <img src='{gifUrl}' width='180' height='180' alt='Motivation GIF' style='border-radius:16px; box-shadow:0 0 8px #ccc; margin-bottom:32px;'><br>
    <h2 style='margin-bottom:12px;'>{msg}</h2>
    <h3>Time remaining: <span style='color:#0078d4'>{minutesLeft:D2}:{secondsLeft:D2}</span></h3>
    <p style='color:#888'>Stay disciplined, and you will thank yourself later.</p>
</body>
</html>";
                e.Ok(html);
            }
            return Task.CompletedTask;
        }

        static bool IsBlocked(string host)
        {
            foreach (var b in blockedList)
            {
                if (host.Equals(b.Host, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + b.Host, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        static void ShowStats()
        {
            Console.WriteLine("=== FocusApp Productivity Stats ===");
            Console.WriteLine($"Total blocked attempts this session: {blockCount}");
            double minutesSaved = blockCount * 1.5;
            Console.WriteLine($"Estimated time saved: {minutesSaved:F1} minutes");
            Console.WriteLine($"Efficiency increased! You’ve protected your focus {blockCount} times.");
            if (blockCount > 0)
                Console.WriteLine("Great job! Every blocked distraction brings you closer to your goals.");
            else
                Console.WriteLine("Start a focus session and see your stats here!");
        }

        static void ShowLog()
        {
            Console.WriteLine("=== Last 20 Website Requests (Blocked and Allowed) ===");
            var last = requestLogs.Skip(Math.Max(0, requestLogs.Count - 20)).ToList();
            if (!last.Any())
            {
                Console.WriteLine("No requests recorded yet.");
                return;
            }
            foreach (var entry in last)
            {
                // Use color for status (where supported)
                if (entry.Status == "Blocked")
                    Console.ForegroundColor = ConsoleColor.Red;
                else
                    Console.ForegroundColor = ConsoleColor.Green;

                Console.Write($"{entry.StatusIcon} ");

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write($"{entry.Time:HH:mm:ss dd-MM} | ");

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{entry.Host.PadRight(25)} ");

                Console.ForegroundColor = entry.Status == "Blocked" ? ConsoleColor.Red : ConsoleColor.Green;
                Console.Write($"{entry.Status.PadRight(7)}");

                if (!string.IsNullOrWhiteSpace(entry.Message))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($" | \"{entry.Message}\"");
                }
                Console.ResetColor();
                Console.WriteLine();
            }
            Console.ResetColor();
        }
    }

    class BlockEntry
    {
        public string Name { get; set; }
        public string Host { get; set; }
    }

    class RequestLogEntry
    {
        public DateTime Time { get; set; }
        public string Host { get; set; }
        public string Status { get; set; } // "Blocked" or "Allowed"
        public string StatusIcon { get; set; } // "🔒" or "✅"
        public string Message { get; set; } // Motivational message if blocked
    }

    public static class ProxyConfigurator
    {
        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        public static void EnableProxy(string proxyServer)
        {
            using var reg = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true);
            reg.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
            reg.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
            RefreshSettings();
            Console.WriteLine($"System proxy set to {proxyServer}");
        }

        public static void DisableProxy()
        {
            using var reg = Registry.CurrentUser.OpenSubKey(
                "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true);
            reg.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            reg.DeleteValue("ProxyServer", throwOnMissingValue: false);
            RefreshSettings();
            Console.WriteLine("System proxy disabled");
        }

        private static void RefreshSettings()
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
    }
}
