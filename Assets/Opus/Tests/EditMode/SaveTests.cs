using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Opus.Core;
using Opus.Lawn;
using Opus.Player;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    public class SaveTests
    {
        readonly List<LawnState> created = new List<LawnState>();
        string directory;

        LawnState NewLawn()
        {
            var lawn = new LawnState(YardBuilder.GenerateLayout(), GrassVariety.All);
            created.Add(lawn);
            return lawn;
        }

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "OpusSaveTests_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.Directory = directory;
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
            SaveSystem.Directory = null;
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        [Test]
        public void PackedBytesRoundTrip()
        {
            var bytes = new byte[5000];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)(i % 7 == 0 ? i : 0);
            string text = SaveSystem.Pack(bytes);
            Assert.Less(text.Length, bytes.Length, "Uniform data compresses");
            CollectionAssert.AreEqual(bytes, SaveSystem.Unpack(text));
            Assert.IsNull(SaveSystem.Unpack("this is not base64!"));
            Assert.IsNull(SaveSystem.Unpack(System.Convert.ToBase64String(new byte[] { 1, 2, 3 })), "data that is not gzip");
        }

        [Test]
        public void LawnSnapshotRoundTripsWorkAndCounters()
        {
            LawnState source = NewLawn();
            source.CutSwept(new Vector2(-6f, -10f), new Vector2(6f, -10f), 0.28f, Vector2.right, true);
            source.CutSwept(new Vector2(6f, -9.5f), new Vector2(-6f, -9.5f), 0.28f, Vector2.left, true);
            DrySpot spot = YardBuilder.DrySpots[0];
            source.Water(spot.Center, 0.9f, 0.8f);
            source.Water(spot.Center + new Vector2(0.7f, 0f), 0.9f, 0.4f);

            byte[] snapshot = source.CaptureSnapshot();
            Assert.AreEqual(source.Grid.CellCount * LawnState.SnapshotBytesPerCell, snapshot.Length);

            LawnState restored = NewLawn();
            Assert.IsTrue(restored.RestoreSnapshot(SaveSystem.Unpack(SaveSystem.Pack(snapshot))));
            Assert.AreEqual(source.TotalCutCells, restored.TotalCutCells);
            Assert.AreEqual(source.TotalWatered, restored.TotalWatered);
            for (int patch = 1; patch <= source.Layout.PatchCount; patch++)
            {
                Assert.AreEqual(source.PatchCutCount(patch), restored.PatchCutCount(patch));
                Assert.AreEqual(source.PatchWateredCount(patch), restored.PatchWateredCount(patch));
            }
            for (int i = 0; i < source.Grid.CellCount; i++)
            {
                Assert.AreEqual(source.IsCut(i), restored.IsCut(i));
                Assert.AreEqual(source.IsWatered(i), restored.IsWatered(i));
                Assert.AreEqual(source.DrynessAt(i), restored.DrynessAt(i), 0.003f);
                Assert.AreEqual(source.GreeningAt(i), restored.GreeningAt(i), 1e-6f);
                Color32 a = source.CellAt(i), b = restored.CellAt(i);
                Assert.AreEqual((a.b, a.a), (b.b, b.a), "The stripe direction is kept");
                if (restored.IsCut(i))
                    Assert.AreEqual(LawnState.Cut, b.r, "Mown grass is short without animation");
                Assert.AreEqual(0f, restored.WetnessAt(i), "Wetness is transient");
            }
        }

        [Test]
        public void PartialHeightsAndOldSnapshotsRestore()
        {
            LawnState source = NewLawn();
            source.CutSwept(new Vector2(-3f, -10f), new Vector2(3f, -10f), 0.3f, Vector2.right, true);
            source.GrowOvernight(40f, null, 0f);
            byte[] snapshot = source.CaptureSnapshot();
            Assert.IsTrue(source.Grid.TryGetCell(new Vector2(0f, -10f), out int x, out int z));
            int cell = source.Grid.Index(x, z);

            LawnState restored = NewLawn();
            Assert.IsTrue(restored.RestoreSnapshot(snapshot));
            Assert.AreEqual(40, restored.HeightAt(cell), "A partial height is kept");
            Assert.AreEqual(source.TotalCutCells, restored.TotalCutCells);

            // U8 save: 6 bytes per cell, no height → the mown ones short, the rest long
            var legacy = new byte[source.Grid.CellCount * LawnState.LegacySnapshotBytesPerCell];
            for (int i = 0; i < source.Grid.CellCount; i++)
                System.Array.Copy(snapshot, i * LawnState.SnapshotBytesPerCell, legacy, i * LawnState.LegacySnapshotBytesPerCell, LawnState.LegacySnapshotBytesPerCell);
            LawnState old = NewLawn();
            Assert.IsTrue(old.RestoreSnapshot(legacy));
            Assert.AreEqual(LawnState.Cut, old.HeightAt(cell));
            Assert.AreEqual(source.TotalCutCells, old.TotalCutCells);
        }

        [Test]
        public void SnapshotOfWrongSizeIsRejected()
        {
            LawnState lawn = NewLawn();
            lawn.CutSwept(new Vector2(0f, -8f), new Vector2(2f, -8f), 0.28f, Vector2.right, true);
            int cut = lawn.TotalCutCells;
            Assert.IsFalse(lawn.RestoreSnapshot(new byte[10]));
            Assert.IsFalse(lawn.RestoreSnapshot(null));
            Assert.AreEqual(cut, lawn.TotalCutCells, "A rejected save does not change the state");
        }

        [Test]
        public void LayoutHashIdentifiesTheYard()
        {
            LawnState a = NewLawn(), b = NewLawn();
            Assert.AreEqual(a.LayoutHash(), b.LayoutHash());
            b.Layout.ClearCell(b.Grid.Index(100, 100));
            Assert.AreNotEqual(a.LayoutHash(), b.LayoutHash());
        }

        [Test]
        public void SaveFileRoundTripsAndRejectsOtherVersions()
        {
            Assert.IsFalse(SaveSystem.Exists);
            Assert.IsFalse(SaveSystem.TryRead(out _, out _));
            var data = new SaveData
            {
                gridWidth = 480,
                gridHeight = 300,
                playerFeet = new Vector3(21.5f, 0.05f, -3f),
                playerYaw = 95f,
                toolSlot = 2,
                sprayMode = 1,
                unlockedAreas = new[] { 1, 2 },
                finishedPatches = new[] { 3, 7 },
                weedHealth = new[] { 0f, 0.35f, 1.2f },
                lawn = SaveSystem.Pack(new byte[] { 9, 8, 7 }),
            };
            SaveSystem.Write(data);
            SaveSystem.Write(data); // overwriting works too
            Assert.IsTrue(SaveSystem.Exists);
            Assert.IsTrue(SaveSystem.TryRead(out SaveData read, out string error), error);
            Assert.AreEqual(data.playerFeet, read.playerFeet);
            Assert.AreEqual(data.playerYaw, read.playerYaw);
            Assert.AreEqual(2, read.toolSlot);
            CollectionAssert.AreEqual(data.unlockedAreas, read.unlockedAreas);
            CollectionAssert.AreEqual(data.finishedPatches, read.finishedPatches);
            CollectionAssert.AreEqual(data.weedHealth, read.weedHealth);
            CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, SaveSystem.Unpack(read.lawn));
            Assert.IsFalse(File.Exists(SaveSystem.FilePath + ".tmp"));

            File.WriteAllText(SaveSystem.FilePath, File.ReadAllText(SaveSystem.FilePath).Replace($"\"version\":{SaveData.CurrentVersion}", "\"version\":99"));
            Assert.IsFalse(SaveSystem.TryRead(out _, out error));
            StringAssert.Contains("version", error);
            File.WriteAllText(SaveSystem.FilePath, "{corrupt");
            Assert.IsFalse(SaveSystem.TryRead(out _, out _));

            SaveSystem.Delete();
            Assert.IsFalse(SaveSystem.Exists);
        }

        [Test]
        public void SettingsClampAndApplyWithoutPersisting()
        {
            bool persistent = GameSettings.Persistent;
            GameSettings.Persistent = false;
            try
            {
                GameSettings.SetLookSensitivity(9f);
                Assert.AreEqual(GameSettings.MaxSensitivity, GameSettings.LookSensitivity);
                Assert.AreEqual(GameSettings.MaxSensitivity, InputReader.LookSensitivity);
                GameSettings.SetLookSensitivity(0.8f);
                Assert.AreEqual(0.8f, InputReader.LookSensitivity, 1e-6f);
                GameSettings.SetInvertY(true);
                Assert.IsTrue(InputReader.InvertY);
                GameSettings.SetGrassDensity(-3);
                Assert.AreEqual(0, GameSettings.GrassDensity);
                GameSettings.SetGrassDensity(7);
                Assert.AreEqual(GameSettings.GrassDensityValues.Length - 1, GameSettings.GrassDensity);
                Assert.AreEqual(GameSettings.GrassDensityNames.Length, GameSettings.GrassDensityValues.Length);
            }
            finally
            {
                GameSettings.SetLookSensitivity(1f);
                GameSettings.SetInvertY(false);
                GameSettings.Persistent = persistent;
            }
        }
    }
}
