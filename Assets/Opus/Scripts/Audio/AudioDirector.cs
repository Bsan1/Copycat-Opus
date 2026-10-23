using System.Collections.Generic;
using Opus.Core;
using Opus.Lawn;
using Opus.Player;
using Opus.Tools;
using Opus.UI;
using Opus.World;
using UnityEngine;

namespace Opus.Audio
{
    /// <summary>
    /// The game's sounds: continuous tool sounds (the mower engine labours under load, the trimmer revs on the trigger,
    /// the hose sprays according to its mode), footsteps in the grass, wind and birds in the surrounding trees, feedback
    /// sounds (patch/area finished, gate, weed pulling, tool switch, menu). Volumes come from the settings
    /// (master/effects/ambience); while the break menu is open the game sounds stop and the menu sounds play.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const float StrideWalking = 0.78f;
        const float StrideMowing = 0.62f;

        /// <summary>Where birds can sing: cypress rows, the tree, distant painted trees.</summary>
        static readonly Vector3[] BirdPerches =
        {
            new Vector3(12f, 5.5f, -5f), new Vector3(-14f, 6f, 16.8f), new Vector3(14f, 6.5f, 16.6f), new Vector3(-19.8f, 5f, 8f),
            new Vector3(31.6f, 5.5f, 9f), new Vector3(-16f, 7f, -30f), new Vector3(8f, 8f, -34f), new Vector3(-34f, 7f, -4f),
            new Vector3(44f, 7f, 2f), new Vector3(25f, 7f, 28f), new Vector3(-6f, 5f, 26f),
        };

        PlayerController player;
        YardSession session;
        MowerTool mower;
        TrimmerTool trimmer;
        HoseTool hose;

        AudioSource engine, engineLoaded, chew, trimmerLoop, trimmerHit, waterWide, waterNarrow, splash, wind;
        AudioSource ui, effects, steps;
        readonly List<AudioSource> world = new List<AudioSource>();
        readonly System.Random random = new System.Random(5);
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

        float engineLevel, loadLevel, chewLevel, rev, hitLevel, sprayLevel;
        float strideDistance;
        float birdTimer = 1.5f;
        bool paused;

        public MixTap Tap { get; private set; }
        public int BirdsSung { get; private set; }
        public int FootstepsPlayed { get; private set; }
        public int OneShotsPlayed { get; private set; }
        public string LastOneShot { get; private set; } = string.Empty;

        /// <summary>Current levels of the continuous sounds (for scenario checks; category multiplier included).</summary>
        public float EngineVolume => engine.volume;
        public float EnginePitch => engine.pitch;
        public float EngineLoadedVolume => engineLoaded.volume;
        public float TrimmerPitch => trimmerLoop.pitch;
        public float TrimmerVolume => trimmerLoop.volume;
        public float WaterVolume => Mathf.Max(waterWide.volume, waterNarrow.volume);
        public bool NarrowWaterAudible => waterNarrow.volume > waterWide.volume;
        public float WindVolume => wind.volume;

        public static AudioDirector Create(Transform parent, PlayerController player, YardSession session, WeedField weeds, GameMenu menu)
        {
            var go = new GameObject("Sound");
            go.transform.SetParent(parent, false);
            var director = go.AddComponent<AudioDirector>();
            director.Initialize(player, session, weeds, menu);
            return director;
        }

        void Initialize(PlayerController owner, YardSession yard, WeedField weeds, GameMenu menu)
        {
            player = owner;
            session = yard;
            mower = player.Tools.FindSlot(Tool.MowerSlot) as MowerTool;
            trimmer = player.Tools.FindSlot(Tool.TrimmerSlot) as TrimmerTool;
            hose = player.Tools.FindSlot(Tool.HoseSlot) as HoseTool;

            GameObject listenerObject = player.WorldCamera.gameObject;
            if (listenerObject.GetComponent<AudioListener>() == null)
                listenerObject.AddComponent<AudioListener>();
            Tap = listenerObject.AddComponent<MixTap>();

            engine = Loop(SoundBank.Engine);
            engineLoaded = Loop(SoundBank.EngineLoaded);
            chew = Loop(SoundBank.MowerChew);
            trimmerLoop = Loop(SoundBank.Trimmer, pan: 0.12f);
            trimmerHit = Loop(SoundBank.TrimmerHit, pan: 0.1f);
            waterWide = Loop(SoundBank.WaterWide, pan: 0.15f);
            waterNarrow = Loop(SoundBank.WaterNarrow, pan: 0.15f);
            splash = Loop(SoundBank.Splash);
            wind = Loop(SoundBank.Wind);
            ui = Source("Interface", loop: false);
            ui.ignoreListenerPause = true;
            effects = Source("Effect", loop: false);
            steps = Source("Step", loop: false);
            for (int i = 0; i < 5; i++)
            {
                AudioSource source = Source("World", loop: false);
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 10f;
                source.maxDistance = 140f;
                source.dopplerLevel = 0f;
                source.spread = 40f;
                world.Add(source);
            }

            session.Tasks.PatchFinished += OnPatchFinished;
            session.Tasks.PatchDiscovered += _ => PlayUi(SoundBank.Discover, 0.5f);
            session.Tasks.AreaCompleted += _ => PlayUi(SoundBank.ChimeArea, 0.8f);
            session.AreaUnlocked += OnAreaUnlocked;
            session.ItemFound += (_, position) => PlayUi(SoundBank.Found, 0.8f);
            session.ApplePicked += _ => PlayUi(SoundBank.ApplePick, 0.55f);
            session.PatternCompleted += _ => PlayUi(SoundBank.PatternDone, 0.8f);
            session.NightStarted += () =>
            {
                PlayUi(SoundBank.Night, 0.6f);
                paused = true;
                AudioListener.pause = true;
            };
            session.DayStarted += _ =>
            {
                paused = false;
                AudioListener.pause = false;
                birdTimer = 0.4f;
            };
            weeds.Removed += weed => PlayAt(SoundBank.WeedPop, weed.Position, 0.9f, Range(0.92f, 1.08f), blend: 0.6f);
            player.Tools.Changed += _ => PlayEffect(SoundBank.ToolSwitch, 0.55f, Range(0.95f, 1.05f));
            if (hose != null)
                hose.DescriptionChanged += _ => PlayEffect(SoundBank.HoseClick, 0.6f, 1f);
            if (menu != null)
            {
                menu.Opened += () => SetPaused(true);
                menu.Closed += () => SetPaused(false);
                menu.Clicked += () => PlayUi(SoundBank.MenuClick, 0.7f);
                menu.Purchased += _ => PlayUi(SoundBank.Purchase, 0.8f);
            }
        }

