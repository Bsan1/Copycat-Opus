using UnityEngine;

namespace Opus.Core
{
    /// <summary>Checks whether the automated screenshots are blank (a single colour).</summary>
    public static class ShotCheck
    {
        /// <summary>A screenshot counts as blank if the standard deviation of brightness is below this (0–255 scale).</summary>
        public const float MinLuminanceDeviation = 2f;

        public static bool LooksBlank(Color32[] pixels)
        {
            return LuminanceDeviation(pixels) < MinLuminanceDeviation;
        }

        public static float LuminanceDeviation(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
                return 0f;

            // Read ~20 thousand pixels picked at even steps, not every pixel.
            int step = Mathf.Max(1, pixels.Length / 20000);
            double sum = 0, sumSq = 0;
            int count = 0;
            for (int i = 0; i < pixels.Length; i += step)
            {
                Color32 p = pixels[i];
                double lum = 0.2126 * p.r + 0.7152 * p.g + 0.0722 * p.b;
                sum += lum;
                sumSq += lum * lum;
                count++;
            }
            double mean = sum / count;
            double variance = System.Math.Max(0.0, sumSq / count - mean * mean);
            return (float)System.Math.Sqrt(variance);
        }
    }
}
