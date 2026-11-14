using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;

namespace AvaloniaDetekceZnacek;

internal static class CropClassifier
{
    // resize size
    // 32x32; 64x64
    private const int SampleSize = 32;

    // hog + k-NN
    private const int CellSize = 8;
    private const int OrientationBins = 9; // 20 stupnu

    internal sealed record Example(string Label, double[] Template, double[] Hog);
    internal sealed record Result(string TemplateLabel, double TemplateSimilarity, bool TemplateOverThreshold, string KnnLabel, double KnnDistance, bool KnnOverThreshold);

    public static List<Example> LoadExamples(string rootDirectory)
    {
        List<Example> examples = new List<Example>();
        foreach (string classDirectory in Directory.GetDirectories(rootDirectory))
        {
            string label = Path.GetFileName(classDirectory);
            foreach (string imagePath in Directory.GetFiles(classDirectory))
            {
                using var image = new Bitmap(imagePath);
                double[] template = CreateGrayTemplate(image);
                examples.Add(new Example(label, template, CreateHOG(template)));
            }
        }
        return examples;
    }

    public static Result Classify(Bitmap crop, IReadOnlyList<Example> examples)
    {
        double[] template = CreateGrayTemplate(crop);
        
        Example bestTemplate = examples[0];
        double bestTemplateDistance = TemplateMatchScore(template, bestTemplate.Template);
        foreach (var example in examples)
        {
            double distance = TemplateMatchScore(template, example.Template);
            if (distance < bestTemplateDistance)
            {
                bestTemplate = example;
                bestTemplateDistance = distance;
            }
        }

        double[] hog = CreateHOG(template);
        int manyNearest = Math.Min(3, examples.Count());
        double[] bestDistances = new double[manyNearest];
        string[] signs = new string[manyNearest];
        for (int i = 0; i < manyNearest; i++)
        {
            bestDistances[i] = double.PositiveInfinity;
        }
        for (int i = 0; i < examples.Count(); i++)
        {
            double dist = EuclideanDistance(hog, examples[i].Hog);
            int j = 0; 
            while (j < manyNearest && bestDistances[j] <= dist)
            {
                j++;
            }
            
            if (j < manyNearest)
            {
                for (int k = manyNearest - 1; k > j; k--)
                {
                    bestDistances[k] = bestDistances[k - 1];
                    signs[k] = signs[k - 1];
                }
                bestDistances[j] = dist;
                signs[j] = examples[i].Label;
            }
        }

        string knnLabel = "";
        int bestCount = 0;
        double bestDistanceSum = double.PositiveInfinity;
        for (int i = 0; i < manyNearest; i++)
        {
            int count = 0;
            double distanceSum = 0;

            for (int j = 0; j < manyNearest; j++)
            {
                if (signs[j] == signs[i])
                {
                    count++;
                    distanceSum += bestDistances[j];
                }
            }

            if (count > bestCount || (count == bestCount && distanceSum < bestDistanceSum))
            {
                knnLabel = signs[i];
                bestCount = count;
                bestDistanceSum = distanceSum;
            }
        }

        string templateName = bestTemplate.Label;
        bool templateOverThreshold = false;
        bool knnOverThreshold = false;
        if (bestTemplateDistance > 0.035)
        {
            templateName = templateName.Insert(0, "Přes práh detekce! ");
            templateOverThreshold = true;
        }
        if (bestDistances[0] > 2.4)
        {
            knnLabel = knnLabel.Insert(0, "Přes práh detekce! ");
            knnOverThreshold = true;
        }


        return new Result(templateName, bestTemplateDistance, templateOverThreshold, knnLabel, bestDistances[0], knnOverThreshold);
    }

    private static double[] CreateGrayTemplate(Bitmap image)
    {
        int width = image.PixelSize.Width;
        int height = image.PixelSize.Height;        
        var widthBytes = width * 4;
        var pixels = new byte[widthBytes * height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        image.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), pixels.Length, widthBytes);
        handle.Free();

        var result = new double[SampleSize * SampleSize];
        for (int y = 0; y < SampleSize; y++)
        {
            for (int x = 0; x < SampleSize; x++)
            {
                // https://en.wikipedia.org/wiki/Grayscale
                int sourceX = System.Math.Min(width - 1, x * width / SampleSize);
                int sourceY = System.Math.Min(height - 1, y * height / SampleSize);
                int offset = (sourceY * width + sourceX) * 4;
                // bgr
                result[y * SampleSize + x] = (0.114 * pixels[offset] + 0.587 * pixels[offset + 1] + 0.299 * pixels[offset + 2]);
            }
        }