        AudioSource Source(string name, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            return source;
        }

        AudioSource Loop(string clip, float pan = 0f)
        {
            AudioSource source = Source(clip, loop: true);
            source.clip = SoundBank.Clip(clip);
            source.volume = 0f;
            source.panStereo = pan;
            source.timeSamples = random.Next(source.clip.samples);
            source.Play();
            return source;
        }

        float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        // ---------- Events ----------

        void OnPatchFinished(PatchProgress patch)
        {
            // Last patch of the area: the area sound will play, so the patch chime must not overlap it
            if (session.Tasks.IsAreaFinished(patch.Area))
                return;
            PlayUi(SoundBank.ChimePatch, 0.75f);
        }

        void OnAreaUnlocked(int area, bool animate)
        {
            if (!animate)
                return;
            foreach (Gate gate in YardBuilder.GatesTo(area))
                PlayAt(SoundBank.GateCreak, gate.OpeningCenter, 1f, 1f, blend: 0.8f);
        }

        void SetPaused(bool value)
        {
            paused = value;
            AudioListener.pause = value;
            PlayUi(value ? SoundBank.MenuOpen : SoundBank.MenuClose, 0.6f);
        }

        void PlayUi(string clip, float volume)
        {
            if (Quiet || TooSoon(clip)) return;
            ui.PlayOneShot(SoundBank.Clip(clip), volume * GameSettings.EffectsVolume);
            Count(clip);
        }

        void PlayEffect(string clip, float volume, float pitch)
        {
            if (Quiet || TooSoon(clip)) return;
            effects.pitch = pitch;
            effects.PlayOneShot(SoundBank.Clip(clip), volume * GameSettings.EffectsVolume);
            Count(clip);
        }

        /// <summary>Plays at a point in the world (from a free 3D source).</summary>
        void PlayAt(string clip, Vector3 position, float volume, float pitch, float blend = 1f, bool ambient = false)
        {
            if (Quiet || TooSoon(clip)) return;
            AudioSource source = world.Find(s => !s.isPlaying) ?? world[random.Next(world.Count)];
            source.transform.position = position;
            source.spatialBlend = blend;
            source.pitch = pitch;
            source.clip = SoundBank.Clip(clip);
            source.volume = volume * (ambient ? GameSettings.AmbientVolume : GameSettings.EffectsVolume);
            source.Play();
            Count(clip);
        }

        /// <summary>No feedback sounds at startup (tool selection while loading a save, etc.).</summary>
        static bool Quiet => Time.timeSinceLevelLoad < 0.3f;

        /// <summary>
        /// If the same sound is requested again within a short time (consecutive finishes, weeds pulled at once) only the first plays,
        /// so they do not stack up and overload the mix.
        /// </summary>
        bool TooSoon(string clip)
        {
            float now = Time.unscaledTime;
            float gap = clip == SoundBank.ChimePatch || clip == SoundBank.Discover ? 0.4f : 0.08f;
            if (lastPlayed.TryGetValue(clip, out float last) && now - last < gap)
                return true;
            lastPlayed[clip] = now;
            return false;
        }

        void Count(string clip)
        {
            OneShotsPlayed++;
            LastOneShot = clip;
            counts[clip] = PlayCount(clip) + 1;
        }

        /// <summary>How many times this one-shot sound has played (for scenario checks).</summary>
        public int PlayCount(string clip) => counts.TryGetValue(clip, out int n) ? n : 0;

