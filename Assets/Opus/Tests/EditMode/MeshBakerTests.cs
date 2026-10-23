using NUnit.Framework;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class MeshBakerTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                Object.DestroyImmediate(root);
        }

        [Test]
        public void BakesPartsIntoOneRendererPerMaterialGroup()
        {
            root = new GameObject("Root");
            root.transform.position = new Vector3(3f, 0f, 2f);
            var parts = new Transform[6];
            for (int i = 0; i < parts.Length; i++)
            {
                Color color = i % 2 == 0 ? Color.red : Color.green;
                parts[i] = PrimitiveKit.Leaf(root.transform, "Leaf", new Vector3(i * 0.2f, 0.1f, 0f), new Vector3(0.1f, 0.02f, 0.2f), color).transform;
            }
            Bounds before = WorldBounds(root);

            BakedParts baked = MeshBaker.Bake(root.transform, parts, "Test");

            Assert.AreEqual(1, root.GetComponentsInChildren<MeshRenderer>().Length, "The source parts are deleted, a single renderer is left");
            Assert.AreEqual(2, baked.SubMeshCount, "One submesh per material");
            Assert.AreEqual(2, baked.Renderer.sharedMaterials.Length);
            Assert.AreEqual(6, baked.PartCount);
            Bounds after = baked.Renderer.bounds;
            Assert.AreEqual(before.center.x, after.center.x, 0.01f, "The merged model stays in place");
            Assert.AreEqual(before.size.z, after.size.z, 0.01f);

            int all = baked.IndexCountFor(6);
            Assert.AreEqual(all, baked.Mesh.GetIndexCount(0) + baked.Mesh.GetIndexCount(1));
            baked.ShowFirst(3);
            Assert.AreEqual(baked.IndexCountFor(3), baked.Mesh.GetIndexCount(0) + baked.Mesh.GetIndexCount(1));
            Assert.AreEqual(all / 2, baked.IndexCountFor(3), "Three of six equal leaves are half of the indices");
            baked.ShowFirst(0);
            Assert.AreEqual(0, baked.Mesh.GetIndexCount(0) + baked.Mesh.GetIndexCount(1));
            baked.ShowFirst(6);
            Assert.AreEqual(all, baked.Mesh.GetIndexCount(0) + baked.Mesh.GetIndexCount(1));
        }

        [Test]
        public void WholeModelBakesAsSinglePart()
        {
            root = new GameObject("Root");
            Transform body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            PrimitiveKit.Box(body, "Box", Vector3.zero, Vector3.one, Color.gray, collider: false);
            PrimitiveKit.Sphere(body, "Ball", Vector3.up, Vector3.one * 0.5f, Color.gray);

            BakedParts baked = MeshBaker.Bake(root.transform);

            Assert.AreEqual(1, baked.PartCount);
            Assert.AreEqual(1, baked.SubMeshCount, "Same colour, same material");
            Assert.AreEqual(1, root.transform.childCount, "Only the merged object is left");
        }

        static Bounds WorldBounds(GameObject go)
        {
            MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (MeshRenderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
