using System;
using System.Collections.Generic;
using UnityEngine;

namespace Opus.Visual
{
    /// <summary>
    /// Seamlessly tiling surface patterns with a hand-painted look (from code; no downloads). Every pattern is a "tone multiplier"
    /// texture: 0.5 grey = the material colour as is, dark = shadow/joint, light = edge highlight. So a single pattern
    /// works in every colour and the lighting cues (shadow under a board, a shingle's lip) are painted into the texture.
    /// The grime texture ("grime") is a direct mask instead (0 clean, 1 dirty). All noises have integer periods: the textures tile.
    /// </summary>
    public static class SurfaceTextures
    {
        public const int Size = 512;
        /// <summary>Raised when the generation changes (the editor regenerates the old assets).</summary>
        public const int Version = 3;

        /// <summary>
        /// Pattern resolution: on large surfaces seen up close (the tree trunk) 512 pixels grew on screen with the repeat length and
        /// pixelated; those are 1024.
        /// </summary>
        public static int SizeOf(string name) => name == "bark" ? 1024 : Size;
        public const string Grime = "grime";
        /// <summary>Folder under Resources (the editor writes the PNGs here).</summary>
        public const string ResourceFolder = "SurfaceTextures";

        static readonly Dictionary<string, Texture2D> Loaded = new Dictionary<string, Texture2D>();

        /// <summary>Pattern texture: the generated asset first (Resources), otherwise computed at runtime (slow, fallback only).</summary>
        public static Texture2D Get(string name)
        {
            if (Loaded.TryGetValue(name, out Texture2D cached) && cached != null)
                return cached;
            Texture2D texture = Resources.Load<Texture2D>(ResourceFolder + "/" + name);
            if (texture == null)
            {
                Debug.LogWarning($"[Opus] Pattern asset missing, generating at runtime: {name}");
                texture = CreateTexture(name);
            }
            Loaded[name] = texture;
            return texture;
        }

        static readonly Dictionary<string, Func<int, Color32[]>> Generators = new Dictionary<string, Func<int, Color32[]>>
        {
            ["paint"] = Paint,
            ["siding"] = Siding,
            ["wood"] = Planks,
            ["vinyl"] = Vinyl,
            ["shingles"] = Shingles,
            ["brick"] = Brick,
            ["concrete"] = Concrete,
            ["stone"] = Stone,
            ["bark"] = Bark,
            ["soil"] = Soil,
            ["rubber"] = Rubber,
            ["metal"] = Metal,
            ["plastic"] = Plastic,
            ["leaf"] = Foliage,
            ["fabric"] = Fabric,
            ["glass"] = Glass,
            [Grime] = GrimeMask,
        };

        public static IEnumerable<string> Names => Generators.Keys;

        public static Color32[] Generate(string name, int size = 0) =>
            Generators.TryGetValue(name, out Func<int, Color32[]> make)
                ? make(size > 0 ? size : SizeOf(name))
                : throw new ArgumentException($"Unknown pattern: {name}");

        public static Texture2D CreateTexture(string name, int size = 0)
        {
            size = size > 0 ? size : SizeOf(name);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "Pattern_" + name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4,
            };
            texture.SetPixels32(Generate(name, size));
            texture.Apply(true, false);
            return texture;
        }

        // ---------- Noise ----------

        /// <summary>Periodic value noise: a px × py lattice on the (u, v) ∈ [0,1) square, the edges join.</summary>
        sealed class Lattice
        {
            readonly float[] values;
            readonly int px, py;

            public Lattice(int px, int py, int seed)
            {
                this.px = Math.Max(1, px);
                this.py = Math.Max(1, py);
                values = new float[this.px * this.py];
                var random = new System.Random(seed);
                for (int i = 0; i < values.Length; i++) values[i] = (float)random.NextDouble();
            }

            public float Sample(float u, float v)
            {
                float x = Frac(u) * px, y = Frac(v) * py;
                int x0 = (int)x, y0 = (int)y;
                float fx = x - x0, fy = y - y0;
                fx = fx * fx * (3f - 2f * fx);
                fy = fy * fy * (3f - 2f * fy);
                int x1 = (x0 + 1) % px, y1 = (y0 + 1) % py;
                x0 %= px;
                y0 %= py;
                float a = Mathf.Lerp(values[y0 * px + x0], values[y0 * px + x1], fx);
                float b = Mathf.Lerp(values[y1 * px + x0], values[y1 * px + x1], fx);
                return Mathf.Lerp(a, b, fy);
            }
        }

