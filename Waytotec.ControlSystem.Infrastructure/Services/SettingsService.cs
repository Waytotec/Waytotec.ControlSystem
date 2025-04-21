using System.Text.Json;

public class SettingsService
{
    private readonly string _filePath;

    public AppSettings Settings { get; private set; }

    public SettingsService(string filePath)
    {
        _filePath = filePath;
        Load();
    }

    public void Load()
    {
        if (!File.Exists(_filePath))
        {
            Settings = new AppSettings();
            Save();
            return;
        }

        var json = File.ReadAllText(_filePath);
        Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}