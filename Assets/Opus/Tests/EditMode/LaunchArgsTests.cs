using NUnit.Framework;
using Opus.Core;

namespace Opus.Tests
{
    public class LaunchArgsTests
    {
        [Test]
        public void ParsesScenarioShotsAndSize()
        {
            LaunchArgs args = LaunchArgs.Parse(new[]
            {
                "CopycatOpus.exe", "-batchmode", "-scenario", "u0_overview", "-shots", @"C:\output folder", "-shot-size", "1280x720", "-save-dir", @"D:\save folder",
            });

            Assert.IsTrue(args.HasScenario);
            Assert.AreEqual("u0_overview", args.Scenario);
            Assert.AreEqual(@"C:\output folder", args.ShotsDirectory);
            Assert.AreEqual(1280, args.ShotWidth);
            Assert.AreEqual(720, args.ShotHeight);
            Assert.AreEqual(@"D:\save folder", args.SaveDirectory);
        }

        [Test]
        public void UsesDefaultsWithoutFlags()
        {
            LaunchArgs args = LaunchArgs.Parse(new[] { "CopycatOpus.exe" });

            Assert.IsFalse(args.HasScenario);
            Assert.IsNull(args.ShotsDirectory);
            Assert.IsNull(args.SaveDirectory);
            Assert.AreEqual(LaunchArgs.DefaultShotWidth, args.ShotWidth);
            Assert.AreEqual(LaunchArgs.DefaultShotHeight, args.ShotHeight);
        }

        [Test]
        public void FlagWithoutValueIsIgnored()
        {
            Assert.IsFalse(LaunchArgs.Parse(new[] { "-scenario" }).HasScenario);
        }

        [Test]
        public void InvalidSizeKeepsDefault()
        {
            LaunchArgs args = LaunchArgs.Parse(new[] { "-shot-size", "12x12" });
            Assert.AreEqual(LaunchArgs.DefaultShotWidth, args.ShotWidth);
        }

        [TestCase("1920x1080", true)]
        [TestCase("1920X1080", true)]
        [TestCase("10x10", false)]
        [TestCase("abc", false)]
        [TestCase("1920x", false)]
        [TestCase(null, false)]
        public void SizeParsing(string value, bool expected)
        {
            Assert.AreEqual(expected, LaunchArgs.TryParseSize(value, out _, out _));
        }

        [TestCase("u0_overview")]
        [TestCase("u1_mower")]
        public void ScenarioExists(string name)
        {
            Assert.IsTrue(Scenarios.TryGet(name, out ScenarioScript script));
            Assert.IsNotNull(script);
        }

        [Test]
        public void UnknownScenarioIsRejected()
        {
            Assert.IsFalse(Scenarios.TryGet("no_such_scenario", out _));
            Assert.IsFalse(Scenarios.TryGet(null, out _));
        }
    }
}