        /// <summary>Periodic fBm (0..1): every octave doubles the period.</summary>
        sealed class Fbm
        {
            readonly Lattice[] octaves;

            public Fbm(int px, int py, int count, int seed)
            {
                octaves = new Lattice[count];
                for (int i = 0; i < count; i++)
                    octaves[i] = new Lattice(px << i, py << i, seed * 31 + i * 7919);
            }

            public float Sample(float u, float v)
            {
                float sum = 0f, amp = 0.5f, norm = 0f;
                foreach (Lattice lattice in octaves)
                {
                    sum += lattice.Sample(u, v) * amp;
                    norm += amp;
                    amp *= 0.5f;
                }
                return sum / norm;
            }
        }

        /// <summary>Periodic cellular (Worley) noise: distance to the two nearest points (in cell size units) and the cell id.</summary>
        sealed class Cells
        {
            readonly Vector2[] points;
            readonly int n;

            public Cells(int n, int seed)
            {
                this.n = n;
                points = new Vector2[n * n];
                var random = new System.Random(seed);
                for (int i = 0; i < points.Length; i++)
                    points[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            }

            public void Sample(float u, float v, out float f1, out float f2, out int id)
            {
                float x = Frac(u) * n, y = Frac(v) * n;
                int cx = (int)x, cy = (int)y;
                f1 = f2 = 9f;
                id = 0;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cx + dx, gy = cy + dy;
                    int wx = ((gx % n) + n) % n, wy = ((gy % n) + n) % n;
                    Vector2 p = points[wy * n + wx] + new Vector2(gx, gy);
                    float d = Vector2.Distance(p, new Vector2(x, y));
                    if (d < f1)
                    {
                        f2 = f1;
                        f1 = d;
                        id = wy * n + wx;
                    }
                    else if (d < f2)
                    {
                        f2 = d;
                    }
                }
            }
        }

        static float Frac(float x) => x - Mathf.Floor(x);
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        static float Hash(int a, int b = 0, int seed = 0)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Writes the tone multiplier (1 = as is) into the texture; warmth adds a brownish tint to dark areas.</summary>
        static Color32 Encode(float m, float warm = 0f)
        {
            float v = Mathf.Clamp01(m * 0.5f);
            float r = Mathf.Clamp01(v * (1f + warm * 0.08f));
            float b = Mathf.Clamp01(v * (1f - warm * 0.08f));
            return new Color32((byte)Mathf.RoundToInt(r * 255f), (byte)Mathf.RoundToInt(v * 255f), (byte)Mathf.RoundToInt(b * 255f), 255);
        }

