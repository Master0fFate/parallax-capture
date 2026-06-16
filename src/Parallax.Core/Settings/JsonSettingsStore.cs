using System.Text.Json;
using Parallax.Core.Platform;

namespace Parallax.Core.Settings;

public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IPlatformLocations _locations;

    public JsonSettingsStore(IPlatformLocations locations)
    {
        _locations = locations;
    }

    public ParallaxSettings Load()
    {
        if (!File.Exists(_locations.SettingsFilePath))
        {
            return ParallaxSettings.CreateDefaults(_locations.ScreenshotsDirectory);
        }

        try
        {
            string json = File.ReadAllText(_locations.SettingsFilePath);
            var settings = JsonSerializer.Deserialize<ParallaxSettings>(json, SerializerOptions)
                ?? ParallaxSettings.CreateDefaults(_locations.ScreenshotsDirectory);
            if (string.IsNullOrWhiteSpace(settings.SaveFolder))
            {
                settings.SaveFolder = _locations.ScreenshotsDirectory;
            }

            return settings;
        }
        catch (JsonException)
        {
            return ParallaxSettings.CreateDefaults(_locations.ScreenshotsDirectory);
        }
        catch (IOException)
        {
            return ParallaxSettings.CreateDefaults(_locations.ScreenshotsDirectory);
        }
        catch (UnauthorizedAccessException)
        {
            return ParallaxSettings.CreateDefaults(_locations.ScreenshotsDirectory);
        }
    }

    public void Save(ParallaxSettings settings)
    {
        Directory.CreateDirectory(_locations.ConfigDirectory);
        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_locations.SettingsFilePath, json);
    }
}
