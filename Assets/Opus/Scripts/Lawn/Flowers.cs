using System.Collections.Generic;
using Opus.Core;
using Opus.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Opus.Lawn
{
    /// <summary>
    /// Small flowers (white, yellow, lilac) that open on finished patches. Shortly after the finish wave reaches a point,
    /// the flower there grows and opens. Drawn with GPU instancing (one call per colour).
    /// </summary>
    public sealed class Flowers : MonoBehaviour
    {
        public const float PerSquareMeter = 2.4f;
        const float GrowSeconds = 0.6f;
        const float Delay = 0.2f;

        sealed class Instance
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
            public float BloomTime;
        }

        /// <summary>An area's flowers: instances per colour and (once all are open) cached matrices.</summary>
        sealed class Group
        {
            public readonly List<Instance>[] ByColor = { new List<Instance>(), new List<Instance>(), new List<Instance>() };
            public readonly List<Matrix4x4>[] Matrices = { new List<Matrix4x4>(), new List<Matrix4x4>(), new List<Matrix4x4>() };
            public Bounds Bounds;
            public bool HasBounds;
            /// <summary>All open: the matrices no longer change and are not recomputed every frame.</summary>
            public bool Settled;
            public int Open;
        }

        static readonly Color[] HeadColors = { Palette.Hex("#F5F2EC"), Palette.Hex("#F2D04A"), Palette.Hex("#B9A3E0") };
        static readonly Color StemColor = Palette.Hex("#6C9A3A");

        readonly SortedDictionary<int, Group> groups = new SortedDictionary<int, Group>();
        readonly System.Random random = new System.Random(41);
        Mesh headMesh;
        Mesh stemMesh;
        Material[] headMaterials;
        Material stemMaterial;

        public int Count { get; private set; }
        public int OpenCount { get; private set; }
        /// <summary>Number of flowers drawn (not occluded) in the last frame.</summary>
        public int DrawnCount { get; private set; }
        public Camera ViewCamera { get; set; }
        /// <summary>Occlusion test (eye, the area's flower bounds → not visible?).</summary>
        public System.Func<Vector3, Bounds, bool> IsHidden { get; set; }

        public static Flowers Create(Transform parent)
        {
            var go = new GameObject("Flowers");
            go.transform.SetParent(parent, false);
            var flowers = go.AddComponent<Flowers>();
            flowers.Build();
            return flowers;
        }

        void Build()
        {
            headMesh = BuildHead();
            stemMesh = BuildStem();
            // In a build, instancing shader variants only survive if a material asset asks for them (Setup creates it).
            Material template = Resources.Load<Material>(InstancedMaterialResource);
            if (template == null)
            {
                Debug.LogError($"[Opus] Flower material not found: Resources/{InstancedMaterialResource}");
                template = PrimitiveKit.FlatMaterialFor(Color.white);
            }
            headMaterials = new Material[HeadColors.Length];
            for (int i = 0; i < HeadColors.Length; i++)
                headMaterials[i] = Colored(template, HeadColors[i]);
            stemMaterial = Colored(template, StemColor);
        }

        public const string InstancedMaterialResource = "Materials/PrimitiveInstanced";

        static Material Colored(Material template, Color color)
        {
            var material = new Material(template) { enableInstancing = true };
            material.SetColor("_BaseColor", color);
            return material;
        }

        /// <summary>Removes all flowers (while loading a save).</summary>
        public void Clear()
        {
            groups.Clear();
            Count = 0;
            OpenCount = 0;
        }

        /// <summary>Adds flowers to the patch already open (restoring from a save).</summary>
        public void BloomInstant(LawnState lawn, PatchProgress patch) => Bloom(lawn, patch, -1000f, 1000f);

        /// <summary>Adds flowers to the patch; each opens when the finish wave gets there.</summary>
        public void Bloom(LawnState lawn, PatchProgress patch, float waveStart, float waveSpeed)
        {
            if (!groups.TryGetValue(patch.Area, out Group group))
                groups[patch.Area] = group = new Group();
            group.Settled = false;

            LawnGrid grid = lawn.Grid;
            byte[] ids = lawn.Layout.PatchIds;
            int target = Mathf.RoundToInt(patch.AreaSquareMeters * PerSquareMeter);
            float Next() => (float)random.NextDouble();
            int attempts = 0;
            int placed = 0;
            while (placed < target && attempts++ < target * 30)
            {
                // A random point in the patch's bounding box; accepted if it falls in the patch
                Vector2 point = patch.Centroid + new Vector2(Next() * 2f - 1f, Next() * 2f - 1f) * patch.Radius;
                if (!grid.TryGetCell(point, out int x, out int z) || ids[grid.Index(x, z)] != patch.Id)
                    continue;
                var instance = new Instance
                {
                    Position = new Vector3(point.x, 0f, point.y),
                    Rotation = Quaternion.Euler(Next() * 12f - 6f, Next() * 360f, Next() * 12f - 6f),
                    Scale = Mathf.Lerp(0.8f, 1.25f, Next()),
                    BloomTime = waveStart + Vector2.Distance(point, patch.Centroid) / waveSpeed + Delay + Next() * 0.25f,
                };
                group.ByColor[random.Next(group.ByColor.Length)].Add(instance);
                // No horizontal margin: the box of a flower at the area border must not spill over the fence line (occlusion test)
                var around = new Bounds(instance.Position + Vector3.up * 0.1f, new Vector3(0f, 0.3f, 0f));
                if (group.HasBounds) group.Bounds.Encapsulate(around);
                else group.Bounds = around;
                group.HasBounds = true;
                placed++;
            }
            Count += placed;
        }

        void LateUpdate()
        {
            if (Count == 0)
            {
                DrawnCount = 0;
                return;
            }
            float now = Time.timeSinceLevelLoad;
            Vector3 eye = ViewCamera != null ? ViewCamera.transform.position : Vector3.zero;
            int open = 0, drawn = 0;
            foreach (Group group in groups.Values)
            {
                if (!group.Settled)
                    Rebuild(group, now);
                open += group.Open;
                if (ViewCamera != null && IsHidden != null && group.HasBounds && IsHidden(eye, group.Bounds))
                    continue;
                for (int c = 0; c < group.Matrices.Length; c++)
                {
                    List<Matrix4x4> matrices = group.Matrices[c];
                    if (matrices.Count == 0)
                        continue;
                    Draw(headMesh, headMaterials[c], matrices);
                    Draw(stemMesh, stemMaterial, matrices);
                    drawn += matrices.Count;
                }
            }
            OpenCount = open;
            DrawnCount = drawn;
        }

        static void Rebuild(Group group, float now)
        {
            int open = 0, total = 0;
            for (int c = 0; c < group.ByColor.Length; c++)
            {
                List<Matrix4x4> matrices = group.Matrices[c];
                matrices.Clear();
                foreach (Instance f in group.ByColor[c])
                {
                    total++;
                    float t = Mathf.Clamp01((now - f.BloomTime) / GrowSeconds);
                    if (t <= 0f)
                        continue;
                    if (t >= 1f) open++;
                    // Growth with a slight overshoot
                    float grow = t < 1f ? Mathf.Sin(t * Mathf.PI * 0.62f) / Mathf.Sin(Mathf.PI * 0.62f) : 1f;
                    matrices.Add(Matrix4x4.TRS(f.Position, f.Rotation, Vector3.one * (f.Scale * grow)));
                }
            }
            group.Open = open;
            group.Settled = open == total;
        }

        static void Draw(Mesh mesh, Material material, List<Matrix4x4> matrices)
        {
            var renderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
            };
            for (int start = 0; start < matrices.Count; start += 1023)
            {
                int count = Mathf.Min(1023, matrices.Count - start);
                Graphics.RenderMeshInstanced(renderParams, mesh, 0, matrices, count, start);
            }
        }

        /// <summary>Five-petal flower head (double-sided), ~13 cm above the ground.</summary>
        static Mesh BuildHead()
        {
            const float height = 0.15f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                if (n.y < 0f) { (b, c) = (c, b); n = -n; }
                foreach (Vector3 side in new[] { n, -n })
                {
                    int s = vertices.Count;
                    vertices.Add(a); vertices.Add(b); vertices.Add(c);
                    normals.Add(side); normals.Add(side); normals.Add(side);
                    if (side == n) { triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2); }
                    else { triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 1); }
                }
            }
            var center = new Vector3(0f, height + 0.005f, 0f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0f, dir.x);
                Vector3 baseL = center + side * 0.012f, baseR = center - side * 0.012f;
                Vector3 mid = center + dir * 0.038f + Vector3.up * 0.006f;
                Vector3 midL = mid + side * 0.02f, midR = mid - side * 0.02f;
                Vector3 tip = center + dir * 0.066f - Vector3.up * 0.004f;
                Tri(baseL, midL, midR); Tri(baseL, midR, baseR); Tri(midL, tip, midR);
            }
            // Centre: a small pyramid
            for (int i = 0; i < 5; i++)
            {
                float a0 = i * Mathf.PI * 2f / 5f, a1 = (i + 1) * Mathf.PI * 2f / 5f;
                Tri(center + Vector3.up * 0.014f, center + new Vector3(Mathf.Cos(a0), 0.003f, Mathf.Sin(a0)) * 0.016f,
                    center + new Vector3(Mathf.Cos(a1), 0.003f, Mathf.Sin(a1)) * 0.016f);
            }
            var mesh = new Mesh { name = "FlowerHead" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildStem()
        {
            const float height = 0.15f, w = 0.004f;
            var mesh = new Mesh { name = "FlowerStem" };
            mesh.SetVertices(new[]
            {
                new Vector3(-w, 0f, 0f), new Vector3(w, 0f, 0f), new Vector3(w, height, 0f), new Vector3(-w, height, 0f),
                new Vector3(0f, 0f, -w), new Vector3(0f, 0f, w), new Vector3(0f, height, w), new Vector3(0f, height, -w),
            });
            mesh.SetNormals(new[]
            {
                Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.right, Vector3.right, Vector3.right, Vector3.right,
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 4, 5, 6, 4, 6, 7 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
