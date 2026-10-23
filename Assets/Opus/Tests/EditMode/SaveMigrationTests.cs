using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Opus.Core;
using Opus.Lawn;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class SaveMigrationTests
    {
        readonly List<LawnState> lawns = new List<LawnState>();

        [TearDown]
        public void TearDown()
        {
            foreach (LawnState lawn in lawns)
            {
                Object.DestroyImmediate(lawn.StateTexture);
                Object.DestroyImmediate(lawn.WetTexture);
                Object.DestroyImmediate(lawn.PatchTexture);
                Object.DestroyImmediate(lawn.PaletteTexture);
            }
            lawns.Clear();
        }

        LawnState NewLawn(YardDefinition definition)
        {
            var lawn = new LawnState(definition.GenerateLayout(), GrassVariety.All);
            lawns.Add(lawn);
            return lawn;
        }

        static SaveData Describe(YardDefinition definition, LawnState lawn, float[] weedHealth)
        {
            var data = new SaveData
            {
                gridWidth = lawn.Grid.Width,
                gridHeight = lawn.Grid.Height,
                patchCount = lawn.Layout.PatchCount,
                layoutHash = lawn.LayoutHash(),
                lawn = SaveSystem.Pack(lawn.CaptureSnapshot()),
                weedHealth = weedHealth,
                unlockedAreas = new[] { 1 },
                playerFeet = definition.SpawnFeet,
            };
            SaveMigration.Stamp(data, definition, lawn.Grid);
            return data;
        }

        /// <summary>
        /// The next version of the home garden: a fifth area is added to the east (its seeds at the front: patch ids shift, the grid
        /// grows east) and one seed of the side yard moves (area 2 counts as changed).
        /// </summary>
        static YardDefinition GrownHomeYard()
        {
            YardDefinition def = HomeYard.Create();
            def.Version = HomeYard.Version + 1;
            def.Areas = def.Areas.Concat(new[] { new LawnArea(5, "East garden", 30f, -15f, 40f, 15f) }).ToArray();
            def.Gates = def.Gates.Concat(new[] { new GateDef(2, 5, new Vector3(30f, 0f, -0.8f), 1.6f) }).ToArray();
            PatchSeed[] seeds = def.PatchSeeds.ToArray();
            seeds[16] = new PatchSeed(22f, -11f, seeds[16].Variety, area: 2);
            def.PatchSeeds = new[] { new PatchSeed(35f, -6f, 1, area: 5), new PatchSeed(35f, 6f, 2, area: 5) }.Concat(seeds).ToArray();
            def.PatchInfos = new[] { new PatchInfo("East 1", "#C94F6D"), new PatchInfo("East 2", "#4FA3A5") }.Concat(def.PatchInfos).ToArray();
            def.WeedSpots = def.WeedSpots.Concat(new[] { new WeedSpot(35f, 0f) }).ToArray();
            return def;
        }

        [Test]
        public void UnstampedSavesOfTheFirstVersionMigrate()
        {
            YardDefinition firstDef = HomeYard.FirstVersion(), home = HomeYard.Create();
            LawnState oldLawn = NewLawn(firstDef);
            oldLawn.CutSwept(new Vector2(-4f, -10f), new Vector2(4f, -10f), 0.3f, Vector2.right, true);
            oldLawn.CutSwept(new Vector2(21f, -10f), new Vector2(27f, -10f), 0.3f, Vector2.right, true);
            // U10–U12 save: version 2, no garden stamp
            var old = new SaveData
            {
                version = 2,
                gridWidth = oldLawn.Grid.Width,
                gridHeight = oldLawn.Grid.Height,
                patchCount = oldLawn.Layout.PatchCount,
                layoutHash = oldLawn.LayoutHash(),
                lawn = SaveSystem.Pack(oldLawn.CaptureSnapshot()),
                weedHealth = Enumerable.Repeat(1f, firstDef.WeedSpots.Length).ToArray(),
                unlockedAreas = new[] { 1, 2 },
                finishedPatches = new[] { 3, 17 },
                day = 6,
            };
            Assert.IsTrue(SaveMigration.StampLegacy(old, firstDef));
            Assert.AreEqual(HomeYard.Id, old.yardId);

            LawnState newLawn = NewLawn(home);
            SaveData fresh = Describe(home, newLawn, Enumerable.Repeat(1f, home.WeedSpots.Length).ToArray());
            Assert.IsTrue(SaveMigration.TryMigrate(old, fresh, out SaveData migrated, out string error), error);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, SaveMigration.KeptAreas(old, fresh));
            Assert.IsTrue(newLawn.RestoreSnapshot(SaveSystem.Unpack(migrated.lawn)));
            Assert.IsTrue(IsCutAt(newLawn, new Vector2(0f, -10f)));
            Assert.IsTrue(IsCutAt(newLawn, new Vector2(24f, -10f)));
            Assert.IsFalse(IsCutAt(newLawn, new Vector2(0f, -22f)), "The new orchard starts fresh");
            CollectionAssert.AreEqual(new[] { 3, 17 }, migrated.finishedPatches, "The ids of the first 22 patches did not change");
            CollectionAssert.AreEqual(new[] { 1, 2 }, migrated.unlockedAreas);
            Assert.AreEqual(6, migrated.day);

            // An unstamped save of another size does not count as the first version of this garden
            var stranger = new SaveData { version = 2, gridWidth = 100, gridHeight = 100, patchCount = 3 };
            Assert.IsFalse(SaveMigration.StampLegacy(stranger, firstDef));
        }

        [Test]
        public void GrownYardIsValid()
        {
            List<string> errors = GrownHomeYard().Validate();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void MigrationKeepsUnchangedAreasAndStartsChangedOnesFresh()
        {
            YardDefinition oldDef = HomeYard.Create(), newDef = GrownHomeYard();
            LawnState oldLawn = NewLawn(oldDef);
            oldLawn.CutSwept(new Vector2(-4f, -10f), new Vector2(4f, -10f), 0.3f, Vector2.right, true);   // area 1
            oldLawn.CutSwept(new Vector2(21f, -10f), new Vector2(27f, -10f), 0.3f, Vector2.right, true);  // area 2
            float[] oldWeeds = Enumerable.Repeat(1f, oldDef.WeedSpots.Length).ToArray();
            oldWeeds[0] = 0f;   // area 1: (-17.4, -14.4)
            oldWeeds[16] = 0f;  // area 2: (29.5, -14.5)
            SaveData old = Describe(oldDef, oldLawn, oldWeeds);
            old.unlockedAreas = new[] { 1, 2 };
            old.discoveredPatches = new[] { 1, 3, 17 };
            old.finishedPatches = new[] { 3, 17 };
            old.everFinishedPatches = new[] { 3, 17 };
            old.day = 4;
            old.points = 777;
            old.pattern = 2;
            old.patternPatch = 3;
            old.playerFeet = new Vector3(1f, 0.05f, -5f);
            old.hiddenFinds = new[]
            {
                new LostItems.SavedItem { id = "para", position = new Vector3(2f, 0f, 3f) },
                new LostItems.SavedItem { id = "anahtar", position = new Vector3(25f, 0f, 3f) },
            };

            LawnState newLawn = NewLawn(newDef);
            SaveData fresh = Describe(newDef, newLawn, Enumerable.Repeat(1f, newDef.WeedSpots.Length).ToArray());
            Assert.AreNotEqual(old.layoutHash, fresh.layoutHash);

            Assert.IsTrue(SaveMigration.TryMigrate(old, fresh, out SaveData migrated, out string error), error);
            CollectionAssert.AreEquivalent(new[] { 1, 3, 4 }, SaveMigration.KeptAreas(old, fresh), "Area 2's seed moved");
            Assert.AreEqual((fresh.gridWidth, fresh.gridHeight, fresh.patchCount, fresh.layoutHash),
                (migrated.gridWidth, migrated.gridHeight, migrated.patchCount, migrated.layoutHash));

            Assert.IsTrue(newLawn.RestoreSnapshot(SaveSystem.Unpack(migrated.lawn)));
            Assert.IsTrue(IsCutAt(newLawn, new Vector2(0f, -10f)), "Area 1's mown grass is kept (even though the grid shifts)");
            Assert.IsFalse(IsCutAt(newLawn, new Vector2(24f, -10f)), "The changed area 2 starts fresh");
            Assert.IsFalse(IsCutAt(newLawn, new Vector2(35f, -10f)), "The new area 5 starts fresh");

            // Area 1's patches shifted by two (area 3's seeds were added at the front), area 2's were dropped
            CollectionAssert.AreEqual(new[] { 5 }, migrated.finishedPatches);
            CollectionAssert.AreEqual(new[] { 3, 5 }, migrated.discoveredPatches);
            Assert.AreEqual((2, 5), (migrated.pattern, migrated.patternPatch));

            Assert.AreEqual(0f, migrated.weedHealth[0], "A weed pulled in area 1 stays pulled");
            Assert.AreEqual(1f, migrated.weedHealth[16], "Area 2's weed starts fresh");
            Assert.AreEqual(newDef.WeedSpots.Length, migrated.weedHealth.Length);

            CollectionAssert.AreEqual(new[] { 1, 2 }, migrated.unlockedAreas);
            Assert.AreEqual((4, 777), (migrated.day, migrated.points));
            Assert.AreEqual(old.playerFeet, migrated.playerFeet);
            Assert.AreEqual(new[] { "para" }, migrated.hiddenFinds.Select(f => f.id).ToArray(), "Only the item in the kept area stays");
            Assert.AreEqual((HomeYard.Id, HomeYard.Version + 1), (migrated.yardId, migrated.yardVersion));
        }

        [Test]
        public void MigrationRefusesOtherYardsAndUnstampedSaves()
        {
            YardDefinition def = HomeYard.Create();
            LawnState lawn = NewLawn(def);
            float[] weeds = Enumerable.Repeat(1f, def.WeedSpots.Length).ToArray();
            SaveData fresh = Describe(def, lawn, weeds);

            SaveData other = Describe(def, lawn, weeds);
            other.yardId = "neighbour";
            Assert.IsFalse(SaveMigration.TryMigrate(other, fresh, out _, out string error));
            StringAssert.Contains("another garden", error);

            SaveData unstamped = Describe(def, lawn, weeds);
            unstamped.version = 2;
            unstamped.areas = new SavedArea[0];
            Assert.IsFalse(SaveMigration.TryMigrate(unstamped, fresh, out _, out error));
            StringAssert.Contains("migration info", error);
        }

        static bool IsCutAt(LawnState lawn, Vector2 point)
        {
            Assert.IsTrue(lawn.Grid.TryGetCell(point, out int x, out int z), point.ToString());
            return lawn.IsCut(lawn.Grid.Index(x, z));
        }
    }
}
