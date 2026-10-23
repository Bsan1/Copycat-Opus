using System.Collections.Generic;
using UnityEngine;

namespace Opus.World
{
    /// <summary>
    /// Helpers for simple hand-built models: box, cylinder, sphere, cone, triangular prism (roof).
    /// All use the "Opus/Surface" shader with one material per surface (kind + colour); a flat colour becomes a painted surface.
    /// </summary>
    public static class PrimitiveKit
    {
        /// <summary>Old flat-colour shader: GPU-instanced flowers and the icon ink.</summary>
        public const string ShaderName = "Opus/Primitive";

        static Shader flatShader;
        static readonly Dictionary<Surface, Material> Materials = new Dictionary<Surface, Material>();
        static readonly Dictionary<Color, Material> FlatMaterials = new Dictionary<Color, Material>();
        static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();

        static SurfaceKind defaultKind = SurfaceKind.Paint;

        /// <summary>
        /// Parts given a flat colour (Paint) within the scope get this kind: <c>using (PrimitiveKit.DefaultKind(SurfaceKind.Plant))</c>.
        /// Saves writing a separate kind on every call for small items and plants near the ground.
        /// </summary>
        public static DefaultKindScope DefaultKind(SurfaceKind kind) => new DefaultKindScope(kind);

        public readonly struct DefaultKindScope : System.IDisposable
        {
            readonly SurfaceKind previous;

            public DefaultKindScope(SurfaceKind kind)
            {
                previous = defaultKind;
                defaultKind = kind;
            }

            public void Dispose() => defaultKind = previous;
        }

        /// <summary>Painted surface material (one per kind, colour and scale; merging groups by these).</summary>
        public static Material MaterialFor(Surface surface)
        {
            if (surface.Kind == SurfaceKind.Paint && defaultKind != SurfaceKind.Paint)
                surface = new Surface(defaultKind, surface.Color, surface.Scale);
            if (Materials.TryGetValue(surface, out Material cached) && cached != null)
                return cached;
            Material material = SurfaceLibrary.Create(surface);
            Materials[surface] = material;
            return material;
        }

        /// <summary>Plain flat-colour material without a pattern (Opus/Primitive).</summary>
        public static Material FlatMaterialFor(Color color)
        {
            if (FlatMaterials.TryGetValue(color, out Material cached) && cached != null)
                return cached;

            if (flatShader == null)
            {
                flatShader = Shader.Find(ShaderName);
                if (flatShader == null)
                {
                    Debug.LogError($"[Opus] {ShaderName} shader not found.");
                    flatShader = Shader.Find("Universal Render Pipeline/Unlit");
                }
            }

            var material = new Material(flatShader) { name = "OpusFlat_" + ColorUtility.ToHtmlStringRGB(color) };
            material.SetColor("_BaseColor", color);
            FlatMaterials[color] = material;
            return material;
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Surface surface, float yaw = 0f, bool collider = true)
        {
            GameObject go = Primitive(PrimitiveType.Cube, parent, name, surface, collider);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            return go;
        }

        /// <summary>Cylinder going up from the centre of its base.</summary>
        public static GameObject Cylinder(Transform parent, string name, Vector3 baseCenter, float radius, float height, Surface surface, bool collider = true)
        {
            GameObject go = Primitive(PrimitiveType.Cylinder, parent, name, surface, collider);
            go.transform.localPosition = baseCenter + new Vector3(0f, height * 0.5f, 0f);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            return go;
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 center, Vector3 size, Surface surface, bool collider = false)
        {
            GameObject go = Primitive(PrimitiveType.Sphere, parent, name, surface, collider);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            return go;
        }

        /// <summary>Flat-shaded (low-poly) cone; for pine trees.</summary>
        public static GameObject Cone(Transform parent, string name, Vector3 baseCenter, float radius, float height, Surface surface, int segments = 9)
        {
            Mesh mesh = CachedMesh($"cone{segments}", () => BuildCone(segments));
            GameObject go = MeshObject(parent, name, mesh, surface);
            go.transform.localPosition = baseCenter;
            go.transform.localScale = new Vector3(radius, height, radius);
            return go;
        }

