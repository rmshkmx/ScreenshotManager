using System.Drawing;

namespace ScreenshotManager.Naming;

/// <summary>
/// Именование по дате и времени.
/// Формат: Screenshot_2026-06-23_14-30-15
/// </summary>
public sealed class DateTimeNamer : INamingStrategy
{
    public Task<string> GenerateNameAsync(Bitmap screenshot, string activeWindowTitle, string activeProcessName)
    {
        var now = DateTime.Now;
        var name = $"Screenshot_{now:yyyy-MM-dd}_{now:HH-mm-ss}";
        return Task.FromResult(name);
    }
}