        // ---------- Frame loop ----------

        void Update()
        {
            AudioListener.volume = GameSettings.MasterVolume;
            if (paused)
                return;
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            float fx = GameSettings.EffectsVolume, amb = GameSettings.AmbientVolume;
            Tool current = player.Tools.Current;
            float speed = player.PlanarVelocity.magnitude;

            // Mower: runs while held; under load the revs drop and the sound deepens
            float mowerPresence = current == mower && mower != null ? mower.Presence : 0f;
            float load = mower != null ? mower.CutLoad : 0f;
            float cutting = mower != null && mower.LastCutCells > 0 ? 1f : 0f;
            engineLevel = Smooth(engineLevel, mowerPresence, 3f, dt);
            loadLevel = Smooth(loadLevel, load * mowerPresence, 6f, dt);
            chewLevel = Smooth(chewLevel, cutting * mowerPresence * Mathf.Lerp(0.35f, 1f, load), 10f, dt);
            float wobble = Mathf.PerlinNoise(Time.time * 0.7f, 3.1f) - 0.5f;
            engine.volume = engineLevel * (0.5f + 0.15f * loadLevel) * fx;
            engine.pitch = 1f + 0.05f * Mathf.Clamp01(speed / 2.1f) - 0.08f * loadLevel + wobble * 0.02f;
            engineLoaded.volume = engineLevel * loadLevel * 0.45f * fx;
            engineLoaded.pitch = engine.pitch * 0.98f;
            chew.volume = chewLevel * 0.42f * fx;

            // Trimmer: idles in hand, revs up on the trigger; a hit sound when the line touches grass/weeds
            float trimmerPresence = current == trimmer && trimmer != null ? trimmer.Presence : 0f;
            bool spinning = trimmer != null && trimmer.IsSpinning;
            rev = Mathf.MoveTowards(rev, spinning ? 1f : 0f, dt / (spinning ? 0.25f : 0.45f));
            float hitting = trimmer != null && spinning && trimmer.LastCutCells > 0 ? 1f : 0f;
            hitLevel = Smooth(hitLevel, hitting * trimmerPresence, 14f, dt);
            trimmerLoop.volume = trimmerPresence * Mathf.Lerp(0.16f, 0.5f, rev) * fx;
            trimmerLoop.pitch = Mathf.Lerp(0.56f, 1f, rev * rev * (3f - 2f * rev));
            trimmerHit.volume = hitLevel * 0.38f * fx;

            // Hose: a wide hiss or a narrow jet depending on the mode; splashing on the ground
            float spray = hose != null && current == hose ? hose.SprayLevel : 0f;
            sprayLevel = Smooth(sprayLevel, spray, 12f, dt);
            bool narrow = hose != null && hose.Mode.Name == SprayMode.Narrow.Name;
            waterWide.volume = sprayLevel * (narrow ? 0f : 0.42f) * fx;
            waterNarrow.volume = sprayLevel * (narrow ? 0.38f : 0f) * fx;
            splash.volume = sprayLevel * (narrow ? 0.22f : 0.3f) * fx;

            // Wind and birds
            wind.volume = 0.4f * amb;
            birdTimer -= dt;
            if (birdTimer <= 0f)
            {
                SingBird();
                birdTimer = Range(1.6f, 6.5f);
            }

            UpdateFootsteps(speed, dt, mowerPresence, fx);
        }

        void SingBird()
        {
            string clip = SoundBank.Birds[random.Next(SoundBank.Birds.Length)];
            // Doves are rare
            if (clip.Contains("dove") && random.NextDouble() < 0.6)
                clip = SoundBank.Birds[random.Next(SoundBank.Birds.Length - 1)];
            Vector3 perch = BirdPerches[random.Next(BirdPerches.Length)] + new Vector3(Range(-1.5f, 1.5f), Range(-1f, 1.5f), Range(-1.5f, 1.5f));
            PlayAt(clip, perch, Range(0.55f, 1f), Range(0.92f, 1.08f), ambient: true);
            BirdsSung++;
        }

        void UpdateFootsteps(float speed, float dt, float mowerPresence, float fx)
        {
            if (speed < 0.35f || !player.Body.isGrounded)
            {
                strideDistance = Mathf.Min(strideDistance, StrideWalking * 0.5f);
                return;
            }
            strideDistance += speed * dt;
            float stride = mowerPresence > 0.5f ? StrideMowing : StrideWalking;
            if (strideDistance < stride)
                return;
            strideDistance -= stride;
            string clip = SoundBank.Footsteps[random.Next(SoundBank.Footsteps.Length)];
            float volume = Mathf.Lerp(0.25f, 0.5f, Mathf.Clamp01(speed / 3.4f)) * (mowerPresence > 0.5f ? 0.5f : 1f);
            steps.pitch = Range(0.9f, 1.1f);
            steps.PlayOneShot(SoundBank.Clip(clip), volume * fx);
            FootstepsPlayed++;
        }

        static float Smooth(float current, float target, float rate, float dt) =>
            Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
    }
}
