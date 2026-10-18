using System.Text.Json;
using Launcher.Models;

namespace Launcher.Services;

public static class ConfigService
{
    public static LauncherConfig Load(string configFile)
    {
        if (!File.Exists(configFile))
            return new LauncherConfig();

        string json = File.ReadAllText(configFile);

        var settings = JsonSerializer.Deserialize<AppSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return settings?.Launcher ?? new LauncherConfig();
    }
}
