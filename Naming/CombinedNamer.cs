using System.Drawing;
using ScreenshotManager.Services.AI;

namespace ScreenshotManager.Naming;

/// <summary>
/// Комбинированное именование: приложение + AI-описание + дата.
/// Формат: VSCode_code_editor_dark_theme_2026-06-23_14-30
/// </summary>
public sealed class CombinedNamer : INamingStrategy
{
    private readonly BlipInferenceService _blip;

    public CombinedNamer(BlipInferenceService blip)
    {
        _blip = blip;
    }

    public async Task<string> GenerateNameAsync(Bitmap screenshot, string activeWindowTitle, string activeProcessName)
    {
        var now = DateTime.Now;
        var safeName = Services.ActiveWindowService.SanitizeForFileName(activeProcessName);

        try
        {
            var caption = await _blip.GenerateCaptionAsync(screenshot);
            var cleanCaption = AINamer.CleanCaption(caption);

            if (!string.IsNullOrWhiteSpace(cleanCaption))
            {
                // Ограничить длину AI-описания
                if (cleanCaption.Length > 40)
                    cleanCaption = cleanCaption[..40].TrimEnd('_');

                return $"{safeName}_{cleanCaption}_{now:yyyy-MM-dd}_{now:HH-mm}";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CombinedNamer] AI error: {ex.Message}");
        }

        // Фолбэк без AI
        return $"{safeName}_{now:yyyy-MM-dd}_{now:HH-mm-ss}";
    }
}
