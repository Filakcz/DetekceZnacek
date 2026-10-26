using System.Collections.Generic;

namespace AvaloniaDetekceZnacek;

internal static class SignShapeDetector
{
    internal readonly record struct Point(int X, int Y);

    public static List<List<Point>> FindOutlines(bool[] mask, int width, int height)
    {
        var outlines = new List<List<Point>>();
        bool[] visited = new bool[mask.Length];
        Queue<int> queue = new Queue<int>();
        int minimumSize = System.Math.Max(80, mask.Length / 2000);
        int maximumSize = mask.Length / 2;

        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || visited[start])
            {
                continue;
            }

            var regionPixels = FindRegion(start, mask, visited, queue, width, height);
            if (regionPixels.Count < minimumSize || regionPixels.Count > maximumSize)
            {
                continue;
            }

            var outline = ConvexHull(regionPixels, mask, width, height);
            if (outline.Count >= 3)
            {
                outlines.Add(outline);
            }
        }

        return outlines;
    }

    private static List<int> FindRegion(int start, bool[] mask, bool[] visited, Queue<int> queue, int width, int height)
    {
        var pixels = new List<int>();
        visited[start] = true;
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            pixels.Add(index);
            int x = index % width;
            int y = index / width;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int neighborX = x + dx;
                    int neighborY = y + dy;
                    if (neighborX < 0 || neighborX >= width || neighborY < 0 || neighborY >= height)
                    {
                        continue;
                    }

                    int neighbor = neighborY * width + neighborX;
                    if (!mask[neighbor] || visited[neighbor])
                    {
                        continue;
                    }

                    visited[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return pixels;
    }
    
    // https://en.wikibooks.org/wiki/Algorithm_Implementation/Geometry/Convex_hull/Monotone_chain#Python
    private static List<Point> ConvexHull(List<int> pixels, bool[] mask, int width, int height)
    {
        var points = new List<Point>();
        foreach (int pixel in pixels)
        {
            int x = pixel % width;
            int y = pixel / width;
            if (IsBoundary(mask, pixel, x, y, width, height))
            {
                points.Add(new Point(x, y));
            }
        }

        points.Sort(ComparePoints);
        var lower = new List<Point>();
        foreach (var point in points)
        {
            while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= 0)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(point);
        }

        var upper = new List<Point>();
        for (int index = points.Count - 1; index >= 0; index--)
        {
            var point = points[index];
            while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= 0)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(point);
        }

        if (lower.Count > 0)
        {
            lower.RemoveAt(lower.Count - 1);
        }

        if (upper.Count > 0)
        {
            upper.RemoveAt(upper.Count - 1);
        }

        lower.AddRange(upper);
        return lower;
    }

    private static bool IsBoundary(bool[] mask, int pixel, int x, int y, int width, int height)
    {
        if (y == 0 || y == height - 1 || x == 0 || x == width - 1)
        {
            return true;
        }

        if (!mask[pixel - width] || !mask[pixel + width] || !mask[pixel - 1])
        {
            return true;
        }

        return !mask[pixel + 1];
    }

    private static int ComparePoints(Point first, Point second)
    {
        int compareX = first.X.CompareTo(second.X);
        if (compareX != 0)
        {
            return compareX;
        }

        return first.Y.CompareTo(second.Y);
    }

    private static long Cross(Point origin, Point first, Point second)
    {
        long firstX = first.X - origin.X;
        long firstY = first.Y - origin.Y;
        long secondX = second.X - origin.X;
        long secondY = second.Y - origin.Y;
        return firstX * secondY - firstY * secondX;
    }
}
