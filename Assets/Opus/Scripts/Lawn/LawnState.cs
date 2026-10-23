using System.Collections.Generic;
using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>
    /// The live state of the lawn (master data on the CPU + GPU textures). Per cell (RGBA8):
    /// r = visible height (255 long, 0 mown), g = dryness, b/a = last mowing direction (x/z, 128 = none).
    /// Wetness in a separate R8 texture (1 when watered, fades over time; looks only).
    /// The logical cut happens instantly (counters, direction); the visible height drops smoothly within a short time.
    /// Shaders read it as global textures: _OpusLawnState, _OpusLawnWet, _OpusPatchMap, _OpusPatchPalette.
    /// </summary>
    public sealed class LawnState
    {
        public const int PaletteWidth = 256;
        public const int PaletteRows = 4; // 0: base (+height multiplier in a), 1: middle, 2: tip, 3: mown
        public const byte Tall = 255;
        public const byte Cut = 0;
        /// <summary>
        /// A cell whose target height is up to this value (0..255) counts as mown. Grass that grows overnight must be mown
        /// again once it passes the threshold; below it the grass looks a little grown but its job counts as done.
        /// </summary>
        public const byte CutThreshold = 64;
        /// <summary>Time for a cut to complete in the visible height (s).</summary>
        public const float CutAnimationSeconds = 0.14f;
        /// <summary>A cell at this dryness (0..255) or above at the start must be watered.</summary>
        public const byte NeedsWaterDryness = 128;
        /// <summary>A cell counts as watered once its dryness drops to this value (0..1).</summary>
        public const float WateredDryness = 0.15f;
        /// <summary>Time for the wet look to fade completely (s).</summary>
        public const float WetFadeSeconds = 9f;
        /// <summary>Inner fraction of the watering circle with the full effect; outside it the effect falls off towards the edge.</summary>
        public const float WaterCore = 0.55f;
        /// <summary>How far a watered dry spot turns fresh green (same as _OpusGreening in the shader).</summary>
        public const float GreeningStrength = 0.8f;

        static readonly int StateId = Shader.PropertyToID("_OpusLawnState");
        static readonly int WetId = Shader.PropertyToID("_OpusLawnWet");
        static readonly int PatchMapId = Shader.PropertyToID("_OpusPatchMap");
        static readonly int PaletteId = Shader.PropertyToID("_OpusPatchPalette");
        static readonly int RectId = Shader.PropertyToID("_OpusLawnRect");
        static readonly int CellsId = Shader.PropertyToID("_OpusLawnCells");
        static readonly int DryBaseId = Shader.PropertyToID("_OpusDryBase");
        static readonly int DryMidId = Shader.PropertyToID("_OpusDryMid");
        static readonly int DryTipId = Shader.PropertyToID("_OpusDryTip");
        static readonly int FreshBaseId = Shader.PropertyToID("_OpusFreshBase");
        static readonly int FreshMidId = Shader.PropertyToID("_OpusFreshMid");
        static readonly int FreshTipId = Shader.PropertyToID("_OpusFreshTip");
        static readonly int GreeningId = Shader.PropertyToID("_OpusGreening");

        readonly Color32[] cells;
        readonly byte[] target;
        readonly List<int> animating = new List<int>();
        readonly bool[] isAnimating;
        readonly IReadOnlyList<GrassVariety> varieties;
        readonly Color[] patchMid;
        readonly Color[] patchTip;
        // Dryness is kept as a float: at high frame rates the per-frame drop stays below one byte.
        readonly float[] dryness;
        readonly float[] wetness;
        // Wetness texture RG8: r = wetness (fades), g = greening (grows permanently as a dry cell is watered)
        readonly byte[] wetBytes;
        readonly bool[] isWet;
        readonly List<int> wetCells = new List<int>();
        int[] patchCells;
        int[] patchCut;
        int[] patchNeedsWater;
        int[] patchWatered;
        bool stateDirty = true;
        bool wetDirty = true;
        bool patchesDirty = true;

        public LawnGrid Grid => Layout.Grid;
        public PatchLayout Layout { get; }

        public Texture2D StateTexture { get; }
        public Texture2D WetTexture { get; }
        public Texture2D PatchTexture { get; }
        public Texture2D PaletteTexture { get; }

        public int TotalGrassCells { get; private set; }
        public int TotalCutCells { get; private set; }
        public float CutFraction => TotalGrassCells == 0 ? 0f : (float)TotalCutCells / TotalGrassCells;
        public int TotalNeedsWater { get; private set; }
        public int TotalWatered { get; private set; }
        public float WaterFraction => TotalNeedsWater == 0 ? 1f : (float)TotalWatered / TotalNeedsWater;

        public LawnState(PatchLayout layout, IReadOnlyList<GrassVariety> varieties)
        {
            Layout = layout;
            this.varieties = varieties;
            LawnGrid grid = layout.Grid;
            cells = new Color32[grid.CellCount];
            target = new byte[grid.CellCount];
            isAnimating = new bool[grid.CellCount];
            dryness = new float[grid.CellCount];
            wetness = new float[grid.CellCount];
            wetBytes = new byte[grid.CellCount * 2];
            isWet = new bool[grid.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                bool grass = layout.PatchIds[i] != PatchLayout.NoGrass;
                target[i] = grass ? Tall : Cut;
                dryness[i] = layout.Dryness[i] / 255f;
                cells[i] = new Color32(target[i], layout.Dryness[i], 128, 128);
            }

            StateTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false, true)
            {
                name = "LawnState", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            WetTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.RG16, false, true)
            {
                name = "Wetness", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            PatchTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.R8, false, true)
            {
                name = "PatchMap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point,
            };
            PaletteTexture = new Texture2D(PaletteWidth, PaletteRows, TextureFormat.RGBA32, false, false)
            {
                name = "PatchPalette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point,
            };
            patchMid = new Color[PaletteWidth];
            patchTip = new Color[PaletteWidth];
            BuildPalette();
            RecountPatches();
        }

        public Color32 CellAt(int index) => cells[index];
        public bool IsGrass(int index) => Layout.PatchIds[index] != PatchLayout.NoGrass;
        public bool IsCut(int index) => IsGrass(index) && target[index] <= CutThreshold;
        /// <summary>Target height of the cell (0 mown .. 255 long).</summary>
        public byte HeightAt(int index) => target[index];
        public int PatchCellCount(int patch) => patchCells[patch];
        public int PatchCutCount(int patch) => patchCut[patch];

        public float PatchCutFraction(int patch) =>
            patchCells[patch] == 0 ? 0f : (float)patchCut[patch] / patchCells[patch];

        /// <summary>Live dryness of the cell (0 watered .. 1 bone dry).</summary>
        public float DrynessAt(int index) => dryness[index];
        public float WetnessAt(int index) => wetness[index];
        /// <summary>How far a dry cell has turned fresh green by watering (0..1, permanent).</summary>
        public float GreeningAt(int index) => wetBytes[index * 2 + 1] / 255f;
        /// <summary>Is this a cell that was dry at the start and must be watered?</summary>
        public bool NeedsWater(int index) => IsGrass(index) && Layout.Dryness[index] >= NeedsWaterDryness;
        public bool IsWatered(int index) => NeedsWater(index) && dryness[index] <= WateredDryness;
        public int PatchNeedsWaterCount(int patch) => patchNeedsWater[patch];
        public int PatchWateredCount(int patch) => patchWatered[patch];

        public float PatchWaterFraction(int patch) =>
            patchNeedsWater[patch] == 0 ? 1f : (float)patchWatered[patch] / patchNeedsWater[patch];

        /// <summary>
        /// Waters the grass in a circle: dryness drops by <paramref name="amount"/> (0..1 scale) at the centre and
        /// smoothly less towards the edge outside <see cref="WaterCore"/>. The effect is linear in time, so the result is the
        /// same however many frames the same total amount is split over. Height (mown state) does not change.
        /// </summary>
        /// <returns>Number of cells counted as watered (crossing the threshold) in this call.</returns>
        public int Water(Vector2 center, float radius, float amount, List<int> newlyWatered = null)
        {
            if (amount <= 0f || radius <= 0f)
                return 0;
            if (!Grid.TryGetCellRange(center - Vector2.one * radius, center + Vector2.one * radius, out Vector2Int lo, out Vector2Int hi))
                return 0;

            int count = 0;
            float radiusSq = radius * radius;
            for (int z = lo.y; z <= hi.y; z++)
            {
                for (int x = lo.x; x <= hi.x; x++)
                {
                    Vector2 c = Grid.CellCenter(x, z);
                    float distanceSq = (c - center).sqrMagnitude;
                    if (distanceSq >= radiusSq)
                        continue;
                    int index = Grid.Index(x, z);
                    if (!IsGrass(index))
                        continue;

                    float falloff = WaterFalloff(Mathf.Sqrt(distanceSq) / radius);
                    if (falloff <= 0f)
                        continue;

                    bool wasWatered = IsWatered(index);
                    float before = dryness[index];
                    if (before > 0f)
                    {
                        dryness[index] = Mathf.Max(0f, before - amount * falloff);
                        byte g = (byte)Mathf.RoundToInt(dryness[index] * 255f);
                        if (g != cells[index].g)
                        {
                            cells[index].g = g;
                            stateDirty = true;
                        }
                        // Greening: a cell that was dry at the start turns fresh green as it is watered (permanent)
                        float initial = Layout.Dryness[index] / 255f;
                        if (initial > 0f)
                        {
                            byte green = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - dryness[index] / initial) * 255f);
                            if (green != wetBytes[index * 2 + 1])
                            {
                                wetBytes[index * 2 + 1] = green;
                                wetDirty = true;
                            }
                        }
                        if (!wasWatered && IsWatered(index))
                        {
                            patchWatered[Layout.PatchIds[index]]++;
                            TotalWatered++;
                            newlyWatered?.Add(index);
                            count++;
                        }
                    }

                    if (falloff > wetness[index])
                    {
                        wetness[index] = falloff;
                        wetBytes[index * 2] = (byte)Mathf.RoundToInt(falloff * 255f);
                        wetDirty = true;
                        if (!isWet[index])
                        {
                            isWet[index] = true;
                            wetCells.Add(index);
                        }
                    }
                }
            }
            return count;
        }

        /// <summary>Fraction of the watering effect by distance (t = distance / radius).</summary>
        public static float WaterFalloff(float t)
        {
            if (t >= 1f) return 0f;
            if (t <= WaterCore) return 1f;
            float k = (t - WaterCore) / (1f - WaterCore);
            return 1f - k * k * (3f - 2f * k);
        }

        /// <summary>
        /// Clears the grass from the cells under obstacles (shed, veranda, flowerbed, tree trunk…).
        /// The ground itself (top y ≤ 0) and objects high above the ground (roof etc.) do not count.
        /// </summary>
        public int ClearUnderColliders(IEnumerable<Collider> colliders)
        {
            Physics.SyncTransforms();
            int cleared = 0;
            foreach (Collider collider in colliders)
            {
                if (collider == null || collider.isTrigger || !collider.enabled)
                    continue;
                Bounds bounds = collider.bounds;
                if (bounds.max.y <= 0.01f || bounds.min.y > 0.3f)
                    continue;
                if (!Grid.TryGetCellRange(new Vector2(bounds.min.x, bounds.min.z), new Vector2(bounds.max.x, bounds.max.z), out Vector2Int from, out Vector2Int to))
                    continue;

                for (int z = from.y; z <= to.y; z++)
                {
                    for (int x = from.x; x <= to.x; x++)
                    {
                        int index = Grid.Index(x, z);
                        if (!IsGrass(index))
                            continue;
                        Vector2 c = Grid.CellCenter(x, z);
                        var point = new Vector3(c.x, Mathf.Clamp(0.1f, bounds.min.y + 0.01f, bounds.max.y - 0.01f), c.y);
                        if ((collider.ClosestPoint(point) - point).sqrMagnitude < 1e-6f)
                        {
                            Layout.ClearCell(index);
                            dryness[index] = 0f;
                            target[index] = Cut;
                            cells[index] = new Color32(Cut, 0, 128, 128);
                            cleared++;
                        }
                    }
                }
            }
            RecountPatches();
            patchesDirty = stateDirty = true;
            return cleared;
        }

        /// <summary>
        /// Mows the cells swept by the blade circle from <paramref name="from"/> to <paramref name="to"/>.
        /// Newly mown cells are added to <paramref name="newlyCut"/> (for clippings). While the mower moves
        /// (<paramref name="updateDirection"/>) the direction of already mown cells is updated too: a pass in the opposite
        /// direction flips the stripe's tone.
        /// </summary>
        /// <returns>Number of newly mown cells.</returns>
        public int CutSwept(Vector2 from, Vector2 to, float radius, Vector2 direction, bool updateDirection, List<int> newlyCut = null)
        {
            Vector2 min = Vector2.Min(from, to) - Vector2.one * radius;
            Vector2 max = Vector2.Max(from, to) + Vector2.one * radius;
            if (!Grid.TryGetCellRange(min, max, out Vector2Int lo, out Vector2Int hi))
                return 0;

            float radiusSq = radius * radius;
            byte dirX = 128, dirZ = 128;
            if (updateDirection && direction.sqrMagnitude > 1e-6f)
            {
                direction.Normalize();
                dirX = (byte)Mathf.RoundToInt((direction.x * 0.5f + 0.5f) * 255f);
                dirZ = (byte)Mathf.RoundToInt((direction.y * 0.5f + 0.5f) * 255f);
            }
            else
            {
                updateDirection = false;
            }

            int count = 0;
            for (int z = lo.y; z <= hi.y; z++)
            {
                for (int x = lo.x; x <= hi.x; x++)
                {
                    Vector2 c = Grid.CellCenter(x, z);
                    // Strict inequality: cell centres exactly on the boundary are excluded (so the stripe width does not drift on aligned paths).
                    if (DistanceToSegmentSq(c, from, to) >= radiusSq)
                        continue;
                    int index = Grid.Index(x, z);
                    if (!IsGrass(index))
                        continue;

                    if (target[index] != Cut)
                    {
                        // Grass that grew a little overnight but still counts as "mown" gets shorter too; only cells past the threshold go into the counters
                        bool wasUncut = target[index] > CutThreshold;
                        target[index] = Cut;
                        if (!isAnimating[index])
                        {
                            isAnimating[index] = true;
                            animating.Add(index);
                        }
                        if (wasUncut)
                        {
                            patchCut[Layout.PatchIds[index]]++;
                            TotalCutCells++;
                            newlyCut?.Add(index);
                            count++;
                        }
                        if (!updateDirection)
                            continue;
                    }
                    if (updateDirection)
                    {
                        cells[index].b = dirX;
                        cells[index].a = dirZ;
                        stateDirty = true;
                    }
                }
            }
            return count;
        }

        /// <summary>Lowers the visible height smoothly towards the logical target, fades the wetness.</summary>
        public void Tick(float deltaTime)
        {
            TickWetness(deltaTime);
            if (animating.Count == 0)
                return;
            int step = Mathf.Max(1, Mathf.RoundToInt(255f * deltaTime / CutAnimationSeconds));
            for (int i = animating.Count - 1; i >= 0; i--)
            {
                int index = animating[i];
                int height = Mathf.Max(target[index], cells[index].r - step);
                cells[index].r = (byte)height;
                if (height == target[index])
                {
                    isAnimating[index] = false;
                    animating.RemoveAt(i);
                }
            }
            stateDirty = true;
        }

        void TickWetness(float deltaTime)
        {
            if (wetCells.Count == 0)
                return;
            float step = deltaTime / WetFadeSeconds;
            for (int i = wetCells.Count - 1; i >= 0; i--)
            {
                int index = wetCells[i];
                float value = Mathf.Max(0f, wetness[index] - step);
                wetness[index] = value;
                wetBytes[index * 2] = (byte)Mathf.RoundToInt(value * 255f);
                if (value <= 0f)
                {
                    isWet[index] = false;
                    // Order does not matter: swap with the last one and remove
                    wetCells[i] = wetCells[wetCells.Count - 1];
                    wetCells.RemoveAt(wetCells.Count - 1);
                }
            }
            wetDirty = true;
        }

        /// <summary>
        /// Finishes a patch without playing: all its cells are mown (the visible height still drops smoothly) and its dry
        /// cells watered. For scenarios and the developer shortcut; the game logic runs from the same counters.
        /// </summary>
        public void CompletePatch(int patch)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (Layout.PatchIds[i] != patch)
                    continue;
                if (target[i] != Cut)
                {
                    target[i] = Cut;
                    if (!isAnimating[i])
                    {
                        isAnimating[i] = true;
                        animating.Add(i);
                    }
                }
                if (dryness[i] > 0f)
                {
                    dryness[i] = 0f;
                    cells[i].g = 0;
                    if (Layout.Dryness[i] > 0)
                        wetBytes[i * 2 + 1] = 255;
                }
            }
            RecountPatches();
            stateDirty = wetDirty = true;
        }

        /// <summary>
        /// A night passes: mown grass grows by <paramref name="growth"/> (0..255 scale) times its patch's growth multiplier;
        /// a cell passing the threshold loses its stripe direction and has to be mown again. Cells that were dry at the start
        /// dry out again by <paramref name="drying"/> of their starting dryness, and part of the greening fades.
        /// On a rainy night the dry spots count as watered and in the morning the whole lawn looks wet.
        /// </summary>
        /// <param name="patchGrowth">Growth multiplier by patch id (null = all 1).</param>
        /// <returns>Number of cells that now need mowing again.</returns>
        public int GrowOvernight(float growth, IReadOnlyList<float> patchGrowth, float drying, bool rain = false, float greeningFade = 0.2f)
        {
            int regrown = 0;
            animating.Clear();
            wetCells.Clear();
            for (int i = 0; i < cells.Length; i++)
            {
                isAnimating[i] = false;
                isWet[i] = false;
                wetness[i] = 0f;
                wetBytes[i * 2] = 0;
                if (!IsGrass(i))
                    continue;

                int patch = Layout.PatchIds[i];
                if (target[i] < Tall)
                {
                    float multiplier = patchGrowth != null && patch < patchGrowth.Count ? patchGrowth[patch] : 1f;
                    bool wasCut = target[i] <= CutThreshold;
                    byte grown = (byte)Mathf.Min(Tall, target[i] + Mathf.RoundToInt(growth * multiplier));
                    target[i] = grown;
                    cells[i].r = grown;
                    if (wasCut && grown > CutThreshold)
                    {
                        regrown++;
                        cells[i].b = 128;
                        cells[i].a = 128;
                    }
                }

                float initial = Layout.Dryness[i] / 255f;
                if (initial > 0f)
                {
                    float dry = rain ? Mathf.Min(dryness[i], WateredDryness * 0.5f) : Mathf.Min(initial, dryness[i] + drying * initial);
                    dryness[i] = dry;
                    cells[i].g = (byte)Mathf.RoundToInt(dry * 255f);
                    byte green = wetBytes[i * 2 + 1];
                    wetBytes[i * 2 + 1] = rain
                        ? (byte)Mathf.Max(green, Mathf.RoundToInt(Mathf.Clamp01(1f - dry / initial) * 255f))
                        : (byte)Mathf.RoundToInt(green * (1f - greeningFade));
                }
                if (rain)
                {
                    wetness[i] = 0.7f;
                    wetBytes[i * 2] = (byte)Mathf.RoundToInt(0.7f * 255f);
                    isWet[i] = true;
                    wetCells.Add(i);
                }
            }
            RecountPatches();
            stateDirty = wetDirty = true;
            return regrown;
        }

        /// <summary>Returns the whole lawn to its starting state: long, at starting dryness, dry (for tests and trials).</summary>
        public void RegrowAll()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                wetness[i] = 0f;
                wetBytes[i * 2] = 0;
                wetBytes[i * 2 + 1] = 0;
                isWet[i] = false;
                if (!IsGrass(i))
                    continue;
                target[i] = Tall;
                dryness[i] = Layout.Dryness[i] / 255f;
                cells[i] = new Color32(Tall, Layout.Dryness[i], 128, 128);
                isAnimating[i] = false;
            }
            animating.Clear();
            wetCells.Clear();
            RecountPatches();
            stateDirty = wetDirty = true;
        }

        /// <summary>
        /// Bytes per cell in a save: flags (1 mown, 2 watered), dryness (ushort), mowing direction (2), greening,
        /// target height. Old (U8) saves had no height byte (6 bytes); they are read too.
        /// </summary>
        public const int SnapshotBytesPerCell = 7;
        public const int LegacySnapshotBytesPerCell = 6;

        /// <summary>Snapshot of the persistent state (transient things such as wetness and the cut animation excluded).</summary>
        public byte[] CaptureSnapshot()
        {
            var data = new byte[cells.Length * SnapshotBytesPerCell];
            for (int i = 0, o = 0; i < cells.Length; i++, o += SnapshotBytesPerCell)
            {
                byte flags = 0;
                if (target[i] <= CutThreshold) flags |= 1;
                if (IsWatered(i)) flags |= 2;
                int dry = Mathf.RoundToInt(Mathf.Clamp01(dryness[i]) * 65535f);
                data[o] = flags;
                data[o + 1] = (byte)dry;
                data[o + 2] = (byte)(dry >> 8);
                data[o + 3] = cells[i].b;
                data[o + 4] = cells[i].a;
                data[o + 5] = wetBytes[i * 2 + 1];
                data[o + 6] = target[i];
            }
            return data;
        }

        /// <summary>
        /// Restores the output of <see cref="CaptureSnapshot"/>; mown grass looks short instantly, wetness is reset.
        /// The "counts as watered" flag is kept together with the dryness (so rounding at the threshold does not shift the counters).
        /// </summary>
        /// <returns>False if the size does not match (state unchanged).</returns>
        public bool RestoreSnapshot(byte[] data)
        {
            int stride = data == null ? 0 : data.Length == cells.Length * SnapshotBytesPerCell ? SnapshotBytesPerCell
                : data.Length == cells.Length * LegacySnapshotBytesPerCell ? LegacySnapshotBytesPerCell : 0;
            if (stride == 0)
                return false;
            animating.Clear();
            wetCells.Clear();
            for (int i = 0, o = 0; i < cells.Length; i++, o += stride)
            {
                isAnimating[i] = false;
                isWet[i] = false;
                wetness[i] = 0f;
                wetBytes[i * 2] = 0;
                if (!IsGrass(i))
                    continue;
                byte flags = data[o];
                float dry = (data[o + 1] | (data[o + 2] << 8)) / 65535f;
                if (NeedsWater(i))
                {
                    bool watered = (flags & 2) != 0;
                    if (watered && dry > WateredDryness) dry = WateredDryness;
                    else if (!watered && dry <= WateredDryness) dry = WateredDryness + 0.002f;
                }
                dryness[i] = dry;
                target[i] = stride == SnapshotBytesPerCell ? data[o + 6] : (flags & 1) != 0 ? Cut : Tall;
                cells[i] = new Color32(target[i], (byte)Mathf.RoundToInt(dry * 255f), data[o + 3], data[o + 4]);
                wetBytes[i * 2 + 1] = data[o + 5];
            }
            RecountPatches();
            stateDirty = wetDirty = true;
            return true;
        }

        /// <summary>Digest of the patch map (to verify that a save belongs to this garden; FNV-1a).</summary>
        public int LayoutHash()
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (byte id in Layout.PatchIds)
                    hash = (hash ^ id) * 16777619;
                hash = (hash ^ (uint)Grid.Width) * 16777619;
                hash = (hash ^ (uint)Grid.Height) * 16777619;
                return (int)hash;
            }
        }

        /// <summary>Grass colour of the cell (sRGB, dryness included); for the clipping particles.</summary>
        public Color GrassColor(int index, float t)
        {
            int patch = Layout.PatchIds[index];
            Color color = Color.Lerp(patchMid[patch], patchTip[patch], t);
            color = Color.Lerp(color, Color.Lerp(GrassVariety.FreshMid, GrassVariety.FreshTip, t), GreeningAt(index) * GreeningStrength);
            Color dry = Color.Lerp(GrassVariety.DryMid, GrassVariety.DryTip, t);
            return Color.Lerp(color, dry, dryness[index]);
        }

        public static float DistanceToSegmentSq(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq < 1e-10f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return (a + ab * t - point).sqrMagnitude;
        }

        /// <summary>Uploads the changed data to the GPU and sets the global shader variables.</summary>
        public void Apply()
        {
            if (patchesDirty)
            {
                PatchTexture.SetPixelData(Layout.PatchIds, 0);
                PatchTexture.Apply(false);
                patchesDirty = false;
            }
            if (stateDirty)
            {
                StateTexture.SetPixelData(cells, 0);
                StateTexture.Apply(false);
                stateDirty = false;
            }
            if (wetDirty)
            {
                WetTexture.SetPixelData(wetBytes, 0);
                WetTexture.Apply(false);
                wetDirty = false;
            }

            Shader.SetGlobalTexture(StateId, StateTexture);
            Shader.SetGlobalTexture(WetId, WetTexture);
            Shader.SetGlobalTexture(PatchMapId, PatchTexture);
            Shader.SetGlobalTexture(PaletteId, PaletteTexture);
            Shader.SetGlobalVector(RectId, Grid.ShaderRect);
            Shader.SetGlobalVector(CellsId, new Vector4(Grid.Width, Grid.Height, Grid.CellSize, 0f));
            Core.ShaderColors.SetGlobal(DryBaseId, GrassVariety.DryBase);
            Core.ShaderColors.SetGlobal(DryMidId, GrassVariety.DryMid);
            Core.ShaderColors.SetGlobal(DryTipId, GrassVariety.DryTip);
            Core.ShaderColors.SetGlobal(FreshBaseId, GrassVariety.FreshBase);
            Core.ShaderColors.SetGlobal(FreshMidId, GrassVariety.FreshMid);
            Core.ShaderColors.SetGlobal(FreshTipId, GrassVariety.FreshTip);
            Shader.SetGlobalFloat(GreeningId, GreeningStrength);
        }

        void RecountPatches()
        {
            patchCells = new int[PaletteWidth];
            patchCut = new int[PaletteWidth];
            patchNeedsWater = new int[PaletteWidth];
            patchWatered = new int[PaletteWidth];
            TotalGrassCells = 0;
            TotalCutCells = 0;
            TotalNeedsWater = 0;
            TotalWatered = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                int patch = Layout.PatchIds[i];
                if (patch == PatchLayout.NoGrass)
                    continue;
                patchCells[patch]++;
                TotalGrassCells++;
                if (target[i] <= CutThreshold)
                {
                    patchCut[patch]++;
                    TotalCutCells++;
                }
                if (NeedsWater(i))
                {
                    patchNeedsWater[patch]++;
                    TotalNeedsWater++;
                    if (IsWatered(i))
                    {
                        patchWatered[patch]++;
                        TotalWatered++;
                    }
                }
            }
        }

        void BuildPalette()
        {
            var pixels = new Color32[PaletteWidth * PaletteRows];
            var random = new System.Random(5);
            for (int patch = 1; patch <= Layout.PatchCount && patch < PaletteWidth; patch++)
            {
                GrassVariety v = varieties[Mathf.Clamp(Layout.Seeds[patch - 1].Variety, 0, varieties.Count - 1)];
                // A small brightness/tone offset so neighbouring patches of the same variety can be told apart
                float jitter = 1f + ((float)random.NextDouble() - 0.5f) * 0.08f;
                pixels[0 * PaletteWidth + patch] = WithAlpha(v.Base * jitter, v.HeightScale / 2f);
                pixels[1 * PaletteWidth + patch] = WithAlpha(v.Mid * jitter, 1f);
                pixels[2 * PaletteWidth + patch] = WithAlpha(v.Tip * jitter, 1f);
                pixels[3 * PaletteWidth + patch] = WithAlpha(v.Cut, 1f);
                patchMid[patch] = WithAlpha(v.Mid * jitter, 1f);
                patchTip[patch] = WithAlpha(v.Tip * jitter, 1f);
            }
            PaletteTexture.SetPixels32(pixels);
            PaletteTexture.Apply(false);
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
