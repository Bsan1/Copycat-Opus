using NUnit.Framework;
using Opus.Lawn;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class LawnTests
    {
        static PatchLayout YardLayout() =>
            YardBuilder.GenerateLayout();

        [Test]
        public void GridCoversTheLawnInTenCentimeterCells()
        {
            LawnGrid grid = YardBuilder.LawnGrid;
            Assert.AreEqual(600, grid.Width);
            Assert.AreEqual(440, grid.Height);
            Assert.AreEqual(new Vector4(-30f, -29f, 60f, 44f), grid.ShaderRect);
        }

        [Test]
        public void CellLookupRoundTrips()
        {
            LawnGrid grid = YardBuilder.LawnGrid;
            Assert.IsTrue(grid.TryGetCell(grid.CellCenter(123, 45), out int x, out int z));
            Assert.AreEqual((123, 45), (x, z));
            Assert.IsFalse(grid.TryGetCell(new Vector2(-30.01f, 0f), out _, out _));
            Assert.IsFalse(grid.TryGetCell(new Vector2(0f, 15.01f), out _, out _));
        }

        [Test]
        public void CellRangeIsClampedToGrid()
        {
            LawnGrid grid = YardBuilder.LawnGrid;
            Assert.IsTrue(grid.TryGetCellRange(new Vector2(-40f, -40f), new Vector2(-29.95f, -28.95f), out Vector2Int from, out Vector2Int to));
            Assert.AreEqual(Vector2Int.zero, from);
            Assert.AreEqual(new Vector2Int(0, 0), to);
            Assert.IsFalse(grid.TryGetCellRange(new Vector2(20f, 20f), new Vector2(25f, 25f), out _, out _));
        }

        [Test]
        public void EveryCellBelongsToAPatchAndEveryPatchIsUsed()
        {
            PatchLayout layout = YardLayout();
            foreach (byte id in layout.PatchIds)
                Assert.That(id, Is.InRange(1, layout.PatchCount));
            for (int id = 1; id <= layout.PatchCount; id++)
                Assert.Greater(layout.CountCells(id), 1000, $"Patch {id} is too small");
        }

        [Test]
        public void AreasDoNotBleedAcrossTheFence()
        {
            PatchLayout layout = YardLayout();
            LawnGrid grid = layout.Grid;
            var patchesPerArea = new System.Collections.Generic.Dictionary<int, int>();
            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int index = grid.Index(x, z);
                    Vector2 center = grid.CellCenter(x, z);
                    int expected = YardBuilder.AreaAt(new Vector3(center.x, 0f, center.y))?.Id ?? 0;
                    Assert.AreNotEqual(0, expected, "Every cell of the grid is in an area");
                    Assert.AreEqual(expected, layout.CellAreas[index]);
                    Assert.AreEqual(expected, layout.AreaOfPatch(layout.PatchIds[index]), $"Cell ({x},{z}) fell into a neighbouring area's patch");
                }
            }
            for (int id = 1; id <= layout.PatchCount; id++)
            {
                int area = layout.AreaOfPatch(id);
                patchesPerArea[area] = patchesPerArea.TryGetValue(area, out int n) ? n + 1 : 1;
            }
            Assert.AreEqual(16, patchesPerArea[1]);
            Assert.AreEqual(6, patchesPerArea[2]);
            Assert.AreEqual(8, patchesPerArea[3]);
            Assert.AreEqual(5, patchesPerArea[4]);
        }

        [Test]
        public void AreaLookupFollowsTheFence()
        {
            Assert.AreEqual(1, YardBuilder.AreaAt(new Vector3(17.9f, 0f, 0f))?.Id);
            Assert.AreEqual(2, YardBuilder.AreaAt(new Vector3(18.1f, 0f, 0f))?.Id);
            Assert.IsNull(YardBuilder.AreaAt(new Vector3(31f, 0f, 0f)));
            Assert.IsNull(YardBuilder.AreaAt(new Vector3(0f, 0f, 16f)));
        }

        [Test]
        public void LayoutIsDeterministic()
        {
            CollectionAssert.AreEqual(YardLayout().PatchIds, YardLayout().PatchIds);
        }

        [Test]
        public void SeedPointsLieInTheirOwnPatchMostly()
        {
            PatchLayout layout = YardLayout();
            int own = 0;
            for (int i = 0; i < layout.PatchCount; i++)
                if (layout.PatchAt(layout.Seeds[i].Position) == i + 1) own++;
            // The warping shifts the borders, but most seeds must stay in their own patch.
            Assert.GreaterOrEqual(own, layout.PatchCount - 2);
        }

        [Test]
        public void DryPatchAndSpotsAreDry()
        {
            PatchLayout layout = YardLayout();
            int dryPatch = System.Array.FindIndex(YardBuilder.PatchSeeds, seed => seed.Dryness >= 1f) + 1;
            Assert.Greater(dryPatch, 0);
            Assert.AreEqual(255, layout.Dryness[CellIndexAt(layout, YardBuilder.PatchSeeds[dryPatch - 1].Position)]);
            DrySpot spot = YardBuilder.DrySpots[0];
            Assert.Greater(layout.Dryness[CellIndexAt(layout, spot.Center)], 200);
            Assert.Greater(layout.CountDryCells() * 0.01f, 20f, "At least 20 m² of dry area");
        }

        [TestCase(0f, 1f)]
        [TestCase(GrassField.FullDensityDistance, 1f)]
        [TestCase(GrassField.FarDensityDistance, GrassField.FarDensityFraction)]
        [TestCase(500f, GrassField.FarDensityFraction)]
        public void DensityFallsSmoothlyWithDistance(float distance, float expected)
        {
            Assert.AreEqual(expected, GrassField.DensityAt(distance), 1e-4f);
        }

        [Test]
        public void DensityNeverIncreasesWithDistance()
        {
            float previous = 1f;
            for (float d = 0f; d < 60f; d += 0.5f)
            {
                float current = GrassField.DensityAt(d);
                Assert.LessOrEqual(current, previous + 1e-6f);
                previous = current;
            }
        }

        static int CellIndexAt(PatchLayout layout, Vector2 world)
        {
            Assert.IsTrue(layout.Grid.TryGetCell(world, out int x, out int z));
            return layout.Grid.Index(x, z);
        }
    }
}
