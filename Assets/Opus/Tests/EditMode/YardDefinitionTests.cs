using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Opus.Lawn;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class YardDefinitionTests
    {
        GameObject root;
        readonly List<LawnState> lawns = new List<LawnState>();

        [TearDown]
        public void TearDown()
        {
            YardBuilder.Use(HomeYard.Create());
            if (root != null)
                Object.DestroyImmediate(root);
            foreach (LawnState lawn in lawns)
            {
                Object.DestroyImmediate(lawn.StateTexture);
                Object.DestroyImmediate(lawn.WetTexture);
                Object.DestroyImmediate(lawn.PatchTexture);
                Object.DestroyImmediate(lawn.PaletteTexture);
            }
            lawns.Clear();
        }

        [Test]
        public void HomeYardIsValid()
        {
            List<string> errors = HomeYard.Create().Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void HomeYardKeepsTheOriginalGridAndFences()
        {
            // First version (areas 1 and 2): the grid up to U13; old saves migrate by it
            YardDefinition first = HomeYard.FirstVersion();
            Assert.AreEqual(new Vector2(-18f, -15f), first.Grid.Min);
            Assert.AreEqual((480, 300), (first.Grid.Width, first.Grid.Height));
            Assert.AreEqual(22, first.PatchSeeds.Length);
            Assert.AreEqual(21, first.WeedSpots.Length);
            Assert.AreEqual(1, first.Gates.Length);

            YardDefinition home = HomeYard.Create();
            Assert.AreEqual(new Vector2(-30f, -29f), home.Grid.Min);
            Assert.AreEqual((600, 440), (home.Grid.Width, home.Grid.Height));

            // The fence pieces written by hand up to U12 are still there (the gate opening in the east fence cut at z -0.8..0.8);
            // the side yard's south fence is now split by the orchard gate (x 23.2..24.8)
            const float x = 18f, z = 15f;
            var expected = new[]
            {
                (new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z)),
                (new Vector3(x, 0f, -z), new Vector3(x, 0f, -0.8f)),
                (new Vector3(x, 0f, 0.8f), new Vector3(x, 0f, z)),
                (new Vector3(-x, 0f, -z), new Vector3(-x, 0f, z)),
                (new Vector3(-x, 0f, z), new Vector3(-8.5f, 0f, z)),
                (new Vector3(8.5f, 0f, z), new Vector3(x, 0f, z)),
                (new Vector3(x, 0f, -z), new Vector3(23.2f, 0f, -z)),
                (new Vector3(24.8f, 0f, -z), new Vector3(30f, 0f, -z)),
                (new Vector3(30f, 0f, -z), new Vector3(30f, 0f, z)),
                (new Vector3(x, 0f, z), new Vector3(30f, 0f, z)),
            };
            List<(Vector3 from, Vector3 to)> segments = home.FenceSegments();
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Less(Vector3.Distance(expected[i].Item1, segments[i].from), 1e-4f, $"start of piece {i}");
                Assert.Less(Vector3.Distance(expected[i].Item2, segments[i].to), 1e-4f, $"end of piece {i}");
            }
            Assert.IsTrue(segments.Any(s => Vector3.Distance(s.from, new Vector3(-30f, 0f, -z)) < 1e-4f && Vector3.Distance(s.to, new Vector3(-24.8f, 0f, -z)) < 1e-4f),
                "The gate opening in the vegetable garden's south fence");
            CollectionAssert.AreEqual(new[] { 2 }, home.AreasOpenedBy(1));
            CollectionAssert.AreEqual(new[] { 3 }, home.AreasOpenedBy(2));
            CollectionAssert.AreEqual(new[] { 4 }, home.AreasOpenedBy(3));
            CollectionAssert.IsEmpty(home.AreasOpenedBy(4));
        }

        [Test]
        public void GateOpeningIsCutFromLinesInEitherDirection()
        {
            var def = new YardDefinition
            {
                Gates = new[] { new GateDef(1, 2, new Vector3(4f, 0f, 10f), 2f, FenceAxis.AlongX) },
                FenceLines = new[]
                {
                    (new Vector3(10f, 0f, 10f), new Vector3(0f, 0f, 10f)),   // reversed
                    (new Vector3(0f, 0f, 20f), new Vector3(10f, 0f, 20f)),   // another line: untouched
                },
            };
            List<(Vector3 from, Vector3 to)> segments = def.FenceSegments();
            Assert.AreEqual(3, segments.Count);
            Assert.AreEqual(new Vector3(10f, 0f, 10f), segments[0].from);
            Assert.AreEqual(new Vector3(6f, 0f, 10f), segments[0].to);
            Assert.AreEqual(new Vector3(4f, 0f, 10f), segments[1].from);
            Assert.AreEqual(new Vector3(0f, 0f, 10f), segments[1].to);
        }

        [Test]
        public void ValidationCatchesBrokenDefinitions()
        {
            YardDefinition def = ThreeAreaYard();
            Assert.IsEmpty(def.Validate());

            def.Gates = new[] { def.Gates[0], new GateDef(2, 3, new Vector3(14f, 0f, 9f), 2f, FenceAxis.AlongX) };
            Assert.IsTrue(def.Validate().Any(e => e.Contains("shared edge")), "The gate is not on a fence line");

            def = ThreeAreaYard();
            def.Gates = new[] { def.Gates[0] };
            Assert.IsTrue(def.Validate().Any(e => e.Contains("Area 3")), "No gate to area 3");

            def = ThreeAreaYard();
            def.PatchSeeds[0] = new PatchSeed(15f, 3f, 0, area: 1);
            Assert.IsTrue(def.Validate().Any(e => e.Contains("Seed")), "Seed in another area");
        }

        /// <summary>
        /// 1 ─gate(x=10)─ 2 ─gate(z=10)─ 3: area 3 borders area 1 with a fence (no gate). The gate chain, building the gates
        /// in both directions and occlusion through two gates.
        /// </summary>
        [Test]
        public void ThreeAreaYardBuildsGatesChainsAndOccludes()
        {
            YardDefinition def = ThreeAreaYard();
            YardBuilder.Use(def);
            root = new GameObject("TestGarden");
            YardBuilder.Build(root.transform);

            Assert.AreEqual(2, YardBuilder.Gates.Count);
            Gate toTwo = YardBuilder.GateTo(2), toThree = YardBuilder.GateTo(3);
            Assert.IsNotNull(toTwo);
            Assert.IsNotNull(toThree);
            Assert.IsNull(YardBuilder.GateTo(1));
            Assert.AreEqual(90f, toThree.transform.eulerAngles.y, 0.01f, "A gate in a fence along X turns");

            // The leaves swing towards the unlocked area
            toTwo.SetOpenInstant(true);
            toThree.SetOpenInstant(true);
            Assert.Greater(toTwo.transform.Find("GateLeaf").forward.x, 0.9f, "area 2 is to the east");
            Assert.Greater(toThree.transform.Find("GateLeaf").forward.z, 0.9f, "area 3 is to the north");
            Assert.Less(Vector3.Distance(new Vector3(15f, 1.2f, 10f), toThree.OpeningCenter), 1e-3f);

            // Area chain and grid
            CollectionAssert.AreEqual(new[] { 2 }, def.AreasOpenedBy(1));
            CollectionAssert.AreEqual(new[] { 3 }, def.AreasOpenedBy(2));
            var lawn = new LawnState(def.GenerateLayout(), GrassVariety.All);
            lawns.Add(lawn);
            var tasks = new LawnTasks(lawn, def.PatchInfos) { AreasOpenedBy = def.AreasOpenedBy };
            Assert.AreEqual(2, tasks.NextArea(1));
            Assert.AreEqual(3, tasks.NextArea(2));
            Assert.AreEqual(0, tasks.NextArea(3));
            Assert.AreEqual(1, tasks.TotalIn(3));
            Assert.AreEqual(3, lawn.Layout.AreaOfPatch(lawn.Layout.PatchAt(new Vector2(15f, 14f))));

            // Occlusion: the eye in area 1, the target in area 3; the line of sight passes through both gates
            var eye = new Vector3(6f, 1.6f, 1f);
            var target = new Bounds(new Vector3(17.5f, 0.4f, 12.5f), Vector3.one);
            var offLine = new Bounds(new Vector3(12f, 0.4f, 16f), Vector3.one);
            Assert.IsFalse(YardVisibility.IsHidden(eye, target), "both gates open, the line passes through both");
            Assert.IsTrue(YardVisibility.IsHidden(eye, offLine), "the line is outside the first opening");
            Assert.IsFalse(YardVisibility.IsHidden(eye + Vector3.up, offLine), "a look over the fence is not occluded");

            toThree.SetOpenInstant(false);
            Assert.IsTrue(YardVisibility.IsHidden(eye, target), "area 3's gate is closed");
            Assert.IsFalse(YardVisibility.IsHidden(eye, new Bounds(new Vector3(14f, 0.4f, 6f), Vector3.one)), "area 2 is visible through the open gate");
            toTwo.SetOpenInstant(false);
            Assert.IsTrue(YardVisibility.IsHidden(eye, new Bounds(new Vector3(14f, 0.4f, 6f), Vector3.one)), "all gates closed");

            // The eye in area 2: area 3 only through the north gate
            toThree.SetOpenInstant(true);
            var eyeTwo = new Vector3(15f, 1.6f, 5f);
            Assert.IsFalse(YardVisibility.IsHidden(eyeTwo, new Bounds(new Vector3(15f, 0.4f, 14f), Vector3.one)));
            Assert.IsTrue(YardVisibility.IsHidden(eyeTwo, new Bounds(new Vector3(11f, 0.4f, 17.5f), Vector3.one * 0.6f)));
        }

        /// <summary>Three-area test garden: 1 (0..10, 0..10), 2 (10..20, 0..10), 3 (10..20, 10..18).</summary>
        static YardDefinition ThreeAreaYard() => new YardDefinition
        {
            Id = "trial",
            Areas = new[]
            {
                new LawnArea(1, "One", 0f, 0f, 10f, 10f),
                new LawnArea(2, "Two", 10f, 0f, 20f, 10f),
                new LawnArea(3, "Three", 10f, 10f, 20f, 18f),
            },
            Gates = new[]
            {
                new GateDef(1, 2, new Vector3(10f, 0f, 4f), 2f, FenceAxis.AlongZ),
                new GateDef(2, 3, new Vector3(14f, 0f, 10f), 2f, FenceAxis.AlongX),
            },
            PatchSeeds = new[]
            {
                new PatchSeed(3f, 3f, 0), new PatchSeed(7f, 7f, 1),
                new PatchSeed(13f, 3f, 2, area: 2), new PatchSeed(17f, 7f, 3, area: 2),
                new PatchSeed(15f, 14f, 4, area: 3),
            },
            PatchInfos = new[]
            {
                new PatchInfo("A", "#C94F6D"), new PatchInfo("B", "#4FA3A5"), new PatchInfo("C", "#E0853A"),
                new PatchInfo("D", "#8E6CB8"), new PatchInfo("E", "#5C8FC9"),
            },
            WeedSpots = new[] { new WeedSpot(9.5f, 9.5f), new WeedSpot(19.5f, 17.5f) },
            FenceLines = new[]
            {
                (new Vector3(0f, 0f, 0f), new Vector3(20f, 0f, 0f)),
                (new Vector3(20f, 0f, 0f), new Vector3(20f, 0f, 18f)),
                (new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f)),
                (new Vector3(0f, 0f, 10f), new Vector3(10f, 0f, 10f)),
                (new Vector3(10f, 0f, 10f), new Vector3(10f, 0f, 18f)),
                (new Vector3(10f, 0f, 18f), new Vector3(20f, 0f, 18f)),
                (new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f)),
                (new Vector3(10f, 0f, 10f), new Vector3(20f, 0f, 10f)),
            },
            SpawnFeet = new Vector3(5f, 0.05f, 2f),
        };
    }
}
