using System.Drawing;

namespace ScreenshotManager.Naming;

/// <summary>
/// Именование по активному приложению + дата.
/// Формат: Chrome_2026-06-23_14-30-15
/// </summary>
public sealed class ActiveWindowNamer : INamingStrategy
{
    public Task<string> GenerateNameAsync(Bitmap screenshot, string activeWindowTitle, string activeProcessName)
    {
        var safeName = Services.ActiveWindowService.SanitizeForFileName(activeProcessName);
        var now = DateTime.Now;
        var name = $"{safeName}_{now:yyyy-MM-dd}_{now:HH-mm-ss}";
        return Task.FromResult(name);
    }
}
