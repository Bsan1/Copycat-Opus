using System.Collections.Generic;
using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>A patch's definition: Voronoi seed, grass variety, starting dryness (0..1) and area.</summary>
    public readonly struct PatchSeed
    {
        public readonly Vector2 Position;
        public readonly int Variety;
        public readonly float Dryness;
        public readonly int Area;

        public PatchSeed(float x, float z, int variety, float dryness = 0f, int area = 1)
        {
            Position = new Vector2(x, z);
            Variety = variety;
            Dryness = dryness;
            Area = area;
        }
    }

    /// <summary>Dry spot: centre, radius (m) and dryness.</summary>
    public readonly struct DrySpot
    {
        public readonly Vector2 Center;
        public readonly float Radius;
        public readonly float Dryness;

        public DrySpot(float x, float z, float radius, float dryness = 1f)
        {
            Center = new Vector2(x, z);
            Radius = radius;
            Dryness = dryness;
        }
    }

    /// <summary>
    /// Splits the garden into patches: every cell falls into the patch of the seed nearest to its noise-warped
    /// position (organically bordered Voronoi). Patch ids 1..N; 0 = no grass (under obstacles etc.).
    /// </summary>
    public sealed class PatchLayout
    {
        public const byte NoGrass = 0;

        public LawnGrid Grid { get; }
        public IReadOnlyList<PatchSeed> Seeds { get; }
        public int PatchCount => Seeds.Count;
        /// <summary>Patch id per cell.</summary>
        public byte[] PatchIds { get; }
        /// <summary>Starting dryness per cell (0..255).</summary>
        public byte[] Dryness { get; }
        /// <summary>Area id per cell (0 = no area).</summary>
        public byte[] CellAreas { get; private set; }

        public int AreaOfPatch(int patch) => patch >= 1 && patch <= Seeds.Count ? Seeds[patch - 1].Area : 0;

        PatchLayout(LawnGrid grid, IReadOnlyList<PatchSeed> seeds)
        {
            Grid = grid;
            Seeds = seeds;
            PatchIds = new byte[grid.CellCount];
            Dryness = new byte[grid.CellCount];
        }

        /// <param name="areas">
        /// If given, every cell falls to one of the seeds of the area it is in (areas separated by fences do not
        /// spill into each other); a cell in no area has no grass. If not given, a single area.
        /// </param>
        public static PatchLayout Generate(LawnGrid grid, IReadOnlyList<PatchSeed> seeds, IReadOnlyList<DrySpot> drySpots,
            IReadOnlyList<LawnArea> areas = null, float warpAmplitude = 1.6f, float warpScale = 0.11f, int noiseSeed = 17)
        {
            if (seeds.Count == 0 || seeds.Count > 254)
                throw new System.ArgumentOutOfRangeException(nameof(seeds), "1..254 patches are supported.");

            var layout = new PatchLayout(grid, seeds);
            layout.CellAreas = new byte[grid.CellCount];
            float ox = noiseSeed * 13.17f, oz = noiseSeed * 7.31f;

            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Vector2 p = grid.CellCenter(x, z);
                    int area = 1;
                    if (areas != null)
                    {
                        area = 0;
                        foreach (LawnArea candidate in areas)
                        {
                            if (candidate.Contains(p))
                            {
                                area = candidate.Id;
                                break;
                            }
                        }
                    }
                    int index = grid.Index(x, z);
                    layout.CellAreas[index] = (byte)area;
                    if (area == 0)
                        continue;

                    Vector2 warped = p + Warp(p, warpAmplitude, warpScale, ox, oz);
                    int best = -1;
                    float bestDistance = float.MaxValue;
                    for (int i = 0; i < seeds.Count; i++)
                    {
                        if (areas != null && seeds[i].Area != area)
                            continue;
                        float d = (warped - seeds[i].Position).sqrMagnitude;
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = i;
                        }
                    }
                    if (best < 0)
                        continue; // an area without a seed: no grass

                    layout.PatchIds[index] = (byte)(best + 1);

                    float dry = seeds[best].Dryness;
                    foreach (DrySpot spot in drySpots)
                    {
                        // Edge noise shifts by at most a quarter radius: a spot does not reach cells that far away (skip the noise)
                        float rawDistance = Vector2.Distance(p, spot.Center);
                        if (rawDistance - spot.Radius * 0.25f >= spot.Radius)
                            continue;
                        // A spot with a noisy, softly fading edge
                        float edgeNoise = (Mathf.PerlinNoise(p.x * 0.9f + ox, p.y * 0.9f + oz) - 0.5f) * spot.Radius * 0.5f;
                        float distance = rawDistance + edgeNoise;
                        float t = Mathf.Clamp01((spot.Radius - distance) / (spot.Radius * 0.35f));
                        dry = Mathf.Max(dry, spot.Dryness * t * t * (3f - 2f * t));
                    }
                    layout.Dryness[index] = (byte)Mathf.RoundToInt(Mathf.Clamp01(dry) * 255f);
                }
            }
            return layout;
        }

        static Vector2 Warp(Vector2 p, float amplitude, float scale, float ox, float oz)
        {
            // Two octaves: large curves + small roughness
            float wx = (Mathf.PerlinNoise(p.x * scale + ox, p.y * scale + oz) - 0.5f)
                       + (Mathf.PerlinNoise(p.x * scale * 3.1f + oz, p.y * scale * 3.1f + ox) - 0.5f) * 0.35f;
            float wz = (Mathf.PerlinNoise(p.x * scale + ox + 41.3f, p.y * scale + oz + 17.9f) - 0.5f)
                       + (Mathf.PerlinNoise(p.x * scale * 3.1f + 5.7f, p.y * scale * 3.1f + 91.1f) - 0.5f) * 0.35f;
            return new Vector2(wx, wz) * (2f * amplitude);
        }

        /// <summary>An independent copy (cell arrays are copied; seeds and the grid are shared).</summary>
        public PatchLayout Clone()
        {
            var copy = new PatchLayout(Grid, Seeds);
            System.Array.Copy(PatchIds, copy.PatchIds, PatchIds.Length);
            System.Array.Copy(Dryness, copy.Dryness, Dryness.Length);
            copy.CellAreas = (byte[])CellAreas.Clone();
            return copy;
        }

        public byte PatchAt(Vector2 world) =>
            Grid.TryGetCell(world, out int x, out int z) ? PatchIds[Grid.Index(x, z)] : NoGrass;

        public int CountCells(int patchId)
        {
            int count = 0;
            foreach (byte id in PatchIds)
                if (id == patchId) count++;
            return count;
        }

        public int CountDryCells(byte threshold = 128)
        {
            int count = 0;
            for (int i = 0; i < Dryness.Length; i++)
                if (PatchIds[i] != NoGrass && Dryness[i] >= threshold) count++;
            return count;
        }

        /// <summary>Removes the grass from a cell (under an obstacle).</summary>
        public void ClearCell(int index)
        {
            PatchIds[index] = NoGrass;
            Dryness[index] = 0;
        }
    }
}
