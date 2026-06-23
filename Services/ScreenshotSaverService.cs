using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using ScreenshotManager.Models;

namespace ScreenshotManager.Services;

/// <summary>
/// Сохранение скриншотов в файловую систему.
/// </summary>
public sealed class ScreenshotSaverService
{
    private readonly SettingsService _settings;

    public ScreenshotSaverService(SettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Сохранить скриншот с указанным именем файла (без расширения).
    /// </summary>
    /// <returns>Полный путь к сохранённому файлу.</returns>
    public string Save(Bitmap screenshot, string fileNameWithoutExtension)
    {
        var folder = _settings.Settings.SaveFolder;
        Directory.CreateDirectory(folder);

        var format = _settings.Settings.ImageFormat.ToLowerInvariant();
        var extension = format switch
        {
            "jpeg" or "jpg" => ".jpg",
            "bmp" => ".bmp",
            _ => ".png"
        };

        var fileName = $"{fileNameWithoutExtension}{extension}";
        var fullPath = Path.Combine(folder, fileName);

        // Защита от перезаписи — добавить индекс
        fullPath = GetUniqueFilePath(fullPath);

        var imageFormat = format switch
        {
            "jpeg" or "jpg" => ImageFormat.Jpeg,
            "bmp" => ImageFormat.Bmp,
            _ => ImageFormat.Png
        };

        if (format is "jpeg" or "jpg")
        {
            SaveJpeg(screenshot, fullPath, _settings.Settings.JpegQuality);
        }
        else
        {
            screenshot.Save(fullPath, imageFormat);
        }

        Debug.WriteLine($"[ScreenshotSaver] Saved: {fullPath}");
        return fullPath;
    }

    /// <summary>
    /// Сохранить JPEG с указанным качеством.
    /// </summary>
    private static void SaveJpeg(Bitmap bitmap, string path, int quality)
    {
        var encoder = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(e => e.FormatID == ImageFormat.Jpeg.Guid);

        if (encoder == null)
        {
            bitmap.Save(path, ImageFormat.Jpeg);
            return;
        }

        var parameters = new EncoderParameters(1);
        parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
        bitmap.Save(path, encoder, parameters);
    }

    /// <summary>
    /// Получить уникальный путь к файлу (добавить _1, _2, ... при конфликте).
    /// </summary>
    private static string GetUniqueFilePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        int counter = 1;
        string newPath;
        do
        {
            newPath = Path.Combine(dir, $"{name}_{counter}{ext}");
            counter++;
        } while (File.Exists(newPath));

        return newPath;
    }
}
