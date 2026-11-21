using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AvaloniaDetekceZnacek;

internal static class ImageProcessing
{
    // fakt musim udelat slozku na objekty
    // DetectionStages
    // Point
    // regions
    internal sealed record DetectionStages(
            Bitmap Original,
            Bitmap RawMask,
            Bitmap CleanMask,
            Bitmap Final,
            List<Bitmap> Crops,
            List<SignShapeDetector.Region> Regions);

    internal sealed record Detection(
            Bitmap Final,
            List<Bitmap> Crops
            );

    public static Detection SimpleProcess(Bitmap image)
    {
        int width = image.PixelSize.Width;
        int height = image.PixelSize.Height;
        byte[] pixels = ReadPixels(image, width, height);
    
        byte[] mask = SignSegmentation.CreateCleanMask(SignSegmentation.CreateRawMask(pixels, width, height), width, height);
        var regions = SignShapeDetector.FindOutlines(mask, width, height);

        return new Detection(
                CreateOutlinedBitmap(image, regions),
                CreateCrops(image, regions)
                );
    }

    public static DetectionStages Process(Bitmap image)
    {
        int width = image.PixelSize.Width;
        int height = image.PixelSize.Height;
        byte[] pixels = ReadPixels(image, width, height);

        byte[] rawMask = SignSegmentation.CreateRawMask(pixels, width, height);
        byte[] cleanMask = SignSegmentation.CreateCleanMask(rawMask, width, height);
        var regions = SignShapeDetector.FindOutlines(cleanMask, width, height);

        return new DetectionStages(
                CreateBitmapCopy(image, width, height),
                CreateMaskBitmap(rawMask, width, height),
                CreateMaskBitmap(cleanMask, width, height),
                CreateOutlinedBitmap(image, regions),
                CreateCrops(image, regions),
                regions);
    }

    private static List<Bitmap> CreateCrops(Bitmap image, List<SignShapeDetector.Region> regions)
    {
        var crops = new List<Bitmap>(regions.Count);
        foreach (var region in regions)
        {
            var crop = new WriteableBitmap(new PixelSize(region.Width, region.Height), new Vector(96, 96),
                PixelFormats.Bgra8888, AlphaFormat.Premul);
            using var framebuffer = crop.Lock();
            image.CopyPixels(new PixelRect(region.X, region.Y, region.Width, region.Height), framebuffer.Address,
                framebuffer.RowBytes * region.Height, framebuffer.RowBytes);
            crops.Add(crop);
        }
        return crops;
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
        int[] palette = {
        unchecked((int)0xFF000000), // 0 cerna
        unchecked((int)0xFFFF0000), // 1 cervena 
        unchecked((int)0xFF0000FF), // 2 modra
        unchecked((int)0xFFFFFF00)}; // 3 zluta

        var bitmap = new WriteableBitmap(
            new PixelSize(width, height), new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
        using var framebuffer = bitmap.Lock();
        int[] row = new int[width];
        for (int y = 0; y < height; y++)
        {
            int offset = y * width; 
            for (int x = 0; x < width; x++)
            {
                row[x] = palette[mask[offset + x]];
            }
            Marshal.Copy(row, 0, framebuffer.Address + y * framebuffer.RowBytes, width);
        }

        return bitmap;
    }

    private static WriteableBitmap CreateOutlinedBitmap(Bitmap image, List<SignShapeDetector.Region> regions)
    {
        int width = image.PixelSize.Width;
        int height = image.PixelSize.Height;

        var bitmap = CreateBitmapCopy(image, width, height);

        using var framebuffer = bitmap.Lock();

        foreach (var region in regions)
        {
            var outline = region.Outline;
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
