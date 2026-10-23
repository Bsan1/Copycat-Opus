using System.Collections.Generic;
using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>
    /// Temporary marks painted on the lawn (all in the grass and ground shaders, OpusLawn.hlsl → LawnOverlay):
    /// the active patch's orange dashed border, the cyan "show remaining" wave and, when a patch is finished, the
    /// cyan + yellow sparkle wave spreading from the centre.
    /// </summary>
    public sealed class LawnOverlay
    {
        /// <summary>Dash + gap length (m), measured along the border.</summary>
        public const float DashPeriod = 0.6f;
        /// <summary>The dash (filled) part of the period.</summary>
        public const float DashFill = 0.6f;
        public const float HighlightSeconds = 2.4f;
        public const float HighlightWaveSpeed = 16f;
        public const float FinishWaveSpeed = 7f;
        public const float FinishGlowSeconds = 1.4f;
        const float EdgeFadeSeconds = 0.35f;

        static readonly int EdgeTextureId = Shader.PropertyToID("_OpusPatchEdge");
        static readonly int EdgeInfoId = Shader.PropertyToID("_OpusEdgeInfo");
        static readonly int EdgeColorId = Shader.PropertyToID("_OpusEdgeColor");
        static readonly int HighlightId = Shader.PropertyToID("_OpusHighlight");
        static readonly int HighlightColorId = Shader.PropertyToID("_OpusHighlightColor");
        static readonly int FinishId = Shader.PropertyToID("_OpusFinish");
        static readonly int FinishInfoId = Shader.PropertyToID("_OpusFinishInfo");
        static readonly int PatternGuideId = Shader.PropertyToID("_OpusPatternGuide");
        static readonly int PatternInfoId = Shader.PropertyToID("_OpusPatternInfo");

        readonly LawnState lawn;
        // RG8: r = border strip, g = dash (filled/empty by the walking distance along the border)
        readonly byte[] edgeCells;
        readonly int[] distance;
        readonly Queue<int> queue = new Queue<int>();
        readonly Texture2D edgeTexture;
        int edgePatch;
        float edgeStrength;
        bool edgeVisible;
        Vector4 edgeInfo;
        float highlightStart = -100f;
        readonly byte[] guideCells;
        readonly Texture2D guideTexture;
        PatternTask pattern;
        int activePatchId;
        float patternStrength;

        public int EdgePatch => edgePatch;
        public float EdgeStrength => edgeStrength;
        public bool IsHighlighting => Now - highlightStart < HighlightSeconds;
        public int FinishPatch { get; private set; }
        public float FinishStart { get; private set; } = -100f;

        static float Now => Time.timeSinceLevelLoad;

        public LawnOverlay(LawnState lawn)
        {
            this.lawn = lawn;
            LawnGrid grid = lawn.Grid;
            edgeCells = new byte[grid.CellCount * 2];
            distance = new int[grid.CellCount];
            edgeTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.RG16, false, true)
            {
                name = "PatchBorder", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            edgeTexture.SetPixelData(edgeCells, 0);
            edgeTexture.Apply(false);
            Shader.SetGlobalTexture(EdgeTextureId, edgeTexture);
            Core.ShaderColors.SetGlobal(EdgeColorId, Core.Palette.PatchOutline);
            Core.ShaderColors.SetGlobal(HighlightColorId, Core.Palette.Highlight);
            Shader.SetGlobalVector(HighlightId, new Vector4(0f, 0f, -100f, 0f)); // w = 0: off
            Shader.SetGlobalVector(FinishId, new Vector4(0f, -100f, 0f, 0f));

            guideCells = new byte[grid.CellCount];
            guideTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.R8, false, true)
            {
                name = "PatternGuide", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point,
            };
            guideTexture.SetPixelData(guideCells, 0);
            guideTexture.Apply(false);
            Shader.SetGlobalTexture(PatternGuideId, guideTexture);
            Shader.SetGlobalVector(PatternInfoId, Vector4.zero);
        }

        public Texture2D GuideTexture => guideTexture;
        public PatternTask Pattern => pattern;
        /// <summary>Current strength of the guide tint (0..1).</summary>
        public float PatternStrength => patternStrength;

        /// <summary>Loads the guide of the pattern task (null or completed: turn off).</summary>
        public void SetPattern(PatternTask task)
        {
            pattern = task;
            if (task != null)
                task.WriteGuide(lawn, guideCells);
            else
                System.Array.Clear(guideCells, 0, guideCells.Length);
            guideTexture.SetPixelData(guideCells, 0);
            guideTexture.Apply(false);
        }

        /// <summary>The guide is only prominent while the player is in the task patch or during "show remaining".</summary>
        void TickPattern(float deltaTime)
        {
            bool show = pattern != null && !pattern.Completed && (activePatchId == pattern.Patch || IsHighlighting);
            patternStrength = Mathf.MoveTowards(patternStrength, show ? 1f : 0f, deltaTime / EdgeFadeSeconds);
            Shader.SetGlobalVector(PatternInfoId, new Vector4(pattern?.Patch ?? 0, patternStrength, 0f, 0f));
        }

        public Texture2D EdgeTexture => edgeTexture;

        /// <summary>Moves the dashed border to this patch (0 or a finished patch: hide). It fades in smoothly on change.</summary>
        public void SetActivePatch(PatchProgress patch)
        {
            activePatchId = patch?.Id ?? 0;
            int id = patch != null && !patch.Finished ? patch.Id : 0;
            edgeVisible = id != 0;
            if (id == edgePatch || id == 0)
                return;

            edgePatch = id;
            edgeStrength = 0f;
            BuildEdge(id);
            edgeInfo = new Vector4(patch.Centroid.x, patch.Centroid.y, 0f, 0f);
        }

        /// <summary>
        /// "Show remaining": a cyan wave spreading from the player paints the work left in the given patch (unmown,
        /// unwatered). Without a patch (0) over the whole garden.
        /// </summary>
        public void StartHighlight(Vector3 origin, int patch)
        {
            highlightStart = Now;
            HighlightPatch = patch;
            Shader.SetGlobalVector(HighlightId, new Vector4(origin.x, origin.z, highlightStart, patch > 0 ? patch : -1f));
        }

        public int HighlightPatch { get; private set; }

        /// <summary>Patch finished: a cyan wave spreading from the centre followed by a yellow sparkle.</summary>
        public void StartFinish(PatchProgress patch)
        {
            FinishPatch = patch.Id;
            FinishStart = Now;
            Shader.SetGlobalVector(FinishId, new Vector4(patch.Id, FinishStart, patch.Centroid.x, patch.Centroid.y));
            Shader.SetGlobalVector(FinishInfoId, new Vector4(FinishWaveSpeed, FinishGlowSeconds, 0f, 0f));
        }

        /// <summary>Cuts the highlight and the finish wave (while loading a save).</summary>
        public void ResetEffects()
        {
            highlightStart = -100f;
            HighlightPatch = 0;
            FinishPatch = 0;
            FinishStart = -100f;
            Shader.SetGlobalVector(HighlightId, new Vector4(0f, 0f, -100f, 0f));
            Shader.SetGlobalVector(FinishId, new Vector4(0f, -100f, 0f, 0f));
        }

        /// <summary>When the finish wave reaches a point (s, from the start of the level).</summary>
        public static float FinishArrival(float start, Vector2 centroid, Vector2 point) =>
            start + Vector2.Distance(centroid, point) / FinishWaveSpeed;

        public void Tick(float deltaTime)
        {
            float target = edgeVisible ? 1f : 0f;
            edgeStrength = Mathf.MoveTowards(edgeStrength, target, deltaTime / EdgeFadeSeconds);
            edgeInfo.w = edgeStrength;
            Shader.SetGlobalVector(EdgeInfoId, edgeInfo);
            TickPattern(deltaTime);
        }

        void BuildEdge(int patch)
        {
            LawnGrid grid = lawn.Grid;
            byte[] ids = lawn.Layout.PatchIds;
            System.Array.Clear(edgeCells, 0, edgeCells.Length);
            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (ids[grid.Index(x, z)] != patch || !LawnTasks.IsBoundary(grid, ids, x, z, patch))
                        continue;
                    // Border cell and its neighbours inside the patch: a ~20 cm line that reads through the grass
                    for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, nz = z + dz;
                        if (nx >= 0 && nz >= 0 && nx < grid.Width && nz < grid.Height && ids[grid.Index(nx, nz)] == patch)
                            edgeCells[grid.Index(nx, nz) * 2] = 255;
                    }
                }
            }
            MarkDashes(grid);
            edgeTexture.SetPixelData(edgeCells, 0);
            edgeTexture.Apply(false);
        }

        /// <summary>
        /// Dashes: along the strip, the breadth-first walking distance from the first cell of every connected piece (8 neighbours,
        /// diagonal step √2). This distance modulo <see cref="DashPeriod"/> says whether it is a dash or a gap;
        /// so the line is dashed evenly whichever way it goes.
        /// </summary>
        void MarkDashes(LawnGrid grid)
        {
            const int Straight = 10, Diagonal = 14;
            int period = Mathf.RoundToInt(DashPeriod / grid.CellSize * Straight);
            int fill = Mathf.RoundToInt(period * DashFill);
            for (int i = 0; i < distance.Length; i++)
                distance[i] = -1;

            for (int start = 0; start < distance.Length; start++)
            {
                if (edgeCells[start * 2] == 0 || distance[start] >= 0)
                    continue;
                distance[start] = 0;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    int x = index % grid.Width, z = index / grid.Width;
                    for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int nx = x + dx, nz = z + dz;
                        if (nx < 0 || nz < 0 || nx >= grid.Width || nz >= grid.Height) continue;
                        int next = grid.Index(nx, nz);
                        if (edgeCells[next * 2] == 0 || distance[next] >= 0) continue;
                        distance[next] = distance[index] + (dx != 0 && dz != 0 ? Diagonal : Straight);
                        queue.Enqueue(next);
                    }
                }
            }

            for (int i = 0; i < distance.Length; i++)
                if (distance[i] >= 0 && distance[i] % period < fill)
                    edgeCells[i * 2 + 1] = 255;
        }

        public bool IsEdgeCell(int index) => edgeCells[index * 2] != 0;
        public bool IsDashCell(int index) => edgeCells[index * 2 + 1] != 0;

        /// <summary>Number of marked cells in the border texture (for tests).</summary>
        public int EdgeCellCount()
        {
            int count = 0;
            for (int i = 0; i < edgeCells.Length; i += 2)
                if (edgeCells[i] != 0) count++;
            return count;
        }
    }
}
