using System;
using System.Linq;
using NUnit.Framework;
using Opus.Visual;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class SurfaceTests
    {
        GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        static string[] TextureNames => SurfaceTextures.Names.ToArray();

        [TestCaseSource(nameof(TextureNames))]
        public void TextureGenerationIsDeterministic(string name)
        {
            Color32[] a = SurfaceTextures.Generate(name, 64);
            Color32[] b = SurfaceTextures.Generate(name, 64);
            Assert.AreEqual(64 * 64, a.Length);
            Assert.IsTrue(a.SequenceEqual(b), $"{name} differs between two generations");
        }

        /// <summary>
        /// Tiling: the difference between the right and left edges (and the top and bottom) must not be bigger than the sharpest
        /// neighbouring column (row) difference inside the texture; otherwise a seam line shows on the wall at every repeat.
        /// </summary>
        [TestCaseSource(nameof(TextureNames))]
        public void TextureTilesWithoutSeams(string name)
        {
            const int size = 128;
            Color32[] pixels = SurfaceTextures.Generate(name, size);
            float ColumnDiff(int x0, int x1) => Enumerable.Range(0, size).Average(y => Diff(pixels[y * size + x0], pixels[y * size + x1]));
            float RowDiff(int y0, int y1) => Enumerable.Range(0, size).Average(x => Diff(pixels[y0 * size + x], pixels[y1 * size + x]));

            float columnSeam = ColumnDiff(size - 1, 0);
            float columnMax = Enumerable.Range(0, size - 1).Max(x => ColumnDiff(x, x + 1));
            float rowSeam = RowDiff(size - 1, 0);
            float rowMax = Enumerable.Range(0, size - 1).Max(y => RowDiff(y, y + 1));
            Assert.LessOrEqual(columnSeam, columnMax * 1.05f + 1f, $"{name}: vertical seam {columnSeam:0.0} > {columnMax:0.0}");
            Assert.LessOrEqual(rowSeam, rowMax * 1.05f + 1f, $"{name}: horizontal seam {rowSeam:0.0} > {rowMax:0.0}");
        }

        static float Diff(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        [TestCaseSource(nameof(TextureNames))]
        public void PatternsAreToneMultipliersAroundMidGray(string name)
        {
            if (name == SurfaceTextures.Grime)
                return;
            Color32[] pixels = SurfaceTextures.Generate(name, 64);
            double mean = pixels.Average(p => p.g / 255.0);
            // 0.5 = the colour as is; the average must not drift much so the material colour stays close to the palette colour
            Assert.That(mean, Is.InRange(0.38, 0.58), $"{name} mean tone {mean:0.00}");
        }

        [Test]
        public void GeneratedAssetsExistForEveryPattern()
        {
            foreach (string name in SurfaceTextures.Names)
            {
                var texture = Resources.Load<Texture2D>(SurfaceTextures.ResourceFolder + "/" + name);
                Assert.IsNotNull(texture, $"Resources/{SurfaceTextures.ResourceFolder}/{name} missing (Setup.GenerateContent)");
                Assert.AreEqual(TextureWrapMode.Repeat, texture.wrapMode, name);
                Assert.Greater(texture.mipmapCount, 1, name);
            }
        }

        [Test]
        public void EveryKindHasALookWithAKnownPattern()
        {
            var names = SurfaceTextures.Names.ToList();
            foreach (SurfaceKind kind in Enum.GetValues(typeof(SurfaceKind)))
            {
                Assert.IsTrue(SurfaceLibrary.Kinds.Contains(kind), $"{kind} is not in the catalogue");
                SurfaceLibrary.Look look = SurfaceLibrary.LookOf(kind);
                Assert.Contains(look.Texture, names, kind.ToString());
                Assert.Greater(look.Tile, 0f, kind.ToString());
            }
        }

        [Test]
        public void SameSurfaceSharesOneMaterial()
        {
            Color color = new Color(0.31f, 0.52f, 0.67f);
            Material a = PrimitiveKit.MaterialFor(color.As(SurfaceKind.Siding));
            Material b = PrimitiveKit.MaterialFor(color.As(SurfaceKind.Siding));
            Material c = PrimitiveKit.MaterialFor(color.As(SurfaceKind.Siding, 1.75f));
            Material d = PrimitiveKit.MaterialFor(color);
            Assert.AreSame(a, b);
            Assert.AreNotSame(a, c, "A different scale is a separate material");
            Assert.AreNotSame(a, d, "A plain colour is a separate material of the paint kind");
            Assert.AreEqual(SurfaceLibrary.ShaderName, a.shader.name);
            Assert.AreEqual(SurfaceLibrary.LookOf(SurfaceKind.Siding).Tile * 1.75f, c.GetFloat("_Tile"), 1e-4f);
            Assert.IsNotNull(a.GetTexture("_BaseMap"));
            Assert.IsNotNull(a.GetTexture("_GrimeMap"));
        }

        [Test]
        public void PlainColorBecomesPaintAndDefaultKindScopeAppliesToIt()
        {
            Surface plain = Color.red;
            Assert.AreEqual(SurfaceKind.Paint, plain.Kind);
            Assert.AreEqual(1f, plain.Scale);

            root = new GameObject("Root");
            Color color = new Color(0.2f, 0.6f, 0.3f);
            GameObject inside, explicitKind;
            using (PrimitiveKit.DefaultKind(SurfaceKind.Plant))
            {
                inside = PrimitiveKit.Box(root.transform, "Inside", Vector3.zero, Vector3.one, color, collider: false);
                explicitKind = PrimitiveKit.Box(root.transform, "Light", Vector3.zero, Vector3.one, color.As(SurfaceKind.Metal), collider: false);
            }
            GameObject outside = PrimitiveKit.Box(root.transform, "Outside", Vector3.zero, Vector3.one, color, collider: false);

            Assert.AreSame(PrimitiveKit.MaterialFor(color.As(SurfaceKind.Plant)), inside.GetComponent<MeshRenderer>().sharedMaterial);
            Assert.AreSame(PrimitiveKit.MaterialFor(color.As(SurfaceKind.Metal)), explicitKind.GetComponent<MeshRenderer>().sharedMaterial,
                "An explicitly given kind is not affected by the scope");
            Assert.AreSame(PrimitiveKit.MaterialFor(color), outside.GetComponent<MeshRenderer>().sharedMaterial, "Paint once the scope ends");
        }
    }
}
