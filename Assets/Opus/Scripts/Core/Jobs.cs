using System;
using System.Collections.Generic;
using Opus.Lawn;
using Opus.World;
using UnityEngine;

namespace Opus.Core
{
    /// <summary>A job on the job board: client, garden definition, target time, requested pattern and payment by stars.</summary>
    public sealed class JobDef
    {
        public string Id;
        public string Client;
        public string Title;
        /// <summary>A one-line introduction on the board.</summary>
        public string Summary;
        public Func<YardDefinition> Yard;
        /// <summary>Latest finishing time for the third star (minutes).</summary>
        public float ParMinutes;
        /// <summary>Pattern requested for the third star (null: none).</summary>
        public PatternKind? Pattern;
        /// <summary>Seed of the patch where the pattern is requested.</summary>
        public Vector2 PatternSeed;
        /// <summary>Payment (points) for 0, 1, 2, 3 stars.</summary>
        public int[] PayByStars;

        public int MaxPay => PayByStars[PayByStars.Length - 1];
        public float ParSeconds => ParMinutes * 60f;
    }

    /// <summary>Result of a delivered or abandoned job.</summary>
    [Serializable]
    public struct JobResult
    {
        public string JobId;
        /// <summary>Completion of the patch tasks weighted by area (0..1).</summary>
        public float Completion;
        public float Seconds;
        public bool PatternDone;
        public int Stars;
        public int Payment;
        /// <summary>false: the job was left half done (no payment, the remaining work waits in the save).</summary>
        public bool Delivered;
    }

    /// <summary>The persistent record of a job (in the home save): best stars and how many times it was delivered.</summary>
    [Serializable]
    public sealed class JobRecord
    {
        public string id;
        public int bestStars;
        public int delivered;
    }

    /// <summary>
    /// Jobs and rating. Stars: completion below 50% gives 0, at least 50% one, two if every patch is finished;
    /// the third star if every patch was finished within the target time and, if the job asks for a pattern, that is done too.
    /// </summary>
    public static class Jobs
    {
        public const float OneStarCompletion = 0.5f;

        public static readonly JobDef[] All =
        {
            new JobDef
            {
                Id = ClientYards.CityId,
                Client = "Ms. Selin",
                Title = "City garden",
                Summary = "Small but busy: a tree, a swing, flowerbeds, a vegetable bed. The edges need the trimmer.",
                Yard = ClientYards.City,
                ParMinutes = 5f,
                PayByStars = new[] { 0, 250, 450, 650 },
            },
            new JobDef
            {
                Id = ClientYards.ManorId,
                Client = "The Kaya family",
                Title = "Manor lawn",
                Summary = "A wide, flat lawn. North-south stripes are wanted on the front lawn.",
                Yard = ClientYards.Manor,
                ParMinutes = 12f,
                Pattern = PatternKind.StripesNorthSouth,
                PatternSeed = ClientYards.ManorPatternSeed,
                PayByStars = new[] { 0, 450, 850, 1200 },
            },
            new JobDef
            {
                Id = ClientYards.CountryId,
                Client = "Uncle Hasan",
                Title = "Country house garden",
                Summary = "Untended for years: a long meadow, lots of weeds and ivy, dried-out spots.",
                Yard = ClientYards.Country,
                ParMinutes = 9f,
                PayByStars = new[] { 0, 350, 650, 950 },
            },
        };

        public static JobDef Find(string id) => Array.Find(All, job => job.Id == id);

        /// <summary>
        /// Per patch, the average of the tasks in that patch (mowing always; watering and weeds if any), weighted by the patch's
        /// area. A finished patch counts in full, an unfinished one does not.
        /// </summary>
        public static float Completion(IReadOnlyList<PatchProgress> patches)
        {
            float done = 0f, total = 0f;
            foreach (PatchProgress p in patches)
            {
                float weight = Mathf.Max(p.AreaSquareMeters, 0.01f);
                float score;
                if (p.Finished)
                {
                    score = 1f;
                }
                else
                {
                    float sum = Mathf.Clamp01(p.MowFraction / LawnTasks.MowTarget);
                    int tasks = 1;
                    if (p.HasWater)
                    {
                        sum += Mathf.Clamp01(p.WaterFraction / LawnTasks.WaterTarget);
                        tasks++;
                    }
                    if (p.WeedsTotal > 0)
                    {
                        sum += 1f - p.WeedsLeft / (float)p.WeedsTotal;
                        tasks++;
                    }
                    score = Mathf.Min(sum / tasks, 0.99f);
                }
                done += score * weight;
                total += weight;
            }
            return total > 0f ? done / total : 0f;
        }

        public static int Stars(JobDef job, float completion, float seconds, bool patternDone)
        {
            if (completion < OneStarCompletion)
                return 0;
            if (completion < 0.999f)
                return 1;
            bool bonus = seconds <= job.ParSeconds && (job.Pattern == null || patternDone);
            return bonus ? 3 : 2;
        }

        public static int Payment(JobDef job, int stars) => job.PayByStars[Mathf.Clamp(stars, 0, job.PayByStars.Length - 1)];

        public static JobResult Evaluate(JobDef job, float completion, float seconds, bool patternDone, bool delivered)
        {
            int stars = delivered ? Stars(job, completion, seconds, patternDone) : 0;
            return new JobResult
            {
                JobId = job.Id,
                Completion = completion,
                Seconds = seconds,
                PatternDone = patternDone,
                Stars = stars,
                Payment = delivered ? Payment(job, stars) : 0,
                Delivered = delivered,
            };
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
