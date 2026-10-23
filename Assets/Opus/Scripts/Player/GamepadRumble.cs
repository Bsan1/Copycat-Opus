using Opus.Core;
using Opus.Tools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Opus.Player
{
    /// <summary>
    /// Gamepad rumble: the mower engine (rising with load), the spinning trimmer and the spraying water give a constant light rumble;
    /// finishing a patch and finding an item a short pulse. Only active when the last input was the gamepad, while the game runs and the setting is on.
    /// </summary>
    public sealed class GamepadRumble : MonoBehaviour
    {
        PlayerController player;
        MowerTool mower;
        TrimmerTool trimmer;
        HoseTool hose;
        float pulseLow, pulseHigh, pulseUntil;
        bool active;

        /// <summary>Motor speeds requested in the last frame (for tests and scenarios).</summary>
        public Vector2 Motors { get; private set; }

        public static GamepadRumble Create(PlayerController player)
        {
            var rumble = player.gameObject.AddComponent<GamepadRumble>();
            rumble.player = player;
            rumble.mower = player.Tools.FindSlot(Tool.MowerSlot) as MowerTool;
            rumble.trimmer = player.Tools.FindSlot(Tool.TrimmerSlot) as TrimmerTool;
            rumble.hose = player.Tools.FindSlot(Tool.HoseSlot) as HoseTool;
            return rumble;
        }

        /// <summary>Short pulse (low motor, high motor, duration).</summary>
        public void Pulse(float low, float high, float seconds)
        {
            pulseLow = low;
            pulseHigh = high;
            pulseUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Motor speeds by the tools' current work (0..1).</summary>
        public Vector2 Compute()
        {
            float low = 0f, high = 0f;
            Tool current = player.Tools.Current;
            if (current == mower && mower != null && mower.Presence >= 1f)
            {
                low += 0.05f + 0.16f * mower.CutLoad;
                high += 0.02f + 0.08f * mower.CutLoad;
            }
            else if (current == trimmer && trimmer != null && trimmer.IsSpinning)
            {
                low += 0.03f;
                high += trimmer.LastCutCells > 0 ? 0.24f : 0.14f;
            }
            else if (current == hose && hose != null)
            {
                high += 0.06f * hose.SprayLevel;
            }
            if (Time.unscaledTime < pulseUntil)
            {
                low = Mathf.Max(low, pulseLow);
                high = Mathf.Max(high, pulseHigh);
            }
            return new Vector2(Mathf.Clamp01(low), Mathf.Clamp01(high));
        }

        void Update()
        {
            Gamepad pad = Gamepad.current;
            bool allowed = pad != null && GameSettings.Rumble && Time.timeScale > 0f && InputReader.LastScheme == InputScheme.Gamepad
                           && !ScenarioRunner.IsRunning;
            Motors = allowed ? Compute() : Vector2.zero;
            if (pad == null)
                return;
            if (Motors == Vector2.zero)
            {
                if (active)
                {
                    pad.SetMotorSpeeds(0f, 0f);
                    active = false;
                }
                return;
            }
            pad.SetMotorSpeeds(Motors.x, Motors.y);
            active = true;
        }

        void OnDisable() => Stop();
        void OnApplicationQuit() => Stop();

        void OnApplicationFocus(bool focus)
        {
            if (!focus)
                Stop();
        }

        void Stop()
        {
            Gamepad.current?.ResetHaptics();
            active = false;
        }
    }
}