        /// <summary>
        /// Flat-faced, squashed octahedral leaf (8 triangles): much cheaper than a sphere for bushes/weeds needing hundreds of leaves.
        /// Size: X width, Y thickness, Z length. It has no collider.
        /// </summary>
        public static GameObject Leaf(Transform parent, string name, Vector3 center, Vector3 size, Surface surface)
        {
            Mesh mesh = CachedMesh("leaf", BuildLeaf);
            GameObject go = MeshObject(parent, name, mesh, surface);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            return go;
        }

        /// <summary>Gable roof: X width, Y height, Z depth; from the centre of its base.</summary>
        public static GameObject Prism(Transform parent, string name, Vector3 baseCenter, Vector3 size, Surface surface, float yaw = 0f)
        {
            Mesh mesh = CachedMesh("prism", BuildPrism);
            GameObject go = MeshObject(parent, name, mesh, surface);
            go.transform.localPosition = baseCenter;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            return go;
        }

        /// <summary>A thin tube between two points (stem, rod).</summary>
        public static GameObject Tube(Transform parent, string name, Vector3 from, Vector3 to, float radius, Surface surface)
        {
            GameObject go = Primitive(PrimitiveType.Cylinder, parent, name, surface, false);
            Vector3 span = to - from;
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, span.normalized);
            go.transform.localScale = new Vector3(radius * 2f, span.magnitude * 0.5f, radius * 2f);
            return go;
        }

        /// <summary>
        /// Extrudes a convex outline in the XZ plane between y0..y1 (capped, sides smooth-shaded).
        /// </summary>
        public static GameObject Extrude(Transform parent, string name, IList<Vector2> outline, float y0, float y1, Surface surface)
        {
            GameObject go = MeshObject(parent, name, BuildExtrusion(outline, y0, y1), surface);
            return go;
        }

        /// <summary>Rounded-rectangle outline (centred on the origin).</summary>
        public static List<Vector2> RoundedRect(float halfWidth, float halfLength, float radius, int cornerSegments = 4)
        {
            radius = Mathf.Min(radius, Mathf.Min(halfWidth, halfLength));
            var points = new List<Vector2>();
            Vector2[] centers =
            {
                new Vector2(halfWidth - radius, halfLength - radius),
                new Vector2(-halfWidth + radius, halfLength - radius),
                new Vector2(-halfWidth + radius, -halfLength + radius),
                new Vector2(halfWidth - radius, -halfLength + radius),
            };
            for (int c = 0; c < 4; c++)
            {
                for (int s = 0; s <= cornerSegments; s++)
                {
                    float angle = (c * 90f + s * 90f / cornerSegments) * Mathf.Deg2Rad;
                    points.Add(centers[c] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                }
            }
            return points;
        }

        static Mesh BuildExtrusion(IList<Vector2> outline, float y0, float y1)
        {
            var points = new List<Vector2>(outline);
            float area = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count];
                area += a.x * b.y - b.x * a.y;
            }
            if (area < 0f)
                points.Reverse(); // counter-clockwise (seen from above)

            int n = points.Count;
            Vector2 centroid = Vector2.zero;
            foreach (Vector2 p in points) centroid += p;
            centroid /= n;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();

