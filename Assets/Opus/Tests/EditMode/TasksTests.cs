using System.Collections.Generic;
using NUnit.Framework;
using Opus.Lawn;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class TasksTests
    {
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

        [Test]
        public void EverySeedHasANameAndColor()
        {
            Assert.AreEqual(YardBuilder.PatchSeeds.Length, YardBuilder.PatchInfos.Length);
            var names = new HashSet<string>();
            foreach (PatchInfo info in YardBuilder.PatchInfos)
                Assert.IsTrue(names.Add(info.Name), $"The same name twice: {info.Name}");
        }

        [Test]
        public void EveryWeedSitsOnAPatch()
        {
            PatchLayout layout = YardBuilder.GenerateLayout();
            foreach (WeedSpot spot in YardBuilder.WeedSpots)
            {
                int patch = layout.PatchAt(new Vector2(spot.Position.x, spot.Position.z));
                Assert.AreNotEqual(PatchLayout.NoGrass, patch, spot.Position.ToString());
                Assert.AreEqual(YardBuilder.AreaAt(spot.Position)?.Id, layout.AreaOfPatch(patch), $"{spot.Position}: the weed is in a patch of its own area");
            }
        }

        [Test]
        public void TaskThresholds()
        {
            var patch = new PatchProgress { MowFraction = 0.975f };
            Assert.IsFalse(patch.MowDone);
            patch.MowFraction = LawnTasks.MowTarget;
            Assert.IsTrue(patch.MowDone);
            Assert.IsTrue(patch.WaterDone, "On a patch without a watering task the water counts as done");
            patch.HasWater = true;
            patch.WaterFraction = 0.94f;
            Assert.IsFalse(patch.AllDone);
            patch.WaterFraction = LawnTasks.WaterTarget;
            patch.WeedsTotal = 2;
            patch.WeedsLeft = 1;
            Assert.IsFalse(patch.AllDone);
            patch.WeedsLeft = 0;
            Assert.IsTrue(patch.AllDone);
        }

        [Test]
        public void NamesAreHiddenUntilFirstProgress()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            int id = lawn.Layout.PatchAt(new Vector2(0f, -8f));
            PatchProgress patch = tasks.Get(id);
            Assert.AreEqual("???", patch.DisplayName);

            int discovered = 0;
            tasks.PatchDiscovered += _ => discovered++;
            lawn.CutSwept(new Vector2(0f, -8f), new Vector2(0f, -8f), 0.28f, Vector2.up, true);
            tasks.Refresh();
            Assert.IsFalse(patch.Discovered, "Where the mower stands (the blade circle) does not count as discovery");

            lawn.CutSwept(new Vector2(0f, -8f), new Vector2(0f, -6f), 0.28f, Vector2.up, true);
            tasks.Refresh();
            tasks.Refresh();
            Assert.IsTrue(patch.Discovered);
            Assert.AreEqual(patch.Name, patch.DisplayName);
            Assert.AreEqual(1, discovered, "Discovery is reported once");
            Assert.IsFalse(patch.Finished);
        }

        [Test]
        public void SmallDrySpillsDoNotCreateWaterTasks()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            int withWater = 0;
            foreach (PatchProgress patch in tasks.Patches)
            {
                Assert.AreEqual(lawn.PatchNeedsWaterCount(patch.Id) >= LawnTasks.MinWaterCells, patch.HasWater, patch.Name);
                if (patch.HasWater) withWater++;
            }
            Assert.GreaterOrEqual(withWater, 3, "Dry patches and spots give a watering task");
        }

        [Test]
        public void CompletingPatchesFinishesThemOnceAndThenTheArea()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            int finished = 0;
            var areas = new List<int>();
            tasks.PatchFinished += _ => finished++;
            tasks.AreaCompleted += areas.Add;
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, tasks.AreaIds);
            Assert.AreEqual(16, tasks.TotalIn(1));
            Assert.AreEqual(6, tasks.TotalIn(2));
            Assert.AreEqual(8, tasks.TotalIn(3));
            Assert.AreEqual(5, tasks.TotalIn(4));
            Assert.AreEqual(2, tasks.NextArea(1), "Without a chain: the next id");
            tasks.AreasOpenedBy = YardBuilder.Definition.AreasOpenedBy;
            Assert.AreEqual(2, tasks.NextArea(1));
            Assert.AreEqual(3, tasks.NextArea(2));
            Assert.AreEqual(4, tasks.NextArea(3));
            Assert.AreEqual(0, tasks.NextArea(4));

            lawn.CompletePatch(1);
            tasks.Refresh();
            tasks.Refresh();
            Assert.IsTrue(tasks.Get(1).Finished);
            Assert.AreEqual(1, finished);
            Assert.AreEqual(1, tasks.FinishedCount);
            Assert.AreEqual(1, tasks.FinishedIn(1));
            CollectionAssert.IsEmpty(areas);

            // A patch of a locked area (even if watered over the fence) does not finish
            int annexPatch = PatchIn(tasks, 2);
            lawn.CompletePatch(annexPatch);
            tasks.Refresh();
            Assert.IsFalse(tasks.IsAreaUnlocked(2));
            Assert.IsFalse(tasks.Get(annexPatch).Finished);
            Assert.IsFalse(tasks.Get(annexPatch).Discovered);

            foreach (PatchProgress patch in tasks.Patches)
                if (patch.Area == 1) lawn.CompletePatch(patch.Id);
            tasks.Refresh();
            Assert.IsTrue(tasks.IsAreaFinished(1));
            Assert.IsFalse(tasks.AllFinished);
            CollectionAssert.AreEqual(new[] { 1 }, areas, "The area finish is reported once");

            tasks.UnlockArea(2);
            Assert.IsTrue(tasks.Get(annexPatch).Finished, "Once unlocked, already done work counts");
            tasks.UnlockArea(3);
            tasks.UnlockArea(4);
            foreach (PatchProgress patch in tasks.Patches)
                lawn.CompletePatch(patch.Id);
            tasks.Refresh();
            Assert.AreEqual(tasks.Total, finished);
            Assert.IsTrue(tasks.AllFinished);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, areas);
        }

        [Test]
        public void RestoringStateSetsFlagsWithoutEvents()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            int events = 0;
            tasks.PatchFinished += _ => events++;
            tasks.PatchDiscovered += _ => events++;
            tasks.AreaCompleted += _ => events++;
            foreach (PatchProgress patch in tasks.Patches)
                if (patch.Area == 1) lawn.CompletePatch(patch.Id);

            tasks.RestoreState(new[] { 2 }, id => id == 17, id => tasks.Get(id).Area == 1);
            Assert.AreEqual(0, events);
            Assert.AreEqual(16, tasks.FinishedCount);
            Assert.IsTrue(tasks.IsAreaFinished(1));
            Assert.IsTrue(tasks.IsAreaUnlocked(2));
            Assert.IsTrue(tasks.Get(17).Discovered);
            tasks.Refresh();
            Assert.AreEqual(0, events, "Restored finishes are not reported again");
        }

        static int PatchIn(LawnTasks tasks, int area)
        {
            foreach (PatchProgress patch in tasks.Patches)
                if (patch.Area == area) return patch.Id;
            Assert.Fail($"No patch in area {area}");
            return 0;
        }

        [Test]
        public void GeometryIsSensible()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            float area = 0f;
            foreach (PatchProgress patch in tasks.Patches)
            {
                Assert.AreEqual(patch.Id, lawn.Layout.PatchAt(patch.Centroid), $"{patch.Name}: the centre must be in its own patch");
                Assert.Greater(patch.Perimeter, 2f * Mathf.Sqrt(Mathf.PI * patch.AreaSquareMeters) * 0.9f, patch.Name);
                area += patch.AreaSquareMeters;
            }
            float expected = 0f;
            foreach (LawnArea a in YardBuilder.Areas)
                expected += a.AreaSquareMeters;
            Assert.AreEqual(expected, area, 1f);
        }

        [Test]
        public void EdgeTextureOutlinesOnlyTheActivePatch()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            var overlay = new LawnOverlay(lawn);
            PatchProgress patch = tasks.Get(3);
            overlay.SetActivePatch(patch);
            Assert.AreEqual(3, overlay.EdgePatch);
            int edges = overlay.EdgeCellCount();
            float boundaryCells = patch.Perimeter / (lawn.Grid.CellSize * 1.08f);
            Assert.That(edges, Is.InRange(boundaryCells * 1.5f, boundaryCells * 3.5f), "A strip ~2-3 cells wide");
            int dashed = 0;
            for (int i = 0; i < lawn.Grid.CellCount; i++)
            {
                if (overlay.IsEdgeCell(i))
                    Assert.AreEqual(3, lawn.Layout.PatchIds[i], "The line is only inside the active patch");
                if (overlay.IsDashCell(i))
                {
                    Assert.IsTrue(overlay.IsEdgeCell(i), "Dashes only on the strip");
                    dashed++;
                }
            }
            Assert.That(dashed / (float)edges, Is.InRange(LawnOverlay.DashFill - 0.12f, LawnOverlay.DashFill + 0.12f),
                "About the dash fraction of the strip is filled");

            overlay.SetActivePatch(null);
            overlay.Tick(1f);
            Assert.AreEqual(0f, overlay.EdgeStrength, "The border fades when there is no active patch");
            Object.DestroyImmediate(overlay.EdgeTexture);
            Object.DestroyImmediate(overlay.GuideTexture);
        }
    }
}
