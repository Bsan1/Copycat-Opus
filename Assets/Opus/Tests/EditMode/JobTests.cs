using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Opus.Core;
using Opus.Lawn;
using Opus.World;
using UnityEngine;

namespace Opus.Tests
{
    /// <summary>U15: client jobs, stars and payment, the client gardens' layouts, job records and save slots.</summary>
    public class JobTests
    {
        GameObject root;
        string directory;

        [TearDown]
        public void TearDown()
        {
            YardBuilder.Use(HomeYard.Create());
            if (root != null)
                Object.DestroyImmediate(root);
            Career.ResetProgress();
            SaveSystem.Slot = SaveSystem.HomeSlot;
            if (directory != null)
            {
                SaveSystem.Directory = null;
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
                directory = null;
            }
        }

        static PatchProgress Patch(float area, bool finished = false, float mow = 0f, bool water = false, float watered = 0f, int weeds = 0, int left = 0) =>
            new PatchProgress
            {
                AreaSquareMeters = area, Finished = finished, MowFraction = mow, HasWater = water, WaterFraction = watered,
                WeedsTotal = weeds, WeedsLeft = left,
            };

        [Test]
        public void CompletionIsAreaWeightedAndCountsEveryTask()
        {
            Assert.AreEqual(0f, Jobs.Completion(new[] { Patch(10f), Patch(30f) }), 1e-4f);
            Assert.AreEqual(1f, Jobs.Completion(new[] { Patch(10f, finished: true), Patch(30f, finished: true) }), 1e-4f);
            // The big patch finished, the small one not at all: 75% weighted by area
            Assert.AreEqual(0.75f, Jobs.Completion(new[] { Patch(10f), Patch(30f, finished: true) }), 1e-4f);
            // Mowed but not watered and not weeded: one of three tasks (no watering is not ignored)
            Assert.AreEqual(1f / 3f, Jobs.Completion(new[] { Patch(10f, mow: 1f, water: true, weeds: 2, left: 2) }), 1e-3f);
            // An unfinished patch does not count in full
            Assert.Less(Jobs.Completion(new[] { Patch(10f, mow: 1f) }), 1f);
        }

        [Test]
        public void StarsFollowCompletionTimeAndPattern()
        {
            JobDef city = Jobs.Find(ClientYards.CityId), manor = Jobs.Find(ClientYards.ManorId);
            Assert.AreEqual(0, Jobs.Stars(city, 0.49f, 10f, false));
            Assert.AreEqual(1, Jobs.Stars(city, 0.5f, 10f, false));
            Assert.AreEqual(1, Jobs.Stars(city, 0.98f, 10f, false));
            Assert.AreEqual(3, Jobs.Stars(city, 1f, city.ParSeconds, false), "A job without a pattern: three when finished within the target time");
            Assert.AreEqual(2, Jobs.Stars(city, 1f, city.ParSeconds + 1f, false), "Two when over time");
            Assert.AreEqual(2, Jobs.Stars(manor, 1f, 60f, false), "Two if the requested pattern was not done");
            Assert.AreEqual(3, Jobs.Stars(manor, 1f, 60f, true));
        }

        [Test]
        public void PaymentFollowsStarsAndLeavingPaysNothing()
        {
            foreach (JobDef job in Jobs.All)
            {
                Assert.AreEqual(4, job.PayByStars.Length, job.Id);
                Assert.AreEqual(0, job.PayByStars[0]);
                for (int s = 1; s < 4; s++)
                    Assert.Greater(job.PayByStars[s], job.PayByStars[s - 1], $"{job.Id}: {s} stars");
                Assert.That(job.PayByStars[1], Is.GreaterThanOrEqualTo(250));
                Assert.That(job.MaxPay, Is.LessThanOrEqualTo(1200));

                JobResult delivered = Jobs.Evaluate(job, 1f, 1f, true, delivered: true);
                Assert.AreEqual((3, job.MaxPay), (delivered.Stars, delivered.Payment));
                JobResult left = Jobs.Evaluate(job, 0.9f, 1f, true, delivered: false);
                Assert.AreEqual((0, 0, false), (left.Stars, left.Payment, left.Delivered));
            }
            Assert.AreEqual("4:05", Jobs.FormatTime(245.7f));
        }

