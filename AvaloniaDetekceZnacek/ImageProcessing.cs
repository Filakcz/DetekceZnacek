using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AvaloniaDetekceZnacek;

internal static class ImageProcessing
{
    public static WriteableBitmap DetectSigns(Bitmap image)
    {
        int width = image.PixelSize.Width;
        int height = image.PixelSize.Height;
        byte[] pixels = ReadPixels(image, width, height);
        bool[] mask = SignSegmentation.CreateMask(pixels, width, height);
        var outlines = SignShapeDetector.FindOutlines(mask, width, height);
        WriteableBitmap result = CreateBitmap(image, width, height);

        using var framebuffer = result.Lock();
        foreach (var outline in outlines)
        {
            DrawOutline(framebuffer, outline);
        }
        return result;
    }

    private static byte[] ReadPixels(Bitmap image, int width, int height)
    {
        var widthBytes = width * 4;
        var pixels = new byte[widthBytes * height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        image.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), pixels.Length, widthBytes);
        handle.Free();
        return pixels;
    }

    private static WriteableBitmap CreateBitmap(Bitmap image, int width, int height)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        using var framebuffer = bitmap.Lock();
        image.CopyPixels(new PixelRect(0, 0, width, height), framebuffer.Address,
            framebuffer.RowBytes * height, framebuffer.RowBytes);
        return bitmap;
    }

    private static void DrawOutline(ILockedFramebuffer framebuffer, List<SignShapeDetector.Point> outline)
    {
        for (int index = 0; index < outline.Count; index++)
        {
            var start = outline[index];
            var end = outline[(index + 1) % outline.Count];
            DrawLine(framebuffer, start.X, start.Y, end.X, end.Y);
        }
    }

    // https://en.wikipedia.org/wiki/Bresenham's_line_algorithm
    private static void DrawLine(ILockedFramebuffer framebuffer, int x1, int y1, int x2, int y2)
    {
        int dx = System.Math.Abs(x2 - x1);
        int sx = 1;
        if (x1 >= x2)
        {
            sx = -1;
        }

        int dy = -System.Math.Abs(y2 - y1);
        int sy = 1;
        if (y1 >= y2)
        {
            sy = -1;
        }

        int error = dx + dy;
        while (true)
        {
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    int x = x1 + offsetX;
                    int y = y1 + offsetY;
                    if (x >= 0 && x < framebuffer.Size.Width && y >= 0 && y < framebuffer.Size.Height)
                    {
                        SetPixel(framebuffer, x, y);
                    }
                }
            }

            if (x1 == x2 && y1 == y2)
            {
                break;
            }

            int doubledError = error * 2;
            if (doubledError >= dy)
            {
                error += dy;
                x1 += sx;
            }

            if (doubledError <= dx)
            {
                error += dx;
                y1 += sy;
            }
        }
    }

    private static void SetPixel(ILockedFramebuffer framebuffer, int x, int y)
    {
        var offset = y * framebuffer.RowBytes + x * 4;
        Marshal.WriteByte(framebuffer.Address, offset, 0); // R
        Marshal.WriteByte(framebuffer.Address, offset + 1, 255); // G
        Marshal.WriteByte(framebuffer.Address, offset + 2, 0); // B
        Marshal.WriteByte(framebuffer.Address, offset + 3, 255); // A
    }
}
