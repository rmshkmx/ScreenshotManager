using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScreenshotManager.Helpers;

/// <summary>
/// Подготовка изображения для BLIP ONNX модели.
/// Resize → Normalize → Float tensor [1, 3, 384, 384].
/// </summary>
public static class ImagePreprocessor
{
    // BLIP image size
    public const int TargetWidth = 384;
    public const int TargetHeight = 384;

    // ImageNet normalization constants (used by BLIP)
    private static readonly float[] Mean = { 0.48145466f, 0.4578275f, 0.40821073f };
    private static readonly float[] Std = { 0.26862954f, 0.26130258f, 0.27577711f };

    /// <summary>
    /// Подготовить изображение для BLIP: resize + normalize → float[1, 3, 384, 384].
    /// </summary>
    public static float[] Preprocess(Bitmap source)
    {
        // 1. Resize до 384×384 с высоким качеством
        using var resized = ResizeBitmap(source, TargetWidth, TargetHeight);

        // 2. Преобразовать пиксели в нормализованный float тензор [1, 3, H, W]
        return BitmapToNormalizedTensor(resized);
    }

    /// <summary>
    /// Resize изображения с высоким качеством интерполяции.
    /// </summary>
    private static Bitmap ResizeBitmap(Bitmap source, int width, int height)
    {
        var dest = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(dest);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.DrawImage(source, 0, 0, width, height);
        return dest;
    }

    /// <summary>
    /// Конвертировать Bitmap в нормализованный float тензор в формате [1, 3, H, W] (CHW).
    /// </summary>
    private static float[] BitmapToNormalizedTensor(Bitmap bitmap)
    {
        int w = bitmap.Width;
        int h = bitmap.Height;
        var tensor = new float[1 * 3 * h * w];

        var bmpData = bitmap.LockBits(
            new Rectangle(0, 0, w, h),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            var stride = bmpData.Stride;
            var pixels = new byte[stride * h];
            Marshal.Copy(bmpData.Scan0, pixels, 0, pixels.Length);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int pixelOffset = y * stride + x * 4;

                    // BGRA → RGB, 0-255 → 0.0-1.0, затем нормализация
                    float b = pixels[pixelOffset] / 255.0f;
                    float g = pixels[pixelOffset + 1] / 255.0f;
                    float r = pixels[pixelOffset + 2] / 255.0f;

                    // Нормализация: (value - mean) / std
                    int idx = y * w + x;
                    tensor[0 * h * w + idx] = (r - Mean[0]) / Std[0]; // R channel
                    tensor[1 * h * w + idx] = (g - Mean[1]) / Std[1]; // G channel
                    tensor[2 * h * w + idx] = (b - Mean[2]) / Std[2]; // B channel
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }

        return tensor;
    }
}
