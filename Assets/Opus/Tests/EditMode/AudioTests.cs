using System;
using System.IO;
using NUnit.Framework;
using Opus.Audio;
using UnityEngine;

namespace Opus.Tests
{
    public class AudioTests
    {
        static float[] Sine(float frequency, float seconds)
        {
            float[] data = Synth.Buffer(seconds);
            for (int i = 0; i < data.Length; i++)
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / Synth.SampleRate);
            return data;
        }

        static float GainOf(Synth.Biquad filter, float frequency)
        {
            float[] data = Sine(frequency, 0.5f);
            float before = Synth.Rms(data);
            Synth.Filter(data, filter);
            // Skip the warm-up part
            float[] tail = new float[data.Length / 2];
            Array.Copy(data, data.Length / 2, tail, 0, tail.Length);
            return Synth.Rms(tail) / before;
        }

        [Test]
        public void FiltersPassAndStopTheRightBands()
        {
            Assert.Greater(GainOf(Synth.Biquad.LowPass(500f), 100f), 0.9f);
            Assert.Less(GainOf(Synth.Biquad.LowPass(500f), 5000f), 0.05f);
            Assert.Greater(GainOf(Synth.Biquad.HighPass(2000f), 8000f), 0.9f);
            Assert.Less(GainOf(Synth.Biquad.HighPass(2000f), 200f), 0.05f);
            Assert.AreEqual(1f, GainOf(Synth.Biquad.BandPass(1000f, 3f), 1000f), 0.05f, "Band-pass has unit gain at the peak");
            Assert.Less(GainOf(Synth.Biquad.BandPass(1000f, 3f), 3000f), 0.3f);
        }

        [Test]
        public void LoopFrequencyFitsWholeCycles()
        {
            int length = Synth.SampleRate; // 1 s
            Assert.AreEqual(118f, Synth.LoopFrequency(118.3f, length), 1e-3f);
            float f = Synth.LoopFrequency(26f, Synth.SampleRate * 2);
            Assert.AreEqual(0f, f * 2f % 1f, 1e-3f);
        }

        [Test]
        public void EverySoundIsAudibleAndDoesNotClip()
        {
            foreach (string name in SoundBank.Names)
            {
                float[] data = SoundBank.Generate(name);
                Assert.Greater(data.Length, Synth.SampleRate / 20, name);
                float peak = Synth.Peak(data);
                Assert.LessOrEqual(peak, 1f, $"{name} clips");
                Assert.Greater(peak, 0.2f, $"{name} is too quiet");
                Assert.Greater(Synth.Rms(data), 0.01f, name);
                foreach (float v in data)
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), name);
            }
        }

        [Test]
        public void LoopsAreSeamless()
        {
            foreach (string name in SoundBank.Names)
            {
                if (!SoundBank.IsLoop(name))
                    continue;
                float[] data = SoundBank.Generate(name);
                var steps = new float[data.Length - 1];
                for (int i = 0; i < steps.Length; i++)
                    steps[i] = Mathf.Abs(data[i + 1] - data[i]);
                Array.Sort(steps);
                float typical = steps[(int)(steps.Length * 0.99f)];
                float wrap = Mathf.Abs(data[0] - data[data.Length - 1]);
                Assert.LessOrEqual(wrap, typical * 1.5f + 1e-4f, $"{name}: jump at the loop seam {wrap:F4} (typical {typical:F4})");
            }
        }

        [Test]
        public void SoundsAreDeterministic()
        {
            CollectionAssert.AreEqual(SoundBank.Generate(SoundBank.Engine), SoundBank.Generate(SoundBank.Engine));
            CollectionAssert.AreEqual(SoundBank.Generate(SoundBank.Birds[0]), SoundBank.Generate(SoundBank.Birds[0]));
        }

        [Test]
        public void EngineThumpsAtItsFiringRate()
        {
            float[] data = SoundBank.Generate(SoundBank.Engine);
            int firings = Mathf.RoundToInt(26f * Synth.Seconds(data));
            int period = Mathf.RoundToInt(data.Length / (float)firings);
            float atPeriod = Autocorrelation(data, period);
            float atHalf = Autocorrelation(data, period / 2);
            Assert.Greater(atPeriod, 0.15f, "Clear repetition at the firing period");
            Assert.Greater(atPeriod, atHalf + 0.1f);
        }

        static float Autocorrelation(float[] data, int lag)
        {
            double sum = 0, energy = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum += data[i] * data[(i + lag) % data.Length];
                energy += data[i] * data[i];
            }
            return (float)(sum / energy);
        }

        [Test]
        public void BirdsSingInTheirRange()
        {
            foreach (string bird in SoundBank.Birds)
            {
                float[] data = SoundBank.Generate(bird);
                float pitch = ZeroCrossingPitch(data, 0.08f);
                if (bird.Contains("dove"))
                    Assert.That(pitch, Is.InRange(300f, 1300f), bird);
                else
                    Assert.That(pitch, Is.InRange(1800f, 7500f), bird);
            }
        }

        /// <summary>Rough pitch (Hz) from the zero crossings of the voiced frames (256-sample blocks whose peak exceeds the threshold).</summary>
        static float ZeroCrossingPitch(float[] data, float threshold)
        {
            const int frame = 256;
            int crossings = 0, activeFrames = 0;
            for (int start = 1; start + frame <= data.Length; start += frame)
            {
                float peak = 0f;
                for (int i = start; i < start + frame; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                if (peak < threshold)
                    continue;
                activeFrames++;
                for (int i = start; i < start + frame; i++)
                    if ((data[i - 1] < 0f) != (data[i] < 0f)) crossings++;
            }
            return activeFrames == 0 ? 0f : crossings / 2f / (activeFrames * frame / (float)Synth.SampleRate);
        }

        [Test]
        public void WavFileHasValidHeader()
        {
            string path = Path.Combine(Path.GetTempPath(), "opus_test_" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                var samples = new[] { 0f, 0.5f, -0.5f, 1f, -1f, 0.25f };
                Synth.WriteWav(path, samples, 2, 48000);
                byte[] bytes = File.ReadAllBytes(path);
                Assert.AreEqual(44 + samples.Length * 2, bytes.Length);
                Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
                Assert.AreEqual("WAVE", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
                Assert.AreEqual(2, BitConverter.ToInt16(bytes, 22));
                Assert.AreEqual(48000, BitConverter.ToInt32(bytes, 24));
                Assert.AreEqual(16383, BitConverter.ToInt16(bytes, 46), 1);
                Assert.AreEqual(32767, BitConverter.ToInt16(bytes, 50));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
