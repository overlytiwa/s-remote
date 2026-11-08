using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SamsungMiniRemote.Services;

/// <summary>
/// Persists TV tokens and the last-selected device to a local JSON file
/// in the user's AppData directory.
/// </summary>
public sealed class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SRemote");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public Dictionary<string, string> Tokens { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string?> MacAddresses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LastTvIp { get; set; }
    public string? LastTvName { get; set; }
    public string? LastTvModel { get; set; }
    public int LastTvPort { get; set; } = 8002;

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, JsonOpts);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Silently ignore save errors; not critical.
        }
    }

    public static SettingsService Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<SettingsService>(json, JsonOpts) ?? new SettingsService();
            }
        }
        catch
        {
            // Corrupted file – start fresh.
        }
        return new SettingsService();
    }

    public string? GetToken(string ip) =>
        Tokens.TryGetValue(ip, out var t) ? t : null;

    public void SetToken(string ip, string token)
    {
        Tokens[ip] = token;
        Save();
    }

    public void RemoveToken(string ip)
    {
        Tokens.Remove(ip);
        Save();
    }

    public string? GetMac(string ip) =>
        MacAddresses.TryGetValue(ip, out var m) ? m : null;

    public void SetMac(string ip, string? mac)
    {
        if (mac != null)
            MacAddresses[ip] = mac;
        Save();
    }
}

