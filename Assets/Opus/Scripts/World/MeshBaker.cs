using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Opus.World
{
    /// <summary>
    /// Merges a moving model built from many small parts (weed, ivy) into a single submesh per material:
    /// hundreds of draw calls drop to a few. If the parts are ordered, <see cref="ShowFirst"/> draws only the first K
    /// parts (the submesh index counts are shortened; vertex data stays the same).
    /// </summary>
    public sealed class BakedParts
    {
        readonly int[] indexStart;
        /// <summary>[submesh][k] = index count of the first k parts in that submesh.</summary>
        readonly int[][] indexCountAfter;

        public Mesh Mesh { get; }
        public MeshRenderer Renderer { get; }
        public int PartCount { get; }
        public int VisibleParts { get; private set; }
        public int SubMeshCount => indexStart.Length;

        internal BakedParts(Mesh mesh, MeshRenderer renderer, int[] indexStart, int[][] indexCountAfter, int partCount)
        {
            Mesh = mesh;
            Renderer = renderer;
            this.indexStart = indexStart;
            this.indexCountAfter = indexCountAfter;
            PartCount = partCount;
            VisibleParts = partCount;
        }

        /// <summary>Draws only the first <paramref name="parts"/> parts in order.</summary>
        public void ShowFirst(int parts)
        {
            parts = Mathf.Clamp(parts, 0, PartCount);
            if (parts == VisibleParts)
                return;
            VisibleParts = parts;
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;
            for (int g = 0; g < indexStart.Length; g++)
                Mesh.SetSubMesh(g, new SubMeshDescriptor(indexStart[g], indexCountAfter[g][parts]), flags);
        }

        /// <summary>Total index count of the first <paramref name="parts"/> parts (for tests).</summary>
        public int IndexCountFor(int parts)
        {
            int total = 0;
            for (int g = 0; g < indexCountAfter.Length; g++)
                total += indexCountAfter[g][Mathf.Clamp(parts, 0, PartCount)];
            return total;
        }
    }

    public static class MeshBaker
    {
        /// <summary>
        /// Merges the parts under <paramref name="root"/> in the root's local space, deletes the source objects and
        /// adds a single renderer under the root. If <paramref name="parts"/> is given, each one (with its subtree) is the next
        /// part in order; otherwise the whole model is a single part.
        /// </summary>
        public static BakedParts Bake(Transform root, IReadOnlyList<Transform> parts = null, string name = "Combined",
            ShadowCastingMode shadows = ShadowCastingMode.On)
        {
            var partRoots = parts ?? new[] { root };
            var materials = new List<Material>();
            var groupIndices = new List<List<int>>();
            var groupCumulative = new List<List<int>>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var meshVertices = new List<Vector3>();
            var meshNormals = new List<Vector3>();
            var meshIndices = new List<int>();
            Matrix4x4 toRoot = root.worldToLocalMatrix;

            for (int p = 0; p < partRoots.Count; p++)
            {
                foreach (MeshFilter filter in partRoots[p].GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    Mesh source = filter.sharedMesh;
                    if (renderer == null || !renderer.enabled || source == null)
                        continue;
                    int group = materials.IndexOf(renderer.sharedMaterial);
                    if (group < 0)
                    {
                        group = materials.Count;
                        materials.Add(renderer.sharedMaterial);
                        groupIndices.Add(new List<int>());
                        var cumulative = new List<int>();
                        for (int i = 0; i <= p; i++) cumulative.Add(0);
                        groupCumulative.Add(cumulative);
                    }

                    Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
                    Matrix4x4 normalMatrix = matrix.inverse.transpose;
                    source.GetVertices(meshVertices);
                    source.GetNormals(meshNormals);
                    int baseVertex = vertices.Count;
                    for (int v = 0; v < meshVertices.Count; v++)
                    {
                        vertices.Add(matrix.MultiplyPoint3x4(meshVertices[v]));
                        normals.Add(v < meshNormals.Count ? normalMatrix.MultiplyVector(meshNormals[v]).normalized : Vector3.up);
                    }
                    // In a mirrored (negative determinant) transform the triangle winding is flipped
                    bool flip = matrix.determinant < 0f;
                    for (int s = 0; s < source.subMeshCount; s++)
                    {
                        source.GetTriangles(meshIndices, s);
                        List<int> target = groupIndices[group];
                        for (int t = 0; t + 2 < meshIndices.Count; t += 3)
                        {
                            target.Add(baseVertex + meshIndices[t]);
                            target.Add(baseVertex + (flip ? meshIndices[t + 2] : meshIndices[t + 1]));
                            target.Add(baseVertex + (flip ? meshIndices[t + 1] : meshIndices[t + 2]));
                        }
                    }
                    renderer.enabled = false;
                }
                // For every submesh, the running index count after this part
                for (int g = 0; g < materials.Count; g++)
                {
                    while (groupCumulative[g].Count < p + 1) groupCumulative[g].Add(groupCumulative[g][groupCumulative[g].Count - 1]);
                    groupCumulative[g].Add(groupIndices[g].Count);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = materials.Count;
            var indexStart = new int[materials.Count];
            var countAfter = new int[materials.Count][];
            for (int g = 0; g < materials.Count; g++)
            {
                mesh.SetTriangles(groupIndices[g], g, calculateBounds: false);
                indexStart[g] = (int)mesh.GetIndexStart(g);
                countAfter[g] = groupCumulative[g].ToArray();
            }
            mesh.RecalculateBounds();

            var baked = new GameObject(name);
            baked.layer = root.gameObject.layer;
            baked.transform.SetParent(root, false);
            baked.AddComponent<MeshFilter>().sharedMesh = mesh;
            var bakedRenderer = baked.AddComponent<MeshRenderer>();
            bakedRenderer.sharedMaterials = materials.ToArray();
            bakedRenderer.shadowCastingMode = shadows;
            bakedRenderer.receiveShadows = true;

            // Delete the sources: the part roots (not the root itself) or the individual source objects
            if (parts != null)
            {
                foreach (Transform part in parts)
                    if (part != root) DestroySafe(part.gameObject);
            }
            else
            {
                foreach (Transform child in root)
                    if (child.gameObject != baked) DestroySafe(child.gameObject);
            }
            return new BakedParts(mesh, bakedRenderer, indexStart, countAfter, partRoots.Count);
        }

        static void DestroySafe(Object obj)
        {
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }
    }
}
