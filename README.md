# VWeatherStation GTA V Plugin

Connect GTA V's in-game weather to [VWeatherStation](https://vweatherstation.com/gta5/). A ScriptHookVDotNet plugin that reports your Story Mode weather, time and location to a live web dashboard.

## Download the compiled plugin
GitHub Actions builds the `.dll` automatically — **you don't need Visual Studio**.
- Go to the **Actions** tab → latest "Build GTA Plugin" run → download the `VWeatherStationGTA` artifact, OR
- Grab it from the latest **Release**.

## Install
1. Install [Script Hook V](https://gtav.mod.works/) + [ScriptHookVDotNet](https://github.com/scripthookvdotnet/scripthookvdotnet/releases).
2. Drop `VWeatherStationGTA.dll` into your GTA V `scripts` folder.
3. Launch GTA V in **Story Mode**, note the pairing code, enter it at
   https://vweatherstation.com/gta5/connect/.

## Build it yourself (optional)
Requires .NET SDK: `dotnet build src/VWeatherStationGTA.csproj -c Release`
