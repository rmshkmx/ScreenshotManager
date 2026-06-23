using System.Drawing;
using System.Text.RegularExpressions;
using ScreenshotManager.Services.AI;

namespace ScreenshotManager.Naming;

/// <summary>
/// AI-именование через BLIP модель.
/// Формат: code_editor_with_dark_theme_2026-06-23
/// Описание приводится к lowercase, удаляются артикли (a, an, the), пробелы → _.
/// </summary>
public sealed class AINamer : INamingStrategy
{
    private readonly BlipInferenceService _blip;

    public AINamer(BlipInferenceService blip)
    {
        _blip = blip;
    }

    public async Task<string> GenerateNameAsync(Bitmap screenshot, string activeWindowTitle, string activeProcessName)
    {
        var now = DateTime.Now;

        try
        {
            if (!_blip.IsModelLoaded)
            {
                // Фолбэк на дату, если модель не загружена
                return $"Screenshot_{now:yyyy-MM-dd}_{now:HH-mm-ss}";
            }

            var caption = await _blip.GenerateCaptionAsync(screenshot);
            var cleanCaption = CleanCaption(caption);

            if (string.IsNullOrWhiteSpace(cleanCaption))
                return $"Screenshot_{now:yyyy-MM-dd}_{now:HH-mm-ss}";

            // Ограничить длину описания
            if (cleanCaption.Length > 60)
                cleanCaption = cleanCaption[..60].TrimEnd('_');

            return $"{cleanCaption}_{now:yyyy-MM-dd}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AINamer] Error: {ex.Message}");
            return $"Screenshot_{now:yyyy-MM-dd}_{now:HH-mm-ss}";
        }
    }

    /// <summary>
    /// Очистить описание от BLIP для использования в имени файла.
    /// lowercase, без артиклей, пробелы → _
    /// </summary>
    public static string CleanCaption(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption))
            return string.Empty;

        // 1. Привести к нижнему регистру
        var result = caption.ToLowerInvariant().Trim();

        // 2. Удалить артикли (a, an, the) как отдельные слова
        result = Regex.Replace(result, @"\b(a|an|the)\b", "", RegexOptions.IgnoreCase).Trim();

        // 3. Удалить символы, недопустимые в именах файлов
        result = Regex.Replace(result, @"[^\w\s-]", "");

        // 4. Заменить множественные пробелы/подчёркивания на одно подчёркивание
        result = Regex.Replace(result, @"[\s_]+", "_");

        // 5. Убрать ведущие/замыкающие подчёркивания
        result = result.Trim('_');

        return result;
    }
}
