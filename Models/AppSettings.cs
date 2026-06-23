namespace ScreenshotManager.Models;

/// <summary>
/// Настройки приложения, сериализуемые в JSON.
/// </summary>
public class AppSettings
{
    /// <summary>Папка сохранения скриншотов.</summary>
    public string SaveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    /// <summary>Режим именования файлов.</summary>
    public NamingMode NamingMode { get; set; } = NamingMode.DateTime;

    /// <summary>Утилита включена/выключена.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Мониторить буфер обмена для автосохранения скриншотов.</summary>
    public bool MonitorClipboard { get; set; } = true;

    /// <summary>AI-именование скриншотов включено.</summary>
    public bool AiNamingEnabled { get; set; } = false;

    /// <summary>AI-модель скачана на диск.</summary>
    public bool AiModelDownloaded { get; set; } = false;

    /// <summary>Токен HuggingFace для AI.</summary>
    public string HfToken { get; set; } = "";

    /// <summary>Формат сохранения: png, jpeg, bmp, webp.</summary>
    public string ImageFormat { get; set; } = "png";

    /// <summary>Качество JPEG (1–100).</summary>
    public int JpegQuality { get; set; } = 90;

    /// <summary>Список пользовательских хоткеев.</summary>
    public List<HotkeyBinding> CustomHotkeys { get; set; } = new();

    /// <summary>Запуск с Windows.</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>Язык интерфейса: ru / en.</summary>
    public string Language { get; set; } = "ru";

    /// <summary>Следующий ID для привязки хоткеев.</summary>
    public int NextHotkeyId { get; set; } = 9000;
}
