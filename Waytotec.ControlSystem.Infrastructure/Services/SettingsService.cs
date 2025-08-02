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
        try
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
        catch (Exception)
        {
            Settings = new AppSettings();
            Save();
        }
    }

    public void Save()
    {
        //var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        //File.WriteAllText(_filePath, json);
        try
        {
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Settings save error: {ex.Message}");
        }
    }
}