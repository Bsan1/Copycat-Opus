using System.Collections.Generic;
using UnityEngine;

namespace Opus.Audio
{
    /// <summary>
    /// Sees the final mix on the listener's (AudioListener) object: records it on request (scenarios write the mix to a
    /// WAV) and mutes the output on request (automated runs must not play through the speakers). Runs on the audio thread.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public sealed class MixTap : MonoBehaviour
    {
        readonly object gate = new object();
        readonly List<float> recorded = new List<float>();
        bool recording;
        float peak;
        double sumSquares;
        long samples;

        /// <summary>Silences the sound going to the speakers (the mix is still measured/recorded).</summary>
        public bool MuteOutput { get; set; }
        public int Channels { get; private set; } = 2;
        public int SampleRate { get; private set; }

        void Awake()
        {
            SampleRate = AudioSettings.outputSampleRate;
            release = 1f - Mathf.Exp(-1f / (0.3f * Mathf.Max(8000, SampleRate)));
        }

        /// <summary>Peak limiter: the mix must not clip when sounds stack up.</summary>
        void Limit(float[] data, int channels)
        {
            float lowest = 1f;
            for (int i = 0; i + channels <= data.Length; i += channels)
            {
                float frame = 0f;
                for (int c = 0; c < channels; c++)
                {
                    float a = data[i + c] < 0f ? -data[i + c] : data[i + c];
                    if (a > frame) frame = a;
                }
                if (frame * limiterGain > LimitThreshold)
                    limiterGain = LimitThreshold / frame;
                else
                    limiterGain += (1f - limiterGain) * release;
                for (int c = 0; c < channels; c++)
                    data[i + c] *= limiterGain;
                if (limiterGain < lowest) lowest = limiterGain;
            }
            LimiterGain = lowest;
        }

        public void StartRecording()
        {
            lock (gate)
            {
                recorded.Clear();
                recording = true;
                peak = 0f;
                sumSquares = 0;
                samples = 0;
            }
        }

        /// <summary>Ends the recording; returns interleaved samples.</summary>
        public float[] StopRecording()
        {
            lock (gate)
            {
                recording = false;
                return recorded.ToArray();
            }
        }

        /// <summary>Peak and RMS of the mix over the recording.</summary>
        public void Levels(out float peakLevel, out float rms)
        {
            lock (gate)
            {
                peakLevel = peak;
                rms = samples > 0 ? Mathf.Sqrt((float)(sumSquares / samples)) : 0f;
            }
        }

        /// <summary>Limiter threshold: the mix never goes above it (instant attack, ~0.3 s release).</summary>
        public const float LimitThreshold = 0.9f;
        float limiterGain = 1f;
        float release;

        /// <summary>The limiter's lowest gain in the last frame (1 = no reduction at all).</summary>
        public float LimiterGain { get; private set; } = 1f;

        void OnAudioFilterRead(float[] data, int channels)
        {
            Channels = channels;
            Limit(data, channels);
            if (recording)
            {
                lock (gate)
                {
                    if (recording)
                    {
                        recorded.AddRange(data);
                        foreach (float v in data)
                        {
                            float a = v < 0f ? -v : v;
                            if (a > peak) peak = a;
                            sumSquares += v * v;
                        }
                        samples += data.Length;
                    }
                }
            }
            if (MuteOutput)
                System.Array.Clear(data, 0, data.Length);
        }
    }
}
