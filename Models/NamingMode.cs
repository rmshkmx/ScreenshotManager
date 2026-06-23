namespace ScreenshotManager.Models;

/// <summary>
/// Режим именования файлов скриншотов.
/// </summary>
public enum NamingMode
{
    /// <summary>По дате и времени: Screenshot_2026-06-23_14-30-15.png</summary>
    DateTime,

    /// <summary>По активному приложению: Chrome_2026-06-23_14-30-15.png</summary>
    ActiveApp,

    /// <summary>AI-описание через BLIP: code_editor_dark_theme_2026-06-23.png</summary>
    AI,

    /// <summary>Комбинация: Chrome_code_editor_dark_theme_2026-06-23_14-30.png</summary>
    Combined
}
