using System.Collections.Generic;
using Opus.Core;
using Opus.Lawn;
using Opus.Player;
using UnityEngine;

namespace Opus.Tools
{
    /// <summary>
    /// String trimmer. The head comes down where the gaze line meets the ground (0.8–1.8 m from the player);
    /// it is swept left and right with the mouse. The head does not enter obstacles (fence, flowerbed edge, shed) but the cutting circle
    /// reaches all the way to the base: this is how the edge strip left by the mower and the weeds are cleaned up.
    /// While the left button is held the line spins, cuts and throws debris.
    /// </summary>
    public sealed class TrimmerTool : Tool, IGrassPusher
    {
        public const float CutRadius = 0.2f;
        public const float MinReach = 0.8f;
        /// <summary>
        /// The farthest the head goes (m from the feet). 1.4 m since 21 October 2026 (was 1.8): in the video's 0:07 frame the trimmer is
        /// held compact, the head just beyond the D handle while the camera looks fairly level.
        /// </summary>
        public const float MaxReach = 1.4f;
        /// <summary>The closest the head centre can get to an obstacle surface.</summary>
        public const float HeadClearance = 0.075f;
        /// <summary>
        /// Height of the front end of the shaft above the ground (the head hangs below it): the head's puck is held about 3 cm off the
        /// ground, as when trimming (at 0.124 its lower edge sat in the mown grass and the blades cut its silhouette into spikes).
        /// </summary>
        const float HeadLift = 0.15f;
        const float SpinDegreesPerSecond = 10800f;
        /// <summary>
        /// Position of the front end of the motor body (the hands) in camera space: bottom right, close to the camera. Fitted to the
        /// video's 0:07 framing (body, battery, D handle and head on screen) by the u16_trimmer_tuning scenario.
        /// </summary>
        public static Vector3 HandAnchor = new Vector3(0.281f, -0.106f, 1.136f);
        /// <summary>Roll of the trimmer about its shaft (degrees); fitted with <see cref="HandAnchor"/>.</summary>
        public static float HoldRoll = -3.9f;
        static readonly Vector3 HiddenOffset = new Vector3(0.2f, -0.6f, -0.5f);

        readonly List<int> newlyCut = new List<int>();
        readonly List<WeedState> touchedWeeds = new List<WeedState>();
        readonly System.Random random = new System.Random(29);
        TrimmerModel model;
        Transform visualRoot;
        float spinAngle;
        float vibrationPhase;
        Vector2 lastHead;
        bool hasLastHead;

        public override int Slot => TrimmerSlot;
        public override string DisplayName => "Trimmer";
        public override string Hint => "Hold left click: trim · 1 mower · 3 hose · 4 hand";
        public override MovementProfile Movement => MovementProfile.Trimming;

        public TrimmerModel Model => model;
        public LawnState Lawn { get; set; }
        public Clippings Clippings { get; set; }
        public WeedField Weeds { get; set; }

        /// <summary>Where the head touches the ground (y = 0).</summary>
        public Vector3 HeadGround { get; private set; }
        /// <summary>Thick line upgrade: multipliers for the cutting circle and the weed pulling speed.</summary>
        public float RadiusScale { get; set; } = 1f;
        public float DamageScale { get; set; } = 1f;
        public bool IsSpinning { get; private set; }
        /// <summary>Did the head rest against an obstacle this frame?</summary>
        public bool HeadBlocked { get; private set; }
        public int LastCutCells { get; private set; }

        public static TrimmerTool Create(PlayerController player)
        {
            var root = new GameObject("Trimmer") { layer = Layers.Tool };
            root.transform.SetParent(player.transform, false);
            var tool = root.AddComponent<TrimmerTool>();
            tool.visualRoot = new GameObject("Visual").transform;
            tool.visualRoot.SetParent(root.transform, false);
            tool.model = TrimmerModel.Build(tool.visualRoot);
            return tool;
        }

        protected override void OnPresenceChanged(float presence)
        {
            bool visible = presence > 0f;
            if (visualRoot.gameObject.activeSelf != visible)
                visualRoot.gameObject.SetActive(visible);
        }

        public override void OnUnequip(PlayerController player)
        {
            player.CameraJitter = Vector3.zero;
            hasLastHead = false;
            IsSpinning = false;
        }

