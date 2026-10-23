using System;
using System.Collections.Generic;
using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>A patch's tasks and status. The numbers are updated by <see cref="LawnTasks.Refresh"/>.</summary>
    public sealed class PatchProgress
    {
        public int Id;
        /// <summary>The area the patch is in (LawnArea.Id).</summary>
        public int Area;
        public string Name;
        public Color Color;
        /// <summary>Average of the patch's cell centres (for effects and the dashed line).</summary>
        public Vector2 Centroid;
        /// <summary>Distance from the centre to the farthest cell (m).</summary>
        public float Radius;
        /// <summary>Approximate border length (m).</summary>
        public float Perimeter;
        public float AreaSquareMeters;

        public bool Discovered;
        /// <summary>All tasks are done right now (reopened by grass growing overnight, a spot drying out or weeds coming back).</summary>
        public bool Finished;
        /// <summary>Finished at least once (unlocking areas and flowers depend on this).</summary>
        public bool EverFinished;
        /// <summary>How many times it was finished in this session.</summary>
        public int Completions;
        public float MowFraction;
        public bool HasWater;
        public float WaterFraction;
        public int WeedsTotal;
        public int WeedsLeft;

        public bool MowDone => MowFraction >= LawnTasks.MowTarget;
        public bool WaterDone => !HasWater || WaterFraction >= LawnTasks.WaterTarget;
        public bool WeedsDone => WeedsLeft == 0;
        public bool AllDone => MowDone && WaterDone && WeedsDone;
        /// <summary>
        /// Whether real work was done: at least 1 m² mown (a standing mower still mows the circle under its blade, which does not count),
        /// 5% of the watering or one weed.
        /// </summary>
        public bool AnyProgress => MowFraction * AreaSquareMeters >= LawnTasks.DiscoverMowedSquareMeters
                                   || (HasWater && WaterFraction > 0.05f) || WeedsLeft < WeedsTotal;

        /// <summary>The name shown in the interface: "???" until discovered.</summary>
        public string DisplayName => Discovered ? Name : "???";
    }

    /// <summary>
    /// The garden's patch-by-patch task list. Every patch: mow (at least 98%), pull the weeds (if any), water (if it has a dry
    /// area, at least 95%). The patch's name is revealed at the first progress; when all its tasks are finished it becomes "done" and
    /// never goes back. When all patches of an area are finished, the area is complete. Patches of locked areas
    /// (even if watered over the fence) are not discovered and do not finish; an area is unlocked with <see cref="UnlockArea"/>.
    /// </summary>
    public sealed class LawnTasks
    {
        public const float MowTarget = 0.98f;
        public const float WaterTarget = 0.95f;
        /// <summary>A patch with fewer cells to water than this (spills over a spot's edge) has no watering task.</summary>
        public const int MinWaterCells = 40;
        /// <summary>The patch's name is revealed once this much area is mown (m²).</summary>
        public const float DiscoverMowedSquareMeters = 1f;
        public const int FirstArea = 1;

        readonly LawnState lawn;
        readonly Func<int, int> weedsLeftInPatch;
        readonly List<PatchProgress> patches = new List<PatchProgress>();
        readonly List<int> areaIds = new List<int>();
        readonly HashSet<int> unlocked = new HashSet<int> { FirstArea };
        readonly HashSet<int> completedAreas = new HashSet<int>();

        public IReadOnlyList<PatchProgress> Patches => patches;
        public int Total => patches.Count;
        public int FinishedCount { get; private set; }
        public bool AllFinished => FinishedCount == patches.Count;

        /// <summary>Are all patches of the unlocked areas finished right now?</summary>
        public bool AllUnlockedFinished
        {
            get
            {
                foreach (PatchProgress p in patches)
                    if (unlocked.Contains(p.Area) && !p.Finished) return false;
                return true;
            }
        }

        public bool IsAreaCompleted(int area) => completedAreas.Contains(area);
        /// <summary>Areas that have patches, in ascending order.</summary>
        public IReadOnlyList<int> AreaIds => areaIds;

        public event Action<PatchProgress> PatchDiscovered;
        public event Action<PatchProgress> PatchFinished;
        /// <summary>A finished patch needs work again (grass grown overnight etc.).</summary>
        public event Action<PatchProgress> PatchReopened;
        /// <summary>All patches of the unlocked areas are finished right now (the day's work is done).</summary>
        public event Action AllUnlockedDone;
        /// <summary>The last patch of an area was finished (area id).</summary>
        public event Action<int> AreaCompleted;

        /// <param name="weedsTotalInPatch">Total number of weeds/ivy in the patch.</param>
        /// <param name="weedsLeftInPatch">Number of weeds/ivy left in the patch (null = no weeds).</param>
        public LawnTasks(LawnState lawn, IReadOnlyList<PatchInfo> infos, Func<int, int> weedsTotalInPatch = null, Func<int, int> weedsLeftInPatch = null)
        {
            this.lawn = lawn;
            this.weedsLeftInPatch = weedsLeftInPatch;
            PatchLayout layout = lawn.Layout;
            for (int id = 1; id <= layout.PatchCount; id++)
            {
                PatchInfo info = id - 1 < infos.Count ? infos[id - 1] : new PatchInfo($"Patch {id}", "#9AA543");
                int area = layout.AreaOfPatch(id);
                patches.Add(new PatchProgress
                {
                    Id = id,
                    Area = area,
                    Name = info.Name,
                    Color = info.Color,
                    HasWater = lawn.PatchNeedsWaterCount(id) >= MinWaterCells,
                    WeedsTotal = weedsTotalInPatch?.Invoke(id) ?? 0,
                });
                if (!areaIds.Contains(area))
                    areaIds.Add(area);
            }
            areaIds.Sort();
            ComputeGeometry();
            Refresh();
        }

        public PatchProgress Get(int id) => id >= 1 && id <= patches.Count ? patches[id - 1] : null;

        public int TotalIn(int area)
        {
            int count = 0;
            foreach (PatchProgress p in patches)
                if (p.Area == area) count++;
            return count;
        }

        public int FinishedIn(int area)
        {
            int count = 0;
            foreach (PatchProgress p in patches)
                if (p.Area == area && p.Finished) count++;
            return count;
        }

        public bool IsAreaFinished(int area) => TotalIn(area) > 0 && FinishedIn(area) == TotalIn(area);
        public bool IsAreaUnlocked(int area) => unlocked.Contains(area);

        /// <summary>
        /// Areas unlocked when an area is finished (the gate chain in the garden definition). If not set, every area unlocks the next id.
        /// </summary>
        public Func<int, IReadOnlyList<int>> AreasOpenedBy { get; set; }

        /// <summary>Areas unlocked when the area is finished (empty if none).</summary>
        public IReadOnlyList<int> OpenedBy(int area)
        {
            if (AreasOpenedBy != null)
                return AreasOpenedBy(area);
            foreach (int id in areaIds)
                if (id > area) return new[] { id };
            return Array.Empty<int>();
        }

        /// <summary>The first area unlocked when the area is finished (0 if none).</summary>
        public int NextArea(int area)
        {
            IReadOnlyList<int> opened = OpenedBy(area);
            return opened.Count > 0 ? opened[0] : 0;
        }

        /// <summary>Makes the area's patches workable (the gate opened).</summary>
        public void UnlockArea(int area)
        {
            if (unlocked.Add(area))
                Refresh();
        }

        /// <summary>Restoring from a save: writes the flags without raising events (the numbers update on the next Refresh).</summary>
        public void RestoreState(IEnumerable<int> unlockedAreas, Func<int, bool> discovered, Func<int, bool> finished, Func<int, bool> everFinished = null)
        {
            unlocked.Clear();
            unlocked.Add(FirstArea);
            if (unlockedAreas != null)
                foreach (int area in unlockedAreas) unlocked.Add(area);
            FinishedCount = 0;
            foreach (PatchProgress p in patches)
            {
                p.Finished = finished(p.Id);
                p.EverFinished = p.Finished || (everFinished != null ? everFinished(p.Id) : p.Finished);
                p.Discovered = p.EverFinished || discovered(p.Id);
                p.Completions = 0;
                if (p.Finished) FinishedCount++;
            }
            // An area counts as complete if all its patches were finished at least once (the gate stays open even if the grass grows back)
            completedAreas.Clear();
            foreach (int area in areaIds)
            {
                bool all = true;
                foreach (PatchProgress p in patches)
                    if (p.Area == area && !p.EverFinished) all = false;
                if (all) completedAreas.Add(area);
            }
            Refresh();
        }

        public IEnumerable<int> UnlockedAreas => unlocked;

        /// <summary>Updates the numbers; raises events on new discoveries and on patch and area finishes.</summary>
        public void Refresh()
        {
            foreach (PatchProgress p in patches)
            {
                p.MowFraction = lawn.PatchCutFraction(p.Id);
                p.WaterFraction = lawn.PatchWaterFraction(p.Id);
                p.WeedsLeft = weedsLeftInPatch?.Invoke(p.Id) ?? 0;
                if (!unlocked.Contains(p.Area))
                    continue;

                if (!p.Discovered && p.AnyProgress)
                {
                    p.Discovered = true;
                    PatchDiscovered?.Invoke(p);
                }
                if (!p.Finished && p.AllDone)
                {
                    p.Finished = true;
                    p.EverFinished = true;
                    p.Discovered = true;
                    p.Completions++;
                    FinishedCount++;
                    PatchFinished?.Invoke(p);
                    if (IsAreaFinished(p.Area) && completedAreas.Add(p.Area))
                        AreaCompleted?.Invoke(p.Area);
                    if (AllUnlockedFinished)
                        AllUnlockedDone?.Invoke();
                }
                else if (p.Finished && !p.AllDone)
                {
                    p.Finished = false;
                    FinishedCount--;
                    PatchReopened?.Invoke(p);
                }
            }
        }

        void ComputeGeometry()
        {
            LawnGrid grid = lawn.Grid;
            byte[] ids = lawn.Layout.PatchIds;
            var sums = new Vector2[patches.Count + 1];
            var counts = new int[patches.Count + 1];
            var edges = new int[patches.Count + 1];
            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int id = ids[grid.Index(x, z)];
                    if (id == PatchLayout.NoGrass) continue;
                    sums[id] += grid.CellCenter(x, z);
                    counts[id]++;
                    if (IsBoundary(grid, ids, x, z, id))
                        edges[id]++;
                }
            }
            foreach (PatchProgress p in patches)
            {
                p.Centroid = counts[p.Id] > 0 ? sums[p.Id] / counts[p.Id] : Vector2.zero;
                p.AreaSquareMeters = counts[p.Id] * grid.CellSize * grid.CellSize;
                p.Perimeter = edges[p.Id] * grid.CellSize * 1.08f; // approximate correction from the stepped border to the curve length
            }
            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int id = ids[grid.Index(x, z)];
                    if (id == PatchLayout.NoGrass) continue;
                    PatchProgress p = patches[id - 1];
                    p.Radius = Mathf.Max(p.Radius, Vector2.Distance(grid.CellCenter(x, z), p.Centroid));
                }
            }
        }

        /// <summary>Is the cell on the patch border (one of its 4 neighbours is another patch, grassless or outside the grid)?</summary>
        public static bool IsBoundary(LawnGrid grid, byte[] ids, int x, int z, int id)
        {
            return x == 0 || z == 0 || x == grid.Width - 1 || z == grid.Height - 1
                || ids[grid.Index(x - 1, z)] != id || ids[grid.Index(x + 1, z)] != id
                || ids[grid.Index(x, z - 1)] != id || ids[grid.Index(x, z + 1)] != id;
        }
    }
}
