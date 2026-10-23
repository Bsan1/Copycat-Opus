using System.Collections.Generic;
using Opus.Core;
using Opus.Lawn;
using Opus.Player;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>A spray mode of the hose: how wide, how far and how fast it waters.</summary>
    public readonly struct SprayMode
    {
        public readonly string Name;
        /// <summary>Radius of the watering circle (m).</summary>
        public readonly float Radius;
        public readonly float MinReach;
        public readonly float MaxReach;
        /// <summary>Dryness removed per second at the circle's centre (0..1 scale).</summary>
        public readonly float Rate;
        public readonly float StreamStartRadius;
        public readonly float StreamEndRadius;
        /// <summary>Arc of the stream: it curves up by this fraction of the distance.</summary>
        public readonly float Arc;
        public readonly float SplashPerSecond;

        public SprayMode(string name, float radius, float minReach, float maxReach, float rate,
            float streamStart, float streamEnd, float arc, float splashPerSecond)
        {
            Name = name;
            Radius = radius;
            MinReach = minReach;
            MaxReach = maxReach;
            Rate = rate;
            StreamStartRadius = streamStart;
            StreamEndRadius = streamEnd;
            Arc = arc;
            SplashPerSecond = splashPerSecond;
        }

        /// <summary>Watering power of the area that can be swept per second (m² × dryness/s): for comparing modes.</summary>
        public float Coverage => Mathf.PI * Radius * Radius * Rate;

        /// <summary>Wide: close, wide and gentle (the "Full" in the reference).</summary>
        public static readonly SprayMode Wide = new SprayMode("Wide", 0.85f, 1.3f, 3.2f, 0.8f, 0.03f, 0.13f, 0.22f, 90f);
        /// <summary>Narrow: far, thin and strong.</summary>
        public static readonly SprayMode Narrow = new SprayMode("Narrow", 0.38f, 2.2f, 6.5f, 1.7f, 0.02f, 0.045f, 0.3f, 50f);
        /// <summary>Sprinkler head (workshop): waters a wide circle gently.</summary>
        public static readonly SprayMode Rain = new SprayMode("Rain", 1.25f, 1.4f, 3.8f, 0.55f, 0.03f, 0.12f, 0.35f, 120f);

        /// <summary>Pressure upgrade: watering power multiplier and extra maximum reach (full in narrow mode, 40% in the others).</summary>
        public SprayMode Upgraded(float rateScale, float reachBonus)
        {
            float bonus = Name == Narrow.Name ? reachBonus : reachBonus * 0.4f;
            return new SprayMode(Name, Radius, MinReach, MaxReach + bonus, Rate * rateScale, StreamStartRadius, StreamEndRadius, Arc, SplashPerSecond);
        }
    }

    /// <summary>
    /// Hose with a pistol nozzle. The target is where the gaze meets the ground (distance limited by the mode, it stops in front
    /// of obstacles). While the left button is held the water flows from the nozzle to the target in an arc, splashes there and removes the
    /// dryness around it; the watered spot looks dark and wet for a while. R / RB changes the spray mode.
    /// Watering does not change the mown state.
    /// </summary>
    public sealed class HoseTool : Tool, IGrassPusher
    {
        /// <summary>
        /// The nozzle's grip is fixed to the camera (like first-person tools): the nozzle's position in camera space (bottom right) and
        /// its rotation relative to the camera (degrees). Both were found by measuring against the video frame: the screen position and
        /// size of the head and the knob, the head's apparent roundness, the grip's direction (scenario u16_hose_tuning).
        /// </summary>
        internal static Vector3 HandAnchor = new Vector3(0.209f, -0.134f, 0.75f);
        internal static Vector3 HoldEuler = new Vector3(-16.1f, -3.7f, -3.1f);
        static readonly Vector3 HiddenOffset = new Vector3(0.05f, -0.45f, -0.35f);
        const float RampPerSecond = 7f;

        readonly System.Random random = new System.Random(31);
        HoseModel model;
        /// <summary>The nozzle model (for the scenarios' measurements).</summary>
        public HoseModel Model => model;
        Transform visualRoot;
        WaterSpray spray;
        /// <summary>The water effect (for scenarios and tests).</summary>
        public WaterSpray Spray => spray;
        readonly System.Collections.Generic.List<SprayMode> modes = new System.Collections.Generic.List<SprayMode> { SprayMode.Wide, SprayMode.Narrow };
        int modeIndex;
        float rateScale = 1f, reachBonus;
        float splashBudget;
        bool hasTarget;

        public SprayMode Mode => modes[modeIndex].Upgraded(rateScale, reachBonus);
        public int ModeCount => modes.Count;
        public int ModeIndex => modeIndex;
        public override int Slot => HoseSlot;
        public override string DisplayName => "Hose / " + Mode.Name;
        public override string Hint => "Left click: water · R: spray · 1 mower · 2 trimmer · 4 hand";
        public override string Prompt => $"{InputGlyphs.Use} (hold): water   ·   {InputGlyphs.Alt}: change spray";
        public override MovementProfile Movement => MovementProfile.Watering;

        public LawnState Lawn { get; set; }
        public Clippings Splashes { get; set; }

        /// <summary>Where the water hits the ground (y = 0).</summary>
        public Vector3 Target { get; private set; }
        /// <summary>0..1: rises while the trigger is held, fades on release.</summary>
        public float SprayLevel { get; private set; }
        public bool IsSpraying => SprayLevel > 0.01f;
        public int LastWateredCells { get; private set; }
        public Vector3 NozzleTip => transform.position;

        public static HoseTool Create(PlayerController player)
        {
            var root = new GameObject("Hose") { layer = Layers.Tool };
            root.transform.SetParent(player.transform, false);
            var tool = root.AddComponent<HoseTool>();
            tool.visualRoot = new GameObject("Visual").transform;
            tool.visualRoot.SetParent(root.transform, false);
            tool.model = HoseModel.Build(tool.visualRoot);
            tool.spray = WaterSpray.Create("Water");
            tool.spray.Clear();
            return tool;
        }

        public void SetMode(int index)
        {
            index = ((index % modes.Count) + modes.Count) % modes.Count;
            if (index == modeIndex)
                return;
            modeIndex = index;
            RaiseDescriptionChanged();
        }

        /// <summary>Workshop upgrades: pressure (power, reach) and the sprinkler head (third mode).</summary>
        public void SetUpgrades(float rate, float reach, bool rainHead)
        {
            rateScale = Mathf.Max(0.1f, rate);
            reachBonus = Mathf.Max(0f, reach);
            bool has = modes.Count > 2;
            if (rainHead && !has)
                modes.Add(SprayMode.Rain);
            else if (!rainHead && has)
            {
                modes.RemoveAt(2);
                if (modeIndex >= modes.Count) modeIndex = 0;
            }
        }

        protected override void OnPresenceChanged(float presence)
        {
            bool visible = presence > 0f;
            if (visualRoot.gameObject.activeSelf != visible)
                visualRoot.gameObject.SetActive(visible);
            if (!visible)
                spray.Clear();
        }

        public override void OnEquip(PlayerController player) => hasTarget = false;

        public override void OnUnequip(PlayerController player)
        {
            SprayLevel = 0f;
            spray.Clear();
        }

        public override void Tick(PlayerController player, in PlayerInputState input, float deltaTime)
        {
            if (input.AltPressed && Presence >= 1f)
                SetMode(modeIndex + 1);

            Transform eye = player.WorldCamera.transform;
            Vector3 aim = ToolAim.GroundPoint(eye.position, eye.forward, player.transform.position, Mode.MinReach, Mode.MaxReach);
            aim = StopAtObstacle(eye.position, aim);
            // The water target follows with a slight delay (the stream's inertia)
            Target = hasTarget ? Vector3.Lerp(Target, aim, 1f - Mathf.Exp(-deltaTime * 16f)) : aim;
            hasTarget = true;

            // The nozzle is held fixed to the camera (bottom right as in the video, its mouth facing forward-left); the water sprays forward from the nozzle
            Vector3 tip = eye.TransformPoint(HandAnchor);
            Quaternion hold = eye.rotation * Quaternion.Euler(HoldEuler);
            transform.SetPositionAndRotation(tip, hold);
            Vector3 flow = new Vector3(Target.x - tip.x, 0f, Target.z - tip.z);
            flow = flow.sqrMagnitude > 1e-6f ? flow.normalized : eye.forward;

            float eased = Presence * Presence * (3f - 2f * Presence);
            visualRoot.localPosition = Vector3.Lerp(HiddenOffset, Vector3.zero, eased);

            bool held = input.UseHeld && Presence >= 1f;
            SprayLevel = Mathf.MoveTowards(SprayLevel, held ? 1f : 0f, RampPerSecond * deltaTime);
            model.Trigger.localRotation = Quaternion.Euler(HoseModel.TriggerSqueeze * SprayLevel, 0f, 0f);

            if (Presence >= 1f)
                spray.Tick(tip, hold * Vector3.forward, Target, Mode, SprayLevel, deltaTime);

            LastWateredCells = 0;
            if (SprayLevel > 0.01f)
            {
                if (Lawn != null)
                    LastWateredCells = Lawn.Water(new Vector2(Target.x, Target.z), Mode.Radius, Mode.Rate * SprayLevel * deltaTime);
                if (Splashes != null)
                    EmitSplash(deltaTime, flow);
            }
        }

        /// <summary>Water does not pass through walls/fences: if there is an obstacle on the eye → target line, it lands in front of it.</summary>
        static Vector3 StopAtObstacle(Vector3 eye, Vector3 aim)
        {
            Vector3 to = aim + Vector3.up * 0.08f - eye;
            if (!Physics.Raycast(eye, to.normalized, out RaycastHit hit, to.magnitude, Layers.ObstacleMask, QueryTriggerInteraction.Ignore))
                return aim;
            Vector3 point = hit.point - new Vector3(to.x, 0f, to.z).normalized * 0.12f;
            point.y = 0f;
            return point;
        }

        float Next() => (float)random.NextDouble();

        void EmitSplash(float deltaTime, Vector3 flow)
        {
            splashBudget += Mode.SplashPerSecond * 0.35f * SprayLevel * deltaTime;   // splash drops are hardly visible in the video
            var flatFlow = new Vector3(flow.x, 0f, flow.z);
            while (splashBudget >= 1f)
            {
                splashBudget -= 1f;
                Vector2 offset = Random.insideUnitCircle * (Mode.Radius * 0.6f);
                var position = new Vector3(Target.x + offset.x, 0.12f + Next() * 0.12f, Target.z + offset.y);
                Vector3 velocity = new Vector3(Next() - 0.5f, 0f, Next() - 0.5f) * 1.8f + flatFlow * 1.2f + Vector3.up * Mathf.Lerp(0.8f, 2f, Next());
                Color color = Color.Lerp(new Color(0.78f, 0.88f, 0.95f), Color.white, Next() * 0.6f);
                Splashes.Emit(position, velocity, color, Mathf.Lerp(0.018f, 0.032f, Next()), Mathf.Lerp(0.25f, 0.4f, Next()));
            }
        }

        public override bool TryGetWorkPoint(out Vector3 point)
        {
            point = Target;
            return Presence >= 1f && hasTarget;
        }

        /// <summary>The water pressure flattens the grass at the target slightly.</summary>
        public bool TryGetGrassPush(out Vector4 push)
        {
            push = new Vector4(Target.x, Target.z, Mode.Radius * 0.9f, 0.4f * SprayLevel);
            return SprayLevel > 0.01f;
        }

        void OnDestroy()
        {
            if (spray != null) Destroy(spray.gameObject);
        }
    }
}
