using System;

namespace AvaloniaDetekceZnacek;

internal static class SignSegmentation
{
    public static byte[] CreateRawMask(byte[] pixels, int width, int height)
    {
        byte[] mask = new byte[width * height];
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

            if (max <= 0.25f || saturation < 0.4f)
            {
                continue;
            }

            if (hue < 12 || hue > 345)
            {
                mask[pixel] = 1;
            }
            else if (hue >= 190 && hue <= 255)
            {
                mask[pixel] = 2;
            }
            else if (hue >= 37 && hue <= 60 && saturation > 0.55f) 
            {
                mask[pixel] = 3;
            }

        }

        return mask;
    }

    public static byte[] CreateCleanMask(byte[] mask, int width, int height)
    {
        byte[] result = new byte[mask.Length];
        result = Erode(mask, width, height);
        result = Dilate(result, width, height);
        result = Dilate(result, width, height);
        result = Erode(result, width, height);

        return result;
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

    private static byte[] Erode(byte[] mask, int width, int height)
    {
        return ApplyMorphology(mask, width, height, true);
    }

    private static byte[] Dilate(byte[] mask, int width, int height)
    {
        return ApplyMorphology(mask, width, height, false);
    }

    private static byte[] ApplyMorphology(byte[] mask, int width, int height, bool erode)
    {
        byte[] result = new byte[mask.Length];
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int index = y * width + x;
                byte current = mask[index];

                if (erode)
                {
                    if (current == 0)
                    {
                        continue;
                    }

                    bool keep = true;
                    for (int dy = -1; dy <= 1 && keep; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (mask[(y + dy) * width + x + dx] != current)
                            {
                                keep = false;
                                break;
                            }
                        }
                    }

                    if (keep)
                    {
                        result[index] = current;
                    }
                }

                else
                {
                    if (current != 0)
                    {
                        result[index] = current;
                        continue;
                    }

                    for (int dy = -1; dy <= 1 && result[index] == 0; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            byte neighbor = mask[(y + dy) * width + x + dx];
                            if (neighbor != 0)
                            {
                                result[index] = neighbor;
                                break;
                            }
                        }
                    }
                }
            }
        }

        return result;
    }
}
