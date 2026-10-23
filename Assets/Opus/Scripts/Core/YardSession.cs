using System;
using System.Collections;
using System.Collections.Generic;
using Opus.Lawn;
using Opus.Player;
using Opus.Tools;
using Opus.UI;
using Opus.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Opus.Core
{
    /// <summary>
    /// The garden session's frame loop (runs after the player): updates the task list, picks the active patch and
    /// the current area, manages the dashed border/highlight/finish effect and the flowers, feeds the interface.
    /// When an area is finished it opens the next area's gate. The day cycle (grass grows at night), points and workshop upgrades,
    /// the day's pattern and the items lost in the grass come together here. Saving/loading/new game and autosave are here too.
    /// F5 quick save, F9 quick load, F8 (developer) finishes the active patch without playing.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class YardSession : MonoBehaviour
    {
        const float StatsInterval = 0.5f;
        const float PatternInterval = 0.25f;
        public const float AutosaveSeconds = 30f;
        public const float NightFadeIn = 0.7f;
        public const float NightHold = 3.2f;
        public const float NightFadeOut = 0.8f;

        PlayerController player;
        LawnState lawn;
        WeedField weeds;
        GrassField grass;
        float statsTimer;
        int statsFrames;
        float autosaveTimer;
        float patternTimer;
        int lastCut, lastWatered;
        readonly HashSet<int> bloomed = new HashSet<int>();
        SaveData initialState;

        public LawnTasks Tasks { get; private set; }
        public LawnOverlay Overlay { get; private set; }
        public Flowers Flowers { get; private set; }
        public Hud Hud { get; private set; }
        public DayCycle Days { get; private set; }
        public Wallet Wallet { get; private set; }
        public Upgrades Upgrades { get; private set; }
        public LostItems Finds { get; private set; }
        /// <summary>Apples dropped under the fruit trees.</summary>
        public FallenApples Apples { get; private set; }
        /// <summary>The day's pattern (null if none).</summary>
        public PatternTask Pattern { get; private set; }
        public PatchProgress ActivePatch { get; private set; }
        /// <summary>The area the player is in.</summary>
        public int CurrentArea { get; private set; } = LawnTasks.FirstArea;
        public float PlaySeconds { get; private set; }
        /// <summary>The end-of-day screen and the night are running (the game stops, the menu does not open).</summary>
        public bool IsNight { get; private set; }
        /// <summary>Autosave (every 30 s, when an area unlocks, when the day ends, on a break and on exit). Off in scenarios.</summary>
        public bool AutosaveEnabled { get; set; }
        /// <summary>Local time of the last successful save (empty if none).</summary>
        public string LastSaveTime { get; private set; } = string.Empty;
        public int SaveCount { get; private set; }

        /// <summary>An area was unlocked (area, animated or not: yes in play, no when restoring a save).</summary>
        public event Action<int, bool> AreaUnlocked;
        public event Action NightStarted;
        /// <summary>The night is over, a new day has started.</summary>
        public event Action<NightReport> DayStarted;
        public event Action<PatternTask> PatternCompleted;
        public event Action<FindDef, Vector3> ItemFound;
        public event Action<Vector3> ApplePicked;

        public static YardSession Create(Transform parent, PlayerController player, LawnState lawn, WeedField weeds, GrassField grass, UiRoot ui)
        {
            var session = parent.gameObject.AddComponent<YardSession>();
            session.player = player;
            session.lawn = lawn;
            session.weeds = weeds;
            session.grass = grass;
            session.Tasks = new LawnTasks(lawn, YardBuilder.PatchInfos, weeds.TotalInPatch, weeds.RemainingInPatch)
            {
                AreasOpenedBy = YardBuilder.Definition.AreasOpenedBy,
            };
            session.Overlay = new LawnOverlay(lawn);
            session.Flowers = Flowers.Create(parent);
            session.Hud = Hud.Create(ui, session.Tasks, player.Tools, ToolIcons.RenderAll(), YardBuilder.Areas);
            session.Days = new DayCycle(lawn.Layout.PatchCount);
            session.Wallet = new Wallet();
            session.Upgrades = new Upgrades();
            session.Upgrades.Changed += _ => session.ApplyUpgrades();
            session.Finds = LostItems.Create(parent, lawn);
            session.Finds.Found += session.OnFound;
            session.Apples = FallenApples.Create(parent, lawn, FallenApples.TreesOf(YardBuilder.Definition));
            session.Apples.Picked += session.OnApplePicked;
            session.Tasks.PatchFinished += session.OnPatchFinished;
            session.Tasks.AreaCompleted += session.OnAreaCompleted;
            session.Tasks.AllUnlockedDone += session.OnAllUnlockedDone;
            weeds.Removed += session.OnWeedRemoved;
            session.Job = Career.ActiveJob;

            session.StartDay();
            // "New game" returns to this state (no need to delete the save and rebuild the scene).
            session.initialState = session.Capture();
            return session;
        }

        bool PatchUnlocked(int patch) => Tasks.IsAreaUnlocked(Tasks.Get(patch)?.Area ?? 0);

        /// <summary>
        /// The day's pattern and the hidden items; the counters' starting values. A client job has no daily pattern or items; if the job
        /// asks for a pattern, that pattern is expected in that patch.
        /// </summary>
        void StartDay()
        {
            if (Job == null)
            {
                Pattern = PatternTask.ForDay(Days.Day, lawn, Tasks);
                Finds.HideForDay(Days.Day, PatchUnlocked);
                Apples.SpawnForDay(Days.Day, Tasks.IsAreaUnlocked);
            }
            else
            {
                Pattern = Job.Pattern.HasValue ? new PatternTask(Job.Pattern.Value, JobPatternPatch(), lawn) : null;
            }
            Overlay.SetPattern(Pattern);
            lastCut = lawn.TotalCutCells;
            lastWatered = lawn.TotalWatered;
        }

        /// <summary>The patch of the pattern the job asks for (the order of the seed in the definition).</summary>
        int JobPatternPatch()
        {
            PatchSeed[] seeds = YardBuilder.PatchSeeds;
            for (int i = 0; i < seeds.Length; i++)
                if ((seeds[i].Position - Job.PatternSeed).sqrMagnitude < 1e-4f)
                    return i + 1;
            return 1;
        }

        // ---------- Client job ----------

        /// <summary>The client job in progress (null at home).</summary>
        public JobDef Job { get; private set; }
        /// <summary>All patches of the client garden are finished (the delivery card opens).</summary>
        public event Action JobFinished;

        /// <summary>The job's current rating (the stars and payment it would get if delivered).</summary>
        public JobResult EvaluateJob(bool delivered) =>
            Jobs.Evaluate(Job, Jobs.Completion(Tasks.Patches), PlaySeconds, Pattern?.Completed ?? false, delivered);

        /// <summary>
        /// Opening of a newly built garden: the transition card opens from the darkened screen; when coming home from a job the night
        /// starts straight away (a job counts as a day).
        /// </summary>
        public void PlayArrival()
        {
            JobResult? result = Job == null ? Career.ConsumeResult() : null;
            string title = Career.ArrivalTitle, body = Career.ArrivalBody;
            Career.ConsumeArrival();
            if (result.HasValue)
                StartCoroutine(EndDayRoutine(JobSummary(result.Value), startDark: true));
            else if (!string.IsNullOrEmpty(title))
                StartCoroutine(ArrivalRoutine(title, body));
        }

        IEnumerator ArrivalRoutine(string title, string body)
        {
            Hud.ShowNight(title, body);
            Hud.NightAlpha = 1f;
            for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                yield return null;
            for (float t = 0f; t < NightFadeOut; t += Time.unscaledDeltaTime)
            {
                Hud.NightAlpha = 1f - t / NightFadeOut;
                yield return null;
            }
            Hud.NightAlpha = 0f;
            if (Job != null)
                Hud.ShowBanner(Job.Title, Job.Pattern.HasValue ? $"{Job.Client} · requested: {Pattern?.Name}" : $"{Job.Client} · target time {Job.ParMinutes:0} min");
        }

        static string JobSummary(JobResult result)
        {
            JobDef job = Jobs.Find(result.JobId);
            string name = job != null ? $"{job.Client} · {job.Title}" : result.JobId;
            if (!result.Delivered)
                return $"Job: {name}\nLeft unfinished ({Mathf.FloorToInt(result.Completion * 100f)}%); you can continue where you left off.\n\n";
            return $"Job: {name}\n{result.Stars} stars  ·  completion {Mathf.FloorToInt(result.Completion * 100f)}%  ·  time {Jobs.FormatTime(result.Seconds)}  ·  +{Hud.FormatPoints(result.Payment)} points\n\n";
        }

        void ApplyUpgrades()
        {
            if (player.Tools.FindSlot(Tool.MowerSlot) is MowerTool mower)
                mower.SetUpgrades(Upgrades.DeckScale, Upgrades.MowerSpeedBonus, Upgrades.MowerSlowdown);
            if (player.Tools.FindSlot(Tool.TrimmerSlot) is TrimmerTool trimmer)
            {
                trimmer.RadiusScale = Upgrades.TrimmerRadiusScale;
                trimmer.DamageScale = Upgrades.TrimmerDamageScale;
            }
            if (player.Tools.FindSlot(Tool.HoseSlot) is HoseTool hose)
                hose.SetUpgrades(Upgrades.HoseRateScale, Upgrades.HoseReachBonus, Upgrades.HasRainHead);
        }

        /// <summary>Points per job; on a client job points only come with the payment on delivery.</summary>
        void Earn(float points)
        {
            if (Job != null)
                return;
            Wallet.Add(points);
            Days.Today.Points += points;
        }

        void OnPatchFinished(PatchProgress patch)
        {
            Overlay.StartFinish(patch);
            // Flowers only open on the first finish; on upkeep the finish wave and points
            if (bloomed.Add(patch.Id))
                Flowers.Bloom(lawn, patch, Overlay.FinishStart, LawnOverlay.FinishWaveSpeed);
            Earn(Rewards.Patch);
            Days.Today.PatchesFinished++;
            Debug.Log($"[Opus] Patch finished: {patch.Name} (area {patch.Area}: {Tasks.FinishedIn(patch.Area)}/{Tasks.TotalIn(patch.Area)})");
        }

        void OnAreaCompleted(int area)
        {
            Debug.Log($"[Opus] Area {area} complete.");
            Earn(Rewards.Area);
            // Gate chain: the area behind every gate leading out of this area is unlocked
            IReadOnlyList<int> opened = Tasks.OpenedBy(area);
            if (opened.Count == 0)
            {
                if (Tasks.AllFinished)
                {
                    Hud.ShowBanner("The whole garden is done!", "Every patch is mown, watered and weeded.");
                    if (Job != null)
                        JobFinished?.Invoke();
                }
                return;
            }
            foreach (int next in opened)
                if (!Tasks.IsAreaUnlocked(next))
                    UnlockArea(next, animate: true);
            if (AutosaveEnabled)
                Save();
        }

        void OnAllUnlockedDone()
        {
            // If the area finish banner came up in the same frame, do not override it; on a client job the delivery card opens
            if (Job == null && string.IsNullOrEmpty(Hud.BannerText))
                Hud.ShowBanner("Done for today!", "Open the break menu to end the day (Esc / Start).");
        }

        void OnWeedRemoved(WeedState weed)
        {
            Earn(weed.Kind == WeedKind.Weed ? Rewards.Weed : Rewards.Overgrowth);
            Days.Today.WeedsRemoved++;
        }

        void OnFound(FindDef def, Vector3 position)
        {
            Earn(Rewards.Find);
            Days.Today.Finds++;
            Hud.ShowBanner($"Found: {def.Name}", $"Collection {Finds.Collected.Count} / {LostItems.Catalog.Length}  ·  +{Rewards.Find} points");
            Debug.Log($"[Opus] Item found: {def.Name}");
            ItemFound?.Invoke(def, position);
        }

        void OnApplePicked(Vector3 position)
        {
            Earn(Rewards.Apple);
            Days.Today.Apples++;
            ApplePicked?.Invoke(position);
        }

        /// <summary>Makes the area workable and opens the gate leading to it.</summary>
        public void UnlockArea(int area, bool animate)
        {
            Tasks.UnlockArea(area);
            foreach (Gate gate in YardBuilder.GatesTo(area))
            {
                if (animate) gate.Open();
                else gate.SetOpenInstant(true);
            }
            if (animate)
            {
                Hud.ShowBanner("Gate opened!", $"{YardBuilder.AreaName(area)} can now be mowed.");
                Finds.HideForDay(Days.Day * 100 + area, PatchUnlocked);
                Apples.SpawnForArea(Days.Day, area);
            }
            Debug.Log($"[Opus] Area {area} unlocked.");
            AreaUnlocked?.Invoke(area, animate);
        }

        /// <summary>Finishes the patch without playing (scenarios and F8).</summary>
        public void CompletePatch(int patch)
        {
            lawn.CompletePatch(patch);
            weeds.RemoveAllInPatch(patch);
            Tasks.Refresh();
        }

        void Update()
        {
            Tasks.Refresh();
            ActivePatch = FindActivePatch() ?? ActivePatch;
            Overlay.SetActivePatch(ActivePatch);
            Overlay.Tick(Time.deltaTime);

            if (player.LastInput.HighlightPressed)
                Overlay.StartHighlight(player.transform.position, ActivePatch != null && !ActivePatch.Finished ? ActivePatch.Id : 0);
            weeds.UpdateMarkers(ActivePatch != null && !ActivePatch.Finished ? ActivePatch.Id : 0, Overlay.IsHighlighting);

            LawnArea? area = YardBuilder.AreaAt(player.transform.position);
            if (area.HasValue)
                CurrentArea = area.Value.Id;
            if (!IsNight)
                Apples.PickNear(player.transform.position);

            TrackEarnings();
            UpdatePattern();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && !ScenarioRunner.IsRunning && !IsNight)
            {
                if (keyboard.f8Key.wasPressedThisFrame && ActivePatch != null)
                    CompletePatch(ActivePatch.Id);
                if (keyboard.f5Key.wasPressedThisFrame && Save())
                    Hud.ShowBanner("Saved", LastSaveTime);
                if (keyboard.f9Key.wasPressedThisFrame)
                {
                    bool loaded = Load(out string error);
                    Hud.ShowBanner(loaded ? "Save loaded" : "Could not load", loaded ? string.Empty : error);
                }
            }

            Hud.Refresh(ActivePatch, CurrentArea);
            if (Job == null)
                Hud.SetDayInfo(Days.Day, Wallet.Points);
            else
                Hud.SetInfoLine($"{Job.Client}  <color=#B9B2A6>·</color>  time <color=#4B4741>{Jobs.FormatTime(PlaySeconds)}</color> / {Job.ParMinutes:0}:00");
            // Task target: the patch's name is written even if it is not discovered (so the player can find it)
            Hud.SetPattern(Pattern != null ? $"Today's pattern: {Pattern.Name}  ·  {Tasks.Get(Pattern.Patch)?.Name}" : null,
                Pattern?.Score ?? 0f, Pattern?.Goal ?? 0f, Pattern?.Reward ?? 0, Pattern?.Completed ?? false);
            PlaySeconds += Time.deltaTime;
            if (AutosaveEnabled && Time.deltaTime > 0f)
            {
                autosaveTimer += Time.deltaTime;
                if (autosaveTimer >= AutosaveSeconds)
                    Save();
            }
            UpdateStats();
        }

        /// <summary>Points for the mown and watered area (when the grass grows at night the counters drop; that gain does not count).</summary>
        void TrackEarnings()
        {
            float cellArea = lawn.Grid.CellSize * lawn.Grid.CellSize;
            int cut = lawn.TotalCutCells, watered = lawn.TotalWatered;
            if (cut > lastCut)
            {
                float m2 = (cut - lastCut) * cellArea;
                Earn(m2 * Rewards.PerSquareMeterMowed);
                Days.Today.MowedSquareMeters += m2;
            }
            if (watered > lastWatered)
            {
                float m2 = (watered - lastWatered) * cellArea;
                Earn(m2 * Rewards.PerSquareMeterWatered);
                Days.Today.WateredSquareMeters += m2;
            }
            lastCut = cut;
            lastWatered = watered;
        }

        void UpdatePattern()
        {
            if (Pattern == null || Pattern.Completed)
                return;
            patternTimer -= Time.deltaTime;
            if (patternTimer > 0f)
                return;
            patternTimer = PatternInterval;
            EvaluatePattern();
        }

        /// <summary>Sets the day's pattern (for scenarios).</summary>
        public void ForcePattern(PatternKind kind, int patch)
        {
            Pattern = new PatternTask(kind, patch, lawn);
            Overlay.SetPattern(Pattern);
        }

        /// <summary>Computes the pattern score right away (for scenarios too).</summary>
        public void EvaluatePattern()
        {
            if (Pattern == null || !Pattern.Evaluate(lawn))
                return;
            Earn(Pattern.Reward);
            Overlay.SetPattern(Pattern);
            Hud.ShowBanner("Pattern done!", $"{Pattern.Name}  ·  +{Pattern.Reward} points");
            Debug.Log($"[Opus] Pattern complete: {Pattern.Name} ({Pattern.Score * 100f:0}%)");
            PatternCompleted?.Invoke(Pattern);
        }

        void OnApplicationQuit()
        {
            if (AutosaveEnabled)
                Save();
        }

        /// <summary>The point the tool is working on, otherwise the feet; null on grassless ground (the previous patch stays).</summary>
        PatchProgress FindActivePatch()
        {
            Tool tool = player.Tools.Current;
            if (tool != null && tool.TryGetWorkPoint(out Vector3 point))
            {
                PatchProgress atTool = Tasks.Get(lawn.Layout.PatchAt(new Vector2(point.x, point.z)));
                if (atTool != null)
                    return atTool;
            }
            Vector3 feet = player.transform.position;
            return Tasks.Get(lawn.Layout.PatchAt(new Vector2(feet.x, feet.z)));
        }

        // ---------- End of day ----------

        /// <summary>Ends the day with the end-of-day screen (the game stops meanwhile).</summary>
        public void BeginEndDay()
        {
            if (!IsNight && Job == null)
                StartCoroutine(EndDayRoutine());
        }

        IEnumerator EndDayRoutine(string preface = "", bool startDark = false)
        {
            IsNight = true;
            NightStarted?.Invoke();
            Time.timeScale = 0f;
            DayStats stats = Days.Today;
            int day = Days.Day;
            Hud.ShowNight($"Day {day} is over", preface + Summary(stats, null));
            for (float t = startDark ? NightFadeIn : 0f; t < NightFadeIn; t += Time.unscaledDeltaTime)
            {
                Hud.NightAlpha = t / NightFadeIn;
                yield return null;
            }
            Hud.NightAlpha = 1f;

            NightReport report = EndDayNow();
            Hud.ShowNight($"Day {day} is over", preface + Summary(stats, report));
            for (float t = 0f; t < NightHold; t += Time.unscaledDeltaTime)
                yield return null;
            for (float t = 0f; t < NightFadeOut; t += Time.unscaledDeltaTime)
            {
                Hud.NightAlpha = 1f - t / NightFadeOut;
                yield return null;
            }
            Hud.NightAlpha = 0f;
            Time.timeScale = 1f;
            IsNight = false;
            Hud.ShowBanner($"Day {Days.Day}", Pattern != null ? $"Today's pattern: {Pattern.Name}" : string.Empty);
            DayStarted?.Invoke(report);
            if (AutosaveEnabled || startDark)
                Save();
        }

        static string Summary(DayStats stats, NightReport? report)
        {
            System.Globalization.CultureInfo culture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
            string text = $"Mowed {stats.MowedSquareMeters.ToString("N0", culture)} m²  ·  Watered {stats.WateredSquareMeters.ToString("N0", culture)} m²\n" +
                          $"Weeds pulled {stats.WeedsRemoved}  ·  Patches finished {stats.PatchesFinished}  ·  Items found {stats.Finds}" +
                          (stats.Apples > 0 ? $"  ·  Apples collected {stats.Apples}\n" : "\n") +
                          $"Points earned {Hud.FormatPoints(Mathf.FloorToInt(stats.Points))}";
            if (report.HasValue)
            {
                NightReport r = report.Value;
                text += "\n\n" + (r.Rain ? "It rained overnight: the dry spots got watered by themselves." : "The grass grew overnight and the dry spots dried out again.");
                if (r.WeedsBack > 0)
                    text += $"\n{r.WeedsBack} weeds came back.";
                text += $"\nDay {r.FinishedDay + 1}: {r.PatchesReopened} patches need upkeep.";
            }
            return text;
        }

        /// <summary>Applies the night right away (without the screen; used by scenarios and the end-of-day screen).</summary>
        public NightReport EndDayNow()
        {
            NightReport report = Days.EndDay(lawn, weeds, Tasks);
            Overlay.ResetEffects();
            StartDay();
            Debug.Log($"[Opus] Day {report.FinishedDay} over: {(report.Rain ? "rain" : "dry night")}, " +
                      $"{report.RegrownCells} cells grew, {report.WeedsBack} weeds came back, {report.PatchesReopened} patches reopened.");
            return report;
        }

        // ---------- Save ----------

        /// <summary>Gathers the session's persistent state.</summary>
        public SaveData Capture()
        {
            var discovered = new List<int>();
            var finished = new List<int>();
            var ever = new List<int>();
            foreach (PatchProgress p in Tasks.Patches)
            {
                if (p.Discovered) discovered.Add(p.Id);
                if (p.Finished) finished.Add(p.Id);
                if (p.EverFinished) ever.Add(p.Id);
            }
            var unlocked = new List<int>(Tasks.UnlockedAreas);
            unlocked.Sort();
            Tool current = player.Tools.Current;
            var hose = player.Tools.FindSlot(Tool.HoseSlot) as HoseTool;
            var data = new SaveData
            {
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                playSeconds = PlaySeconds,
                gridWidth = lawn.Grid.Width,
                gridHeight = lawn.Grid.Height,
                patchCount = lawn.Layout.PatchCount,
                layoutHash = lawn.LayoutHash(),
                playerFeet = player.transform.position,
                playerYaw = player.Yaw,
                playerPitch = player.Pitch,
                toolSlot = current != null ? current.Slot : Tool.MowerSlot,
                sprayMode = hose != null ? hose.ModeIndex : 0,
                unlockedAreas = unlocked.ToArray(),
                discoveredPatches = discovered.ToArray(),
                finishedPatches = finished.ToArray(),
                everFinishedPatches = ever.ToArray(),
                weedHealth = weeds.CaptureHealth(),
                lawn = SaveSystem.Pack(lawn.CaptureSnapshot()),
                day = Days.Day,
                points = Wallet.Points,
                upgrades = Upgrades.Capture(),
                collected = Finds.CaptureCollected(),
                hiddenFinds = Finds.CaptureActive(),
                pattern = Pattern != null ? (int)Pattern.Kind + 1 : 0,
                patternPatch = Pattern?.Patch ?? 0,
                patternCompleted = Pattern?.Completed ?? false,
                apples = Apples.Capture(),
                applesDay = Days.Day,
                jobRecords = Job == null ? Career.CaptureRecords() : System.Array.Empty<JobRecord>(),
            };
            SaveMigration.Stamp(data, YardBuilder.Definition, lawn.Grid);
            return data;
        }

        /// <summary>
        /// Restores the state. Everything is validated first (garden size, patch map, lawn and weed data);
        /// with a mismatching save nothing changes. U8 saves (version 1) are read too: day 1, no points or upgrades.
        /// A version 3 save belonging to another version of the same garden is migrated area by area (<see cref="SaveMigration"/>).
        /// </summary>
        public bool Restore(SaveData data, out string error)
        {
            error = null;
            if (data == null)
            {
                error = "The save is empty.";
                return false;
            }
            if (data.gridWidth != lawn.Grid.Width || data.gridHeight != lawn.Grid.Height || data.patchCount != lawn.Layout.PatchCount
                || data.layoutHash != lawn.LayoutHash())
            {
                // Pre-U13 saves are unstamped; they belong to the first version of the home garden (areas 1 and 2)
                if (YardBuilder.Definition.Id == HomeYard.Id && !SaveMigration.StampLegacy(data, HomeYard.FirstVersion()))
                {
                    error = "The save belongs to an unknown version of this garden.";
                    return false;
                }
                if (!SaveMigration.TryMigrate(data, initialState, out SaveData migrated, out error))
                    return false;
                Debug.Log($"[Opus] Save migrated from version {data.yardVersion} of the garden to version {migrated.yardVersion} " +
                          $"(kept areas: {string.Join(", ", SaveMigration.KeptAreas(data, initialState))}).");
                data = migrated;
            }
            byte[] snapshot = SaveSystem.Unpack(data.lawn);
            int cells = lawn.Grid.CellCount;
            if (snapshot == null || (snapshot.Length != cells * LawnState.SnapshotBytesPerCell && snapshot.Length != cells * LawnState.LegacySnapshotBytesPerCell))
            {
                error = "The lawn data in the save is corrupt.";
                return false;
            }
            if (data.weedHealth == null || data.weedHealth.Length != weeds.States.Count)
            {
                error = "The weed data in the save does not match.";
                return false;
            }

            lawn.RestoreSnapshot(snapshot);
            weeds.RestoreHealth(data.weedHealth);
            var discovered = new HashSet<int>(data.discoveredPatches ?? Array.Empty<int>());
            var finished = new HashSet<int>(data.finishedPatches ?? Array.Empty<int>());
            var ever = new HashSet<int>(data.everFinishedPatches ?? Array.Empty<int>());
            Tasks.RestoreState(data.unlockedAreas, discovered.Contains, finished.Contains, id => ever.Contains(id) || finished.Contains(id));

            Days.Restore(data.day);
            Wallet.Restore(data.points);
            if (Job == null)
                Career.LoadRecords(data.jobRecords);
            Upgrades.Restore(data.upgrades);
            Finds.Restore(data.collected, data.hiddenFinds);
            if (data.pattern > 0 && Tasks.Get(data.patternPatch) != null)
                Pattern = new PatternTask((PatternKind)(data.pattern - 1), data.patternPatch, lawn, data.patternCompleted);
            else
                Pattern = PatternTask.ForDay(Days.Day, lawn, Tasks);
            Overlay.SetPattern(Pattern);
            if (data.version < 2 || (data.collected == null && data.hiddenFinds == null))
                Finds.HideForDay(Days.Day, PatchUnlocked);
            // If the apples are from the day they were saved, they stay in place; otherwise (an old save) the day's apples drop again
            if (data.applesDay == Days.Day && data.apples != null)
                Apples.Restore(data.apples);
            else
                Apples.SpawnForDay(Days.Day, Tasks.IsAreaUnlocked);

            Flowers.Clear();
            bloomed.Clear();
            foreach (PatchProgress p in Tasks.Patches)
            {
                if (!p.EverFinished) continue;
                Flowers.BloomInstant(lawn, p);
                bloomed.Add(p.Id);
            }
            foreach (Gate gate in YardBuilder.Gates)
                gate.SetOpenInstant(Tasks.IsAreaUnlocked(gate.Def.To));

            player.Teleport(data.playerFeet, data.playerYaw, data.playerPitch);
            player.Tools.EquipImmediate(data.toolSlot, player);
            if (player.Tools.FindSlot(Tool.HoseSlot) is HoseTool hose)
                hose.SetMode(data.sprayMode);

            PlaySeconds = data.playSeconds;
            ActivePatch = null;
            Overlay.ResetEffects();
            Hud.ResetTransient();
            LawnArea? at = YardBuilder.AreaAt(data.playerFeet);
            CurrentArea = at.HasValue ? at.Value.Id : LawnTasks.FirstArea;
            lastCut = lawn.TotalCutCells;
            lastWatered = lawn.TotalWatered;
            autosaveTimer = 0f;
            return true;
        }

        /// <summary>Writes the save file; false on a disk error (the game goes on).</summary>
        public bool Save()
        {
            autosaveTimer = 0f;
            try
            {
                SaveSystem.Write(Capture());
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Opus] Could not save: {exception.Message}");
                return false;
            }
            LastSaveTime = DateTime.Now.ToString("HH:mm");
            SaveCount++;
            Debug.Log($"[Opus] Saved: {SaveSystem.FilePath}");
            return true;
        }

        public bool Load(out string error)
        {
            if (!SaveSystem.TryRead(out SaveData data, out error))
                return false;
            if (!Restore(data, out error))
            {
                Debug.LogWarning($"[Opus] Could not load the save: {error}");
                return false;
            }
            Debug.Log($"[Opus] Save loaded ({data.savedAt}).");
            return true;
        }

        /// <summary>Deletes the save and returns the garden to its starting state.</summary>
        public void NewGame()
        {
            SaveSystem.Delete();
            foreach (JobDef job in Jobs.All)
                SaveSystem.DeleteSlot(job.Id);
            Career.ResetProgress();
            Restore(initialState, out _);
            Career.ResetProgress();
            PlaySeconds = 0f;
            LastSaveTime = string.Empty;
            Debug.Log("[Opus] New game.");
        }

        void UpdateStats()
        {
            statsFrames++;
            statsTimer += Time.unscaledDeltaTime;
            if (statsTimer < StatsInterval)
                return;
            float fps = statsFrames / statsTimer;
            Hud.ShowDevStats($"{fps:0} fps · {grass.DrawnTufts / 1000f:0.#}k tufts · mowed {lawn.CutFraction * 100f:0.0}% · " +
                             $"watered {lawn.WaterFraction * 100f:0}% · weeds {weeds.RemainingWeeds}/{weeds.TotalWeeds} · " +
                             $"ivy {weeds.RemainingOvergrowth}/{weeds.TotalOvergrowth} · flowers {Flowers.Count}");
            statsTimer = 0f;
            statsFrames = 0;
        }
    }
}
