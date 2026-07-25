using System.Collections.ObjectModel;
using System.Text.Json;
using MyFanControl.Modern.Models;

namespace MyFanControl.Modern.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsService()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyFanControlModern");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), _jsonOptions)
                       ?? new AppSettings();
        }
        catch
        {
            // Invalid or old settings should never prevent safe fan control startup.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings) =>
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, _jsonOptions));

    public static ObservableCollection<FanCurvePoint> ToPoints(IEnumerable<FanCurvePointData> values) =>
        new(values.Select(value => new FanCurvePoint(value.Temperature, value.Duty)));

    public static List<FanCurvePointData> FromPoints(IEnumerable<FanCurvePoint> values) =>
        values.Select(point => new FanCurvePointData
        {
            Temperature = point.Temperature,
            Duty = point.Duty
        }).ToList();
}
