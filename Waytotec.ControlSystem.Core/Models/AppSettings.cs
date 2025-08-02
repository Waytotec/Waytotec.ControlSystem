public class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string WindowBackdropType { get; set; } = "Mica";
    public string Language { get; set; } = "Korean";
    public bool AutoStart { get; set; } = false;
    public int LogLevel { get; set; } = 2; // 0: None, 1: Error, 2: Warning, 3: Info, 4: Debug
}