            // Caps
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? y1 : y0;
                Vector3 normal = cap == 0 ? Vector3.up : Vector3.down;
                int start = vertices.Count;
                vertices.Add(new Vector3(centroid.x, y, centroid.y));
                normals.Add(normal);
                foreach (Vector2 p in points)
                {
                    vertices.Add(new Vector3(p.x, y, p.y));
                    normals.Add(normal);
                }
                for (int i = 0; i < n; i++)
                {
                    int a = start + 1 + i, b = start + 1 + (i + 1) % n;
                    if (cap == 0) { triangles.Add(start); triangles.Add(b); triangles.Add(a); }
                    else { triangles.Add(start); triangles.Add(a); triangles.Add(b); }
                }
            }

            // Sides: the corner normal is the average of the outer normals of the two neighbouring edges.
            int sideStart = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = points[(i - 1 + n) % n], current = points[i], next = points[(i + 1) % n];
                Vector2 n0 = EdgeNormal(prev, current), n1 = EdgeNormal(current, next);
                Vector2 smooth = (n0 + n1).normalized;
                var normal = new Vector3(smooth.x, 0f, smooth.y);
                vertices.Add(new Vector3(current.x, y0, current.y));
                normals.Add(normal);
                vertices.Add(new Vector3(current.x, y1, current.y));
                normals.Add(normal);
            }
            for (int i = 0; i < n; i++)
            {
                int ib = sideStart + i * 2, it = ib + 1;
                int jb = sideStart + (i + 1) % n * 2, jt = jb + 1;
                triangles.Add(ib); triangles.Add(it); triangles.Add(jt);
                triangles.Add(ib); triangles.Add(jt); triangles.Add(jb);
            }

            var mesh = new Mesh { name = "Extrusion" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector2 EdgeNormal(Vector2 from, Vector2 to)
        {
            Vector2 d = (to - from).normalized;
            return new Vector2(d.y, -d.x);
        }

        static GameObject Primitive(PrimitiveType type, Transform parent, string name, Surface surface, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            if (!collider)
            {
                // During play Destroy waits for the end of the frame; collision queries made in that same frame
                // (such as the lawn's under-obstacle scan) must not see this decoration.
                Collider unwanted = go.GetComponent<Collider>();
                unwanted.enabled = false;
                DestroySafe(unwanted);
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(surface);
            return go;
        }

        static GameObject MeshObject(Transform parent, string name, Mesh mesh, Surface surface)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = MaterialFor(surface);
            return go;
        }

        static Mesh CachedMesh(string key, System.Func<Mesh> build)
        {
            if (!Meshes.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = build();
                mesh.name = key;
                Meshes[key] = mesh;
            }
            return mesh;
        }

        // Unit cone: base radius 1, height 1.
        static Mesh BuildCone(int segments)
        {
            var builder = new FlatMeshBuilder(new Vector3(0f, 0.35f, 0f));
            var apex = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                builder.Triangle(apex, p0, p1);
                builder.Triangle(Vector3.zero, p1, p0);
            }
            return builder.Build();
        }

        // Unit leaf: octahedron with a slightly raised midrib and pointed tips.
        static Mesh BuildLeaf()
        {
            var builder = new FlatMeshBuilder(Vector3.zero);
            var tip = new Vector3(0f, 0.15f, 0.5f);
            var stem = new Vector3(0f, 0f, -0.5f);
            var left = new Vector3(-0.5f, 0f, 0.05f);
            var right = new Vector3(0.5f, 0f, 0.05f);
            var top = new Vector3(0f, 0.5f, 0f);
            var bottom = new Vector3(0f, -0.5f, 0f);
            foreach (Vector3 side in new[] { left, right })
            {
                builder.Triangle(top, tip, side);
                builder.Triangle(top, side, stem);
                builder.Triangle(bottom, tip, side);
                builder.Triangle(bottom, side, stem);
            }
            return builder.Build();
        }

        // Unit prism: base -0.5..0.5 (X), apex y=1, depth -0.5..0.5 (Z).
        static Mesh BuildPrism()
        {
            var builder = new FlatMeshBuilder(new Vector3(0f, 0.33f, 0f));
            var lf = new Vector3(-0.5f, 0f, -0.5f);
            var rf = new Vector3(0.5f, 0f, -0.5f);
            var tf = new Vector3(0f, 1f, -0.5f);
            var lb = new Vector3(-0.5f, 0f, 0.5f);
            var rb = new Vector3(0.5f, 0f, 0.5f);
            var tb = new Vector3(0f, 1f, 0.5f);
            builder.Triangle(lf, rf, tf);
            builder.Triangle(lb, rb, tb);
            builder.Quad(lf, tf, tb, lb);
            builder.Quad(rf, tf, tb, rb);
            builder.Quad(lf, rf, rb, lb);
            return builder.Build();
        }

        static void DestroySafe(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        /// <summary>
        /// Mesh builder giving every triangle its own vertices (flat-shaded). The triangle winding is corrected automatically
        /// to face away from the shape's centre; so it only suits convex shapes.
        /// </summary>
        sealed class FlatMeshBuilder
        {
            readonly Vector3 center;
            readonly List<Vector3> vertices = new List<Vector3>();

            public FlatMeshBuilder(Vector3 center) { this.center = center; }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 outward = (a + b + c) / 3f - center;
                if (Vector3.Dot(normal, outward) < 0f)
                    (b, c) = (c, b);
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(a, b, c);
                Triangle(a, c, d);
            }

            public Mesh Build()
            {
                var indices = new int[vertices.Count];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                var mesh = new Mesh();
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
