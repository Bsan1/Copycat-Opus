using UnityEngine;
using UnityEngine.InputSystem;

namespace Opus.Player
{
    /// <summary>One frame of player input; from keyboard/mouse, gamepad or scenario simulation.</summary>
    public struct PlayerInputState
    {
        /// <summary>X: right/left, Y: forward/back; length at most 1.</summary>
        public Vector2 Move;
        /// <summary>Look change this frame, in degrees (X: right, Y: up).</summary>
        public Vector2 LookDelta;
        public bool UseHeld;
        public bool UsePressed;
        /// <summary>Directly selected tool slot (0..3), otherwise -1.</summary>
        public int SelectSlot;
        /// <summary>Tool cycling: -1 previous, +1 next, 0 none.</summary>
        public int CycleTool;
        public bool AltPressed;
        public bool HighlightPressed;

        public static PlayerInputState None => new PlayerInputState { SelectSlot = -1 };
    }

    /// <summary>
    /// Reads the input devices directly (no action asset, everything in code). Scenarios
    /// take over the input with <see cref="Simulate"/>; meanwhile the real devices are ignored.
    /// </summary>
    /// <summary>The last used input type (the on-screen button names follow it).</summary>
    public enum InputScheme
    {
        KeyboardMouse,
        Gamepad,
    }

    /// <summary>Button names for the last used device (prompt strips, hints).</summary>
    public static class InputGlyphs
    {
        static bool Pad => InputReader.LastScheme == InputScheme.Gamepad;
        public static string Use => Pad ? "RT" : "Left click";
        public static string Alt => Pad ? "RB" : "R";
        public static string Highlight => Pad ? "R3" : "H";
        public static string Cycle => Pad ? "LB / D-pad" : "Q / E";
        public static string Pause => Pad ? "Start" : "Esc";
        public static string ToolbarHint => Pad ? "LB / D-pad: switch tool" : "1-4 select  ·  Q / E next tool";
    }

    public static class InputReader
    {
        /// <summary>The last used input type; raises <see cref="SchemeChanged"/> when it changes.</summary>
        public static InputScheme LastScheme { get; private set; } = InputScheme.KeyboardMouse;
        public static event System.Action<InputScheme> SchemeChanged;

        /// <summary>Forces the input type (scenarios: to show the gamepad button names).</summary>
        public static void ForceScheme(InputScheme scheme)
        {
            if (scheme == LastScheme)
                return;
            LastScheme = scheme;
            SchemeChanged?.Invoke(scheme);
        }

        /// <summary>Updates the last input type from device activity (every frame; can also be called in the menu).</summary>
        public static void DetectScheme()
        {
            Gamepad pad = Gamepad.current;
            bool padActive = pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.06f || pad.rightStick.ReadValue().sqrMagnitude > 0.06f
                || pad.buttonSouth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed || pad.buttonNorth.isPressed
                || pad.leftTrigger.isPressed || pad.rightTrigger.isPressed || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed
                || pad.dpad.ReadValue().sqrMagnitude > 0.1f || pad.startButton.isPressed);
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool kmActive = (keyboard != null && keyboard.anyKey.isPressed)
                || (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.isPressed || mouse.rightButton.isPressed));
            InputScheme scheme = padActive ? InputScheme.Gamepad : kmActive ? InputScheme.KeyboardMouse : LastScheme;
            if (simulating)
                return;
            if (scheme == LastScheme)
                return;
            LastScheme = scheme;
            SchemeChanged?.Invoke(scheme);
        }

        public static float MouseDegreesPerPixel = 0.09f;
        public static float StickDegreesPerSecond = 150f;
        /// <summary>From the settings: look sensitivity multiplier and vertical look inversion (mouse and gamepad).</summary>
        public static float LookSensitivity = 1f;
        public static bool InvertY;

        static bool simulating;
        static PlayerInputState simulated = PlayerInputState.None;

        public static bool IsSimulating => simulating;

        public static void Simulate(PlayerInputState state)
        {
            simulating = true;
            simulated = state;
        }

        public static void StopSimulation()
        {
            simulating = false;
            simulated = PlayerInputState.None;
        }

        public static PlayerInputState Read(float deltaTime)
        {
            if (simulating)
            {
                PlayerInputState state = simulated;
                // One-shot presses only last one frame.
                simulated.UsePressed = false;
                simulated.SelectSlot = -1;
                simulated.CycleTool = 0;
                simulated.AltPressed = false;
                simulated.HighlightPressed = false;
                return state;
            }

            PlayerInputState input = PlayerInputState.None;
            DetectScheme();
            ReadKeyboardMouse(ref input);
            ReadGamepad(ref input, deltaTime);
            input.Move = Vector2.ClampMagnitude(input.Move, 1f);
            input.LookDelta *= LookSensitivity;
            if (InvertY) input.LookDelta.y = -input.LookDelta.y;
            return input;
        }

        static void ReadKeyboardMouse(ref PlayerInputState input)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool locked = Cursor.lockState == CursorLockMode.Locked;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.Move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.Move.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.Move.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.Move.x -= 1f;

                if (keyboard.digit1Key.wasPressedThisFrame) input.SelectSlot = 0;
                if (keyboard.digit2Key.wasPressedThisFrame) input.SelectSlot = 1;
                if (keyboard.digit3Key.wasPressedThisFrame) input.SelectSlot = 2;
                if (keyboard.digit4Key.wasPressedThisFrame) input.SelectSlot = 3;
                if (keyboard.qKey.wasPressedThisFrame) input.CycleTool = -1;
                if (keyboard.eKey.wasPressedThisFrame) input.CycleTool = 1;
                input.AltPressed |= keyboard.rKey.wasPressedThisFrame;
                input.HighlightPressed |= keyboard.hKey.wasPressedThisFrame;
            }

            if (mouse != null && locked)
            {
                input.LookDelta += mouse.delta.ReadValue() * MouseDegreesPerPixel;
                input.UseHeld |= mouse.leftButton.isPressed;
                input.UsePressed |= mouse.leftButton.wasPressedThisFrame;

                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.01f) input.CycleTool = -1;
                else if (scroll < -0.01f) input.CycleTool = 1;
            }
        }

        static void ReadGamepad(ref PlayerInputState input, float deltaTime)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null)
                return;

            input.Move += pad.leftStick.ReadValue();

            Vector2 stick = pad.rightStick.ReadValue();
            // Square curve: precise on small movements, fast on full press.
            stick = stick.normalized * (stick.magnitude * stick.magnitude);
            input.LookDelta += stick * (StickDegreesPerSecond * deltaTime);

            input.UseHeld |= pad.rightTrigger.isPressed || pad.buttonWest.isPressed;
            input.UsePressed |= pad.rightTrigger.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame;
            if (pad.leftShoulder.wasPressedThisFrame) input.CycleTool = 1;
            // D-pad: left/right tool, up show remaining, down spray
            if (pad.dpad.left.wasPressedThisFrame) input.CycleTool = -1;
            if (pad.dpad.right.wasPressedThisFrame) input.CycleTool = 1;
            input.HighlightPressed |= pad.dpad.up.wasPressedThisFrame;
            input.AltPressed |= pad.dpad.down.wasPressedThisFrame;
            input.AltPressed |= pad.rightShoulder.wasPressedThisFrame;
            input.HighlightPressed |= pad.rightStickButton.wasPressedThisFrame;
        }
    }
}
