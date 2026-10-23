using System.Collections.Generic;
using NUnit.Framework;
using Opus.Lawn;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class MowingTests
    {
        const float Radius = 0.25f;
        static readonly Vector2 Open = new Vector2(0.05f, -7.95f); // an open spot in the garden, exactly a cell centre

        LawnState lawn;

        [SetUp]
        public void SetUp()
        {
            lawn = new LawnState(YardBuilder.GenerateLayout(), GrassVariety.All);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(lawn.StateTexture);
            Object.DestroyImmediate(lawn.WetTexture);
            Object.DestroyImmediate(lawn.PatchTexture);
            Object.DestroyImmediate(lawn.PaletteTexture);
        }

        [Test]
        public void StampCutsADiscOfBladeArea()
        {
            var newly = new List<int>();
            int cut = lawn.CutSwept(Open, Open, Radius, Vector2.up, true, newly);
            float expected = Mathf.PI * Radius * Radius / 0.01f; // ~19.6 cells
            Assert.AreEqual(expected, cut, 4f);
            Assert.AreEqual(cut, newly.Count);
            Assert.AreEqual(cut, lawn.TotalCutCells);
        }

        [Test]
        public void SweepCutsAStripAlongThePath()
        {
            int cut = lawn.CutSwept(Open, Open + new Vector2(0f, 2f), Radius, Vector2.up, true);
            float expected = (2f * Radius * 2f + Mathf.PI * Radius * Radius) / 0.01f; // ~120 cells
            Assert.AreEqual(expected, cut, expected * 0.08f);
        }

        [Test]
        public void CuttingTheSameSpotTwiceDoesNotDoubleCount()
        {
            int first = lawn.CutSwept(Open, Open, Radius, Vector2.up, true);
            int second = lawn.CutSwept(Open, Open, Radius, Vector2.down, true);
            Assert.Greater(first, 0);
            Assert.AreEqual(0, second);
            Assert.AreEqual(first, lawn.TotalCutCells);
        }

        [Test]
        public void PatchCountersAddUpToTotal()
        {
            lawn.CutSwept(new Vector2(-10f, -10f), new Vector2(10f, 10f), Radius, Vector2.one, true);
            int sum = 0;
            for (int patch = 1; patch <= lawn.Layout.PatchCount; patch++)
            {
                sum += lawn.PatchCutCount(patch);
                Assert.LessOrEqual(lawn.PatchCutCount(patch), lawn.PatchCellCount(patch));
            }
            Assert.AreEqual(lawn.TotalCutCells, sum);
            Assert.Greater(lawn.CutFraction, 0f);
            Assert.Less(lawn.CutFraction, 0.1f);
        }

        [Test]
        public void VisibleHeightFallsGraduallyAfterLogicalCut()
        {
            var newly = new List<int>();
            lawn.CutSwept(Open, Open, Radius, Vector2.up, true, newly);
            int cell = newly[0];
            Assert.IsTrue(lawn.IsCut(cell), "The logical cut happens instantly.");
            Assert.AreEqual(LawnState.Tall, lawn.CellAt(cell).r, "The visible height has not dropped yet.");

            lawn.Tick(LawnState.CutAnimationSeconds * 0.5f);
            Assert.That(lawn.CellAt(cell).r, Is.InRange(100, 160), "An intermediate height halfway.");

            lawn.Tick(LawnState.CutAnimationSeconds);
            Assert.AreEqual(LawnState.Cut, lawn.CellAt(cell).r);
        }

        [Test]
        public void MowingDirectionIsStoredAndFlipsOnReturnPass()
        {
            var newly = new List<int>();
            lawn.CutSwept(Open, Open, Radius, Vector2.right, true, newly);
            Color32 cell = lawn.CellAt(newly[0]);
            Assert.AreEqual(255, cell.b, 1);
            Assert.AreEqual(128, cell.a, 1);

            lawn.CutSwept(Open, Open, Radius, Vector2.left, true);
            Assert.AreEqual(0, lawn.CellAt(newly[0]).b, 1, "A pass in the opposite direction flips the stripe.");
        }

        [Test]
        public void StandingStillDoesNotChangeStripeDirection()
        {
            var newly = new List<int>();
            lawn.CutSwept(Open, Open, Radius, Vector2.up, true, newly);
            lawn.CutSwept(Open, Open, Radius, Vector2.right, false);
            Assert.AreEqual(255, lawn.CellAt(newly[0]).a, 1);
        }

        [Test]
        public void CellsWithoutGrassAreNeverCut()
        {
            int index = 0;
            lawn.Layout.ClearCell(index);
            Vector2 corner = lawn.Grid.CellCenter(index);
            var newly = new List<int>();
            lawn.CutSwept(corner, corner, 0.01f, Vector2.up, true, newly);
            CollectionAssert.DoesNotContain(newly, index);
        }

        [Test]
        public void RegrowRestoresEverything()
        {
            lawn.CutSwept(Open, Open + Vector2.up * 3f, Radius, Vector2.up, true);
            lawn.RegrowAll();
            Assert.AreEqual(0, lawn.TotalCutCells);
            Assert.AreEqual(0f, lawn.CutFraction);
        }

        [Test]
        public void DistanceToSegment()
        {
            Assert.AreEqual(1f, LawnState.DistanceToSegmentSq(new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, 0f)), 1e-5f);
            Assert.AreEqual(4f, LawnState.DistanceToSegmentSq(new Vector2(-2f, 0f), Vector2.zero, new Vector2(2f, 0f)), 1e-5f);
            Assert.AreEqual(9f, LawnState.DistanceToSegmentSq(new Vector2(3f, 0f), Vector2.zero, Vector2.zero), 1e-5f);
        }
    }
}
