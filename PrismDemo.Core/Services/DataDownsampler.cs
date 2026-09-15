using PrismDemo.Core.Models;
using System;
using System.Collections.Generic;

namespace PrismDemo.Core.Services
{
    public static class DataDownsampler
    {
        private const int DefaultMaxPoints = 2000;

        public static ChartQueryResult Downsample(ChartQueryResult data, int maxPoints = DefaultMaxPoints)
        {
            if (data == null || data.Timestamps.Length <= maxPoints)
                return data;

            var result = new Dictionary<string, double[]>(data.Values.Count);
            foreach (var kv in data.Values)
            {
                var (_, y) = LTTB(data.Timestamps, kv.Value, maxPoints);
                result[kv.Key] = y;
            }

            var (x, _) = LTTB(data.Timestamps, data.Timestamps, maxPoints);

            return new ChartQueryResult
            {
                Timestamps = x,
                Values = result
            };
        }

        public static (double[] x, double[] y) LTTB(double[] x, double[] y, int maxPoints)
        {
            int n = x.Length;
            if (maxPoints >= n || maxPoints < 2)
                return (x, y);

            var resultX = new double[maxPoints];
            var resultY = new double[maxPoints];

            resultX[0] = x[0];
            resultY[0] = y[0];
            resultX[maxPoints - 1] = x[n - 1];
            resultY[maxPoints - 1] = y[n - 1];

            int[] selected = new int[maxPoints];
            selected[0] = 0;
            selected[maxPoints - 1] = n - 1;

            int bucketSize = (n - 2) / (maxPoints - 2);

            for (int i = 1; i < maxPoints - 1; i++)
            {
                int avgStart = i * bucketSize + 1;
                int avgEnd = (i + 1) * bucketSize + 1;
                if (avgEnd > n) avgEnd = n;

                double avgX = 0, avgY = 0;
                for (int j = avgStart; j < avgEnd; j++)
                {
                    avgX += x[j];
                    avgY += y[j];
                }
                int avgCount = avgEnd - avgStart;
                avgX /= avgCount;
                avgY /= avgCount;

                int prevIdx = selected[i - 1];
                int rangeStart = (i - 1) * bucketSize + 1;
                int rangeEnd = i * bucketSize + 1;
                if (rangeStart < 1) rangeStart = 1;
                if (rangeEnd > n - 1) rangeEnd = n - 1;

                double maxArea = -1;
                int maxIdx = rangeStart;

                double px = x[prevIdx], py = y[prevIdx];
                for (int j = rangeStart; j < rangeEnd; j++)
                {
                    double area = Math.Abs(
                        (px - avgX) * (y[j] - py) -
                        (px - x[j]) * (avgY - py));
                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxIdx = j;
                    }
                }

                selected[i] = maxIdx;
                resultX[i] = x[maxIdx];
                resultY[i] = y[maxIdx];
            }

            return (resultX, resultY);
        }
    }
}
