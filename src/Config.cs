/*
 * Config.cs  ,  loads VWeatherStationGTA.ini from the scripts folder.
 * Keeps credentials and settings out of the compiled DLL.
 */
using System;
using System.IO;
using System.Collections.Generic;

public class Config
{
    public const string Version = "0.1.0";

    public bool   Enabled                 = true;
    public bool   TelemetryEnabled        = true;
    public int    TelemetryIntervalSeconds = 2;
    public bool   RemoteWeather           = true;
    public bool   PullRealWeather         = false;      // bring real-world weather INTO the game
    public double RealWeatherLat          = 51.5074;
    public double RealWeatherLon          = -0.1278;
    public int    RealWeatherIntervalSeconds = 600;
    public string SiteUrl                 = "https://vweatherstation.com";
    public string ApiBaseUrl              = "https://vweatherstation.com/api/v1";

    private const string IniName = "VWeatherStationGTA.ini";

    public static Config Load()
    {
        var c = new Config();
        try
        {
            if (!File.Exists(IniName)) { c.WriteDefault(); return c; }
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in File.ReadAllLines(IniName))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("[")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                map[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            c.Enabled                  = GetBool(map, "Enabled", c.Enabled);
            c.TelemetryEnabled         = GetBool(map, "TelemetryEnabled", c.TelemetryEnabled);
            c.TelemetryIntervalSeconds = GetInt(map, "TelemetryIntervalSeconds", c.TelemetryIntervalSeconds);
            c.RemoteWeather            = GetBool(map, "RemoteWeather", c.RemoteWeather);
            c.PullRealWeather          = GetBool(map, "PullRealWeather", c.PullRealWeather);
            c.RealWeatherLat           = GetDouble(map, "RealWeatherLat", c.RealWeatherLat);
            c.RealWeatherLon           = GetDouble(map, "RealWeatherLon", c.RealWeatherLon);
            c.RealWeatherIntervalSeconds = GetInt(map, "RealWeatherIntervalSeconds", c.RealWeatherIntervalSeconds);
            if (map.ContainsKey("SiteUrl"))    c.SiteUrl    = map["SiteUrl"];
            if (map.ContainsKey("ApiBaseUrl")) c.ApiBaseUrl = map["ApiBaseUrl"];
        }
        catch { /* fall back to defaults */ }
        if (c.TelemetryIntervalSeconds < 1) c.TelemetryIntervalSeconds = 1;
        return c;
    }

    private void WriteDefault()
    {
        try
        {
            File.WriteAllText(IniName,
@"[General]
Enabled=true
TelemetryEnabled=true
TelemetryIntervalSeconds=2

[Connection]
SiteUrl=https://vweatherstation.com
ApiBaseUrl=https://vweatherstation.com/api/v1

[Features]
RemoteWeather=true
");
        }
        catch { }
    }

    private static bool GetBool(Dictionary<string, string> m, string k, bool d)
        => m.ContainsKey(k) ? (m[k].Equals("true", StringComparison.OrdinalIgnoreCase) || m[k] == "1") : d;
    private static int GetInt(Dictionary<string, string> m, string k, int d)
        => m.ContainsKey(k) && int.TryParse(m[k], out int v) ? v : d;
    private static double GetDouble(Dictionary<string, string> m, string k, double d)
        => m.ContainsKey(k) && double.TryParse(m[k], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : d;
}
