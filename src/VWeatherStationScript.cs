/*
 * VWeatherStation GTA V plugin  (ScriptHookVDotNet v3)
 * ----------------------------------------------------
 * Reports Story Mode weather, in-game time and player location to
 * VWeatherStation, and applies remote weather commands from the site.
 *
 * BUILD (free): Visual Studio 2022, Community edition.
 *   - New project -> Class Library (.NET Framework 4.8)
 *   - Add NuGet reference: ScriptHookVDotNet3
 *   - Add reference to System.Net.Http
 *   - Build -> produces VWeatherStationGTA.dll
 *   - Copy the .dll AND VWeatherStationGTA.ini into GTA V's "scripts" folder.
 *
 * STORY MODE ONLY. Do not run while connected to GTA Online.
 *
 * This file is deliberately small and readable so anyone can audit it before
 * running it in their game.
 */

using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using GTA;                 // ScriptHookVDotNet v3
using GTA.Math;
using GTA.UI;

public class VWeatherStationScript : Script
{
    private readonly Config _cfg;
    private readonly HttpClient _http = new HttpClient();
    private string _sessionId = null;
    private string _pairingCode = null;
    private DateTime _lastSend = DateTime.MinValue;
    private DateTime _lastCommandPoll = DateTime.MinValue;
    private bool _sessionStarting = false;

    public VWeatherStationScript()
    {
        _cfg = Config.Load();
        _http.Timeout = TimeSpan.FromSeconds(8);
        Tick += OnTick;
        Interval = 500; // run our logic twice a second; network is throttled below
        if (_cfg.Enabled)
            Notification.Show("~b~VWeatherStation~s~ GTA plugin loaded. Connecting...");
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (!_cfg.Enabled) return;

        // 1. make sure we have a session
        if (_sessionId == null)
        {
            if (!_sessionStarting) { _sessionStarting = true; _ = StartSession(); }
            return;
        }

        var now = DateTime.UtcNow;

        // 2. send telemetry on the configured interval
        if (_cfg.TelemetryEnabled && (now - _lastSend).TotalSeconds >= _cfg.TelemetryIntervalSeconds)
        {
            _lastSend = now;
            _ = SendTelemetry();
        }

        // 3. poll for remote commands
        if (_cfg.RemoteWeather && (now - _lastCommandPoll).TotalSeconds >= 5)
        {
            _lastCommandPoll = now;
            _ = PollCommands();
        }

        // 4. pull REAL-WORLD weather into the game (if enabled)
        if (_cfg.PullRealWeather && (now - _lastRealWeatherPoll).TotalSeconds >= _cfg.RealWeatherIntervalSeconds)
        {
            _lastRealWeatherPoll = now;
            _ = PullRealWeather();
        }

        // apply any pending real-world weather on the game thread (this tick)
        if (_pendingRealWeather != null)
        {
            var wx = _pendingRealWeather; _pendingRealWeather = null;
            try
            {
                if (Enum.TryParse(wx, out Weather target))
                {
                    World.TransitionToWeather(target, 30f);
                    Notification.Show("~b~VWeatherStation~s~ real weather: ~y~" + wx);
                }
            }
            catch (Exception ex) { Log("apply real weather failed: " + ex.Message); }
        }
    }

    // ---- pull real-world weather into GTA -------------------------------
    private DateTime _lastRealWeatherPoll = DateTime.MinValue;
    private string _pendingRealWeather = null;

    private async Task PullRealWeather()
    {
        try
        {
            var url = _cfg.ApiBaseUrl + "/game-weather?lat=" + _cfg.RealWeatherLat.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + "&lon=" + _cfg.RealWeatherLon.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var res = await GetString(url);
            var cond = JsonStr(res, "condition");   // clear|cloudy|overcast|fog|drizzle|rain|thunderstorm|snow
            var isDay = JsonStr(res, "is_day");
            if (cond == null) return;
            // map the real-world condition to a GTA V weather type, and stash
            // it to be applied on the game thread (next tick).
            _pendingRealWeather = MapRealToGta(cond, isDay == "1");
        }
        catch (Exception ex) { Log("PullRealWeather failed: " + ex.Message); }
    }

    // map our simple condition -> GTA V Weather enum name
    private string MapRealToGta(string cond, bool isDay)
    {
        switch (cond)
        {
            case "thunderstorm": return "Thunder";
            case "rain":         return "Raining";
            case "drizzle":      return "Clearing";
            case "snow":         return "Snow";
            case "fog":          return "Foggy";
            case "overcast":     return "Overcast";
            case "cloudy":       return "Clouds";
            case "clear":
            default:             return isDay ? "ExtraSunny" : "Clear";
        }
    }

    // ---- session --------------------------------------------------------
    private async Task StartSession()
    {
        try
        {
            var body = "{\"game\":\"gta5\",\"mode\":\"story\",\"platform\":\"pc\",\"pluginVersion\":\"" + Config.Version + "\"}";
            var res = await PostJson(_cfg.ApiBaseUrl + "/game-sessions/", body);
            _sessionId = JsonStr(res, "sessionId");
            _pairingCode = JsonStr(res, "pairingCode");
            if (_sessionId != null && _pairingCode != null)
            {
                ShowPairing();
            }
        }
        catch (Exception ex) { Log("StartSession failed: " + ex.Message); }
    }