        [Test]
        public void ClientYardsAreValidAndDistinct()
        {
            var ids = new HashSet<string> { HomeYard.Id };
            foreach (JobDef job in Jobs.All)
            {
                YardDefinition yard = job.Yard();
                Assert.AreEqual(job.Id, yard.Id, "The job id is the garden id and the save slot");
                Assert.IsTrue(ids.Add(yard.Id), $"{yard.Id} repeated");
                List<string> errors = yard.Validate();
                Assert.IsEmpty(errors, $"{yard.Id}:\n" + string.Join("\n", errors));
                Assert.AreEqual(1, yard.Props.Count(p => p.Kind == PropKind.House), $"{yard.Id}: the client's house");
                if (job.Pattern.HasValue)
                    Assert.IsTrue(yard.PatchSeeds.Any(s => (s.Position - job.PatternSeed).sqrMagnitude < 1e-4f), $"{yard.Id}: pattern patch in the definition");
            }
            // Characters: the city one small, the manor big, the country house weedy
            float Area(YardDefinition d) => d.Areas.Sum(a => a.AreaSquareMeters);
            Assert.Less(Area(ClientYards.City()), 250f);
            Assert.Greater(Area(ClientYards.Manor()), 1000f);
            Assert.Greater(ClientYards.Country().WeedSpots.Length, ClientYards.Manor().WeedSpots.Length * 2);
        }

        /// <summary>When each client garden is built, weeds, dry spots and the entry point must not end up under props.</summary>
        [TestCase(ClientYards.CityId)]
        [TestCase(ClientYards.ManorId)]
        [TestCase(ClientYards.CountryId)]
        public void ClientYardSpotsAreClearOfObjects(string id)
        {
            YardDefinition yard = Jobs.Find(id).Yard();
            YardBuilder.Use(yard);
            root = new GameObject("ClientGarden");
            YardBuilder.Build(root.transform);
            Physics.SyncTransforms(); // in edit mode the bounds of freshly moved colliders only update this way
            Collider[] colliders = root.GetComponentsInChildren<Collider>().Where(c => c.enabled && c.bounds.max.y > 0.05f).ToArray();
            Collider Blocker(Vector3 p, float margin) => colliders.FirstOrDefault(c =>
            {
                Bounds b = c.bounds;
                b.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
                return b.Contains(new Vector3(p.x, 0.1f, p.z));
            });
            foreach (WeedSpot spot in yard.WeedSpots)
                Assert.IsNull(Blocker(spot.Position, 0.02f), $"{id}: weed {spot.Position} is under: {Blocker(spot.Position, 0.02f)?.name}");
            foreach (DrySpot spot in yard.DrySpots)
                Assert.IsNull(Blocker(new Vector3(spot.Center.x, 0f, spot.Center.y), 0f), $"{id}: dry spot {spot.Center} is under a prop");
            Assert.IsNull(Blocker(yard.SpawnFeet, 0.35f), $"{id}: entry point blocked: {Blocker(yard.SpawnFeet, 0.35f)?.name}");
        }

        [Test]
        public void JobRecordsRoundTripAndReset()
        {
            Career.ResetProgress();
            Career.LoadRecords(new[] { new JobRecord { id = ClientYards.CityId, bestStars = 2, delivered = 3 }, null });
            Assert.AreEqual(2, Career.RecordOf(ClientYards.CityId).bestStars);
            Assert.AreEqual(0, Career.RecordOf(ClientYards.ManorId).delivered, "A job without a record starts from zero");
            JobRecord[] captured = Career.CaptureRecords();
            Assert.AreEqual(2, captured.Length);
            captured[0].bestStars = 0;
            Assert.AreEqual(2, Career.RecordOf(ClientYards.CityId).bestStars, "Returns a copy");
            Career.ResetProgress();
            Assert.AreEqual(0, Career.Records.Count);
            Assert.IsTrue(Career.AtHome);
            Assert.AreEqual(SaveSystem.HomeSlot, Career.CurrentSlot);
            Assert.AreEqual(HomeYard.Id, Career.CurrentYard().Id);
        }

        [Test]
        public void SaveSlotsAreSeparateFiles()
        {
            directory = Path.Combine(Path.GetTempPath(), "OpusJobTests_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.Directory = directory;
            SaveSystem.Slot = SaveSystem.HomeSlot;
            SaveSystem.Write(new SaveData { day = 4, jobRecords = new[] { new JobRecord { id = ClientYards.CityId, bestStars = 3, delivered = 1 } } });
            SaveSystem.Slot = ClientYards.CityId;
            Assert.IsFalse(SaveSystem.Exists, "The job slot is separate");
            SaveSystem.Write(new SaveData { day = 4, playSeconds = 90f });
            Assert.IsTrue(SaveSystem.SlotExists(SaveSystem.HomeSlot) && SaveSystem.SlotExists(ClientYards.CityId));
            Assert.IsTrue(SaveSystem.TryRead(out SaveData job, out string error), error);
            Assert.AreEqual(90f, job.playSeconds);
            SaveSystem.Slot = SaveSystem.HomeSlot;
            Assert.IsTrue(SaveSystem.TryRead(out SaveData home, out error), error);
            Assert.AreEqual(3, home.jobRecords[0].bestStars, "Job records are in the home save");
            SaveSystem.DeleteSlot(ClientYards.CityId);
            Assert.IsFalse(SaveSystem.SlotExists(ClientYards.CityId));
            Assert.IsTrue(SaveSystem.Exists);
        }
    }
}
