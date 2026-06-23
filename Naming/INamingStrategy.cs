using System.Drawing;

namespace ScreenshotManager.Naming;

/// <summary>
/// Интерфейс стратегии именования файлов скриншотов.
/// </summary>
public interface INamingStrategy
{
    /// <summary>
    /// Генерирует имя файла (без расширения) для скриншота.
    /// </summary>
    /// <param name="screenshot">Захваченное изображение.</param>
    /// <param name="activeWindowTitle">Заголовок активного окна.</param>
    /// <param name="activeProcessName">Имя процесса активного окна.</param>
    /// <returns>Имя файла без расширения.</returns>
    Task<string> GenerateNameAsync(Bitmap screenshot, string activeWindowTitle, string activeProcessName);
}