        // https://en.wikipedia.org/wiki/Cross-correlation#Zero-normalized_cross-correlation_(ZNCC)
        double avg = result.Average();
        double squaredSum = 0;
        for (int i = 0; i < result.Length; i++)
        {
            squaredSum += (result[i] - avg) * (result[i] - avg);
        }
        double norm = System.Math.Sqrt(squaredSum);
        // https://cs.wikipedia.org/wiki/Norma_(matematika)
        // floating point error
        if (norm > 1e-12)
        {
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = (result[i] - avg) / norm;
            }
        }
        return result;
    }

    private static double[] CreateHOG(double[] gray)
    {
        // https://www.geeksforgeeks.org/computer-vision/histogram-of-oriented-gradients/
        // https://learnopencv.com/histogram-of-oriented-gradients/
        int cellsPerSide = SampleSize / CellSize;
        double[] cellHistograms = new double[cellsPerSide * cellsPerSide * OrientationBins];
        // 9 orientation bins = 20 stupnu na jeden
        for (int y = 1; y < SampleSize - 1; y++)
        {
            for (int x = 1; x < SampleSize - 1; x++)
            {
                double gx = gray[y * SampleSize + x + 1] - gray[y * SampleSize + x - 1];
                double gy = gray[(y + 1) * SampleSize + x] - gray[(y - 1) * SampleSize + x];
                double magnitude = System.Math.Sqrt(gx * gx + gy * gy);
                double angle = System.Math.Atan2(gy, gx) * 180 / System.Math.PI; // na stupne
                if (angle < 0)
                {
                    angle += 180;
                } 
                if (angle >= 180)
                {
                    angle -= 180;
                }

                double bin = angle / 180 * OrientationBins;
                int lowerBin = Convert.ToInt16(Math.Floor(bin));
                int uperBin = (lowerBin + 1 ) % OrientationBins;
                double upperMagn = bin - lowerBin;
                double lowerMagn = 1 - upperMagn;

                int cellX = x / CellSize;
                int cellY = y / CellSize;
                int cellOfset = (cellY * cellsPerSide + cellX) * OrientationBins;
                cellHistograms[lowerBin + cellOfset] += magnitude * lowerMagn;
                cellHistograms[uperBin + cellOfset] += magnitude * upperMagn;
            }
        }

        int cellsPerBlock = 2;
        int blocksPerSide = cellsPerSide - cellsPerBlock + 1;
        int featuresPerBlock = cellsPerBlock * cellsPerBlock * OrientationBins;

        double[] descriptor = new double[blocksPerSide * blocksPerSide * featuresPerBlock];
        for (int blockY = 0; blockY < blocksPerSide; blockY++)
        {
            for (int blockX = 0; blockX < blocksPerSide; blockX++)
            {
                int blockOffset = (blockY * blocksPerSide + blockX) * featuresPerBlock;
                int featureIndex = 0;

                for (int cellY = blockY; cellY < blockY + cellsPerBlock; cellY++)
                {
                    for (int cellX = blockX; cellX < blockX + cellsPerBlock; cellX++)
                    {
                        int cellOfset = (cellY * cellsPerSide + cellX) * OrientationBins;
                        for (int i = 0; i < OrientationBins; i++)
                        {
                            descriptor[blockOffset + featureIndex] = cellHistograms[cellOfset + i];
                            featureIndex++;
                        }
                    }
                }

                double sum = 0;
                for (int i = 0; i < featuresPerBlock; i++)
                {
                    double value = descriptor[blockOffset + i];
                    sum += value * value;
                }

                double norm = System.Math.Sqrt(sum);
                if (norm > 1e-12)
                {
                    for (int i = 0; i < featuresPerBlock; i++)
                    {
                        descriptor[blockOffset + i] = descriptor[blockOffset + i] / norm;
                    }
                }

            }
        }

        return descriptor;
    }

    //https://docs.opencv.org/5.0/main_modules/namespace_cv.html#group__imgproc__object_1ga586ebfb0a7fb604b35a23d85391329be
    // https://docs.opencv.org/5.0/main_modules/namespace_cv.html#group-imgproc-object-1ga3a7850640f1fe1f58fe91a2d7583695d
    private static double TemplateMatchScore(double[] first, double[] second)
    {
        // RMSE = method=TM_CCOEFF_NORMED
        double sum = 0;
        for (int i = 0; i < first.Length; i++)
        {
            double difference = first[i] - second[i];
            sum += difference * difference;
        }
        return System.Math.Sqrt(sum / first.Length);
    }

    private static double EuclideanDistance(double[] first, double[] second)
    {
        double sum = 0;
        for (int i = 0; i < first.Length; i++)
        {
            double difference = first[i] - second[i];
            sum += difference * difference;
        }
        return System.Math.Sqrt(sum);
    }
}
