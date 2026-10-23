using Opus.Lawn;
using Opus.Player;
using UnityEngine;

namespace Opus.Core
{
    /// <summary>
    /// Player settings (PlayerPrefs): look sensitivity, vertical look inversion, grass density, fullscreen,
    /// vertical sync and volumes (master, effects, ambience). Changes apply instantly. Not persistent in scenarios (they play with the defaults
    /// and do not touch the user's settings).
    /// </summary>
    public static class GameSettings
    {
        public const float MinSensitivity = 0.3f;
        public const float MaxSensitivity = 2.5f;
        public static readonly string[] GrassDensityNames = { "Low", "Medium", "High" };
        public static readonly float[] GrassDensityValues = { 0.45f, 0.7f, 1f };

        const string Prefix = "opus.";

        public static float LookSensitivity { get; private set; } = 1f;
        public static bool InvertY { get; private set; }
        public static int GrassDensity { get; private set; } = 2;
        public static bool Fullscreen { get; private set; } = true;
        public static bool VSync { get; private set; } = true;
        /// <summary>Volumes 0..1 (in steps of a tenth).</summary>
        public static float MasterVolume { get; private set; } = 1f;
        public static float EffectsVolume { get; private set; } = 1f;
        public static float AmbientVolume { get; private set; } = 0.8f;
        /// <summary>Gamepad rumble.</summary>
        public static bool Rumble { get; private set; } = true;
        /// <summary>While false (scenarios) the settings are neither read nor written.</summary>
        public static bool Persistent { get; set; } = true;

        static GrassField grass;

        /// <summary>Reads and applies the saved settings.</summary>
        public static void Load(GrassField grassField)
        {
            grass = grassField;
            if (Persistent)
            {
                LookSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + "sensitivity", 1f), MinSensitivity, MaxSensitivity);
                InvertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) != 0;
                GrassDensity = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "grassDensity", 2), 0, GrassDensityValues.Length - 1);
                Fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", Screen.fullScreen ? 1 : 0) != 0;
                VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) != 0;
                MasterVolume = Volume(PlayerPrefs.GetFloat(Prefix + "masterVolume", 1f));
                EffectsVolume = Volume(PlayerPrefs.GetFloat(Prefix + "effectsVolume", 1f));
                AmbientVolume = Volume(PlayerPrefs.GetFloat(Prefix + "ambientVolume", 0.8f));
                Rumble = PlayerPrefs.GetInt(Prefix + "rumble", 1) != 0;
            }
            Apply(applyScreen: Persistent);
        }

        public static void SetLookSensitivity(float value)
        {
            LookSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
            Save();
        }

        public static void SetInvertY(bool value)
        {
            InvertY = value;
            Save();
        }

        public static void SetGrassDensity(int index)
        {
            GrassDensity = Mathf.Clamp(index, 0, GrassDensityValues.Length - 1);
            Save();
        }

        public static void SetFullscreen(bool value)
        {
            Fullscreen = value;
            Save();
            if (Persistent)
                Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        public static void SetVSync(bool value)
        {
            VSync = value;
            Save();
        }

        /// <summary>Rounds to the 0..1 range and to steps of a tenth.</summary>
        public static float Volume(float value) => Mathf.Round(Mathf.Clamp01(value) * 10f) / 10f;

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Volume(value);
            Save();
        }

        public static void SetEffectsVolume(float value)
        {
            EffectsVolume = Volume(value);
            Save();
        }

        public static void SetRumble(bool value)
        {
            Rumble = value;
            Save();
        }

        public static void SetAmbientVolume(float value)
        {
            AmbientVolume = Volume(value);
            Save();
        }

        static void Save()
        {
            Apply(applyScreen: false);
            if (!Persistent)
                return;
            PlayerPrefs.SetFloat(Prefix + "sensitivity", LookSensitivity);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "grassDensity", GrassDensity);
            PlayerPrefs.SetInt(Prefix + "fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "masterVolume", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "effectsVolume", EffectsVolume);
            PlayerPrefs.SetFloat(Prefix + "ambientVolume", AmbientVolume);
            PlayerPrefs.SetInt(Prefix + "rumble", Rumble ? 1 : 0);
            PlayerPrefs.Save();
        }

        static void Apply(bool applyScreen)
        {
            InputReader.LookSensitivity = LookSensitivity;
            InputReader.InvertY = InvertY;
            AudioListener.volume = MasterVolume;
            if (grass != null)
                grass.Density = GrassDensityValues[GrassDensity];
            // Scenarios run without vsync for the measurements; the settings do not touch them.
            if (Persistent)
            {
                QualitySettings.vSyncCount = VSync ? 1 : 0;
                if (applyScreen && Screen.fullScreen != Fullscreen)
                    Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            }
        }
    }
}
