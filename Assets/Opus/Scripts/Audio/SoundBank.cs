using System.Collections.Generic;
using UnityEngine;
using static Opus.Audio.Synth;

namespace Opus.Audio
{
    /// <summary>
    /// All the game's sounds, synthesised from code (seeded, identical every time). Loops are seamless;
    /// the start/end of one-shot sounds is softened. They are turned into clips the first time they are requested and cached.
    /// </summary>
    public static class SoundBank
    {
        // Loops
        public const string Engine = "motor";
        public const string EngineLoaded = "motor_load";
        public const string MowerChew = "mowing";
        public const string Trimmer = "trimmer";
        public const string TrimmerHit = "trimmer_hit";
        public const string WaterWide = "water_wide";
        public const string WaterNarrow = "water_narrow";
        public const string Splash = "water_splash";
        public const string Wind = "wind";
        // One-shot
        public const string ChimePatch = "patch_done";
        public const string ChimeArea = "area_done";
        public const string Discover = "discovery";
        public const string GateCreak = "door";
        public const string MenuClick = "menu_click";
        public const string MenuOpen = "menu_open";
        public const string MenuClose = "menu_close";
        public const string ToolSwitch = "tool_switch";
        public const string HoseClick = "hose_click";
        public const string WeedPop = "weed_pull";
        public const string Found = "item_found";
        public const string ApplePick = "apple";
        public const string Purchase = "purchase";
        public const string PatternDone = "pattern_done";
        public const string Night = "night";
        public static readonly string[] Footsteps = { "step_1", "step_2", "step_3", "step_4" };
        public static readonly string[] Birds = { "bird_chirp_1", "bird_chirp_2", "bird_chirp_3", "bird_trill_1", "bird_trill_2", "bird_whistle_1", "bird_whistle_2", "bird_sparrow_1", "bird_sparrow_2", "bird_dove" };

        static readonly Dictionary<string, System.Func<float[]>> Generators = new Dictionary<string, System.Func<float[]>>
        {
            [Engine] = () => EngineLoop(11, 26f, roughness: 1f, low: 1f, chop: 1f),
            [EngineLoaded] = () => EngineLoop(12, 24.5f, roughness: 1.9f, low: 1.5f, chop: 1.7f),
            [MowerChew] = () => Chew(21),
            [Trimmer] = () => TrimmerLoop(31),
            [TrimmerHit] = () => TrimmerHitLoop(32),
            [WaterWide] = () => Water(41, narrow: false),
            [WaterNarrow] = () => Water(42, narrow: true),
            [Splash] = () => SplashLoop(43),
            [Wind] = () => WindLoop(51),
            [ChimePatch] = () => Chime(new[] { 784f, 988f, 1175f, 1568f }, 0.09f, 0.45f, 1.7f, 0.55f),
            [ChimeArea] = () => AreaFanfare(),
            [Discover] = () => Chime(new[] { 1319f, 1976f }, 0.07f, 0.3f, 0.9f, 0.35f),
            [GateCreak] = () => Gate(61),
            [MenuClick] = () => WoodTick(71),
            [MenuOpen] = () => PaperSwish(72, 900f, 2600f),
            [MenuClose] = () => PaperSwish(73, 2400f, 850f),
            [ToolSwitch] = () => ToolSwap(74),
            [HoseClick] = () => PlasticClick(75),
            [WeedPop] = () => Pop(76),
            [Found] = () => Chime(new[] { 1568f, 2093f, 2637f }, 0.05f, 0.25f, 0.9f, 0.45f),
            [ApplePick] = () => Chime(new[] { 1046.5f, 1568f }, 0.035f, 0.12f, 0.45f, 0.35f),
            [Purchase] = () => Chime(new[] { 1975.5f, 2637f }, 0.06f, 0.18f, 0.6f, 0.45f),
            [PatternDone] = () => Chime(new[] { 659.25f, 783.99f, 987.77f, 1318.5f, 1568f }, 0.07f, 0.4f, 1.6f, 0.55f),
            [Night] = () => Chime(new[] { 783.99f, 659.25f, 523.25f, 392f }, 0.16f, 0.9f, 3f, 0.45f),
        };

