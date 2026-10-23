using System.Collections.Generic;
using Opus.Lawn;
using UnityEngine;

namespace Opus.Core
{
    /// <summary>The work done in a day (for the end-of-day summary).</summary>
    public sealed class DayStats
    {
        public float MowedSquareMeters;
        public float WateredSquareMeters;
        public int WeedsRemoved;
        public int PatchesFinished;
        public float Points;
        public int Finds;
        public int Apples;
    }

    /// <summary>The result of the night.</summary>
    public readonly struct NightReport
    {
        public readonly int FinishedDay;
        public readonly bool Rain;
        public readonly int RegrownCells;
        public readonly int WeedsBack;
        public readonly int PatchesReopened;

        public NightReport(int finishedDay, bool rain, int regrownCells, int weedsBack, int patchesReopened)
        {
            FinishedDay = finishedDay;
            Rain = rain;
            RegrownCells = regrownCells;
            WeedsBack = weedsBack;
            PatchesReopened = patchesReopened;
        }
    }

    /// <summary>
    /// Day cycle (§3-C): when the player ends the day it becomes night. Mown grass grows back at different speeds by patch
    /// (fast patches want mowing again the next day, slow ones two days later), dry spots dry out again,
    /// some of the pulled weeds come back. Some nights it rains: the dry spots end up watered and the grass is wet in the morning.
    /// All of it is seeded with the day number (the same day gives the same night).
    /// </summary>
    public sealed class DayCycle
    {
        /// <summary>Growth of mown grass in a night (on the 0..255 height scale, before the patch multiplier).</summary>
        public const float Growth = 72f;
        /// <summary>How far dry spots return to their starting dryness in a night.</summary>
        public const float Drying = 0.35f;
        public const float RainChance = 0.25f;
        public const float WeedRegrowChance = 0.3f;

        readonly float[] patchGrowth;

        public int Day { get; private set; } = 1;
        public DayStats Today { get; private set; } = new DayStats();
        public NightReport? LastNight { get; private set; }
        /// <summary>Growth multiplier by patch id (0.8..1.35).</summary>
        public IReadOnlyList<float> PatchGrowth => patchGrowth;

        public DayCycle(int patchCount)
        {
            patchGrowth = new float[patchCount + 1];
            for (int id = 1; id <= patchCount; id++)
            {
                // Per patch, seeded: some patches grow fast
                float h = Mathf.Abs(Mathf.Sin(id * 12.9898f + 4.1414f) * 43758.5453f) % 1f;
                patchGrowth[id] = Mathf.Lerp(0.8f, 1.35f, h);
            }
        }

        /// <summary>Whether tonight will be rainy (it never rains on the first night).</summary>
        public static bool RainsAfter(int day) => day > 1 && new System.Random(day * 7919 + 13).NextDouble() < RainChance;

        /// <summary>Ends the day, applies the night and moves on to the next day.</summary>
        public NightReport EndDay(LawnState lawn, WeedField weeds, LawnTasks tasks)
        {
            int finished = Day;
            bool rain = RainsAfter(Day);
            var random = new System.Random(Day * 104729 + 7);
            int before = tasks.FinishedCount;
            int regrown = lawn.GrowOvernight(Growth * (rain ? 1.25f : 1f), patchGrowth, Drying, rain);
            int weedsBack = weeds.RegrowSome(WeedRegrowChance, random, patch => tasks.IsAreaUnlocked(tasks.Get(patch)?.Area ?? 0));
            tasks.Refresh();
            Day++;
            Today = new DayStats();
            var report = new NightReport(finished, rain, regrown, weedsBack, Mathf.Max(0, before - tasks.FinishedCount));
            LastNight = report;
            return report;
        }

        /// <summary>Restoring from a save.</summary>
        public void Restore(int day)
        {
            Day = Mathf.Max(1, day);
            Today = new DayStats();
            LastNight = null;
        }
    }
}
