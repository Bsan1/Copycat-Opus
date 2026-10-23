using System;
using System.Collections;
using System.Collections.Generic;
using Opus.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Opus.Core
{
    /// <summary>
    /// State that lasts across garden changes (even though the scene is rebuilt): which garden you are in (home or a client job),
    /// the profile carried between gardens (day, points, upgrades, collection), job records and the cards shown during the
    /// transition. The source on disk is the home save (the job records are inside it); this class only carries the transitions.
    /// </summary>
    public static class Career
    {
        public sealed class Profile
        {
            public int Day = 1;
            public int Points;
            public int[] Upgrades = Array.Empty<int>();
            public string[] Collected = Array.Empty<string>();
        }

        public const float FadeOutSeconds = 0.55f;

        static Profile profile;
        static readonly List<JobRecord> records = new List<JobRecord>();

        /// <summary>First-launch work (deleting scenario saves, continuing from the save) is done once.</summary>
        public static bool Booted { get; set; }
        /// <summary>The job in progress (null at home).</summary>
        public static JobDef ActiveJob { get; private set; }
        public static bool AtHome => ActiveJob == null;
        /// <summary>Did the garden change in this run (the profile and job records are in memory, newer than the save)?</summary>
        public static bool HasProfile => profile != null;
        public static IReadOnlyList<JobRecord> Records => records;
        /// <summary>The job result to show on the night you come home.</summary>
        public static JobResult? PendingResult { get; private set; }
        /// <summary>The card shown on the darkened screen while a new garden opens.</summary>
        public static string ArrivalTitle { get; private set; }
        public static string ArrivalBody { get; private set; }
        /// <summary>When the last transition started (real time) and how long the new garden took to build.</summary>
        public static float TransitionStartedAt { get; set; } = -1f;
        public static float LastBuildSeconds { get; set; }
        public static int Transitions { get; private set; }
        /// <summary>A transition is fading (the menu does not open); it ends when the new garden is built.</summary>
        public static bool Traveling { get; set; }

        public static YardDefinition CurrentYard() => ActiveJob != null ? ActiveJob.Yard() : HomeYard.Create();
        public static string CurrentSlot => ActiveJob != null ? ActiveJob.Id : SaveSystem.HomeSlot;

        public static JobRecord RecordOf(string id)
        {
            JobRecord record = records.Find(r => r.id == id);
            if (record == null)
            {
                record = new JobRecord { id = id };
                records.Add(record);
            }
            return record;
        }

        /// <summary>Job records from the home save; if the garden changed in this run the ones in memory are newer and left alone.</summary>
        public static void LoadRecords(JobRecord[] saved)
        {
            if (HasProfile)
                return;
            records.Clear();
            if (saved != null)
                foreach (JobRecord record in saved)
                    if (record != null && !string.IsNullOrEmpty(record.id))
                        records.Add(new JobRecord { id = record.id, bestStars = record.bestStars, delivered = record.delivered });
        }

        public static JobRecord[] CaptureRecords()
        {
            var copy = new JobRecord[records.Count];
            for (int i = 0; i < records.Count; i++)
                copy[i] = new JobRecord { id = records[i].id, bestStars = records[i].bestStars, delivered = records[i].delivered };
            return copy;
        }

        public static Profile CaptureProfile(YardSession session) => new Profile
        {
            Day = session.Days.Day,
            Points = session.Wallet.Points,
            Upgrades = session.Upgrades.Capture(),
            Collected = session.Finds.CaptureCollected(),
        };

        /// <summary>Writes the carried profile into the newly built garden's session (does nothing if the garden did not change).</summary>
        public static void ApplyProfile(YardSession session)
        {
            if (profile == null)
                return;
            session.Days.Restore(profile.Day);
            session.Wallet.Restore(profile.Points);
            session.Upgrades.Restore(profile.Upgrades);
            session.Finds.Restore(profile.Collected, session.Finds.CaptureActive());
        }

        /// <summary>New game: the job records and the carried profile are deleted.</summary>
        public static void ResetProgress()
        {
            records.Clear();
            profile = null;
            PendingResult = null;
        }

        public static void ConsumeArrival()
        {
            ArrivalTitle = ArrivalBody = null;
        }

        public static JobResult? ConsumeResult()
        {
            JobResult? result = PendingResult;
            PendingResult = null;
            return result;
        }

        // ---------- Transitions ----------

        /// <summary>From home to a job: home is saved, the profile is carried, the scene is rebuilt with the job's garden.</summary>
        public static void GoToJob(YardSession home, JobDef job)
        {
            bool resume = SaveSystem.SlotExists(job.Id);
            home.StartCoroutine(Travel(home, $"{job.Client}",
                $"{job.Title}\n{(resume ? "Continue the unfinished job" : job.Summary)}", () =>
                {
                    home.Save();
                    profile = CaptureProfile(home);
                    ActiveJob = job;
                    PendingResult = null;
                }));
        }

        /// <summary>
        /// From the job to home: if delivered, the payment is added to the profile, the job record is updated and the half-done job save is deleted;
        /// if left, the job is saved to its own slot. It becomes night at home (a job counts as a day).
        /// </summary>
        public static void ReturnHome(YardSession jobSession, JobResult result)
        {
            JobDef job = ActiveJob;
            jobSession.StartCoroutine(Travel(jobSession, "Heading home",
                result.Delivered ? $"{job?.Client}: {result.Stars} stars  ·  +{result.Payment} points" : "The job is unfinished; you can continue where you left off.", () =>
                {
                    if (result.Delivered)
                        SaveSystem.DeleteSlot(job.Id);
                    else
                        jobSession.Save();
                    profile = CaptureProfile(jobSession);
                    profile.Points += result.Payment;
                    if (result.Delivered)
                    {
                        JobRecord record = RecordOf(job.Id);
                        record.delivered++;
                        record.bestStars = Mathf.Max(record.bestStars, result.Stars);
                    }
                    ActiveJob = null;
                    PendingResult = result;
                }));
        }

        static IEnumerator Travel(YardSession from, string title, string body, Action prepare)
        {
            Traveling = true;
            Time.timeScale = 0f;
            from.Hud.ShowNight(title, body);
            for (float t = 0f; t < FadeOutSeconds; t += Time.unscaledDeltaTime)
            {
                from.Hud.NightAlpha = t / FadeOutSeconds;
                yield return null;
            }
            from.Hud.NightAlpha = 1f;
            prepare();
            ArrivalTitle = title;
            ArrivalBody = body;
            Transitions++;
            TransitionStartedAt = Time.realtimeSinceStartup;
            YardBuilder.Use(CurrentYard());
            SaveSystem.Slot = CurrentSlot;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
