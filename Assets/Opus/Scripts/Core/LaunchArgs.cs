using System;
using System.Collections.Generic;

namespace Opus.Core
{
    /// <summary>Command line arguments of the player exe: -scenario, -shots, -shot-size, -save-dir.</summary>
    public sealed class LaunchArgs
    {
        public const int DefaultShotWidth = 1920;
        public const int DefaultShotHeight = 1080;

        public string Scenario { get; private set; }
        public string ShotsDirectory { get; private set; }
        /// <summary>Save folder (if not given, the persistent data folder; a temporary folder in scenarios).</summary>
        public string SaveDirectory { get; private set; }
        public int ShotWidth { get; private set; } = DefaultShotWidth;
        public int ShotHeight { get; private set; } = DefaultShotHeight;

        public bool HasScenario => !string.IsNullOrEmpty(Scenario);

        public static LaunchArgs FromEnvironment() => Parse(Environment.GetCommandLineArgs());

        public static LaunchArgs Parse(IReadOnlyList<string> args)
        {
            var result = new LaunchArgs();
            if (args == null)
                return result;

            for (int i = 0; i < args.Count; i++)
            {
                string next = i + 1 < args.Count ? args[i + 1] : null;
                switch (args[i])
                {
                    case "-scenario":
                        result.Scenario = next;
                        i++;
                        break;
                    case "-shots":
                        result.ShotsDirectory = next;
                        i++;
                        break;
                    case "-save-dir":
                        result.SaveDirectory = next;
                        i++;
                        break;
                    case "-shot-size":
                        if (TryParseSize(next, out int width, out int height))
                        {
                            result.ShotWidth = width;
                            result.ShotHeight = height;
                        }
                        i++;
                        break;
                }
            }
            return result;
        }

        /// <summary>Parses the "1920x1080" format; returns false if it is outside sensible limits.</summary>
        public static bool TryParseSize(string value, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrEmpty(value))
                return false;

            string[] parts = value.ToLowerInvariant().Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int w) || !int.TryParse(parts[1], out int h))
                return false;
            if (w < 64 || h < 64 || w > 7680 || h > 4320)
                return false;

            width = w;
            height = h;
            return true;
        }
    }
}
