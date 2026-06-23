using System.Text.Json;
using ScreenshotManager.Models;
using ScreenshotManager.Localization;

namespace ScreenshotManager.Services;

/// <summary>
/// Загрузка/сохранение настроек приложения в JSON-файл.
/// </summary>
public sealed class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenshotManager");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettings Settings { get; private set; } = new();

    /// <summary>
    /// Загрузить настройки из файла. Если файла нет — использовать значения по умолчанию.
    /// </summary>
    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Load error: {ex.Message}");
            Settings = new AppSettings();
        }

        // Применить язык
        Loc.Instance.Language = Settings.Language;
    }

    /// <summary>
    /// Сохранить текущие настройки в файл.
    /// </summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsService] Save error: {ex.Message}");
        }
    }

    /// <summary>
    /// Путь к папке данных приложения (модели, кеш и т.д.).
    /// </summary>
    public static string AppDataDir => SettingsDir;
}
