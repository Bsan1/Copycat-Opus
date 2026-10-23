using System.Collections.Generic;
using NUnit.Framework;
using Opus.Lawn;
using Opus.Tools;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class WateringTests
    {
        static readonly Vector2 DrySpot = new Vector2(5.05f, -5.95f);   // centre of YardBuilder.DrySpots[0]
        static readonly Vector2 GreenSpot = new Vector2(0.05f, -7.95f); // an open area that is not dry

        readonly List<LawnState> created = new List<LawnState>();

        LawnState NewLawn()
        {
            var lawn = new LawnState(YardBuilder.GenerateLayout(), GrassVariety.All);
            created.Add(lawn);
            return lawn;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (LawnState lawn in created)
            {
                Object.DestroyImmediate(lawn.StateTexture);
                Object.DestroyImmediate(lawn.WetTexture);
                Object.DestroyImmediate(lawn.PatchTexture);
                Object.DestroyImmediate(lawn.PaletteTexture);
            }
            created.Clear();
        }

        static int Cell(LawnState lawn, Vector2 world)
        {
            Assert.IsTrue(lawn.Grid.TryGetCell(world, out int x, out int z));
            return lawn.Grid.Index(x, z);
        }

        [Test]
        public void StartsWithDrySpotsUnwatered()
        {
            LawnState lawn = NewLawn();
            Assert.Greater(lawn.TotalNeedsWater, 2000, "At least 20 m² of dry area");
            Assert.AreEqual(0, lawn.TotalWatered);
            Assert.AreEqual(0f, lawn.WaterFraction);
            Assert.IsTrue(lawn.NeedsWater(Cell(lawn, DrySpot)));
            Assert.IsFalse(lawn.NeedsWater(Cell(lawn, GreenSpot)));
        }

        [Test]
        public void FalloffIsFullInCoreAndZeroAtEdge()
        {
            Assert.AreEqual(1f, LawnState.WaterFalloff(0f));
            Assert.AreEqual(1f, LawnState.WaterFalloff(LawnState.WaterCore));
            Assert.AreEqual(0f, LawnState.WaterFalloff(1f));
            float previous = 1f;
            for (float t = 0f; t <= 1f; t += 0.02f)
            {
                float value = LawnState.WaterFalloff(t);
                Assert.LessOrEqual(value, previous + 1e-6f);
                previous = value;
            }
        }

        [Test]
        public void WaterOnlyAffectsCellsInsideTheCircle()
        {
            LawnState lawn = NewLawn();
            lawn.Water(DrySpot, 0.5f, 1f);
            Assert.AreEqual(0f, lawn.DrynessAt(Cell(lawn, DrySpot)), 1e-5f, "The centre is fully watered");
            Assert.Greater(lawn.DrynessAt(Cell(lawn, DrySpot + new Vector2(0.75f, 0f))), 0.9f, "Outside the circle stays dry");
        }

        [Test]
        public void WateringIsFrameRateIndependent()
        {
            LawnState once = NewLawn(), many = NewLawn();
            once.Water(DrySpot, 0.85f, 0.6f);
            for (int i = 0; i < 60; i++)
                many.Water(DrySpot, 0.85f, 0.01f);
            for (float dx = -0.9f; dx <= 0.9f; dx += 0.1f)
            {
                int index = Cell(once, DrySpot + new Vector2(dx, 0.02f));
                Assert.AreEqual(once.DrynessAt(index), many.DrynessAt(index), 1e-4f, $"dx {dx:F1}");
            }
            Assert.AreEqual(once.TotalWatered, many.TotalWatered);
        }

        [Test]
        public void WateredCellsAreCountedOnceAndOnlyIfTheyWereDry()
        {
            LawnState lawn = NewLawn();
            Assert.AreEqual(0, lawn.Water(GreenSpot, 0.85f, 2f), "Grass that is not dry does not count");
            Assert.AreEqual(0, lawn.TotalWatered);

            var newly = new List<int>();
            int first = lawn.Water(DrySpot, 0.4f, 2f, newly);
            Assert.Greater(first, 20);
            Assert.AreEqual(first, newly.Count);
            Assert.AreEqual(first, lawn.TotalWatered);
            Assert.AreEqual(0, lawn.Water(DrySpot, 0.4f, 2f), "Not counted twice");

            int sum = 0;
            for (int patch = 1; patch <= lawn.Layout.PatchCount; patch++)
                sum += lawn.PatchWateredCount(patch);
            Assert.AreEqual(lawn.TotalWatered, sum);
        }

        [Test]
        public void WaterAndMowingAreIndependent()
        {
            LawnState lawn = NewLawn();
            int index = Cell(lawn, DrySpot);
            float dry = lawn.DrynessAt(index);
            lawn.CutSwept(DrySpot, DrySpot, 0.3f, Vector2.up, true);
            Assert.AreEqual(dry, lawn.DrynessAt(index), "Mowing does not change dryness");

            lawn.RegrowAll();
            lawn.Water(DrySpot, 0.5f, 2f);
            Assert.IsFalse(lawn.IsCut(index), "Watering does not mow");
            Assert.AreEqual(LawnState.Tall, lawn.CellAt(index).r);
        }

        [Test]
        public void WetnessFadesOverTime()
        {
            LawnState lawn = NewLawn();
            int index = Cell(lawn, GreenSpot);
            lawn.Water(GreenSpot, 0.85f, 0.1f);
            Assert.AreEqual(1f, lawn.WetnessAt(index), 1e-5f, "Grass that is not dry gets wet too");
            lawn.Tick(LawnState.WetFadeSeconds * 0.5f);
            Assert.AreEqual(0.5f, lawn.WetnessAt(index), 0.01f);
            lawn.Tick(LawnState.WetFadeSeconds);
            Assert.AreEqual(0f, lawn.WetnessAt(index));
        }

        [Test]
        public void WateredDrySpotTurnsGreenPermanently()
        {
            LawnState lawn = NewLawn();
            int dry = Cell(lawn, DrySpot), green = Cell(lawn, GreenSpot);
            lawn.Water(DrySpot, 0.5f, 0.5f);
            Assert.AreEqual(0.5f, lawn.GreeningAt(dry), 0.02f, "Half watered, half greened");
            lawn.Water(DrySpot, 0.5f, 1f);
            Assert.AreEqual(1f, lawn.GreeningAt(dry), 1e-3f);
            lawn.Tick(LawnState.WetFadeSeconds * 2f);
            Assert.AreEqual(1f, lawn.GreeningAt(dry), 1e-3f, "The wetness fades, the greening stays");
            Assert.AreEqual(0f, lawn.WetnessAt(dry));

            lawn.Water(GreenSpot, 0.5f, 1f);
            Assert.AreEqual(0f, lawn.GreeningAt(green), "Grass that is already green does not change");
        }

        [Test]
        public void RegrowRestoresDryness()
        {
            LawnState lawn = NewLawn();
            lawn.Water(DrySpot, 0.85f, 2f);
            lawn.RegrowAll();
            Assert.AreEqual(0, lawn.TotalWatered);
            Assert.Greater(lawn.DrynessAt(Cell(lawn, DrySpot)), 0.95f);
        }

        [Test]
        public void SprayModesTradeReachForCoverage()
        {
            Assert.Greater(SprayMode.Narrow.MaxReach, SprayMode.Wide.MaxReach + 2f, "Narrow reaches far");
            Assert.Greater(SprayMode.Wide.Coverage, SprayMode.Narrow.Coverage * 2f, "Wide covers more area");
            Assert.Greater(SprayMode.Narrow.Rate, SprayMode.Wide.Rate, "Narrow is stronger");
        }
    }
}
