using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ScreenshotManager.Services;

/// <summary>
/// Захват экрана (полный экран или все мониторы).
/// </summary>
public sealed class ScreenCaptureService
{
    /// <summary>
    /// Захватить полный экран (основной монитор).
    /// </summary>
    public Bitmap CapturePrimaryScreen()
    {
        var bounds = Screen.PrimaryScreen!.Bounds;
        return CaptureRegion(bounds);
    }

    /// <summary>
    /// Захватить все мониторы в одно изображение.
    /// </summary>
    public Bitmap CaptureAllScreens()
    {
        var bounds = SystemInformation.VirtualScreen;
        return CaptureRegion(bounds);
    }

    /// <summary>
    /// Захватить указанную область экрана.
    /// </summary>
    public Bitmap CaptureRegion(Rectangle region)
    {
        var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(region.Left, region.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        return bmp;
    }
}
