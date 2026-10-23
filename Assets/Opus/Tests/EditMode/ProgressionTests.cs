using System.Collections.Generic;
using NUnit.Framework;
using Opus.Core;
using Opus.Lawn;
using Opus.Tools;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    /// <summary>U10: day cycle and regrowth, points and upgrades, pattern tasks, lost items.</summary>
    public class ProgressionTests
    {
        readonly List<LawnState> created = new List<LawnState>();
        readonly List<Object> objects = new List<Object>();

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
            foreach (Object obj in objects)
                if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
        }

        static int CellAt(LawnState lawn, float x, float z)
        {
            Assert.IsTrue(lawn.Grid.TryGetCell(new Vector2(x, z), out int cx, out int cz));
            return lawn.Grid.Index(cx, cz);
        }

        // ---------- Regrowth ----------

        [Test]
        public void CutGrassRegrowsPastTheThresholdOvernight()
        {
            LawnState lawn = NewLawn();
            lawn.CutSwept(new Vector2(-4f, -8f), new Vector2(4f, -8f), 0.3f, Vector2.right, true);
            int cell = CellAt(lawn, 0f, -8f);
            Assert.IsTrue(lawn.IsCut(cell));
            int cut = lawn.TotalCutCells;

            int regrown = lawn.GrowOvernight(40f, null, 0f);
            Assert.AreEqual(0, regrown, "Grass growing below the threshold counts as mown");
            Assert.IsTrue(lawn.IsCut(cell));
            Assert.AreEqual(40, lawn.HeightAt(cell));
            Assert.AreEqual(cut, lawn.TotalCutCells);

            regrown = lawn.GrowOvernight(40f, null, 0f);
            Assert.AreEqual(cut, regrown, "On the second night all passed the threshold");
            Assert.IsFalse(lawn.IsCut(cell));
            Assert.AreEqual(0, lawn.TotalCutCells);
            Assert.AreEqual((128, 128), ((int)lawn.CellAt(cell).b, (int)lawn.CellAt(cell).a), "Regrown grass loses its stripe direction");
        }

        [Test]
        public void SlightlyGrownGrassIsRecutWithoutCountingTwice()
        {
            LawnState lawn = NewLawn();
            lawn.CutSwept(new Vector2(-2f, -8f), new Vector2(2f, -8f), 0.3f, Vector2.right, true);
            int cut = lawn.TotalCutCells;
            lawn.GrowOvernight(30f, null, 0f);
            int again = lawn.CutSwept(new Vector2(-2f, -8f), new Vector2(2f, -8f), 0.3f, Vector2.left, true);
            Assert.AreEqual(0, again, "A cell already counted as mown is not counted again");
            Assert.AreEqual(cut, lawn.TotalCutCells);
            Assert.AreEqual(LawnState.Cut, lawn.HeightAt(CellAt(lawn, 0f, -8f)), "But it still gets shorter");
        }

        [Test]
        public void DrySpotsDryAgainUnlessItRains()
        {
            LawnState lawn = NewLawn();
            DrySpot spot = YardBuilder.DrySpots[0];
            int cell = CellAt(lawn, spot.Center.x, spot.Center.y);
            lawn.Water(spot.Center, 1.5f, 5f);
            Assert.IsTrue(lawn.IsWatered(cell));

            lawn.GrowOvernight(0f, null, DayCycle.Drying);
            Assert.IsFalse(lawn.IsWatered(cell), "Dried out overnight, must be watered again");
            Assert.Greater(lawn.DrynessAt(cell), LawnState.WateredDryness);

            lawn.GrowOvernight(0f, null, DayCycle.Drying, rain: true);
            Assert.IsTrue(lawn.IsWatered(cell), "The rain watered it");
            Assert.Greater(lawn.WetnessAt(cell), 0.5f, "The grass is wet in the morning");
        }

        [Test]
        public void PatchesReopenAfterNightButAreaStaysCompleted()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            var finishedEvents = new List<int>();
            var reopened = new List<int>();
            var areas = new List<int>();
            tasks.PatchFinished += p => finishedEvents.Add(p.Id);
            tasks.PatchReopened += p => reopened.Add(p.Id);
            tasks.AreaCompleted += areas.Add;

            foreach (PatchProgress p in tasks.Patches)
                if (p.Area == 1) lawn.CompletePatch(p.Id);
            tasks.Refresh();
            CollectionAssert.AreEqual(new[] { 1 }, areas);
            Assert.AreEqual(16, tasks.FinishedCount);

            lawn.GrowOvernight(DayCycle.Growth * 1.5f, null, DayCycle.Drying);
            tasks.Refresh();
            Assert.AreEqual(16, reopened.Count, "Every patch needs work again");
            Assert.AreEqual(0, tasks.FinishedIn(1));
            Assert.IsTrue(tasks.Get(1).EverFinished);
            Assert.IsTrue(tasks.IsAreaCompleted(1), "The area stays complete (the gate does not close)");

            lawn.CompletePatch(1);
            tasks.Refresh();
            Assert.IsTrue(tasks.Get(1).Finished);
            Assert.AreEqual(2, tasks.Get(1).Completions, "Finished again on upkeep");
            foreach (PatchProgress p in tasks.Patches)
                if (p.Area == 1) lawn.CompletePatch(p.Id);
            tasks.Refresh();
            CollectionAssert.AreEqual(new[] { 1 }, areas, "The area finish is only reported the first time");
        }

        [Test]
        public void DayCycleIsDeterministicAndFirstNightIsDry()
        {
            Assert.IsFalse(DayCycle.RainsAfter(1));
            Assert.AreEqual(DayCycle.RainsAfter(5), DayCycle.RainsAfter(5));
            int rainy = 0;
            for (int day = 2; day < 202; day++)
                if (DayCycle.RainsAfter(day)) rainy++;
            Assert.That(rainy, Is.InRange(25, 75), "About a quarter of the nights are rainy");

            var cycle = new DayCycle(22);
            for (int id = 1; id <= 22; id++)
                Assert.That(cycle.PatchGrowth[id], Is.InRange(0.8f, 1.35f));
        }

        [Test]
        public void EndingTheDayRegrowsAndAdvances()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            var root = new GameObject("Weeds");
            objects.Add(root);
            WeedField weeds = WeedField.Create(root.transform, YardBuilder.WeedSpots, lawn.Layout);
            foreach (PatchProgress p in tasks.Patches)
                if (p.Area == 1) lawn.CompletePatch(p.Id);
            tasks.Refresh();

            var cycle = new DayCycle(lawn.Layout.PatchCount);
            cycle.Today.Points = 50f;
            NightReport report = cycle.EndDay(lawn, weeds, tasks);
            Assert.AreEqual(1, report.FinishedDay);
            Assert.AreEqual(2, cycle.Day);
            Assert.IsFalse(report.Rain);
            Assert.Greater(report.RegrownCells, 0);
            Assert.Greater(report.PatchesReopened, 0);
            Assert.AreEqual(0f, cycle.Today.Points, "The new day's counters are zero");
        }

        // ---------- Points and upgrades ----------

        [Test]
        public void WalletAccumulatesFractionsAndSpends()
        {
            var wallet = new Wallet();
            for (int i = 0; i < 10; i++) wallet.Add(0.25f);
            Assert.AreEqual(2, wallet.Points);
            Assert.IsFalse(wallet.TrySpend(3));
            Assert.IsTrue(wallet.TrySpend(2));
            Assert.AreEqual(0, wallet.Points);
            Assert.AreEqual(2, wallet.Earned);
        }

        [Test]
        public void UpgradesCostLevelAndApply()
        {
            var wallet = new Wallet();
            var upgrades = new Upgrades();
            var changed = new List<UpgradeId>();
            upgrades.Changed += changed.Add;
            Assert.AreEqual(1f, upgrades.DeckScale);
            Assert.IsFalse(upgrades.TryBuy(UpgradeId.WideDeck, wallet), "No points");

            wallet.Add(5000f);
            Assert.IsTrue(upgrades.TryBuy(UpgradeId.WideDeck, wallet));
            Assert.IsTrue(upgrades.TryBuy(UpgradeId.WideDeck, wallet));
            Assert.IsFalse(upgrades.TryBuy(UpgradeId.WideDeck, wallet), "Top level");
            Assert.AreEqual(1.3f, upgrades.DeckScale, 1e-4f);
            Assert.AreEqual(5000 - 220 - 520, wallet.Points);
            Assert.IsTrue(upgrades.TryBuy(UpgradeId.RainHead, wallet));
            Assert.IsTrue(upgrades.HasRainHead);
            CollectionAssert.AreEqual(new[] { UpgradeId.WideDeck, UpgradeId.WideDeck, UpgradeId.RainHead }, changed);

            var restored = new Upgrades();
            restored.Restore(new[] { 9, 1, -3 });
            Assert.AreEqual(2, restored.Level(UpgradeId.WideDeck), "The level in the save is clamped to the limit");
            Assert.AreEqual(1, restored.Level(UpgradeId.StrongEngine));
            Assert.AreEqual(0, restored.Level(UpgradeId.ThickLine));
        }

        [Test]
        public void PressureAndRainHeadChangeTheHose()
        {
            SprayMode narrow = SprayMode.Narrow.Upgraded(1.5f, 2f);
            Assert.AreEqual(SprayMode.Narrow.MaxReach + 2f, narrow.MaxReach, 1e-4f);
            Assert.AreEqual(SprayMode.Narrow.Rate * 1.5f, narrow.Rate, 1e-4f);
            Assert.AreEqual(SprayMode.Wide.MaxReach + 0.8f, SprayMode.Wide.Upgraded(1f, 2f).MaxReach, 1e-4f);
            Assert.Greater(SprayMode.Rain.Radius, SprayMode.Wide.Radius);
            Assert.Greater(SprayMode.Rain.Coverage, SprayMode.Wide.Coverage, "The sprinkler waters a wide area more efficiently");
        }

        // ---------- Pattern ----------

        [Test]
        public void PatternDirectionsAlternateByBand()
        {
            LawnState lawn = NewLawn();
            var stripes = new PatternTask(PatternKind.StripesNorthSouth, 1, lawn);
            Vector2 o = stripes.Origin;
            Assert.AreEqual(Vector2.up, stripes.Desired(o + new Vector2(0.1f, 0f)));
            Assert.AreEqual(Vector2.down, stripes.Desired(o + new Vector2(PatternTask.BandWidth + 0.1f, 0f)));
            Assert.AreEqual(Vector2.up, stripes.Desired(o + new Vector2(PatternTask.BandWidth * 2f + 0.1f, 5f)), "The same direction along a band");

            var checker = new PatternTask(PatternKind.Checker, 1, lawn);
            Vector2 c = checker.Origin;
            Vector2 a = checker.Desired(c + new Vector2(0.1f, 0.1f));
            Assert.AreEqual(-a, checker.Desired(c + new Vector2(PatternTask.CheckerSize + 0.1f, 0.1f)));
            Assert.AreEqual(a, checker.Desired(c + new Vector2(PatternTask.CheckerSize + 0.1f, PatternTask.CheckerSize + 0.1f)));
        }

        [Test]
        public void MowingThePatternCompletesTheTask()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            int patch = lawn.Layout.PatchAt(new Vector2(1f, -3f));
            var task = new PatternTask(PatternKind.StripesNorthSouth, patch, lawn);
            PatchProgress info = tasks.Get(patch);
            float reach = info.Radius + 1f;

            // Mowing in the wrong direction (always north) does not count as the pattern
            MowLanes(lawn, info.Centroid, reach, _ => Vector2.up);
            Assert.IsFalse(task.Evaluate(lawn));
            Assert.That(task.Score, Is.InRange(0.3f, 0.7f), "About half right by chance");

            // Mow every band in the requested direction
            MowLanes(lawn, info.Centroid, reach, x => task.Desired(new Vector2(x, info.Centroid.y)));
            Assert.IsTrue(task.Evaluate(lawn));
            Assert.IsTrue(task.Completed);
            Assert.GreaterOrEqual(task.Score, task.Goal);
            Assert.IsFalse(task.Evaluate(lawn), "Completion is reported once");

            var guide = new byte[lawn.Grid.CellCount];
            task.WriteGuide(lawn, guide);
            foreach (byte b in guide) Assert.AreEqual(0, b, "The guide turns off once completed");
        }

        /// <summary>North-south stripes 0.1 m apart; the direction is given by x.</summary>
        static void MowLanes(LawnState lawn, Vector2 center, float reach, System.Func<float, Vector2> direction)
        {
            for (float x = center.x - reach; x <= center.x + reach; x += 0.1f)
            {
                Vector2 dir = direction(x);
                Vector2 from = new Vector2(x, center.y - reach * dir.y), to = new Vector2(x, center.y + reach * dir.y);
                lawn.CutSwept(from, to, 0.08f, dir, true);
            }
        }

        [Test]
        public void DailyPatternIsChosenFromUnlockedPatches()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            for (int day = 1; day <= 10; day++)
            {
                PatternTask task = PatternTask.ForDay(day, lawn, tasks);
                Assert.IsNotNull(task);
                Assert.IsTrue(tasks.IsAreaUnlocked(tasks.Get(task.Patch).Area), "No pattern is requested in a locked area");
                if (day < 3) Assert.AreNotEqual(PatternKind.Checker, task.Kind, "Checker after the third day");
                Assert.AreEqual(task.Kind, PatternTask.ForDay(day, lawn, tasks).Kind, "Seeded");
            }
        }

        // ---------- Lost items ----------

        [Test]
        public void LostItemsHideInTallGrassAndAreFoundWhenCut()
        {
            LawnState lawn = NewLawn();
            var tasks = new LawnTasks(lawn, YardBuilder.PatchInfos);
            var root = new GameObject("Root");
            objects.Add(root);
            LostItems items = LostItems.Create(root.transform, lawn);
            int hidden = items.HideForDay(1, p => tasks.IsAreaUnlocked(tasks.Get(p).Area));
            Assert.AreEqual(LostItems.MaxActive, hidden);
            List<Vector3> positions = items.HiddenPositions();
            foreach (Vector3 p in positions)
                Assert.AreEqual(1, YardBuilder.AreaAt(p)?.Id, "Only in the unlocked area");
            Assert.AreEqual(0, items.HideForDay(1, _ => true), "At most three hidden items");

            var found = new List<string>();
            items.Found += (def, _) => found.Add(def.Id);
            Vector3 first = positions[0];
            lawn.CutSwept(new Vector2(first.x - 0.3f, first.z), new Vector2(first.x + 0.3f, first.z), 0.2f, Vector2.right, true);
            Assert.AreEqual(1, items.CheckFound());
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual(1, items.Collected.Count);
            Assert.AreEqual(0, items.CheckFound(), "Found only once");

            // Save round trip
            string[] collected = items.CaptureCollected();
            LostItems.SavedItem[] saved = items.CaptureActive();
            Assert.AreEqual(LostItems.MaxActive - 1, saved.Length);
            LostItems other = LostItems.Create(root.transform, lawn);
            other.Restore(collected, saved);
            CollectionAssert.AreEquivalent(collected, other.CaptureCollected());
            Assert.AreEqual(saved.Length, other.HiddenPositions().Count);
        }

        [Test]
        public void CatalogHasTwelveDistinctEverydayFinds()
        {
            var ids = new HashSet<string>();
            foreach (FindDef def in LostItems.Catalog)
            {
                Assert.IsTrue(ids.Add(def.Id), def.Id);
                Assert.IsFalse(string.IsNullOrEmpty(def.Name));
                var root = new GameObject("Model");
                objects.Add(root);
                def.Build(root.transform);
                Assert.Greater(root.GetComponentsInChildren<MeshRenderer>().Length, 0, def.Name);
            }
            Assert.AreEqual(12, ids.Count);
        }
    }
}