        static Color32[] Render(int size, Func<float, float, Color32> pixel)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = pixel((x + 0.5f) / size, (y + 0.5f) / size);
            return pixels;
        }

        // ---------- Patterns ----------

        /// <summary>General paint: horizontal and vertical brush marks, large speckle, paper texture.</summary>
        static Color32[] Paint(int size)
        {
            var strokes = new Fbm(3, 24, 3, 1);
            var cross = new Fbm(24, 3, 2, 2);
            var mottle = new Fbm(4, 4, 3, 3);
            var grain = new Lattice(128, 128, 4);
            return Render(size, (u, v) => Encode(1f + (strokes.Sample(u, v) - 0.5f) * 0.16f + (cross.Sample(u, v) - 0.5f) * 0.06f
                                                 + (mottle.Sample(u, v) - 0.5f) * 0.12f + (grain.Sample(u, v) - 0.5f) * 0.05f));
        }

        /// <summary>Horizontal lap siding: 8 boards; a shadow line at the bottom edge, a highlight on the lip, darkening upwards.</summary>
        static Color32[] Siding(int size)
        {
            const int rows = 8;
            var streaks = new Fbm(2, 96, 3, 11);
            var mottle = new Fbm(4, 4, 3, 12);
            var chips = new Cells(24, 13);
            return Render(size, (u, v) =>
            {
                float rv = v * rows;
                int row = Mathf.FloorToInt(rv);
                float t = rv - row;
                float shadow = 1f - Smooth(0.025f, 0.05f, t);
                float lip = Smooth(0.04f, 0.07f, t) * (1f - Smooth(0.09f, 0.14f, t));
                float m = 1.03f - 0.13f * t + (Hash(row, 0, 1) - 0.5f) * 0.08f;
                m += (streaks.Sample(u, v) - 0.5f) * 0.12f + (mottle.Sample(u, v) - 0.5f) * 0.08f;
                chips.Sample(u, v, out float f1, out _, out int id);
                if (f1 < 0.12f && Hash(id, 0, 2) > 0.85f) m += 0.08f;
                m = m * (1f - 0.5f * shadow) + 0.1f * lip;
                return Encode(m, shadow);
            });
        }

        /// <summary>Vertical boards: 6 boards, dark joints between them, chamfer highlight, lengthwise grain and knots.</summary>
        static Color32[] Planks(int size)
        {
            const int boards = 6;
            var grain = new Fbm(boards * 12, 2, 3, 21);
            var mottle = new Fbm(3, 3, 3, 22);
            return Render(size, (u, v) =>
            {
                float su = u * boards;
                int board = Mathf.FloorToInt(su);
                float s = su - board;
                float gap = 1f - Smooth(0.018f, 0.045f, s);
                float bevel = Smooth(0.04f, 0.065f, s) * (1f - Smooth(0.09f, 0.13f, s));
                float edge = Smooth(0.93f, 1f, s);
                float m = 1f + (Hash(board, 0, 3) - 0.5f) * 0.1f;
                m += (grain.Sample(u, Frac(v + Hash(board, 0, 4))) - 0.5f) * 0.16f + (mottle.Sample(u, v) - 0.5f) * 0.08f;
                // One knot per board
                float kx = (board + 0.3f + Hash(board, 0, 5) * 0.4f) / boards, ky = Hash(board, 0, 6);
                float dy = Mathf.Abs(Frac(v - ky + 0.5f) - 0.5f) * 1.6f, dx = (u - kx) * boards * 0.35f;
                float knot = Mathf.Sqrt(dx * dx + dy * dy);
                if (knot < 0.06f) m *= 0.78f + knot * 2f;
                m = m * (1f - 0.55f * gap) + 0.08f * bevel - 0.07f * edge;
                return Encode(m, gap);
            });
        }

        /// <summary>Vinyl: a very light large speckle and vertical streaks.</summary>
        static Color32[] Vinyl(int size)
        {
            var mottle = new Fbm(3, 3, 3, 31);
            var streak = new Fbm(40, 2, 2, 32);
            return Render(size, (u, v) => Encode(1f + (mottle.Sample(u, v) - 0.5f) * 0.06f + (streak.Sample(u, v) - 0.5f) * 0.04f));
        }

        /// <summary>Shingles: 8 rows, 4 tabs per row (rows offset by half), joints between tabs, light bottom edge, shaded top.</summary>
        static Color32[] Shingles(int size)
        {
            const int rows = 8, tabs = 4;
            var granules = new Lattice(256, 256, 41);
            var mottle = new Fbm(4, 4, 3, 42);
            return Render(size, (u, v) =>
            {
                float rv = v * rows;
                int row = Mathf.FloorToInt(rv);
                float t = rv - row;
                float su = u * tabs + (row % 2) * 0.5f;
                int tab = Mathf.FloorToInt(su);
                float s = su - tab;
                float gapV = 1f - Smooth(0.012f, 0.03f, Mathf.Min(s, 1f - s));
                float underLip = Smooth(0.86f, 0.99f, t);
                float m = 1.08f - 0.24f * t + (Hash(row, tab, 7) - 0.5f) * 0.18f;
                m += (granules.Sample(u, v) - 0.5f) * 0.14f + (mottle.Sample(u, v) - 0.5f) * 0.08f;
                m *= (1f - 0.5f * underLip) * (1f - 0.6f * gapV);
                return Encode(m, underLip);
            });
        }

        /// <summary>Brick: 12 rows, staggered; light mortar, a tone difference per brick.</summary>
        static Color32[] Brick(int size)
        {
            const int rows = 12, bricks = 4;
            var mottle = new Fbm(6, 6, 3, 51);
            var grain = new Lattice(160, 160, 52);
            return Render(size, (u, v) =>
            {
                float rv = v * rows;
                int row = Mathf.FloorToInt(rv);
                float t = rv - row;
                float su = u * bricks + (row % 2) * 0.5f;
                int brick = Mathf.FloorToInt(su);
                float s = su - brick;
                float mortar = Mathf.Max(1f - Smooth(0.05f, 0.09f, Mathf.Min(t, 1f - t)), 1f - Smooth(0.02f, 0.04f, Mathf.Min(s, 1f - s)));
                float m = 1f + (Hash(row, brick, 8) - 0.5f) * 0.24f + (mottle.Sample(u, v) - 0.5f) * 0.12f + (grain.Sample(u, v) - 0.5f) * 0.08f;
                m = Mathf.Lerp(m, 1.38f, mortar);
                return Encode(m);
            });
        }

        /// <summary>Concrete: speckled, small pores, thin cracks.</summary>
        static Color32[] Concrete(int size)
        {
            var mottle = new Fbm(4, 4, 4, 61);
            var grain = new Lattice(160, 160, 62);
            var pits = new Cells(40, 63);
            var crack = new Fbm(6, 6, 3, 64);
            return Render(size, (u, v) =>
            {
                float m = 1f + (mottle.Sample(u, v) - 0.5f) * 0.16f + (grain.Sample(u, v) - 0.5f) * 0.07f;
                pits.Sample(u, v, out float f1, out _, out int id);
                if (f1 < 0.14f && Hash(id, 0, 9) > 0.6f) m *= 0.82f;
                float ridge = 1f - Mathf.Abs(crack.Sample(u, v) * 2f - 1f);
                m *= 1f - 0.22f * Smooth(0.955f, 0.985f, ridge);
                return Encode(m);
            });
        }

        /// <summary>Stone: large blotches, light and dark speckles.</summary>
        static Color32[] Stone(int size)
        {
            var mottle = new Fbm(3, 3, 4, 71);
            var speck = new Lattice(200, 200, 72);
            return Render(size, (u, v) =>
            {
                float m = 1f + (mottle.Sample(u, v) - 0.5f) * 0.24f;
                float s = speck.Sample(u, v);
                if (s > 0.82f) m += 0.12f;
                else if (s < 0.16f) m -= 0.12f;
                return Encode(m);
            });
        }

        /// <summary>
        /// Bark: soft vertical fibres, shallow fissures and a broad, light tone variation. No blotches: the dark areas of the trunk
        /// in the video are the leaves' shadow (the sun and the crown give that; the previous version painted them into the texture). No sharp
        /// edges either: so the edges do not pixelate up close.
        /// </summary>
        static Color32[] Bark(int size)
        {
            var fibers = new Fbm(30, 3, 3, 83);
            var ridges = new Fbm(7, 1, 3, 81);
            var mottle = new Fbm(3, 2, 3, 82);
            return Render(size, (u, v) =>
            {
                float r = ridges.Sample(u, v);
                float groove = 1f - Smooth(0f, 0.14f, Mathf.Abs(r * 2f - 1f));
                float m = 1f + (fibers.Sample(u, v) - 0.5f) * 0.16f + (mottle.Sample(u, v) - 0.5f) * 0.12f;
                m *= 1f - 0.2f * groove;
                m *= 1f + 0.05f * Smooth(0.6f, 0.85f, r);
                return Encode(m, groove * 0.5f);
            });
        }

        /// <summary>Soil/mulch: lumps, dark gaps between them, light crumbs.</summary>
        static Color32[] Soil(int size)
        {
            var chunks = new Cells(24, 91);
            var flecks = new Lattice(220, 220, 92);
            return Render(size, (u, v) =>
            {
                chunks.Sample(u, v, out float f1, out float f2, out int id);
                float edge = Smooth(0f, 0.1f, f2 - f1);
                float m = (0.92f + (Hash(id, 0, 10) - 0.5f) * 0.4f) * Mathf.Lerp(0.55f, 1f, edge);
                if (flecks.Sample(u, v) > 0.8f) m += 0.18f;
                return Encode(m, 1f - edge);
            });
        }

        static Color32[] Rubber(int size)
        {
            var grain = new Lattice(90, 90, 101);
            var mottle = new Fbm(6, 6, 2, 102);
            return Render(size, (u, v) => Encode(1f + (grain.Sample(u, v) - 0.5f) * 0.14f + (mottle.Sample(u, v) - 0.5f) * 0.08f));
        }

        /// <summary>Painted metal: horizontal brushing marks, fine scratches.</summary>
        static Color32[] Metal(int size)
        {
            var brushed = new Fbm(2, 48, 3, 111);
            var scratch = new Fbm(16, 2, 2, 112);
            var mottle = new Fbm(5, 5, 2, 113);
            return Render(size, (u, v) =>
            {
                float m = 1f + (brushed.Sample(u, v) - 0.5f) * 0.14f + (mottle.Sample(u, v) - 0.5f) * 0.07f;
                float ridge = 1f - Mathf.Abs(scratch.Sample(u, v) * 2f - 1f);
                m += 0.1f * Smooth(0.97f, 0.995f, ridge);
                return Encode(m);
            });
        }

        static Color32[] Plastic(int size)
        {
            var mottle = new Fbm(5, 5, 3, 121);
            var speck = new Lattice(150, 150, 122);
            return Render(size, (u, v) => Encode(1f + (mottle.Sample(u, v) - 0.5f) * 0.08f + (speck.Sample(u, v) - 0.5f) * 0.04f));
        }

        /// <summary>
        /// Leaf clusters with a soft brush: every cell is a cluster (light in the middle, darkening towards the edge), the gaps
        /// only lightly shaded; small leaf blotches on top.
        /// </summary>
        static Color32[] Foliage(int size)
        {
            var clumps = new Cells(10, 131);
            var dabs = new Fbm(24, 24, 2, 132);
            var mottle = new Fbm(3, 3, 3, 133);
            return Render(size, (u, v) =>
            {
                clumps.Sample(u, v, out float f1, out float f2, out int id);
                float edge = Smooth(0f, 0.22f, f2 - f1);
                float m = 0.9f + 0.2f * (1f - Smooth(0.05f, 0.8f, f1)) + (Hash(id, 0, 11) - 0.5f) * 0.12f;
                m *= Mathf.Lerp(0.84f, 1f, edge);
                m += (Smooth(0.45f, 0.75f, dabs.Sample(u, v)) - 0.4f) * 0.1f + (mottle.Sample(u, v) - 0.5f) * 0.1f;
                return Encode(m);
            });
        }

        static Color32[] Fabric(int size)
        {
            var grain = new Lattice(128, 128, 141);
            return Render(size, (u, v) =>
            {
                float weave = Mathf.Sin(u * Mathf.PI * 2f * 64f) * Mathf.Sin(v * Mathf.PI * 2f * 64f);
                return Encode(1f + weave * 0.05f + (grain.Sample(u, v) - 0.5f) * 0.1f);
            });
        }

        /// <summary>Glass: diagonal reflection bands, slight brightening upwards.</summary>
        static Color32[] Glass(int size)
        {
            var mottle = new Fbm(3, 3, 2, 151);
            return Render(size, (u, v) =>
            {
                float d = Frac(u + v);
                float band = Smooth(0.38f, 0.46f, d) * (1f - Smooth(0.5f, 0.6f, d)) + 0.5f * Smooth(0.66f, 0.69f, d) * (1f - Smooth(0.71f, 0.75f, d));
                float m = 0.94f + 0.22f * band + 0.06f * Mathf.Sin(v * Mathf.PI * 2f) + (mottle.Sample(u, v) - 0.5f) * 0.05f;
                return Encode(m);
            });
        }

        /// <summary>
        /// Grime mask (R): soft-edged blotches like paint stains, splash speckles, vertical streaks.
        /// G: large-scale tone variation (breaks up the pattern repeat), 0.5 = neutral.
        /// </summary>
        static Color32[] GrimeMask(int size)
        {
            var blotch = new Fbm(4, 4, 4, 161);
            var splatter = new Lattice(96, 96, 162);
            var drip = new Fbm(24, 3, 2, 163);
            var tone = new Fbm(3, 3, 3, 164);
            return Render(size, (u, v) =>
            {
                float g = Smooth(0.52f, 0.57f, blotch.Sample(u, v));
                g = Mathf.Max(g, Smooth(0.84f, 0.9f, splatter.Sample(u, v)) * 0.7f);
                g = Mathf.Max(g, Smooth(0.6f, 0.7f, drip.Sample(u, v)) * 0.45f);
                float t = Mathf.Clamp01(0.5f + (tone.Sample(u, v) - 0.5f) * 1.6f);
                return new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255f), (byte)Mathf.RoundToInt(t * 255f), 0, 255);
            });
        }
    }
}
