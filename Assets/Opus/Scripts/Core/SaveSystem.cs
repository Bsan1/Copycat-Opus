using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace Opus.Core
{
    /// <summary>
    /// Content of the save file (JsonUtility). The lawn state is a few bytes per cell, so it is gzipped and
    /// stored as base64 in a single field; the rest is readable JSON.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// 3: garden definition id/version and geometry (migration by area, U13). 2: day, points, upgrades,
        /// collection, pattern, grass height (U10). 1: U8 saves. All are read.
        /// </summary>
        public const int CurrentVersion = 3;
        public const int OldestVersion = 1;

        public int version = CurrentVersion;
        public string savedAt;
        public float playSeconds;

        // To verify that the save belongs to this garden
        public int gridWidth;
        public int gridHeight;
        public int patchCount;
        public int layoutHash;

        public Vector3 playerFeet;
        public float playerYaw;
        public float playerPitch;
        public int toolSlot;
        public int sprayMode;

        public int[] unlockedAreas = Array.Empty<int>();
        public int[] discoveredPatches = Array.Empty<int>();
        public int[] finishedPatches = Array.Empty<int>();
        public float[] weedHealth = Array.Empty<float>();
        public string lawn = string.Empty;

        // Version 2
        public int[] everFinishedPatches = Array.Empty<int>();
        public int day = 1;
        public int points;
        public int[] upgrades = Array.Empty<int>();
        public string[] collected = Array.Empty<string>();
        public Opus.Lawn.LostItems.SavedItem[] hiddenFinds = Array.Empty<Opus.Lawn.LostItems.SavedItem>();
        /// <summary>The day's pattern: kind + 1 (0 = none).</summary>
        public int pattern;
        public int patternPatch;
        public bool patternCompleted;

        // Version 3: which version of which garden and its geometry (for migration by area when the garden changes)
        public string yardId = string.Empty;
        public int yardVersion;
        public float gridMinX;
        public float gridMinZ;
        public float cellSize;
        public SavedArea[] areas = Array.Empty<SavedArea>();
        public SavedSeed[] patchSeeds = Array.Empty<SavedSeed>();
        public Vector2[] weedPositions = Array.Empty<Vector2>();
        /// <summary>Records of the client jobs (only in the home save): best stars, delivery count.</summary>
        public JobRecord[] jobRecords = Array.Empty<JobRecord>();
        /// <summary>The apples on the ground and the day they belong to (0 = not saved: the day drops them again).</summary>
        public Vector3[] apples = Array.Empty<Vector3>();
        public int applesDay;
    }

    [Serializable]
    public sealed class SavedArea
    {
        public int id;
        public float minX, minZ, maxX, maxZ;
    }

    [Serializable]
    public sealed class SavedSeed
    {
        public float x, z;
        public int variety;
        public int area;
    }

    /// <summary>
    /// Single-slot save: bahce.json in the persistent data folder ("bahce" is the slot name from the Turkish build, kept so existing
    /// saves still load). It is written to a temporary file first and then moved into place (a write cut short does not damage the old save). Scenarios use a separate folder.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>Save slot of the home garden (and the career).</summary>
        public const string HomeSlot = "bahce";

        /// <summary>The active save slot: home or a client job's unfinished state ("is_…", Turkish slot names kept for existing saves).</summary>
        public static string Slot { get; set; } = HomeSlot;

        public static string FileName => Slot + ".json";
        public static string PathOf(string slot) => Path.Combine(Directory, slot + ".json");
        public static bool SlotExists(string slot) => File.Exists(PathOf(slot));

        public static void DeleteSlot(string slot)
        {
            if (SlotExists(slot))
                File.Delete(PathOf(slot));
        }

        static string directory;

        /// <summary>Save folder; Application.persistentDataPath if not set.</summary>
        public static string Directory
        {
            get => string.IsNullOrEmpty(directory) ? Application.persistentDataPath : directory;
            set => directory = value;
        }

        public static string FilePath => Path.Combine(Directory, FileName);
        public static bool Exists => File.Exists(FilePath);

        public static void Write(SaveData data)
        {
            System.IO.Directory.CreateDirectory(Directory);
            string json = JsonUtility.ToJson(data);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            // On Windows a virus scanner/indexer can hold the file for a moment: retry with short waits,
            // otherwise copy over it (the temporary file still protects the old save).
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(FilePath))
                        File.Replace(temp, FilePath, null);
                    else
                        File.Move(temp, FilePath);
                    return;
                }
                catch (IOException) when (attempt < 4)
                {
                    System.Threading.Thread.Sleep(25 * (attempt + 1));
                }
                catch (IOException)
                {
                    File.Copy(temp, FilePath, overwrite: true);
                    File.Delete(temp);
                    return;
                }
            }
        }

        public static bool TryRead(out SaveData data, out string error)
        {
            data = null;
            error = null;
            if (!Exists)
            {
                error = "No save.";
                return false;
            }
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception exception)
            {
                error = "Could not read the save: " + exception.Message;
                return false;
            }
            if (data == null)
            {
                error = "The save is empty.";
                return false;
            }
            if (data.version < SaveData.OldestVersion || data.version > SaveData.CurrentVersion)
            {
                error = $"Incompatible save version ({data.version}).";
                data = null;
                return false;
            }
            return true;
        }

        public static void Delete()
        {
            if (Exists)
                File.Delete(FilePath);
        }

        /// <summary>Gzips the bytes and turns them into base64 text.</summary>
        public static string Pack(byte[] bytes)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(bytes, 0, bytes.Length);
            return Convert.ToBase64String(output.ToArray());
        }

        /// <summary>The reverse of <see cref="Pack"/>; null for corrupt text.</summary>
        public static byte[] Unpack(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;
            try
            {
                using var input = new MemoryStream(Convert.FromBase64String(text));
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                return output.ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
