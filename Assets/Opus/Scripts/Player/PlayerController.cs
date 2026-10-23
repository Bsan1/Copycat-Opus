using Opus.Core;
using Opus.Lawn;
using Opus.Tools;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Opus.Player
{
    /// <summary>
    /// First-person player. The movement feel comes from the tool in hand's <see cref="MovementProfile"/>:
    /// velocity is kept in local axes (forward/sideways), so when turning with the mower the velocity turns with it.
    /// A separate URP Overlay camera for the interface is stacked on top of the world camera.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour, IGrassPusher
    {
        public const float EyeHeight = 1.62f;
        const float Gravity = 20f;
        const float FieldOfViewSmoothTime = 0.25f;
        const float MaxTurnLag = 120f;
        const float EquipPitchSmoothTime = 0.22f;
        const float EquipPitchSeconds = 0.6f;
        const float ContactMemorySeconds = 0.15f;

        CharacterController body;
        Transform cameraTransform;
        float targetYaw, yawVelocity;
        float targetPitch, pitchVelocity;
        float equipPitchTimer;
        float verticalSpeed;
        float bobPhase;
        Vector3 cameraBase;
        Vector3 contactNormal;
        float contactTimer;
        float fovVelocity;

        public Camera WorldCamera { get; private set; }
        public Camera UiCamera { get; private set; }
        public ToolBelt Tools { get; private set; }
        public CharacterController Body => body;

        /// <summary>Body heading (degrees, unwrapped).</summary>
        public float Yaw { get; private set; }
        /// <summary>Camera pitch (degrees, positive down).</summary>
        public float Pitch { get; private set; }
        /// <summary>Actual local velocity (X: sideways, Y: forward), drops when resting against an obstacle.</summary>
        public Vector2 LocalVelocity { get; private set; }
        public float TurnRate { get; private set; }
        /// <summary>The input read this frame (for systems running after the player).</summary>
        public PlayerInputState LastInput { get; private set; } = PlayerInputState.None;
        /// <summary>Horizontal velocity in world space.</summary>
        public Vector3 PlanarVelocity => Quaternion.Euler(0f, Yaw, 0f) * new Vector3(LocalVelocity.x, 0f, LocalVelocity.y);
        public MovementProfile Profile { get; private set; } = MovementProfile.Walking;
        /// <summary>Small vibration the tools add to the camera (local position), rewritten every frame.</summary>
        public Vector3 CameraJitter { get; set; }
        /// <summary>Fixed field of view for framing trials; NaN = use the profile's.</summary>
        public float FieldOfViewOverride { get; set; } = float.NaN;

        public static PlayerController Create(Vector3 feet, float yaw)
        {
            var root = new GameObject("Player") { layer = Layers.Player };
            var body = root.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.35f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.3f;
            body.slopeLimit = 50f;
            body.skinWidth = 0.03f;
            // The default 1 mm threshold swallows movement entirely at high frame rates (< 1 mm step per frame).
            body.minMoveDistance = 0f;

            var controller = root.AddComponent<PlayerController>();
            controller.body = body;

            var cameraObject = new GameObject("Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            controller.cameraBase = new Vector3(0f, EyeHeight, 0f);
            cameraObject.transform.localPosition = controller.cameraBase;
            controller.cameraTransform = cameraObject.transform;

            var worldCamera = cameraObject.AddComponent<Camera>();
            worldCamera.fieldOfView = MovementProfile.Walking.FieldOfView;
            worldCamera.nearClipPlane = 0.05f;
            worldCamera.farClipPlane = 500f;
            worldCamera.clearFlags = CameraClearFlags.Skybox;
            worldCamera.cullingMask = ~(1 << Layers.UI);
            var worldData = worldCamera.GetUniversalAdditionalCameraData();
            worldData.renderShadows = true;
            worldData.renderPostProcessing = true;

            var uiObject = new GameObject("InterfaceCamera");
            uiObject.transform.SetParent(cameraObject.transform, false);
            var uiCamera = uiObject.AddComponent<Camera>();
            uiCamera.cullingMask = 1 << Layers.UI;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 10f;
            var uiData = uiCamera.GetUniversalAdditionalCameraData();
            uiData.renderType = CameraRenderType.Overlay;
            uiData.renderShadows = false;
            worldData.cameraStack.Add(uiCamera);

            controller.WorldCamera = worldCamera;
            controller.UiCamera = uiCamera;
            controller.Tools = root.AddComponent<ToolBelt>();
            controller.Tools.Changed += controller.OnToolChanged;
            controller.Teleport(feet, yaw, 0f);
            return controller;
        }

        /// <summary>Sets the position and look instantly; velocity and smoothing state are reset.</summary>
        public void Teleport(Vector3 feet, float yaw, float pitch)
        {
            body.enabled = false;
            transform.position = feet;
            Yaw = targetYaw = yaw;
            Pitch = targetPitch = Mathf.Clamp(pitch, Profile.MinPitch, Profile.MaxPitch);
            yawVelocity = pitchVelocity = 0f;
            equipPitchTimer = 0f;
            verticalSpeed = 0f;
            LocalVelocity = Vector2.zero;
            TurnRate = 0f;
            contactTimer = 0f;
            fovVelocity = 0f;
            if (WorldCamera != null)
                WorldCamera.fieldOfView = TargetFieldOfView;
            ApplyLook();
            body.enabled = true;
        }

        float TargetFieldOfView => float.IsNaN(FieldOfViewOverride) ? Profile.FieldOfView : FieldOfViewOverride;

        void OnToolChanged(Tool tool)
        {
            Profile = tool.Movement;
            if (!float.IsNaN(Profile.EquipPitch))
            {
                targetPitch = Profile.EquipPitch;
                equipPitchTimer = EquipPitchSeconds;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
                return;

            UpdateCursor();
            PlayerInputState input = InputReader.Read(dt);
            LastInput = input;
            Tools.Tick(this, input, dt);
            Profile = Tools.Movement;

            UpdateLook(input, dt);
            UpdateMovement(input, dt);
            Tools.Current?.ResolveBody(this);
            UpdateCameraMotion(dt);
            Tools.Current?.Tick(this, input, dt);
            cameraTransform.localPosition = cameraBase + BobOffset() + CameraJitter;
        }

        /// <summary>A left click locks the cursor; releasing it is the break menu's job (Esc).</summary>
        void UpdateCursor()
        {
            if (InputReader.IsSimulating || ScenarioRunner.IsRunning)
                return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void UpdateLook(in PlayerInputState input, float dt)
        {
            targetYaw += input.LookDelta.x;
            targetPitch = Mathf.Clamp(targetPitch - input.LookDelta.y, Profile.MinPitch, Profile.MaxPitch);

            float previousYaw = Yaw;
            Yaw = MotionMath.SmoothAngle(Yaw, ref targetYaw, ref yawVelocity, Profile.TurnSmoothTime, Profile.MaxTurnSpeed, MaxTurnLag, dt);
            TurnRate = (Yaw - previousYaw) / dt;

            equipPitchTimer -= dt;
            float pitchSmooth = equipPitchTimer > 0f ? EquipPitchSmoothTime : Profile.TurnSmoothTime;
            Pitch = pitchSmooth > 0f
                ? Mathf.SmoothDamp(Pitch, targetPitch, ref pitchVelocity, pitchSmooth, Mathf.Infinity, dt)
                : targetPitch;
            ApplyLook();
        }

        void UpdateMovement(in PlayerInputState input, float dt)
        {
            Vector2 desired = MotionMath.DesiredLocalVelocity(input.Move, Profile);
            Vector2 local = MotionMath.Approach(LocalVelocity, desired, Profile.Acceleration, Profile.Deceleration, dt);

            Quaternion heading = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 planar = heading * new Vector3(local.x, 0f, local.y);

            // If the tool recently rested against an obstacle, the velocity component towards that obstacle is cut from the start
            // (otherwise it would push in by millimetres and be pushed back every frame, and the velocity would never reach zero).
            if (contactTimer > 0f)
            {
                contactTimer -= dt;
                float into = Vector3.Dot(planar, contactNormal);
                if (into < 0f)
                {
                    planar -= contactNormal * into;
                    Vector3 slid = Quaternion.Inverse(heading) * planar;
                    local = new Vector2(slid.x, slid.z);
                }
            }

            verticalSpeed = body.isGrounded ? -1f : verticalSpeed - Gravity * dt;
            Vector3 before = transform.position;
            body.Move((planar + Vector3.up * verticalSpeed) * dt);
            Vector3 moved = transform.position - before;
            moved.y = 0f;

            // When resting against an obstacle, the velocity drops to the direction and amount actually travelled (no "pushing into" walls).
            // Only applied to a clear blocking; small numeric differences must not wear the velocity down.
            Vector3 intended = planar * dt;
            if (intended.sqrMagnitude > 1e-12f && moved.sqrMagnitude < intended.sqrMagnitude * 0.96f)
            {
                Vector3 actual = moved / dt;
                planar = Vector3.Dot(actual, planar) <= 0f ? Vector3.zero : Vector3.ClampMagnitude(actual, planar.magnitude);
                Vector3 back = Quaternion.Inverse(heading) * planar;
                local = new Vector2(back.x, back.z);
            }
            LocalVelocity = local;
        }

        /// <summary>Pushes the player to get the tool body out of an obstacle and cuts the velocity in the push direction.</summary>
        public void Push(Vector3 displacement)
        {
            displacement.y = 0f;
            if (displacement.sqrMagnitude < 1e-10f)
                return;
            body.Move(displacement);

            Quaternion heading = Quaternion.Euler(0f, Yaw, 0f);
            Vector3 planar = heading * new Vector3(LocalVelocity.x, 0f, LocalVelocity.y);
            Vector3 normal = displacement.normalized;
            contactNormal = normal;
            contactTimer = ContactMemorySeconds;
            float into = Vector3.Dot(planar, normal);
            if (into < 0f)
                planar -= normal * into;
            Vector3 back = Quaternion.Inverse(heading) * planar;
            LocalVelocity = new Vector2(back.x, back.z);
        }

        void UpdateCameraMotion(float dt)
        {
            WorldCamera.fieldOfView = Mathf.SmoothDamp(WorldCamera.fieldOfView, TargetFieldOfView, ref fovVelocity, FieldOfViewSmoothTime, Mathf.Infinity, dt);

            float speed = LocalVelocity.magnitude;
            float stride = Mathf.Max(0.1f, Profile.ForwardSpeed / Mathf.Max(0.1f, Profile.BobFrequency));
            bobPhase += speed / stride * Mathf.PI * 2f * dt;
        }

        Vector3 BobOffset()
        {
            float weight = Mathf.Clamp01(LocalVelocity.magnitude / Mathf.Max(0.1f, Profile.ForwardSpeed));
            float amplitude = Profile.BobAmplitude * weight;
            return new Vector3(Mathf.Sin(bobPhase * 0.5f) * amplitude * 0.5f, Mathf.Sin(bobPhase) * amplitude, 0f);
        }

        /// <summary>The grass around the feet leans aside slightly.</summary>
        public bool TryGetGrassPush(out Vector4 push)
        {
            Vector3 feet = transform.position;
            push = new Vector4(feet.x, feet.z, 0.4f, 0.7f);
            return true;
        }

        void ApplyLook()
        {
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
        }
    }
}
