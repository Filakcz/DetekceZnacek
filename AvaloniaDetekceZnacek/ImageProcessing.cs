using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AvaloniaDetekceZnacek;

internal static class ImageProcessing
{
    // fakt musim udelat slozku na objekty
    // DetectionStages
    // Point
    internal sealed record DetectionStages(
            Bitmap Original,
            Bitmap RawMask,
            Bitmap CleanMask,
            Bitmap Final);

    public static DetectionStages Process(Bitmap image)
    {
        int width = image.PixelSize.Width;
        int height = image.PixelSize.Height;
        byte[] pixels = ReadPixels(image, width, height);

        byte[] rawMask = SignSegmentation.CreateRawMask(pixels, width, height);
        byte[] cleanMask = SignSegmentation.CreateCleanMask(rawMask, width, height);
        var outlines = SignShapeDetector.FindOutlines(cleanMask, width, height);

        return new DetectionStages(
                CreateBitmapCopy(image, width, height),
                CreateMaskBitmap(rawMask, width, height),
                CreateMaskBitmap(cleanMask, width, height),
                CreateOutlinedBitmap(image, outlines));
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

    private static WriteableBitmap CreateMaskBitmap(byte[] mask, int width, int height)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        using var framebuffer = bitmap.Lock();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (mask[y * width + x] == 0)
                {
                    SetPixel(framebuffer, x, y, 0, 0, 0, 255);
                }
                else if (mask[y * width + x] == 1)
                {
                    SetPixel(framebuffer, x, y, 0, 0, 255, 255);
                }
                else if (mask[y * width + x] == 2)
                {
                    SetPixel(framebuffer, x, y, 255, 0, 0, 255);
                }
                else
                {
                    SetPixel(framebuffer, x, y, 0, 255, 255, 255);
                }
            }
        }

        return bitmap;
    }

    private static WriteableBitmap CreateOutlinedBitmap(Bitmap image, List<List<SignShapeDetector.Point>> outlines)
    {
        int width = image.PixelSize.Width;
        int height = image.PixelSize.Height;

        var bitmap = CreateBitmapCopy(image, width, height);

        using var framebuffer = bitmap.Lock();

        foreach (var outline in outlines)
        {
            for (int index = 0; index < outline.Count; index++)
            {
                var start = outline[index];
                var end = outline[(index + 1) % outline.Count];
                DrawLine(framebuffer, start.X, start.Y, end.X, end.Y);
            }
        }
        return bitmap;
    }

    private static WriteableBitmap CreateBitmapCopy(Bitmap image, int width, int height)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        using var framebuffer = bitmap.Lock();
        image.CopyPixels(new PixelRect(0, 0, width, height), framebuffer.Address,
            framebuffer.RowBytes * height, framebuffer.RowBytes);
        return bitmap;
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
                        SetPixel(framebuffer, x, y, 0, 255, 0, 255);
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

    private static void SetPixel(ILockedFramebuffer framebuffer, int x, int y, byte B, byte G, byte R, byte A)
    {
        var offset = y * framebuffer.RowBytes + x * 4;
        Marshal.WriteByte(framebuffer.Address, offset, B); // B
        Marshal.WriteByte(framebuffer.Address, offset + 1, G); // G
        Marshal.WriteByte(framebuffer.Address, offset + 2, R); // R
        Marshal.WriteByte(framebuffer.Address, offset + 3, A); // A
    }
}
