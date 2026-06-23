using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ScreenshotManager.Helpers;

namespace ScreenshotManager.Services;

/// <summary>
/// Источник скриншота.
/// </summary>
public enum ScreenshotSource
{
    Clipboard,
    Hotkey
}

/// <summary>
/// Мониторинг буфера обмена для автоматического сохранения скриншотов.
/// Использует AddClipboardFormatListener / WM_CLIPBOARDUPDATE.
/// </summary>
public sealed class ClipboardMonitorService : IDisposable
{
    private HwndSource? _hwndSource;
    private bool _isListening;
    private bool _disposed;

    /// <summary>
    /// Вызывается при появлении нового изображения в буфере обмена.
    /// </summary>
    public event Action<Bitmap>? ClipboardImageCaptured;

    /// <summary>
    /// Начать мониторинг буфера обмена.
    /// Должен вызываться из UI-потока.
    /// </summary>
    public void Start()
    {
        if (_isListening) return;

        var parameters = new HwndSourceParameters("ScreenshotManager_ClipboardMonitor")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0 // Скрытое окно
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        if (!NativeInterop.AddClipboardFormatListener(_hwndSource.Handle))
        {
            Debug.WriteLine("[ClipboardMonitor] Failed to register clipboard listener");
            return;
        }

        _isListening = true;
        Debug.WriteLine("[ClipboardMonitor] Started monitoring");
    }

    /// <summary>
    /// Остановить мониторинг буфера обмена.
    /// </summary>
    public void Stop()
    {
        if (!_isListening || _hwndSource == null) return;

        NativeInterop.RemoveClipboardFormatListener(_hwndSource.Handle);
        _hwndSource.RemoveHook(WndProc);
        _hwndSource.Dispose();
        _hwndSource = null;
        _isListening = false;

        Debug.WriteLine("[ClipboardMonitor] Stopped monitoring");
    }

    /// <summary>
    /// Обработчик оконных сообщений.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeInterop.WM_CLIPBOARDUPDATE)
        {
            OnClipboardUpdate();
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Обработка изменения буфера обмена.
    /// </summary>
    private void OnClipboardUpdate()
    {
        try
        {
            // Проверяем, есть ли изображение в буфере обмена
            // Должен выполняться в STA-потоке (UI-потоке WPF)
            if (!System.Windows.Clipboard.ContainsImage())
                return;

            var bitmapSource = System.Windows.Clipboard.GetImage();
            if (bitmapSource == null) return;

            // Конвертировать WPF BitmapSource → System.Drawing.Bitmap
            var bitmap = ConvertToBitmap(bitmapSource);
            if (bitmap == null) return;

            Debug.WriteLine($"[ClipboardMonitor] Image captured: {bitmap.Width}x{bitmap.Height}");
            ClipboardImageCaptured?.Invoke(bitmap);
        }
        catch (COMException ex)
        {
            // Буфер обмена может быть заблокирован другим приложением
            Debug.WriteLine($"[ClipboardMonitor] COM error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClipboardMonitor] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Конвертировать WPF BitmapSource в System.Drawing.Bitmap.
    /// </summary>
    private static Bitmap? ConvertToBitmap(System.Windows.Media.Imaging.BitmapSource source)
    {
        try
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;

            var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                System.Drawing.Imaging.ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Используем FormatConvertedBitmap для гарантии формата Bgra32
            var converted = new System.Windows.Media.Imaging.FormatConvertedBitmap(
                source,
                System.Windows.Media.PixelFormats.Bgra32,
                null, 0);

            converted.CopyPixels(
                new System.Windows.Int32Rect(0, 0, width, height),
                bmpData.Scan0,
                bmpData.Stride * height,
                bmpData.Stride);

            bmp.UnlockBits(bmpData);
            return bmp;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ClipboardMonitor] Bitmap conversion error: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
