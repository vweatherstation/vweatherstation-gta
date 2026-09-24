# VWeatherStation GTA V Plugin — Source

This folder contains the full source for the GTA V Story Mode plugin that
reports your in-game weather to VWeatherStation. It's open source on purpose:
you (and anyone else) can read exactly what it does before running it.

## What it does
- Reads GTA V's current weather, next weather, weather transition, in-game
  clock, and your player position (via ScriptHookVDotNet v3).
- Sends that to `vweatherstation.com/api/v1/game-telemetry` every couple of
  seconds while you play Story Mode.
- Optionally applies "set weather" commands the website sends back.
- Shows a pairing code on screen so you can link the session to the site.

## STORY MODE ONLY
This is a single-player mod. **Do not run it while connected to GTA Online.**
Modded files present during an Online session can get your account banned.
Remove or disable the plugin before playing Online.

## How to build (free)
1. Install **Visual Studio 2022 Community** (free).
2. Create a new project → **Class Library (.NET Framework)** → target
   **.NET Framework 4.8**.
3. Add both source files to the project:
   - `VWeatherStationScript.cs`
   - `Config.cs`
4. Add NuGet package **ScriptHookVDotNet3** (this brings in the GTA API).
   - Also ensure **System.Net.Http** is referenced (it's part of the framework).
   - If you use the JSON `using` line, add **Newtonsoft.Json** too — or delete
     that `using` line, the plugin hand-parses the small responses and doesn't
     strictly need it.
5. **Build**. The output is `VWeatherStationGTA.dll`.

## How to install
1. Make sure you already have **ScriptHookV** and **ScriptHookVDotNet** set up
   in GTA V (the standard PC modding tools).
2. Copy `VWeatherStationGTA.dll` into your GTA V **`scripts`** folder.
3. Launch GTA V in Story Mode. On first run the plugin writes a default
   `VWeatherStationGTA.ini` next to it — you can edit that to change the
   update interval or turn remote control off.
4. A pairing code appears on screen. Enter it at
   **vweatherstation.com/gta5/connect/** to see your live page.

## Configuration (VWeatherStationGTA.ini)
```
[General]
Enabled=true
TelemetryEnabled=true
TelemetryIntervalSeconds=2

[Connection]
SiteUrl=https://vweatherstation.com
ApiBaseUrl=https://vweatherstation.com/api/v1

[Features]
RemoteWeather=true
```

## Notes for building this out further
- The HTTP calls are fire-and-forget `Task`s launched from `OnTick` so they
  never block the game thread. For a production build you'd want a small
  background queue and to marshal weather changes back onto the game thread
  explicitly — this version keeps it simple and readable.
- The JSON parsing is intentionally minimal (no dependency required). If you
  add Newtonsoft.Json you can replace `JsonStr`/`ParseCommands` with proper
  deserialization.
- The API is game-agnostic (`/game-sessions`, `/game-telemetry`), so a future
  GTA VI or FiveM adapter can reuse the same server side.
