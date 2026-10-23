using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Opus.Lawn;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    /// <summary>U14: orchard and vegetable garden layout, falling apples, grass mesh LOD.</summary>
    public class GardenTests
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

        LawnState NewLawn()
        {
            var lawn = new LawnState(YardBuilder.GenerateLayout(), GrassVariety.All);
            lawns.Add(lawn);
            return lawn;
        }

        [Test]
        public void NewAreasAreValidAndChained()
        {
            YardDefinition home = HomeYard.Create();
            List<string> errors = home.Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual("Orchard", home.AreaName(3));
            Assert.AreEqual("Vegetable garden", home.AreaName(4));
            Assert.AreEqual(15, home.Props.Count(p => p.Kind == PropKind.FruitTree));
            Assert.AreEqual(6, home.Props.Count(p => p.Kind == PropKind.RaisedBed));
            Assert.IsTrue(home.PatchSeeds.Any(s => s.Area == 4 && s.Dryness >= 1f), "A patch that starts dry in the vegetable garden");
        }

        /// <summary>
        /// The whole garden is built in edit mode: weeds and dry spots must not end up under props (a weed on a grassless cell
        /// cannot be pulled, a spot cannot be watered), the gate openings must be clear, the gaps between beds narrow for the mower but passable.
        /// </summary>
        [Test]
        public void WeedsDrySpotsAndGatesAreClearOfObjects()
        {
            root = new GameObject("Garden");
            YardBuilder.Build(root.transform);
            Physics.SyncTransforms(); // in edit mode the bounds of freshly moved colliders only update this way
            Collider[] colliders = root.GetComponentsInChildren<Collider>().Where(c => c.enabled && c.GetComponent<Gate>() == null && c.GetComponentInParent<Gate>() == null).ToArray();
            bool Blocked(Vector3 p, float margin) => colliders.Any(c =>
            {
                Bounds b = c.bounds;
                b.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
                return b.Contains(new Vector3(p.x, 0.1f, p.z));
            });

            foreach (WeedSpot spot in HomeYard.Create().WeedSpots)
                Assert.IsFalse(Blocked(spot.Position, 0.02f), $"Weed {spot.Position} is under a prop");
            foreach (DrySpot spot in HomeYard.Create().DrySpots)
                Assert.IsFalse(Blocked(new Vector3(spot.Center.x, 0f, spot.Center.y), 0f), $"Dry spot {spot.Center} is under a prop");
            foreach (Gate gate in YardBuilder.Gates)
            {
                Vector3 center = gate.Def.OpeningCenter;
                Vector3 across = gate.Def.Axis == FenceAxis.AlongZ ? Vector3.right : Vector3.forward;
                foreach (float side in new[] { -1f, 1f })
                    Assert.IsFalse(Blocked(center + across * side * 1.2f, 0.3f), $"Gate {gate.Def.From}→{gate.Def.To} is blocked in front");
            }
            // Gaps between beds: 1.8 m (mower deck ~0.5 m)
            foreach (Vector3 bed in HomeYard.RaisedBeds)
                Assert.IsFalse(Blocked(bed + new Vector3(HomeYard.RaisedBedSize.x * 0.5f + 0.9f, 0f, 0f), 0.3f), $"The path east of {bed} is blocked");
        }

        [Test]
        public void ApplesFallOnlyUnderTreesOfOpenAreasAndArePickedUp()
        {
            LawnState lawn = NewLawn();
            root = new GameObject("Apples");
            List<FallenApples.Tree> trees = FallenApples.TreesOf(HomeYard.Create()).ToList();
            Assert.AreEqual(15, trees.Count);
            Assert.IsTrue(trees.All(t => t.Area == 3), "All fruit trees are in the orchard");
            FallenApples apples = FallenApples.Create(root.transform, lawn, trees);

            Assert.AreEqual(0, apples.SpawnForDay(1, area => area == 1), "No apples drop in the locked orchard");
            int count = apples.SpawnForDay(1, area => area <= 3);
            Assert.That(count, Is.InRange(8, 30));
            Vector3[] first = apples.Capture();
            foreach (Vector3 apple in first)
            {
                float nearest = trees.Min(t => Vector2.Distance(new Vector2(t.Position.x, t.Position.z), new Vector2(apple.x, apple.z)));
                Assert.That(nearest, Is.InRange(FallenApples.MinDrop - 0.01f, FallenApples.MaxDrop + 0.01f));
                Assert.AreEqual(3, YardBuilder.AreaAt(apple)?.Id);
            }
            apples.SpawnForDay(1, area => area <= 3);
            CollectionAssert.AreEqual(first, apples.Capture(), "The same day's apples land in the same places");
            apples.SpawnForDay(2, area => area <= 3);
            CollectionAssert.AreNotEqual(first, apples.Capture(), "They drop anew every day");

            var picked = new List<Vector3>();
            apples.Picked += picked.Add;
            Vector3 target = apples.Capture()[0];
            int before = apples.Count;
            Assert.GreaterOrEqual(apples.PickNear(target + new Vector3(0.3f, 0.05f, 0.2f)), 1);
            Assert.AreEqual(before - picked.Count, apples.Count);
            Assert.Contains(target, picked);
            Assert.AreEqual(0, apples.PickNear(new Vector3(0f, 0f, -12f)), "Not collected from afar");

            Vector3[] saved = apples.Capture();
            apples.Clear();
            apples.Restore(saved);
            CollectionAssert.AreEqual(saved, apples.Capture());
        }

        [Test]
        public void FarTuftMeshIsMuchLighter()
        {
            Mesh near = TuftMesh.Build(GrassField.NearBlades);
            Mesh far = TuftMesh.Build(GrassField.FarBlades, segments: GrassField.FarSegments, widthScale: GrassField.FarWidthScale);
            try
            {
                Assert.AreEqual(GrassField.NearBlades * 7 * 3, near.GetIndexCount(0));
                Assert.AreEqual(GrassField.FarBlades * 3 * 3, far.GetIndexCount(0));
                Assert.Less(far.GetIndexCount(0), near.GetIndexCount(0) * 0.35f);
            }
            finally
            {
                Object.DestroyImmediate(near);
                Object.DestroyImmediate(far);
            }
        }

        [Test]
        public void MeshLodSwitchesWithHysteresis()
        {
            float d = GrassField.FarMeshDistance, h = GrassField.FarMeshHysteresis;
            Assert.IsFalse(GrassField.ChooseFar(false, d, 1f), "Stays near at the boundary");
            Assert.IsTrue(GrassField.ChooseFar(false, d + h + 0.1f, 1f));
            Assert.IsTrue(GrassField.ChooseFar(true, d - h + 0.1f, 1f), "Coming back from far it stays far inside the band");
            Assert.IsFalse(GrassField.ChooseFar(true, d - h - 0.1f, 1f));
            float reduced = d * GrassField.ReducedDensityLodScale + h + 0.1f;
            Assert.IsTrue(GrassField.ChooseFar(false, reduced, 0.7f), "Far mesh earlier at low density");
            Assert.IsFalse(GrassField.ChooseFar(false, reduced, 1f));
        }
    }
}