        static readonly HashSet<string> Loops = new HashSet<string> { Engine, EngineLoaded, MowerChew, Trimmer, TrimmerHit, WaterWide, WaterNarrow, Splash, Wind };
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        static SoundBank()
        {
            for (int i = 0; i < Footsteps.Length; i++)
            {
                int seed = 80 + i;
                Generators[Footsteps[i]] = () => Footstep(seed);
            }
            int b = 0;
            foreach (string bird in Birds)
            {
                int seed = 100 + b++;
                Generators[bird] = () => BirdSong(bird, seed);
            }
        }

        public static IEnumerable<string> Names => Generators.Keys;
        public static bool IsLoop(string name) => Loops.Contains(name);

        public static float[] Generate(string name) =>
            Generators.TryGetValue(name, out System.Func<float[]> make) ? make() : throw new System.ArgumentException($"Unknown sound: {name}");

        public static AudioClip Clip(string name)
        {
            if (!Clips.TryGetValue(name, out AudioClip clip) || clip == null)
                Clips[name] = clip = ToClip(name, Generate(name));
            return clip;
        }

        // ---------- Mower ----------

        /// <summary>
        /// Single-cylinder four-stroke engine: every firing is a short noisy pulse passed through exhaust and housing
        /// resonances; on top, the blade's air hum and mechanical ticking. Firing time and strength wobble slightly (liveliness).
        /// </summary>
        static float[] EngineLoop(int seed, float firingHz, float roughness, float low, float chop)
        {
            float[] data = Buffer(2f);
            int n = data.Length;
            var rng = new Rng(seed);
            int firings = Mathf.RoundToInt(firingHz * Seconds(data));
            float period = n / (float)firings;

            var excite = new float[n];
            int pulse = Mathf.RoundToInt(0.007f * SampleRate);
            var shape = new float[pulse];
            for (int k = 0; k < firings; k++)
            {
                float amp = 1f + rng.Signed() * 0.2f * roughness;
                for (int i = 0; i < pulse; i++)
                {
                    float t = i / (float)SampleRate;
                    shape[i] = (t < 0.0016f ? Mathf.Sin(Mathf.PI * t / 0.0016f) : 0f) + rng.Signed() * Mathf.Exp(-t / 0.0022f) * 0.6f;
                }
                int at = Mathf.RoundToInt(k * period + rng.Signed() * period * 0.035f * roughness);
                AddWrapped(excite, shape, at, amp);
            }
            AddFilteredLoop(data, excite, Biquad.BandPass(82f, 3.2f), 1.5f * low);
            AddFilteredLoop(data, excite, Biquad.BandPass(170f, 4f), 1.1f);
            AddFilteredLoop(data, excite, Biquad.BandPass(410f, 3f), 0.95f);
            AddFilteredLoop(data, excite, Biquad.BandPass(690f, 2.5f), 0.45f);
            AddFilteredLoop(data, excite, Biquad.BandPass(1150f, 2f), 0.22f * roughness);

            // Blade hum: band noise that swells and fades at the rev rate (2x the firing rate)
            var air = new float[n];
            White(air, ref rng);
            FilterLoop(air, Biquad.BandPass(520f, 0.8f));
            float rev = LoopFrequency(firingHz * 2f, n);
            // Mechanical ticking: high noise in short bursts locked to the firing
            var clatter = new float[n];
            White(clatter, ref rng);
            FilterLoop(clatter, Biquad.BandPass(2600f, 1.5f));
            float fire = firings / Seconds(data);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float swoosh = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * rev * t);
                data[i] += air[i] * swoosh * swoosh * 0.55f * chop;
                float phase = t * fire % 1f;
                data[i] += clatter[i] * Mathf.Exp(-phase * 14f) * 0.12f * roughness;
            }
            RemoveDc(data);
            Saturate(data, 1.7f);
            // The housing and exhaust swallow the highs: no hiss left
            FilterLoop(data, Biquad.LowPass(3600f));
            Normalize(data, 0.8f);
            return data;
        }

        /// <summary>The blade chewing grass: clipping grains hitting the housing + a low rustle.</summary>
        static float[] Chew(int seed)
        {
            float[] data = Buffer(3f);
            var rng = new Rng(seed);
            int grains = Mathf.RoundToInt(Seconds(data) * 95f);
            for (int g = 0; g < grains; g++)
            {
                float length = rng.Range(0.004f, 0.016f);
                float[] grain = Burst(ref rng, length, length * 0.35f, Biquad.BandPass(rng.Range(1400f, 4600f), 1.2f), 0.0008f);
                AddWrapped(data, grain, (int)(rng.Next() * data.Length), rng.Range(0.25f, 1f));
            }
            var hiss = new float[data.Length];
            White(hiss, ref rng);
            AddFilteredLoop(data, hiss, Biquad.BandPass(700f, 0.7f), 0.18f);
            RemoveDc(data);
            Normalize(data, 0.7f);
            return data;
        }

        // ---------- Trimmer ----------

        /// <summary>Small two-stroke engine: band-limited sawtooth + nose resonances + line buzz.</summary>
        static float[] TrimmerLoop(int seed)
        {
            float[] data = Buffer(1f);
            int n = data.Length;
            var rng = new Rng(seed);
            float f = LoopFrequency(118f, n);
            var saw = new float[n];
            int harmonics = Mathf.FloorToInt(4200f / f);
            for (int h = 1; h <= harmonics; h++)
            {
                float hf = f * h, gain = (h % 2 == 0 ? -1f : 1f) / h;
                for (int i = 0; i < n; i++)
                    saw[i] += Mathf.Sin(2f * Mathf.PI * hf * i / SampleRate) * gain;
            }
            // Slight strength wobble per revolution (slow noise)
            var wobble = new float[n];
            White(wobble, ref rng);
            FilterLoop(wobble, Biquad.LowPass(28f));
            Normalize(wobble, 1f);
            for (int i = 0; i < n; i++)
                saw[i] *= 1f + wobble[i] * 0.12f;

            for (int i = 0; i < n; i++) data[i] = saw[i] * 0.3f;
            AddFilteredLoop(data, saw, Biquad.BandPass(950f, 2.5f), 1.3f);
            AddFilteredLoop(data, saw, Biquad.BandPass(2400f, 3f), 0.8f);
            var line = new float[n];
            White(line, ref rng);
            FilterLoop(line, Biquad.BandPass(5200f, 2f));
            for (int i = 0; i < n; i++)
            {
                float m = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 2f * f * i / SampleRate);
                data[i] += line[i] * m * 0.35f;
            }
            RemoveDc(data);
            Saturate(data, 1.3f);
            Normalize(data, 0.75f);
            return data;
        }

        /// <summary>The line hitting grass/weeds: dense, high-pitched grain sounds.</summary>
        static float[] TrimmerHitLoop(int seed)
        {
            float[] data = Buffer(2f);
            var rng = new Rng(seed);
            int grains = Mathf.RoundToInt(Seconds(data) * 170f);
            for (int g = 0; g < grains; g++)
            {
                bool slap = rng.Next() < 0.25f;
                float length = slap ? rng.Range(0.008f, 0.02f) : rng.Range(0.003f, 0.008f);
                Biquad filter = slap ? Biquad.BandPass(rng.Range(900f, 1600f), 1f) : Biquad.HighPass(rng.Range(2500f, 5000f));
                float[] grain = Burst(ref rng, length, length * 0.3f, filter, 0.0005f);
                AddWrapped(data, grain, (int)(rng.Next() * data.Length), rng.Range(0.3f, 1f));
            }
            RemoveDc(data);
            Normalize(data, 0.7f);
            return data;
        }

        // ---------- Su ----------

        static float[] Water(int seed, bool narrow)
        {
            float[] data = Buffer(3f);
            int n = data.Length;
            var rng = new Rng(seed);
            var noise = new float[n];
            White(noise, ref rng);
            if (narrow)
            {
                AddFilteredLoop(data, noise, Biquad.BandPass(3300f, 0.9f), 1f);
                AddFilteredLoop(data, noise, Biquad.HighPass(6500f), 0.35f);
            }
            else
            {
                var band = (float[])noise.Clone();
                FilterLoop(band, Biquad.HighPass(480f));
                FilterLoop(band, Biquad.LowPass(6500f));
                for (int i = 0; i < n; i++) data[i] = band[i];
                AddFilteredLoop(data, noise, Biquad.BandPass(950f, 3f), 0.35f);
            }
            // Pressure fluctuation
            var flutter = new float[n];
            White(flutter, ref rng);
            FilterLoop(flutter, Biquad.LowPass(7f));
            Normalize(flutter, 1f);
            float depth = narrow ? 0.15f : 0.25f;
            for (int i = 0; i < n; i++)
                data[i] *= 1f + flutter[i] * depth;
            RemoveDc(data);
            Normalize(data, narrow ? 0.75f : 0.7f);
            return data;
        }

        /// <summary>Water falling on grass/soil: broadband hiss + drip ticks.</summary>
        static float[] SplashLoop(int seed)
        {
            float[] data = Buffer(3f);
            var rng = new Rng(seed);
            var noise = new float[data.Length];
            White(noise, ref rng);
            AddFilteredLoop(data, noise, Biquad.BandPass(1600f, 0.7f), 0.5f);
            int drops = Mathf.RoundToInt(Seconds(data) * 260f);
            for (int d = 0; d < drops; d++)
            {
                float length = rng.Range(0.002f, 0.006f);
                float[] drop = Burst(ref rng, length, length * 0.4f, Biquad.BandPass(rng.Range(2000f, 7000f), 3f), 0.0003f);
                AddWrapped(data, drop, (int)(rng.Next() * data.Length), rng.Range(0.2f, 1f));
            }
            RemoveDc(data);
            Normalize(data, 0.7f);
            return data;
        }

        // ---------- Ambience ----------

        /// <summary>Wind: brown noise + leaf rustle carried by gusts (16 s loop).</summary>
        static float[] WindLoop(int seed)
        {
            float[] data = Buffer(16f);
            int n = data.Length;
            var rng = new Rng(seed);
            var white = new float[n];
            White(white, ref rng);
            // Leaky integrator (two passes: cyclic)
            float y = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    y = y * 0.996f + white[i] * 0.03f;
                    if (pass == 1) data[i] = y;
                }
            }
            FilterLoop(data, Biquad.LowPass(650f));
            RemoveDc(data);
            Normalize(data, 1f);
            float seconds = Seconds(data);
            float p1 = rng.Next() * 6.28f, p2 = rng.Next() * 6.28f;
            var gust = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                gust[i] = 0.55f + 0.25f * Mathf.Sin(2f * Mathf.PI * 2f / seconds * t + p1) + 0.2f * Mathf.Sin(2f * Mathf.PI * 5f / seconds * t + p2);
                data[i] *= gust[i];
            }
            var leaves = new float[n];
            White(leaves, ref rng);
            FilterLoop(leaves, Biquad.BandPass(3500f, 0.6f));
            for (int i = 0; i < n; i++)
            {
                float g = Mathf.Max(0f, gust[i] - 0.55f);
                data[i] += leaves[i] * g * g * 3.5f;
            }
            Normalize(data, 0.6f);
            return data;
        }

        /// <summary>Short pitch-gliding chirp note (sine + weak 2nd harmonic, with vibrato).</summary>
        static void Note(float[] data, float start, float duration, float f0, float f1, float amp, float curve = 1f,
            float vibratoHz = 0f, float vibratoDepth = 0f, float harmonic = 0.18f)
        {
            int from = Mathf.RoundToInt(start * SampleRate);
            int length = Mathf.RoundToInt(duration * SampleRate);
            float phase = 0f;
            for (int i = 0; i < length && from + i < data.Length; i++)
            {
                float t = i / (float)length;
                float f = (f0 + (f1 - f0) * Mathf.Pow(t, curve)) * (1f + vibratoDepth * Mathf.Sin(2f * Mathf.PI * vibratoHz * i / SampleRate));
                phase += 2f * Mathf.PI * f / SampleRate;
                // Soft attack and release (zero derivative at the ends): no click at the start/end of the note
                float elapsed = i / (float)SampleRate;
                float attack = Mathf.Min(0.012f, duration * 0.3f), release = Mathf.Min(0.03f, duration * 0.4f);
                float up = Mathf.Clamp01(elapsed / attack), down = Mathf.Clamp01((duration - elapsed) / release);
                float env = up * up * (3f - 2f * up) * down * down * (3f - 2f * down);
                data[from + i] += (Mathf.Sin(phase) + harmonic * Mathf.Sin(2f * phase)) * env * amp;
            }
        }

        static float[] BirdSong(string name, int seed)
        {
            var rng = new Rng(seed);
            float[] data;
            if (name.Contains("chirp"))
            {
                data = Buffer(1.2f);
                float t = 0.02f;
                int count = 3 + (int)(rng.Next() * 4f);
                float baseF = rng.Range(3200f, 4600f);
                for (int i = 0; i < count && t < 1.05f; i++)
                {
                    float d = rng.Range(0.045f, 0.09f);
                    float f0 = baseF * rng.Range(0.9f, 1.12f);
                    Note(data, t, d, f0, f0 * rng.Range(0.65f, 1.3f), rng.Range(0.6f, 1f), rng.Range(0.5f, 2f), 40f, 0.03f);
                    t += d + rng.Range(0.035f, 0.08f);
                }
            }
            else if (name.Contains("trill"))
            {
                data = Buffer(1f);
                int count = 12 + (int)(rng.Next() * 7f);
                float f = rng.Range(3900f, 4700f);
                for (int i = 0; i < count; i++)
                {
                    float shape = Mathf.Sin(Mathf.PI * (i + 0.5f) / count);
                    Note(data, 0.02f + i * 0.037f, 0.022f, f, f * 0.88f, 0.4f + 0.6f * shape);
                }
            }
            else if (name.Contains("whistle"))
            {
                data = Buffer(0.75f);
                float f = rng.Range(2700f, 3100f);
                Note(data, 0.02f, 0.22f, f, f * 0.985f, 0.9f, 1f, 6f, 0.01f, 0.08f);
                Note(data, 0.31f, 0.27f, f * 0.84f, f * 0.83f, 0.8f, 1f, 6f, 0.01f, 0.08f);
            }
            else if (name.Contains("sparrow"))
            {
                data = Buffer(1.3f);
                float t = 0.02f;
                int count = 5 + (int)(rng.Next() * 4f);
                for (int i = 0; i < count && t < 1.2f; i++)
                {
                    float d = rng.Range(0.015f, 0.026f);
                    Note(data, t, d, rng.Range(5400f, 6400f), rng.Range(2900f, 3500f), rng.Range(0.6f, 1f), 0.5f, 0f, 0f, 0.3f);
                    t += d + rng.Range(0.06f, 0.13f);
                }
            }
            else
            {
                // Dove: low, breathy "hu-HUU-hu-hu"
                data = Buffer(2.3f);
                float[] starts = { 0.02f, 0.5f, 1.25f, 1.7f };
                float[] lengths = { 0.35f, 0.6f, 0.32f, 0.32f };
                float f = rng.Range(480f, 560f);
                for (int i = 0; i < starts.Length; i++)
                    Note(data, starts[i], lengths[i], f * (i == 1 ? 1.08f : 1f), f * 0.92f, i == 1 ? 1f : 0.75f, 1f, 4f, 0.01f, 0.3f);
                var breath = new float[data.Length];
                White(breath, ref rng);
                Filter(breath, Biquad.BandPass(f * 1.5f, 1.2f));
                for (int i = 0; i < data.Length; i++)
                    data[i] += breath[i] * Mathf.Abs(data[i]) * 0.6f;
                Filter(data, Biquad.LowPass(1600f));
            }
            Fade(data, 0.004f, 0.02f);
            Normalize(data, name.Contains("dove") ? 0.5f : 0.6f);
            return data;
        }

        // ---------- Feedback ----------

        /// <summary>Soft bell/marimba note: fundamental + fast-decaying 2nd and 3rd partials.</summary>
        static void Bell(float[] data, float start, float frequency, float decay, float amp)
        {
            int from = Mathf.RoundToInt(start * SampleRate);
            for (int i = 0; from + i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float attack = Mathf.Min(1f, t / 0.003f);
                float s = Mathf.Sin(2f * Mathf.PI * frequency * t) * Mathf.Exp(-t / decay)
                          + 0.32f * Mathf.Sin(2f * Mathf.PI * 2f * frequency * t) * Mathf.Exp(-t / (decay * 0.45f))
                          + 0.1f * Mathf.Sin(2f * Mathf.PI * 3.01f * frequency * t) * Mathf.Exp(-t / (decay * 0.25f));
                data[from + i] += s * attack * amp;
                if (t > decay * 7f) break;
            }
        }

        static float[] Chime(float[] notes, float step, float decay, float seconds, float peak)
        {
            float[] data = Buffer(seconds);
            for (int i = 0; i < notes.Length; i++)
                Bell(data, 0.01f + i * step, notes[i], decay, 1f - i * 0.08f);
            Filter(data, Biquad.LowPass(7000f));
            Fade(data, 0.002f, 0.15f);
            Normalize(data, peak);
            return data;
        }

        /// <summary>Area finished: rising major arpeggio + soft chord.</summary>
        static float[] AreaFanfare()
        {
            float[] data = Buffer(3f);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f, 1568f };
            for (int i = 0; i < notes.Length; i++)
                Bell(data, 0.01f + i * 0.11f, notes[i], 0.7f, 0.9f);
            float[] pad = { 261.63f, 329.63f, 392f };
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Clamp01(t / 0.35f) * Mathf.Exp(-Mathf.Max(0f, t - 0.6f) / 0.8f);
                float s = 0f;
                foreach (float f in pad) s += Mathf.Sin(2f * Mathf.PI * f * t);
                data[i] += s * env * 0.12f;
            }
            Filter(data, Biquad.LowPass(7000f));
            Fade(data, 0.002f, 0.3f);
            Normalize(data, 0.6f);
            return data;
        }

        /// <summary>Latch click, then hinge creak (stick-slip friction) and wood groan.</summary>
        static float[] Gate(int seed)
        {
            float[] data = Buffer(1.6f);
            var rng = new Rng(seed);
            AddLatch(data, 0f, ref rng, 1f);

            int from = Mathf.RoundToInt(0.14f * SampleRate), length = Mathf.RoundToInt(1.2f * SampleRate);
            var friction = new float[length];
            var jitter = new float[length];
            White(jitter, ref rng);
            Filter(jitter, Biquad.LowPass(6f));
            Normalize(jitter, 1f);
            float phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float f = 290f + 190f * t * t * (3f - 2f * t) + 30f * jitter[i];
                phase += f / SampleRate;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    friction[i] = rng.Range(0.6f, 1f);
                }
            }
            var creak = new float[length];
            AddFiltered(creak, friction, Biquad.BandPass(900f, 3f), 1f);
            AddFiltered(creak, friction, Biquad.BandPass(2100f, 4f), 0.6f);
            AddFiltered(creak, friction, Biquad.BandPass(3400f, 5f), 0.3f);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)length;
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1f - t) / 0.2f) * (0.75f + 0.25f * Mathf.Sin(t * 23f));
                data[from + i] += creak[i] * env * 2.2f;
            }
            var groan = new float[length];
            White(groan, ref rng);
            Filter(groan, Biquad.BandPass(260f, 1.5f));
            for (int i = 0; i < length; i++)
                data[from + i] += groan[i] * Mathf.Sin(Mathf.PI * i / length) * 0.25f;
            RemoveDc(data);
            Fade(data, 0.001f, 0.1f);
            Normalize(data, 0.6f);
            return data;
        }

        static void AddLatch(float[] data, float at, ref Rng rng, float gain)
        {
            int offset = Mathf.RoundToInt(at * SampleRate);
            AddWrappedOnce(data, Burst(ref rng, 0.04f, 0.006f, Biquad.BandPass(2600f, 4f), 0.0003f), offset, gain);
            AddWrappedOnce(data, Ping(3150f, 0.04f, 0.15f), offset, gain * 0.35f);
            AddWrappedOnce(data, Ping(4700f, 0.025f, 0.1f), offset, gain * 0.2f);
        }

        static void AddWrappedOnce(float[] data, float[] grain, int at, float gain)
        {
            for (int i = 0; i < grain.Length && at + i < data.Length; i++)
                if (at + i >= 0) data[at + i] += grain[i] * gain;
        }

        /// <summary>Wooden "tock": two short resonances + a click.</summary>
        static float[] WoodTick(int seed)
        {
            float[] data = Buffer(0.09f);
            var rng = new Rng(seed);
            AddWrappedOnce(data, Ping(1250f, 0.012f, 0.09f), 0, 1f);
            AddWrappedOnce(data, Ping(2700f, 0.007f, 0.09f), 0, 0.6f);
            AddWrappedOnce(data, Burst(ref rng, 0.02f, 0.003f, Biquad.BandPass(3000f, 2f), 0.0002f), 0, 0.5f);
            Fade(data, 0.0005f, 0.02f);
            Normalize(data, 0.45f);
            return data;
        }

        /// <summary>Paper rustle: noise with a sliding filter frequency.</summary>
        static float[] PaperSwish(int seed, float fromHz, float toHz)
        {
            float[] data = Buffer(0.26f);
            var rng = new Rng(seed);
            Biquad filter = Biquad.BandPass(fromHz, 1.1f);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)data.Length;
                if (i % 64 == 0)
                {
                    filter.Retune(Biquad.BandPass(Mathf.Lerp(fromHz, toHz, t), 1.1f));
                }
                data[i] = filter.Process(rng.Signed()) * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 1.5f);
            }
            Normalize(data, 0.35f);
            return data;
        }

        /// <summary>Tool switch: fabric rustle + a small metal clink.</summary>
        static float[] ToolSwap(int seed)
        {
            float[] data = PaperSwish(seed, 650f, 1400f);
            System.Array.Resize(ref data, Mathf.RoundToInt(0.32f * SampleRate));
            for (int i = 0; i < data.Length; i++) data[i] *= 0.6f;
            int at = Mathf.RoundToInt(0.12f * SampleRate);
            AddWrappedOnce(data, Ping(1850f, 0.06f, 0.2f), at, 0.5f);
            AddWrappedOnce(data, Ping(2930f, 0.04f, 0.2f), at, 0.35f);
            AddWrappedOnce(data, Ping(4410f, 0.025f, 0.15f), at, 0.2f);
            Fade(data, 0.002f, 0.05f);
            Normalize(data, 0.5f);
            return data;
        }

        /// <summary>Plastic double click (turning the spray head).</summary>
        static float[] PlasticClick(int seed)
        {
            float[] data = Buffer(0.12f);
            var rng = new Rng(seed);
            foreach (float at in new[] { 0f, 0.045f })
            {
                int offset = Mathf.RoundToInt(at * SampleRate);
                AddWrappedOnce(data, Burst(ref rng, 0.02f, 0.004f, Biquad.BandPass(3800f, 3f), 0.0002f), offset, 1f);
                AddWrappedOnce(data, Ping(1600f, 0.01f, 0.05f), offset, 0.5f);
            }
            Fade(data, 0.0005f, 0.02f);
            Normalize(data, 0.4f);
            return data;
        }

        /// <summary>Weed pulling: a descending low thump + the crackle of roots tearing.</summary>
        static float[] Pop(int seed)
        {
            float[] data = Buffer(0.3f);
            var rng = new Rng(seed);
            float phase = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                phase += 2f * Mathf.PI * Mathf.Lerp(180f, 90f, Mathf.Clamp01(t / 0.08f)) / SampleRate;
                data[i] += Mathf.Sin(phase) * Mathf.Exp(-t / 0.06f) * 0.8f;
            }
            int crackles = 26;
            for (int c = 0; c < crackles; c++)
            {
                float at = rng.Range(0f, 0.18f);
                float[] grain = Burst(ref rng, 0.006f, 0.0015f, Biquad.BandPass(rng.Range(1200f, 3500f), 1.5f), 0.0002f);
                AddWrappedOnce(data, grain, Mathf.RoundToInt(at * SampleRate), rng.Range(0.2f, 0.7f) * (1f - at / 0.2f));
            }
            Fade(data, 0.001f, 0.05f);
            Normalize(data, 0.6f);
            return data;
        }

        /// <summary>Footstep in grass: soft crushing crackles + a light dull thud.</summary>
        static float[] Footstep(int seed)
        {
            float[] data = Buffer(0.18f);
            var rng = new Rng(seed);
            int grains = 22 + (int)(rng.Next() * 14f);
            for (int g = 0; g < grains; g++)
            {
                float at = rng.Range(0f, 0.09f);
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(at / 0.09f));
                float[] grain = Burst(ref rng, 0.006f, rng.Range(0.0015f, 0.003f), Biquad.BandPass(rng.Range(1200f, 4000f), 1.3f), 0.0005f);
                AddWrappedOnce(data, grain, Mathf.RoundToInt(at * SampleRate), envelope * rng.Range(0.3f, 1f));
            }
            AddWrappedOnce(data, Ping(110f, 0.025f, 0.12f), 0, 0.3f);
            Fade(data, 0.002f, 0.04f);
            Normalize(data, 0.35f);
            return data;
        }
    }
}
