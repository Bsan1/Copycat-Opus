using Opus.Core;
using Opus.Lawn;
using Opus.Player;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>
    /// Lawn mower: a real world object in front of the player, turning with them.
    /// The sense of weight comes from the movement profile (slow start, coasting, delayed turning) and the visual
    /// reactions: engine vibration, leaning in turns, the nose lifting on take-off, spinning wheels.
    /// Every frame the blade circle is swept from the last position to the new one and mows the grass under it.
    /// The housing cannot enter obstacles and the blade is narrower than the housing; so an unmowable strip is left
    /// by itself at fence/flowerbed bases (the trimmer's job).
    /// </summary>
    public sealed class MowerTool : Tool, IGrassPusher
    {
        /// <summary>Forward distance from the player's feet to the housing centre.</summary>
        public const float ForwardOffset = 1.15f;
        /// <summary>Position of the right-hand grip relative to the player.</summary>
        public static readonly Vector3 GripFromPlayer = new Vector3(0.34f, 0.88f, 0.12f);
        /// <summary>Radius of the blade circle; a little narrower than half the housing width (0.29 m).</summary>
        public const float BladeRadius = 0.28f;
        /// <summary>At most this much slowdown while mowing long grass.</summary>
        public const float CuttingSlowdown = 0.1f;

        static readonly Vector3 HullCenter = new Vector3(0f, 0.27f, -0.02f);
        static readonly Vector3 HullSize = new Vector3(0.72f, 0.4f, 0.74f);
        static readonly Vector3 BladeCenter = new Vector3(0f, 0.05f, 0.03f);
        static readonly Vector3 HiddenOffset = new Vector3(0f, -0.55f, -0.7f);
        const int MaxResolveIterations = 4;
        const float MovingSpeed = 0.25f;
        /// <summary>Full load: mowing this many cells (0.01 m²) per second.</summary>
        const float FullLoadCellsPerSecond = 90f;

        readonly Collider[] overlaps = new Collider[16];
        readonly System.Collections.Generic.List<int> newlyCut = new System.Collections.Generic.List<int>();
        readonly System.Random random = new System.Random(19);
        MowerModel model;
        BoxCollider hull;
        Transform visualRoot;
        float frontSpin, rearSpin;
        float lean, nose, previousForwardSpeed;
        float enginePhase;
        Vector2 lastBlade;
        bool hasLastBlade;
        float cutRate;

        public override int Slot => MowerSlot;
        public override string DisplayName => "Mower";
        public override string Hint => "WASD push · Mouse steer · 2 trimmer · 3 hose · 4 hand";

        public override MovementProfile Movement
        {
            get
            {
                MovementProfile profile = MovementProfile.Mowing;
                profile.ForwardSpeed *= (1f + SpeedBonus) * (1f - Slowdown * CutLoad);
                return profile;
            }
        }

        /// <summary>Mowing load on the engine (0..1): rises in long grass, slows down and strengthens the vibration.</summary>
        public float CutLoad { get; private set; }
        /// <summary>Number of cells newly mown in the last frame.</summary>
        public int LastCutCells { get; private set; }
        public LawnState Lawn { get; set; }
        public Clippings Clippings { get; set; }

        /// <summary>Upgrade: deck width multiplier (the blade circle and the housing X/Z grow together).</summary>
        public float DeckScale { get; private set; } = 1f;
        public float SpeedBonus { get; private set; }
        public float Slowdown { get; private set; } = CuttingSlowdown;
        /// <summary>The blade circle including upgrades.</summary>
        public float CurrentBladeRadius => BladeRadius * DeckScale;

        /// <summary>Applies the workshop upgrades.</summary>
        public void SetUpgrades(float deckScale, float speedBonus, float slowdown)
        {
            DeckScale = Mathf.Max(1f, deckScale);
            SpeedBonus = Mathf.Max(0f, speedBonus);
            Slowdown = Mathf.Clamp01(slowdown);
            hull.size = new Vector3(HullSize.x * DeckScale, HullSize.y, HullSize.z * (1f + (DeckScale - 1f) * 0.6f));
            model.SetDeckScale(DeckScale);
        }

        /// <summary>Total push needed to get out of obstacles in the last frame (m); 0 = clear.</summary>
        public float LastPushDistance { get; private set; }
        public Vector3 HullWorldCenter => transform.TransformPoint(HullCenter);
        /// <summary>Front end of the housing (wheels included), in world coordinates.</summary>
        public Vector3 FrontTip => transform.TransformPoint(new Vector3(0f, 0.1f, HullCenter.z + hull.size.z * 0.5f));

        public static MowerTool Create(PlayerController player)
        {
            var root = new GameObject("Mower") { layer = Layers.Tool };
            root.transform.SetParent(player.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, ForwardOffset);

            var tool = root.AddComponent<MowerTool>();
            tool.hull = root.AddComponent<BoxCollider>();
            tool.hull.center = HullCenter;
            tool.hull.size = HullSize;

            tool.visualRoot = new GameObject("Visual").transform;
            tool.visualRoot.SetParent(root.transform, false);
            // The model's handle reaches the hands at GripFromPlayer (ModelImportTests checks the connection point)
            tool.model = MowerModel.Build(tool.visualRoot);
            return tool;
        }

        protected override void OnPresenceChanged(float presence)
        {
            float eased = presence * presence * (3f - 2f * presence);
            visualRoot.localPosition = Vector3.Lerp(HiddenOffset, Vector3.zero, eased);
            bool visible = presence > 0f;
            if (visualRoot.gameObject.activeSelf != visible)
                visualRoot.gameObject.SetActive(visible);
            hull.enabled = visible;
        }

        /// <summary>Gets the housing out of obstacles: the push vectors of all overlapping obstacles are summed.</summary>
        public override void ResolveBody(PlayerController player)
        {
            LastPushDistance = 0f;
            if (Presence <= 0f)
                return;

            for (int iteration = 0; iteration < MaxResolveIterations; iteration++)
            {
                Vector3 push = ComputePush();
                if (push.sqrMagnitude < 1e-8f)
                    break;
                LastPushDistance += push.magnitude;
                player.Push(push + push.normalized * 0.002f);
            }
        }

        /// <summary>Is the housing inside an obstacle right now (should be false after resolving)?</summary>
        public bool OverlapsObstacle(float tolerance = 0.005f)
        {
            return ComputePush().magnitude > tolerance;
        }

        Vector3 ComputePush()
        {
            Transform t = transform;
            Quaternion rotation = t.rotation;
            Vector3 position = t.position;
            int count = Physics.OverlapBoxNonAlloc(t.TransformPoint(HullCenter), hull.size * 0.5f, overlaps, rotation,
                Layers.ObstacleMask, QueryTriggerInteraction.Ignore);

            Vector3 push = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (other == hull || other is CharacterController)
                    continue;
                if (Physics.ComputePenetration(hull, position, rotation, other, other.transform.position, other.transform.rotation,
                        out Vector3 direction, out float distance))
                {
                    direction.y = 0f;
                    if (direction.sqrMagnitude > 1e-6f)
                        push += direction.normalized * distance;
                }
            }
            return push;
        }

        public override void Tick(PlayerController player, in PlayerInputState input, float deltaTime)
        {
            Vector2 velocity = player.LocalVelocity;
            float speed = velocity.magnitude;

            // Wheels: forward speed + the two sides do not spin at different speeds in turns (kept simple).
            frontSpin += velocity.y * deltaTime / MowerModel.FrontWheelRadius * Mathf.Rad2Deg;
            rearSpin += velocity.y * deltaTime / MowerModel.RearWheelRadius * Mathf.Rad2Deg;
            foreach (Transform axle in model.FrontAxles) axle.localRotation = Quaternion.Euler(frontSpin, 0f, 0f);
            foreach (Transform axle in model.RearAxles) axle.localRotation = Quaternion.Euler(rearSpin, 0f, 0f);

            // Leaning outwards in turns, the nose lifting slightly on acceleration (springy).
            float forwardAccel = (velocity.y - previousForwardSpeed) / deltaTime;
            previousForwardSpeed = velocity.y;
            float targetLean = Mathf.Clamp(-player.TurnRate * 0.025f * Mathf.Clamp01(speed + 0.3f), -3f, 3f);
            float targetNose = Mathf.Clamp(-forwardAccel * 0.5f, -1.6f, 1.6f);
            float blend = 1f - Mathf.Exp(-deltaTime * 8f);
            lean = Mathf.Lerp(lean, targetLean, blend);
            nose = Mathf.Lerp(nose, targetNose, blend);

            Mow(player, deltaTime);

            // Engine vibration: light at idle, stronger while moving and mowing long grass.
            float load = Mathf.Clamp01(speed / MovementProfile.Mowing.ForwardSpeed);
            enginePhase += deltaTime * Mathf.PI * 2f * Mathf.Lerp(24f, 31f, load);
            float shake = Mathf.Lerp(0.0016f, 0.0032f, load) + CutLoad * 0.0016f;
            Vector3 vibration = new Vector3(Mathf.Sin(enginePhase * 1.37f) * shake * 0.4f, Mathf.Sin(enginePhase) * shake, 0f);

            model.Chassis.localPosition = vibration;
            model.Chassis.localRotation = Quaternion.Euler(nose, 0f, lean);

            // The handle is attached to the hands: a very small part of the vibration reaches the camera.
            player.CameraJitter = Presence > 0.99f ? vibration * 0.25f : Vector3.zero;
        }

        /// <summary>Sweeps the blade circle from the last position to this one and throws clippings from the newly mown cells.</summary>
        void Mow(PlayerController player, float deltaTime)
        {
            LastCutCells = 0;
            if (Lawn == null || Presence < 1f)
            {
                hasLastBlade = false;
                cutRate = 0f;
                CutLoad = 0f;
                return;
            }

            Vector3 bladeWorld = transform.TransformPoint(BladeCenter);
            var blade = new Vector2(bladeWorld.x, bladeWorld.z);
            // On big jumps such as teleporting, the path in between is not mown.
            Vector2 from = hasLastBlade && (blade - lastBlade).sqrMagnitude < 1f ? lastBlade : blade;
            Vector3 velocity = player.PlanarVelocity;
            bool moving = velocity.magnitude > MovingSpeed;

            newlyCut.Clear();
            LastCutCells = Lawn.CutSwept(from, blade, CurrentBladeRadius, new Vector2(velocity.x, velocity.z), moving, newlyCut);
            lastBlade = blade;
            hasLastBlade = true;

            // Smooth the cut rate first, then clip: at high frame rates most frames cut no cells,
            // clipping per frame and averaging would show a lower load than the real one.
            cutRate = Mathf.Lerp(cutRate, LastCutCells / deltaTime, 1f - Mathf.Exp(-deltaTime * 4f));
            CutLoad = Mathf.Clamp01(cutRate / FullLoadCellsPerSecond);

            if (Clippings != null)
                EmitClippings(bladeWorld, velocity);
        }

        /// <summary>
        /// Clippings are flung from the edge of the housing in the cutting direction (from under the housing they would be invisible);
        /// a little darker than the grass itself because of the cut tips and shadow.
        /// </summary>
        void EmitClippings(Vector3 bladeWorld, Vector3 mowerVelocity)
        {
            float Next() => (float)random.NextDouble();
            foreach (int index in newlyCut)
            {
                Vector2 cell = Lawn.Grid.CellCenter(index);
                Vector3 outward = new Vector3(cell.x, 0f, cell.y) - new Vector3(bladeWorld.x, 0f, bladeWorld.z);
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : transform.right;
                // 1-2 pieces per cell: ~150 clippings per second when walking steadily
                int pieces = Next() < 0.6f ? 2 : 1;
                for (int k = 0; k < pieces; k++)
                {
                    Vector3 spread = Quaternion.Euler(0f, (Next() - 0.5f) * 50f, 0f) * outward;
                    Vector3 position = bladeWorld + spread * (CurrentBladeRadius + 0.06f) + Vector3.up * Mathf.Lerp(0.3f, 0.42f, Next());
                    Vector3 velocity = spread * Mathf.Lerp(0.8f, 1.9f, Next()) + Vector3.up * Mathf.Lerp(2.4f, 3.8f, Next()) + mowerVelocity * 0.5f;
                    Color color = Lawn.GrassColor(index, Next()) * Mathf.Lerp(0.62f, 0.85f, Next());
                    color.a = 1f;
                    Clippings.Emit(position, velocity, color, Mathf.Lerp(0.05f, 0.09f, Next()), Mathf.Lerp(0.7f, 1.3f, Next()));
                }
            }
        }

        public override void OnUnequip(PlayerController player)
        {
            player.CameraJitter = Vector3.zero;
            hasLastBlade = false;
        }

        public override bool TryGetWorkPoint(out Vector3 point)
        {
            point = transform.TransformPoint(BladeCenter);
            return Presence >= 1f;
        }

        /// <summary>The housing flattens the grass under and around it outwards (so blades do not poke through the housing).</summary>
        public bool TryGetGrassPush(out Vector4 push)
        {
            Vector3 center = HullWorldCenter;
            push = new Vector4(center.x, center.z, 0.48f * DeckScale, Presence);
            return Presence > 0f;
        }
    }
}