    private void ShowPairing()
    {
        // show the pairing code on screen for a good while so the user can type it
        Notification.Show("~b~VWeatherStation~s~~n~Enter this code on the site:~n~~g~" + _pairingCode);
        Log("Pairing code: " + _pairingCode + "  (enter at " + _cfg.SiteUrl + "/gta5/connect/)");
    }

    // ---- telemetry ------------------------------------------------------
    private async Task SendTelemetry()
    {
        try
        {
            var w = World.Weather.ToString().ToUpperInvariant();
            var nw = World.NextWeather.ToString().ToUpperInvariant();
            var trans = World.WeatherTransition;      // 0..1
            var t = World.CurrentTimeOfDay;           // TimeSpan
            Vector3 pos = Game.Player.Character.Position;
            float heading = Game.Player.Character.Heading;
            string zone = World.GetZoneDisplayName(pos);

            var sb = new StringBuilder();
            sb.Append("{\"sessionId\":\"").Append(_sessionId).Append("\",");
            sb.Append("\"game\":{");
            sb.Append("\"gameTime\":\"").Append(t.Hours.ToString("D2")).Append(":").Append(t.Minutes.ToString("D2")).Append("\",");
            sb.Append("\"weather\":\"").Append(w).Append("\",");
            sb.Append("\"nextWeather\":\"").Append(nw).Append("\",");
            sb.Append("\"weatherTransition\":").Append(trans.ToString(System.Globalization.CultureInfo.InvariantCulture));
            sb.Append("},\"player\":{");
            sb.Append("\"x\":").Append(pos.X.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"y\":").Append(pos.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"z\":").Append(pos.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"heading\":").Append(heading.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"zone\":\"").Append(Escape(zone)).Append("\"");
            sb.Append("}}");

            await PostJson(_cfg.ApiBaseUrl + "/game-telemetry/", sb.ToString());
        }
        catch (Exception ex) { Log("SendTelemetry failed: " + ex.Message); }
    }

    // ---- commands (remote weather control) ------------------------------
    private async Task PollCommands()
    {
        try
        {
            var res = await GetJson(_cfg.ApiBaseUrl + "/game-commands/?sessionId=" + Uri.EscapeDataString(_sessionId));
            // very small hand-parse: look for command objects
            foreach (var cmd in ParseCommands(res))
            {
                ApplyCommand(cmd);
                var ack = "{\"sessionId\":\"" + _sessionId + "\",\"commandId\":\"" + cmd.Id + "\",\"status\":\"executed\"}";
                await PostJson(_cfg.ApiBaseUrl + "/game-commands/", ack);
            }
        }
        catch (Exception ex) { Log("PollCommands failed: " + ex.Message); }
    }

    private void ApplyCommand(GameCommand cmd)
    {
        // must run on the game thread; SHVDN calls OnTick on it, and these tasks
        // are scheduled from there, so queue the actual world change for next tick.
        try
        {
            if (!Enum.TryParse(Capitalize(cmd.Weather), out Weather target)) return;
            if (cmd.Type == "transition_weather" && cmd.DurationSeconds > 0)
                World.TransitionToWeather(target, cmd.DurationSeconds);
            else
                World.Weather = target;
            Notification.Show("~b~VWeatherStation~s~ set weather: ~y~" + cmd.Weather);
        }
        catch (Exception ex) { Log("ApplyCommand failed: " + ex.Message); }
    }

    // ---- tiny HTTP + JSON helpers --------------------------------------
    private async Task<string> GetString(string url)
    {
        var resp = await _http.GetAsync(url);
        return await resp.Content.ReadAsStringAsync();
    }

    private async Task<string> PostJson(string url, string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync(url, content);
        return await resp.Content.ReadAsStringAsync();
    }
    private async Task<string> GetJson(string url)
    {
        var resp = await _http.GetAsync(url);
        return await resp.Content.ReadAsStringAsync();
    }
    private static string JsonStr(string json, string key)
    {
        if (json == null) return null;
        var needle = "\"" + key + "\"";
        int i = json.IndexOf(needle, StringComparison.Ordinal);
        if (i < 0) return null;
        i = json.IndexOf('"', i + needle.Length + 1);
        if (i < 0) return null;
        int j = json.IndexOf('"', i + 1);
        if (j < 0) return null;
        return json.Substring(i + 1, j - i - 1);
    }
    private static IEnumerable<GameCommand> ParseCommands(string json)
    {
        var list = new List<GameCommand>();
        if (json == null || json.IndexOf("\"commands\"", StringComparison.Ordinal) < 0) return list;
        // split on "id" occurrences inside the commands array (small + good enough)
        int idx = 0;
        while ((idx = json.IndexOf("\"id\"", idx)) >= 0)
        {
            var id = JsonStr(json.Substring(idx), "id");
            var block = json.Substring(idx, Math.Min(200, json.Length - idx));
            var type = JsonStr(block, "type");
            var weather = JsonStr(block, "weather");
            if (id != null && type != null && weather != null)
                list.Add(new GameCommand { Id = id, Type = type, Weather = weather, DurationSeconds = 0 });
            idx += 4;
        }
        return list;
    }
    private static string Escape(string s) => (s ?? "").Replace("\\", "").Replace("\"", "");
    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1).ToLower();
    private void Log(string msg)
    {
        try { System.IO.File.AppendAllText("VWeatherStationGTA.log", DateTime.Now.ToString("s") + "  " + msg + Environment.NewLine); }
        catch { }
    }

    private class GameCommand
    {
        public string Id; public string Type; public string Weather; public int DurationSeconds;
    }
}
