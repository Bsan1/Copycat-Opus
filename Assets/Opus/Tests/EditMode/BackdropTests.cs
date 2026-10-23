using NUnit.Framework;
using Opus.Visual;
using UnityEngine;

namespace Opus.Tests
{
    public class BackdropTests
    {
        [TestCase(1000, false)]
        [TestCase(1037, true)]
        public void PaintedTreeHasAnOpaqueCrownAndClearCorners(int seed, bool pointed)
        {
            Texture2D texture = PaintedBackdrop.PaintTree(seed, new Color(0.85f, 0.64f, 0.24f), pointed, keepReadable: true);
            try
            {
                int size = texture.width;
                Color32[] pixels = texture.GetPixels32();
                Assert.AreEqual(0, pixels[0].a, "Bottom-left corner transparent");
                Assert.AreEqual(0, pixels[size - 1].a, "Bottom-right corner transparent");
                Assert.AreEqual(0, pixels[pixels.Length - 1].a, "Top-right corner transparent");
                Assert.Greater(pixels[(int)(size * 0.55f) * size + size / 2].a, 200, "Middle of the crown filled");

                int opaque = 0;
                foreach (Color32 p in pixels)
                    if (p.a > 128) opaque++;
                Assert.That(opaque / (float)pixels.Length, Is.InRange(0.12f, 0.6f), "The crown covers a reasonable part of the texture");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TreesAreDeterministic()
        {
            Texture2D a = PaintedBackdrop.PaintTree(1074, Color.green, false, keepReadable: true);
            Texture2D b = PaintedBackdrop.PaintTree(1074, Color.green, false, keepReadable: true);
            try
            {
                CollectionAssert.AreEqual(a.GetPixels32(), b.GetPixels32());
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }
}
