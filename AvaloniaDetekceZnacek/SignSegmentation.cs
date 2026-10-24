using System;

namespace AvaloniaDetekceZnacek;

internal static class SignSegmentation
{
    public static bool[] CreateMask(byte[] pixels, int width, int height)
    {
        var mask = new bool[width * height];
        for (var pixel = 0; pixel < mask.Length; pixel++)
        {
            int offset = pixel * 4;
            float blue = pixels[offset] / 255f;
            float green = pixels[offset + 1] / 255f;
            float red = pixels[offset + 2] / 255f;
            float max = Math.Max(red, Math.Max(green, blue));
            float min = Math.Min(red, Math.Min(green, blue));
            float delta = max - min;
            float hue = GetHue(red, green, blue, max, delta);
            float saturation = 0;
            if (max != 0)
            {
                saturation = delta / max;
            }

            bool isRed = hue < 12 || hue > 345;
            bool isBlue = hue >= 190 && hue <= 255;
            bool isYellow = hue >= 40 && hue <= 60 && saturation > 0.55f;
            mask[pixel] = max > 0.25f && saturation > 0.4f && (isRed || isBlue || isYellow);
        }

        mask = Erode(mask, width, height);
        mask = Dilate(mask, width, height);
        mask = Dilate(mask, width, height);
        return Erode(mask, width, height);
        // return mask;
    }

    private static float GetHue(float red, float green, float blue, float max, float delta)
    {
        if (delta == 0)
            return 0;

        float hue;
        if (max == red)
        {
            hue = 60 * (((green - blue) / delta) % 6);
        }
        else if (max == green)
        {
            hue = 60 * ((blue - red) / delta + 2);
        }
        else
        {
            hue = 60 * ((red - green) / delta + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }
        return hue;
    }

    private static bool[] Erode(bool[] mask, int width, int height)
    {
        return ApplyMorphology(mask, width, height, true);
    }

    private static bool[] Dilate(bool[] mask, int width, int height)
    {
        return ApplyMorphology(mask, width, height, false);
    }

    private static bool[] ApplyMorphology(bool[] mask, int width, int height, bool erode)
    {
        var result = new bool[mask.Length];
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                bool value = erode;
                for (int dy = -1; dy <= 1 && value == erode; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var pixel = mask[(y + dy) * width + x + dx];
                        if (erode && !pixel || !erode && pixel)
                        {
                            value = !erode;
                            break;
                        }
                    }
                }

                result[y * width + x] = value;
            }
        }

        return result;
    }
}