        /// <summary>
        /// Places the trimmer for an eye and the player's feet: the head comes down where the gaze meets the ground, the shaft runs from
        /// the hands (<see cref="HandAnchor"/> in camera space) to it. Separate from <see cref="Tick"/> so the framing fit can call it.
        /// </summary>
        public void PoseFrom(Transform eye, Vector3 feet)
        {
            Vector3 aim = ToolAim.GroundPoint(eye.position, eye.forward, feet, MinReach, MaxReach);

            // The head must not enter obstacles: sweep a low sphere from the feet to the aim point.
            Vector3 from = new Vector3(feet.x, 0.12f, feet.z);
            Vector3 toward = new Vector3(aim.x, 0.12f, aim.z) - from;
            HeadBlocked = false;
            if (Physics.SphereCast(from, HeadClearance, toward.normalized, out RaycastHit hit, toward.magnitude, Layers.ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                aim = from + toward.normalized * hit.distance;
                aim.y = 0f;
                HeadBlocked = true;
            }
            HeadGround = aim;

            // Shaft: from the motor body (camera bottom right) to above the head
            Vector3 shaftEnd = aim + Vector3.up * HeadLift;
            Vector3 anchor = eye.TransformPoint(HandAnchor);
            Vector3 along = shaftEnd - anchor;
            transform.SetPositionAndRotation(shaftEnd, Quaternion.LookRotation(along.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, HoldRoll));
            model.SetLength(along.magnitude);
            model.Head.rotation = Quaternion.LookRotation(new Vector3(along.x, 0f, along.z).normalized, Vector3.up);
        }

        public override void Tick(PlayerController player, in PlayerInputState input, float deltaTime)
        {
            PoseFrom(player.WorldCamera.transform, player.transform.position);

            float eased = Presence * Presence * (3f - 2f * Presence);
            visualRoot.localPosition = Vector3.Lerp(HiddenOffset, Vector3.zero, eased);

            IsSpinning = input.UseHeld && Presence >= 1f;
            model.Spinner.gameObject.SetActive(IsSpinning);
            model.IdleLines.gameObject.SetActive(!IsSpinning);
            if (IsSpinning)
            {
                spinAngle = (spinAngle + SpinDegreesPerSecond * deltaTime) % 360f;
                model.Spinner.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
                vibrationPhase += deltaTime * Mathf.PI * 2f * 38f;
                model.Root.localPosition = new Vector3(Mathf.Sin(vibrationPhase * 1.3f), Mathf.Sin(vibrationPhase), 0f) * 0.0025f;
                player.CameraJitter = new Vector3(0f, Mathf.Sin(vibrationPhase) * 0.0008f, 0f);
                Trim(player, deltaTime);
            }
            else
            {
                model.Root.localPosition = Vector3.zero;
                player.CameraJitter = Vector3.zero;
                hasLastHead = false;
                LastCutCells = 0;
            }
        }

        void Trim(PlayerController player, float deltaTime)
        {
            var head = new Vector2(HeadGround.x, HeadGround.z);
            Vector2 previous = hasLastHead && (head - lastHead).sqrMagnitude < 1f ? lastHead : head;
            lastHead = head;
            hasLastHead = true;

            if (Lawn != null)
            {
                newlyCut.Clear();
                // The trimmer writes no stripe direction: the cut area looks flatly mown.
                LastCutCells = Lawn.CutSwept(previous, head, CutRadius * RadiusScale, Vector2.zero, false, newlyCut);
                if (Clippings != null)
                    EmitGrassDebris();
            }

            if (Weeds != null)
            {
                touchedWeeds.Clear();
                Weeds.Damage(HeadGround, CutRadius * RadiusScale, deltaTime * DamageScale, touchedWeeds);
                if (Clippings != null)
                    EmitWeedDebris(deltaTime);
            }
        }

        float Next() => (float)random.NextDouble();

        /// <summary>The line flings clippings sideways, low and fast.</summary>
        void EmitGrassDebris()
        {
            foreach (int index in newlyCut)
            {
                if (Next() > 0.8f)
                    continue;
                Vector2 cell = Lawn.Grid.CellCenter(index);
                Vector3 outward = new Vector3(cell.x - HeadGround.x, 0f, cell.y - HeadGround.z);
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : Random.insideUnitSphere;
                outward.y = 0f;
                Vector3 position = new Vector3(cell.x, 0.06f + Next() * 0.08f, cell.y);
                Vector3 velocity = outward.normalized * Mathf.Lerp(2f, 3.8f, Next()) + Vector3.up * Mathf.Lerp(0.8f, 2.2f, Next());
                Color color = Lawn.GrassColor(index, Next()) * Mathf.Lerp(0.55f, 0.8f, Next());
                color.a = 1f;
                Clippings.Emit(position, velocity, color, Mathf.Lerp(0.035f, 0.065f, Next()), Mathf.Lerp(0.5f, 0.9f, Next()));
            }
        }

        /// <summary>Dark leaf pieces (the blackish debris in the reference) fly when it touches weeds.</summary>
        void EmitWeedDebris(float deltaTime)
        {
            foreach (WeedState weed in touchedWeeds)
            {
                int pieces = Mathf.Max(1, Mathf.RoundToInt(60f * deltaTime + Next()));
                for (int i = 0; i < pieces; i++)
                {
                    Vector3 outward = Quaternion.Euler(0f, Next() * 360f, 0f) * Vector3.forward;
                    Vector3 position = Vector3.Lerp(HeadGround, weed.Position, 0.5f) + Vector3.up * Mathf.Lerp(0.08f, 0.3f, Next());
                    Vector3 velocity = outward * Mathf.Lerp(1.5f, 3.2f, Next()) + Vector3.up * Mathf.Lerp(1.2f, 2.6f, Next());
                    Color color = weed.Kind == WeedKind.Weed
                        ? Color.Lerp(WeedField.LeafDark, Color.black, Next() * 0.6f)
                        : Color.Lerp(WeedField.VineBrown, Color.black, Next() * 0.5f);
                    Clippings.Emit(position, velocity, color, Mathf.Lerp(0.04f, 0.08f, Next()), Mathf.Lerp(0.5f, 1f, Next()));
                }
            }
        }

        public override bool TryGetWorkPoint(out Vector3 point)
        {
            point = HeadGround;
            return Presence >= 1f;
        }

        /// <summary>The head flattens the grass slightly; wider and stronger while the line spins.</summary>
        public bool TryGetGrassPush(out Vector4 push)
        {
            push = new Vector4(HeadGround.x, HeadGround.z, IsSpinning ? 0.24f : 0.18f, (IsSpinning ? 0.7f : 0.6f) * Presence);
            return Presence > 0f;
        }
    }
}
