# FocusApp — README

FocusApp is a Windows console utility that enforces a timed “focus mode” by routing traffic through a local HTTP/HTTPS explicit proxy and blocking access to configured distracting domains. It also displays motivational messages, captures basic statistics, and keeps a rolling request log for the current session.

---

## Table of Contents

1. [Features](#features)
2. [How it Works](#how-it-works)
3. [Requirements](#requirements)
4. [Build & Run](#build--run)
5. [Commands](#commands)
6. [Configuration Files](#configuration-files)
7. [Session Statistics & Logs](#session-statistics--logs)
8. [Proxy Behavior & Compatibility](#proxy-behavior--compatibility)
9. [Troubleshooting](#troubleshooting)
10. [Security & Privacy Notes](#security--privacy-notes)
11. [Known Limitations](#known-limitations)
12. [Directory Structure](#directory-structure)
13. [FAQ](#faq)
14. [License](#license)

---

## Features

* Starts a local explicit proxy (`127.0.0.1:8888`) for focus sessions.
* Blocks configured domains (and subdomains) with a motivational HTML page.
* Auto-creates and persists:

  * `blockedSites.json` with a default blocklist (e.g., x.com, facebook.com, instagram.com).
  * `messages.json` with 10 motivational messages.
* Enables Windows system proxy on **focus start** and disables on **focus stop**.
* Timed sessions with an automatic shutdown when the timer elapses.
* Console commands to manage the blocklist and view status/logs.
* Basic session statistics (blocked attempts count and a rough “minutes saved” estimate).
* Rolling in-memory request log (keeps the last 100 entries; `log` shows the last 20).

---

## How it Works

1. **Focus Session**

   * `focus [minutes]` sets `HKCU\Software\Microsoft\Windows\CurrentVersion\Internet Settings`

     * `ProxyEnable = 1`
     * `ProxyServer = 127.0.0.1:8888`
   * Starts a Titanium Web Proxy endpoint on `127.0.0.1:8888` with SSL support enabled.

2. **Request Handling**

   * Each request’s host is checked against the blocklist.
   * If blocked, the proxy returns an HTML page with a motivational message and time remaining.
   * If allowed, the proxy forwards the request normally.

3. **Session End**

   * On timer expiry or `stop`, the proxy is shut down and the system proxy is disabled.

---

## Requirements

* **OS:** Windows 10/11 (current user must be allowed to modify Internet Settings in HKCU).
* **.NET SDK:** .NET 7.0 (or .NET 6.0).
* **NuGet packages:**

  * `Titanium.Web.Proxy`

> Note: HTTPS interception details are handled by Titanium Web Proxy. For host-based blocking, full certificate deployment is not strictly required; the app blocks by host name. Some HTTPS behaviors may vary (see [Proxy Behavior & Compatibility](#proxy-behavior--compatibility)).

---

## Build & Run

```bash
# 1) Create console project (if starting fresh)
dotnet new console -n FocusApp
cd FocusApp

# 2) Add Titanium Web Proxy
dotnet add package Titanium.Web.Proxy

# 3) Replace Program.cs with the provided source (namespace FocusApp)

# 4) Build
dotnet build -c Release

# 5) Run
dotnet run -c Release
```

On first run, the app will create `blockedSites.json` and `messages.json` in the working directory.

---

## Commands

Within the application prompt:

* `add [name] [host]`
  Add a site to the blocklist.
  Example: `add X x.com`

* `remove [name]`
  Remove an entry by its name (case-insensitive).
  Example: `remove X`

* `list`
  List all blocked entries as `Name = Host`.

* `focus [minutes]`
  Start a focus session for the given number of minutes.

  * Enables system proxy to `127.0.0.1:8888`
  * Starts the proxy server
  * Resets stats and logs for the new session

* `stop`
  Stop the current focus session.

  * Stops the proxy server
  * Disables the system proxy

* `stats`
  Show session statistics:

  * Blocked attempts count
  * Estimated time saved (simple heuristic)

* `log`
  Show the last 20 request entries (blocked and allowed).

* `exit`
  Cleanly stop focus mode (if active), disable proxy, and exit.

---

## Configuration Files

Both files are auto-created if missing. They are JSON and can be edited manually while the app is **not** running a session.

### `blockedSites.json`

```json
[
  { "Name": "X", "Host": "x.com" },
  { "Name": "Facebook", "Host": "facebook.com" },
  { "Name": "Facebook WWW", "Host": "www.facebook.com" },
  { "Name": "Instagram", "Host": "instagram.com" },
  { "Name": "Instagram WWW", "Host": "www.instagram.com" }
]
```

* **Matching rule:** A request is blocked if `host == Host` or `host` ends with `.` + `Host`.

  * Example: `sub.instagram.com` is blocked by `instagram.com`.

### `messages.json`

```json
[
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
]
```

* A random message is shown on the block page.

---

## Session Statistics & Logs

* **Stats**

  * `blockCount` increments on each blocked request.
  * “Estimated time saved” = `blockCount × 1.5` minutes (heuristic).

* **Logs**

  * In-memory buffer of the last 100 requests.
  * Each entry includes: timestamp, host, status (`Blocked`/`Allowed`), icon (🔒/✅), and the motivational message (if blocked).
  * `log` shows the last 20 entries with colored status in the console.

---

## Proxy Behavior & Compatibility

* **Scope:** The app sets the **system proxy** for the **current user** via HKCU. Applications that honor the system proxy will route through FocusApp during a session.
* **HTTPS:**

  * The proxy endpoint is created with SSL support enabled.
  * FocusApp performs **host-based** filtering; it does not inspect or store content bodies.
  * Titanium can block by hostname without full decryption in many cases. If a client enforces strict certificate pinning or bypasses the proxy, requests may not be intercepted.
* **Subdomains:** Block rule includes exact host and any subdomain of the configured `Host`.

---

## Troubleshooting

1. **Internet seems blocked after a crash**

   * Re-run the app and execute `stop`, **or**
   * Manually disable system proxy:
     **Windows Settings → Network & Internet → Proxy → Use a proxy server: Off**
     (Alternatively, clear `ProxyEnable` and `ProxyServer` in `HKCU\...\Internet Settings`.)

2. **Browser still reaches blocked sites**

   * Ensure the session is active (`focus [minutes]`).
   * Confirm Windows system proxy is set to `127.0.0.1:8888`.
   * Some applications may bypass the system proxy. Test with Edge/Chrome/Firefox.
   * Corporate VPNs, custom proxies, or group policies can override settings.

3. **Ports already in use**

   * Another process may be using `8888`. Stop that process or change the port in code and rebuild.

4. **Antivirus/Firewall prompts**

   * Allow local loopback for the app if prompted. The proxy listens on `127.0.0.1` only.

---

## Security & Privacy Notes

* The app modifies **current-user** Internet proxy settings during a session and restores them on stop.
* FocusApp does **not** persist request data to disk. Logs and stats are in-memory for the current run.
* The app uses host-based decisions only; it does not save or analyze page content.
* If you fork to add disk logging, ensure you comply with local privacy and organizational policies.

---

## Known Limitations

* Applications that hardcode direct connections, use their own proxy settings, or enforce certificate pinning may bypass FocusApp.
* QUIC/HTTP3 behaviors vary. Browsers typically fall back to HTTP/1.1 or HTTP/2 over the configured proxy, but this depends on the browser and policy.
* The proxy listens only on loopback; remote devices are not covered.
* The time-saved statistic is a rough heuristic.
* The app assumes a single-user context (HKCU). Multi-user scenarios require separate sessions per user.

---

## Directory Structure

```
FocusApp/
├─ Program.cs
├─ blockedSites.json      # auto-created on first run
└─ messages.json          # auto-created on first run
```

---

## FAQ

**Q: Do I need administrator rights?**
A: No, the app writes to HKCU (current user). However, corporate policies or endpoint security tools may require elevated privileges to alter proxy settings.

**Q: Can I change the proxy port?**
A: Yes. Update `new ExplicitProxyEndPoint(IPAddress.Loopback, 8888, true)` and the corresponding `EnableProxy("127.0.0.1:8888")` string, then rebuild.

**Q: Does it work without decrypting HTTPS?**
A: The app blocks by hostname. For most browsers honoring the proxy, hostname-based blocking is sufficient. Full MITM is not required for the intended behavior.

**Q: Where are logs stored?**
A: In memory only; they are not written to disk.

**Q: How do I preload my own messages or blocklist?**
A: Edit `messages.json` and `blockedSites.json` before starting a session.

---

## License

Specify your license choice (e.g., MIT). Example:

```
MIT License

Copyright (c) 2025 Shantanu Singh

Permission is hereby granted, free of charge, to any person obtaining a copy
...
```

---

**Note:** Use this tool responsibly. Enforcing focus is effective only if all critical applications are configured to respect system proxy settings during sessions.
