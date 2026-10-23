using System.Collections.Generic;
using UnityEngine;

namespace Opus.Lawn
{
    /// <summary>
    /// A single grass tuft: a few blades spread in a small circle. The real shape of the blades
    /// (height, bend, wind, colour) is computed in the Opus/Grass shader; the mesh only carries this data:
    /// position.xz = the blade's base point within the tuft,
    /// uv0 = (across the width 0..1, along the length 0..1),
    /// uv1 = (direction 0..1, height multiplier, natural bend, width multiplier),
    /// uv2 = (tone offset 0..1, -, -, -).
    /// </summary>
    public static class TuftMesh
    {
        public const int Segments = 4;

        /// <param name="segments">Segments per blade (2 in the far LOD: less than half the vertices).</param>
        /// <param name="widthScale">
        /// Blade width multiplier. When fewer blades are asked for with the same seed the first blades come out the same (every blade
        /// draws the same number of values from the random sequence); the far mesh is a subset of the near mesh and covers the missing blades by widening.
        /// </param>
        public static Mesh Build(int blades = 10, float radius = 0.085f, int seed = 3, int segments = Segments, float widthScale = 1f)
        {
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();

            var positions = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var uv2 = new List<Vector4>();
            var triangles = new List<int>();

            for (int b = 0; b < blades; b++)
            {
                float r = radius * Mathf.Sqrt(Next());
                float a = Next() * Mathf.PI * 2f;
                var root = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                var blade = new Vector4(Next(), Mathf.Lerp(0.65f, 1.15f, Next()), Mathf.Lerp(0.15f, 0.9f, Next()), Mathf.Lerp(0.8f, 1.25f, Next()) * widthScale);
                var extra = new Vector4(Next(), 0f, 0f, 0f);

                int start = positions.Count;
                for (int s = 0; s < segments; s++)
                {
                    float t = (float)s / segments;
                    for (int side = 0; side < 2; side++)
                    {
                        positions.Add(root);
                        uv0.Add(new Vector2(side, t));
                        uv1.Add(blade);
                        uv2.Add(extra);
                    }
                }
                positions.Add(root);
                uv0.Add(new Vector2(0.5f, 1f));
                uv1.Add(blade);
                uv2.Add(extra);

                for (int s = 0; s < segments - 1; s++)
                {
                    int l0 = start + s * 2, r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3;
                    triangles.Add(l0); triangles.Add(l1); triangles.Add(r1);
                    triangles.Add(l0); triangles.Add(r1); triangles.Add(r0);
                }
                int lastL = start + (segments - 1) * 2, tip = start + segments * 2;
                triangles.Add(lastL); triangles.Add(tip); triangles.Add(lastL + 1);
            }

            var mesh = new Mesh { name = $"CimTutami_{blades}x{segments}" };
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetTriangles(triangles, 0);
            // The real position is computed in the shader; the bounds are given in the draw call.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
