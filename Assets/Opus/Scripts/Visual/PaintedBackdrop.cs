using System.Collections.Generic;
using Opus.Core;
using Opus.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Opus.Visual
{
    /// <summary>
    /// The scenery outside the garden: a dark green cypress row (3D) right behind the fence and, further away,
    /// watercolour tree cards painted from code (orange, mustard, teal, green). The cards face the middle of the
    /// garden; since the player is always in the garden, it is not noticeable that they are flat cards.
    /// </summary>
    public static class PaintedBackdrop
    {
        public const int TextureSize = 256;
        public const string ShaderName = "Opus/PaintedCard";

        /// <summary>Distant tree tones in the reference (measured + by eye).</summary>
        static readonly Color[] TreeColors =
        {
            Palette.Hex("#D8A23E"), Palette.Hex("#C98A3C"), Palette.Hex("#5F9A8C"), Palette.Hex("#86AA5C"),
            Palette.Hex("#6E8F45"), Palette.Hex("#B9B04A"), Palette.Hex("#4F7E6E"),
        };
        static readonly Color[] CypressColors = { Palette.Hex("#3E6838"), Palette.Hex("#47733F"), Palette.Hex("#365E36") };

        public static int CardCount { get; private set; }
        static readonly Dictionary<int, Texture2D> PaintedCache = new Dictionary<int, Texture2D>();

        /// <param name="yard">XZ rectangle of the garden (all areas); the scenery is built outside it and around its middle.</param>
        public static Transform Build(Transform parent, Rect yard)
        {
            Transform root = new GameObject("Scenery").transform;
            root.SetParent(parent, false);
            BuildCypressRow(root, yard);
            BuildCards(root, yard);
            return root;
        }

        /// <summary>Horizontal distance from the point to the rectangle (0 if inside).</summary>
        public static float DistanceToRect(Rect rect, Vector2 point)
        {
            float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
            float dz = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ---------- Cypress row ----------

        static void BuildCypressRow(Transform root, Rect yard)
        {
            Transform row = new GameObject("Cypresses").transform;
            row.SetParent(root, false);
            var random = new System.Random(13);
            float Next() => (float)random.NextDouble();

            // Just outside the north (both sides of the house), west and east fences, spaced out
            var spots = new List<Vector3>();
            for (float x = yard.xMin + 1f; x <= yard.xMax - 1f; x += 1.7f + Next() * 0.6f)
                if (Mathf.Abs(x) > 9.5f) spots.Add(new Vector3(x, 0f, yard.yMax + 1.4f + Next() * 0.8f));
            // On the sides only in the north half, sparse (in the reference mostly painted trees are seen beyond the fence)
            for (float z = 3f; z <= yard.yMax; z += 2.4f + Next() * 1.2f)
            {
                spots.Add(new Vector3(yard.xMin - 1.4f - Next() * 0.8f, 0f, z));
                spots.Add(new Vector3(yard.xMax + 1.4f + Next() * 0.8f, 0f, z));
            }

            foreach (Vector3 spot in spots)
            {
                float height = Mathf.Lerp(4.5f, 7.5f, Next());
                float width = Mathf.Lerp(1.2f, 1.7f, Next());
                Color color = CypressColors[random.Next(CypressColors.Length)];
                Transform tree = new GameObject("Cypress").transform;
                tree.SetParent(row, false);
                tree.localPosition = spot;
                // Overlapping ellipsoids: a column with a wide base and a pointed tip
                int lumps = 4;
                for (int i = 0; i < lumps; i++)
                {
                    float t = (i + 0.5f) / lumps;
                    float w = width * Mathf.Lerp(1f, 0.45f, t) * Mathf.Lerp(0.9f, 1.1f, Next());
                    PrimitiveKit.Sphere(tree, "Body", new Vector3((Next() - 0.5f) * 0.15f, height * t, (Next() - 0.5f) * 0.15f),
                        new Vector3(w, height / lumps * 1.7f, w), Color.Lerp(color, Palette.Fog, t * 0.08f).As(SurfaceKind.Foliage));
                }
            }
        }

        // ---------- Painted cards ----------

        static void BuildCards(Transform root, Rect yard)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[Opus] {ShaderName} shader not found.");
                return;
            }

            const int variants = 6;
            var materials = new Material[variants];
            for (int v = 0; v < variants; v++)
            {
                // The painted textures are not repainted when the garden changes (the scene reloads)
                int seed = 1000 + v * 37;
                if (!PaintedCache.TryGetValue(seed, out Texture2D texture) || texture == null)
                    PaintedCache[seed] = texture = PaintTree(seed, TreeColors[v % TreeColors.Length], v % 3 == 2);
                materials[v] = new Material(shader) { name = "PaintedTree" + v, mainTexture = texture };
            }

            Transform cards = new GameObject("PaintedTrees").transform;
            cards.SetParent(root, false);
            Mesh quad = BuildQuad();
            var random = new System.Random(29);
            float Next() => (float)random.NextDouble();
            Vector2 center = yard.center;
            Vector2 half = yard.size * 0.5f;

            int count = 0;
            // Two nested belts around the garden rectangle: near (9–22 m from the fence) larger, far (30–58 m) paler
            for (int ring = 0; ring < 2; ring++)
            {
                int perRing = ring == 0 ? 44 : 50;
                for (int i = 0; i < perRing; i++)
                {
                    float angle = (i + Next() * 0.8f) / perRing * Mathf.PI * 2f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    // Distance to the garden edge in this direction + belt margin
                    float edge = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(direction.x), 1e-4f), half.y / Mathf.Max(Mathf.Abs(direction.y), 1e-4f));
                    float offset = ring == 0 ? Mathf.Lerp(9f, 22f, Next()) : Mathf.Lerp(30f, 58f, Next());
                    Vector2 flat = center + direction * (edge + offset);
                    if (DistanceToRect(yard, flat) < 4f)
                        continue;
                    var position = new Vector3(flat.x, -0.4f, flat.y);
                    // The back of the house (north) is already closed; fewer cards there
                    if (position.z > yard.yMax + 4f && Mathf.Abs(position.x) < 10f && Next() < 0.6f)
                        continue;
                    float height = Mathf.Lerp(7f, 13f, Next()) * (ring == 0 ? 1f : 1.25f);
                    var go = new GameObject("PaintedTree");
                    go.transform.SetParent(cards, false);
                    go.transform.localPosition = position;
                    Vector3 fromCenter = new Vector3(flat.x - center.x, 0f, flat.y - center.y);
                    go.transform.localRotation = Quaternion.LookRotation(fromCenter.normalized, Vector3.up) * Quaternion.Euler(0f, (Next() - 0.5f) * 20f, 0f);
                    go.transform.localScale = new Vector3(height * Mathf.Lerp(0.8f, 1.15f, Next()), height, 1f);
                    go.AddComponent<MeshFilter>().sharedMesh = quad;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials[random.Next(variants)];
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    count++;
                }
            }
            CardCount = count;
        }

        /// <summary>A vertical unit quad with its base in the middle.</summary>
        static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "TreeCard" };
            mesh.SetVertices(new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f) });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            mesh.SetNormals(new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Watercolour tree: a crown from the union of a few circles (edge torn by noise), a short trunk. Light top left,
        /// dark bottom right; paint pooling at the edge; paper texture. The top is pointed (<paramref name="pointed"/>) or round.
        /// </summary>
        public static Texture2D PaintTree(int seed, Color baseColor, bool pointed, bool keepReadable = false)
        {
            const int size = TextureSize;
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            float ox = seed * 0.37f, oz = seed * 0.71f;

            // Crown circles (uv 0..1)
            int blobs = pointed ? 5 : 7;
            var centers = new Vector2[blobs];
            var radii = new float[blobs];
            for (int i = 0; i < blobs; i++)
            {
                float t = (float)i / (blobs - 1);
                float y = pointed ? Mathf.Lerp(0.3f, 0.82f, t) : 0.35f + Next() * 0.4f;
                float spread = pointed ? Mathf.Lerp(0.18f, 0.04f, t) : 0.22f;
                centers[i] = new Vector2(0.5f + (Next() - 0.5f) * 2f * spread, y);
                radii[i] = pointed ? Mathf.Lerp(0.22f, 0.1f, t) : Mathf.Lerp(0.15f, 0.24f, Next());
            }

            var pixels = new Color32[size * size];
            Color light = Color.Lerp(baseColor, Color.white, 0.22f);
            Color dark = Color.Lerp(baseColor, new Color(0.25f, 0.22f, 0.3f), 0.35f);
            Color trunk = Palette.Hex("#6B5848");
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float field = -1f;
                    for (int i = 0; i < blobs; i++)
                        field = Mathf.Max(field, 1f - Vector2.Distance(uv, centers[i]) / radii[i]);
                    // Torn edge
                    float edgeNoise = (Mathf.PerlinNoise(uv.x * 9f + ox, uv.y * 9f + oz) - 0.5f) * 0.5f
                                      + (Mathf.PerlinNoise(uv.x * 23f + oz, uv.y * 23f + ox) - 0.5f) * 0.25f;
                    float f = field + edgeNoise;
                    float alpha = Mathf.Clamp01(f * 6f + 0.5f);

                    // Trunk: a thin vertical line under the crown
                    float trunkAlpha = 0f;
                    if (uv.y < 0.42f && Mathf.Abs(uv.x - 0.5f - (uv.y - 0.2f) * 0.05f) < 0.018f + (0.42f - uv.y) * 0.02f)
                        trunkAlpha = Mathf.Clamp01((uv.y - 0.02f) * 20f);

                    Color color;
                    if (alpha > trunkAlpha)
                    {
                        // Light from the top left: light-dark transition + inner blotches + paint pooling at the edge
                        float lightness = Mathf.Clamp01(0.55f + (uv.y - 0.55f) * 0.9f - (uv.x - 0.45f) * 0.6f);
                        float blotch = Mathf.PerlinNoise(uv.x * 6f + oz, uv.y * 6f + ox);
                        color = Color.Lerp(dark, light, Mathf.Clamp01(lightness * 0.8f + blotch * 0.4f - 0.1f));
                        float rim = Mathf.Clamp01(1f - f * 5f);
                        color = Color.Lerp(color, dark, rim * 0.35f);
                    }
                    else
                    {
                        color = trunk;
                    }
                    float grain = 1f + (Mathf.PerlinNoise(uv.x * 90f + ox, uv.y * 90f) - 0.5f) * 0.08f;
                    color *= grain;
                    color.a = Mathf.Max(alpha, trunkAlpha);
                    pixels[y * size + x] = color;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "PaintedTreeTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 2,
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, !keepReadable);
            return texture;
        }
    }
}
