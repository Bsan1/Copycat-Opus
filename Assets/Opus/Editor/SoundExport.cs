using System.IO;
using Opus.Audio;
using UnityEditor;
using UnityEngine;

namespace Opus.Editor
{
    /// <summary>Writes all synthesised sounds as WAVs under Builds/Sounds (to listen to them and look at their spectrum).</summary>
    public static class SoundExport
    {
        public const string Folder = "Builds/Sounds";

        [MenuItem("Opus/Export the sounds as WAV")]
        public static void Export() => BatchUtil.Run("Sound export", ExportInternal);

        static void ExportInternal()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Folder));
            Directory.CreateDirectory(folder);
            int count = 0;
            foreach (string name in SoundBank.Names)
            {
                float[] data = SoundBank.Generate(name);
                // Write loops twice: so the seam can be heard by listening too
                if (SoundBank.IsLoop(name))
                {
                    var twice = new float[data.Length * 2];
                    data.CopyTo(twice, 0);
                    data.CopyTo(twice, data.Length);
                    data = twice;
                }
                Synth.WriteWav(Path.Combine(folder, name + ".wav"), data, 1, Synth.SampleRate);
                count++;
            }
            Debug.Log($"[Opus] {count} sounds written: {folder}");
        }
    }
}
