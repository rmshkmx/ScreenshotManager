using System.Diagnostics;
using System.Text;
using ScreenshotManager.Helpers;

namespace ScreenshotManager.Services;

/// <summary>
/// Определение активного окна: заголовок и имя процесса.
/// </summary>
public sealed class ActiveWindowService
{
    /// <summary>
    /// Получить информацию об активном окне.
    /// </summary>
    /// <returns>(Заголовок окна, Имя процесса)</returns>
    public (string Title, string ProcessName) GetActiveWindowInfo()
    {
        try
        {
            var hwnd = NativeInterop.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return ("Unknown", "Unknown");

            // Заголовок окна
            var sb = new StringBuilder(512);
            NativeInterop.GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString();

            // Имя процесса
            NativeInterop.GetWindowThreadProcessId(hwnd, out uint pid);
            var process = Process.GetProcessById((int)pid);
            var processName = process.ProcessName;

            return (
                string.IsNullOrWhiteSpace(title) ? processName : title,
                processName
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ActiveWindowService] Error: {ex.Message}");
            return ("Unknown", "Unknown");
        }
    }

    /// <summary>
    /// Получить очищенное имя процесса, пригодное для имени файла.
    /// </summary>
    public string GetSafeProcessName()
    {
        var (_, processName) = GetActiveWindowInfo();
        return SanitizeForFileName(processName);
    }

    /// <summary>
    /// Очистить строку для использования в имени файла.
    /// </summary>
    public static string SanitizeForFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "Unknown";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);

        foreach (var c in input)
        {
            if (Array.IndexOf(invalidChars, c) < 0)
                sb.Append(c);
            else
                sb.Append('_');
        }

        // Ограничить длину до 50 символов
        var result = sb.ToString().Trim();
        if (result.Length > 50)
            result = result[..50];

        return string.IsNullOrWhiteSpace(result) ? "Unknown" : result;
    }
}
