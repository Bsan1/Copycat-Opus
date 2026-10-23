using System;
using System.IO;
using UnityEngine;

namespace Opus.Audio
{
    /// <summary>
    /// Sound synthesis building blocks: deterministic noise, biquad/one-pole filters, envelopes, loop seams,
    /// normalisation, AudioClip and WAV. All sounds are generated from code (no ready-made sound files).
    /// Rule for loops: the frequency of tonal components is rounded to a whole multiple of the loop length, filters
    /// run twice through <see cref="FilterLoop"/> (on the second pass the state comes from the end of the loop), so
    /// the start and end of the loop join seamlessly.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        /// <summary>xorshift32: platform-independent, reproducible from a seed.</summary>
        public struct Rng
        {
            uint state;

            public Rng(int seed) => state = (uint)seed * 2654435761u + 0x9E3779B9u | 1u;

            public uint NextUInt()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }

            /// <summary>[0, 1)</summary>
            public float Next() => (NextUInt() >> 8) * (1f / 16777216f);
            /// <summary>[-1, 1)</summary>
            public float Signed() => Next() * 2f - 1f;
            public float Range(float min, float max) => min + (max - min) * Next();
        }

        /// <summary>RBJ biquad (transposed direct form II).</summary>
        public struct Biquad
        {
            float b0, b1, b2, a1, a2, z1, z2;

            static Biquad Make(float b0, float b1, float b2, float a0, float a1, float a2) =>
                new Biquad { b0 = b0 / a0, b1 = b1 / a0, b2 = b2 / a0, a1 = a1 / a0, a2 = a2 / a0 };

            public static Biquad LowPass(float frequency, float q = 0.7071f)
            {
                Prepare(frequency, q, out float cos, out float alpha);
                return Make((1f - cos) * 0.5f, 1f - cos, (1f - cos) * 0.5f, 1f + alpha, -2f * cos, 1f - alpha);
            }

            public static Biquad HighPass(float frequency, float q = 0.7071f)
            {
                Prepare(frequency, q, out float cos, out float alpha);
                return Make((1f + cos) * 0.5f, -(1f + cos), (1f + cos) * 0.5f, 1f + alpha, -2f * cos, 1f - alpha);
            }

            /// <summary>Band-pass with a peak gain of 1 (resonator).</summary>
            public static Biquad BandPass(float frequency, float q)
            {
                Prepare(frequency, q, out float cos, out float alpha);
                return Make(alpha, 0f, -alpha, 1f + alpha, -2f * cos, 1f - alpha);
            }

            static void Prepare(float frequency, float q, out float cos, out float alpha)
            {
                float w = TwoPi * Mathf.Clamp(frequency, 10f, SampleRate * 0.45f) / SampleRate;
                cos = Mathf.Cos(w);
                alpha = Mathf.Sin(w) / (2f * q);
            }

            public float Process(float x)
            {
                float y = b0 * x + z1;
                z1 = b1 * x - a1 * y + z2;
                z2 = b2 * x - a2 * y;
                return y;
            }

            public void Reset() => z1 = z2 = 0f;

            /// <summary>Changes the coefficients, keeps the internal state (no crackle in a filter with a sliding frequency).</summary>
            public void Retune(Biquad other)
            {
                b0 = other.b0; b1 = other.b1; b2 = other.b2; a1 = other.a1; a2 = other.a2;
            }
        }

        public static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate))];
        public static float Seconds(float[] data) => data.Length / (float)SampleRate;

        /// <summary>The nearest frequency that completes a whole number of cycles over the loop length.</summary>
        public static float LoopFrequency(float frequency, int length)
        {
            float seconds = length / (float)SampleRate;
            return Mathf.Max(1f, Mathf.Round(frequency * seconds)) / seconds;
        }

        public static void White(float[] data, ref Rng rng, float gain = 1f)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] += rng.Signed() * gain;
        }

        /// <summary>Applies the filter cyclically: first warms up over the whole buffer, the second pass's output is written.</summary>
        public static void FilterLoop(float[] data, Biquad filter)
        {
            for (int i = 0; i < data.Length; i++)
                filter.Process(data[i]);
            for (int i = 0; i < data.Length; i++)
                data[i] = filter.Process(data[i]);
        }

        /// <summary>Plain filter for one-shot sounds.</summary>
        public static void Filter(float[] data, Biquad filter)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = filter.Process(data[i]);
        }

        /// <summary>Filters <paramref name="source"/> and adds it to <paramref name="target"/> with <paramref name="gain"/> (cyclic).</summary>
        public static void AddFilteredLoop(float[] target, float[] source, Biquad filter, float gain)
        {
            for (int i = 0; i < source.Length; i++)
                filter.Process(source[i]);
            for (int i = 0; i < source.Length; i++)
                target[i] += filter.Process(source[i]) * gain;
        }

        public static void AddFiltered(float[] target, float[] source, Biquad filter, float gain, int offset = 0)
        {
            for (int i = 0; i < source.Length && offset + i < target.Length; i++)
            {
                float y = filter.Process(source[i]);
                if (offset + i >= 0)
                    target[offset + i] += y * gain;
            }
        }

        /// <summary>Adds a short piece to a looping buffer (the part running past the end wraps to the start).</summary>
        public static void AddWrapped(float[] target, float[] grain, int at, float gain)
        {
            int n = target.Length;
            for (int i = 0; i < grain.Length; i++)
                target[((at + i) % n + n) % n] += grain[i] * gain;
        }

        /// <summary>Exponentially decaying sine (impact resonance), one-shot.</summary>
        public static float[] Ping(float frequency, float decaySeconds, float seconds, float phase = 0f)
        {
            float[] data = Buffer(seconds);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Sin(TwoPi * frequency * t + phase) * Mathf.Exp(-t / decaySeconds);
            }
            return data;
        }

        /// <summary>Filtered noise burst: fast attack, exponential decay.</summary>
        public static float[] Burst(ref Rng rng, float seconds, float decaySeconds, Biquad filter, float attackSeconds = 0.002f)
        {
            float[] data = Buffer(seconds);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Min(1f, t / Mathf.Max(attackSeconds, 1e-4f)) * Mathf.Exp(-t / decaySeconds);
                data[i] = filter.Process(rng.Signed()) * env;
            }
            return data;
        }

        /// <summary>Soft saturation (a sense of compression), the peak is roughly preserved.</summary>
        public static void Saturate(float[] data, float drive)
        {
            float norm = 1f / (float)Math.Tanh(drive);
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)Math.Tanh(data[i] * drive) * norm;
        }

        public static void RemoveDc(float[] data)
        {
            double sum = 0;
            foreach (float v in data) sum += v;
            float mean = (float)(sum / data.Length);
            for (int i = 0; i < data.Length; i++) data[i] -= mean;
        }

        public static void Normalize(float[] data, float peak)
        {
            float max = Peak(data);
            if (max < 1e-6f) return;
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        /// <summary>A short fade at the start/end of one-shot sounds (no clicks).</summary>
        public static void Fade(float[] data, float inSeconds, float outSeconds)
        {
            int fin = Mathf.Min(data.Length, Mathf.RoundToInt(inSeconds * SampleRate));
            int fout = Mathf.Min(data.Length, Mathf.RoundToInt(outSeconds * SampleRate));
            for (int i = 0; i < fin; i++) data[i] *= i / (float)fin;
            for (int i = 0; i < fout; i++) data[data.Length - 1 - i] *= i / (float)fout;
        }

        public static float Peak(float[] data)
        {
            float max = 0f;
            foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
            return max;
        }

        public static float Rms(float[] data)
        {
            double sum = 0;
            foreach (float v in data) sum += v * v;
            return (float)Math.Sqrt(sum / Math.Max(1, data.Length));
        }

        public static AudioClip ToClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Writes a 16-bit PCM WAV (interleaved channels).</summary>
        public static void WriteWav(string path, float[] samples, int channels, int sampleRate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            int dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
            foreach (float sample in samples)
                writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1f, 1f) * 32767f));
        }
    }
}
