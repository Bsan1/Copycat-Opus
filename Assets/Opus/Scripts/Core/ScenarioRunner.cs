using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Opus.Player;
using UnityEngine;

namespace Opus.Core
{
    /// <summary>
    /// Plays a scenario: the scenario script positions the player, simulates input, runs checks and
    /// asks for screenshots. For a screenshot the world camera (with the interface camera in its stack) is drawn into a
    /// RenderTexture. The results are written to result.json and the game quits.
    /// It needs the normal game loop: in batchmode Unity does not render cameras or uGUI canvases.
    /// tools/Shots.ps1 does this off-screen by embedding the window in an off-screen parent window (-parentHWND).
    /// </summary>
    public sealed class ScenarioRunner : MonoBehaviour
    {
        public static bool IsRunning { get; private set; }
        /// <summary>The running runner (not destroyed with the scene on a garden change).</summary>
        public static ScenarioRunner Instance { get; private set; }

        [Serializable]
        sealed class ShotRecord
        {
            public string name;
            public string file;
            public float luminanceDeviation;
            public bool blank;
        }

        [Serializable]
        sealed class CheckRecord
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        sealed class Report
        {
            public string scenario;
            public bool passed;
            public string unityVersion;
            public string graphicsDevice;
            public int screenWidth;
            public int screenHeight;
            public float averageFrameMs;
            public float averageCpuMainMs;
            public float averageCpuRenderMs;
            public float averageGpuMs;
            public float averageBatches;
            public List<ShotRecord> shots = new List<ShotRecord>();
            public List<CheckRecord> checks = new List<CheckRecord>();
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        const int TimingFrames = 120;

        readonly Report report = new Report();
        LaunchArgs args;
        PlayerController player;
        RenderTexture target;
        string outputDirectory;
        Hash128 previousShotHash;

        public void Begin(LaunchArgs launchArgs, PlayerController playerController)
        {
            Instance = this;
            args = launchArgs;
            player = playerController;
            IsRunning = true;
            Application.logMessageReceived += OnLog;
            StartCoroutine(Run());
        }

        internal PlayerController CurrentPlayer => player;

        /// <summary>The garden changed: the new player's camera draws into the screenshot target.</summary>
        public void Rebind(PlayerController playerController)
        {
            player = playerController;
            if (target != null)
                player.WorldCamera.targetTexture = target;
            Debug.Log($"[Opus] Scenario attached to the new garden: {Opus.World.YardBuilder.Definition.Id}");
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            InputReader.StopSimulation();
            IsRunning = false;
            if (Instance == this)
                Instance = null;
        }

        void OnLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add($"{type}: {message}");
            else if (type == LogType.Warning)
                report.warnings.Add(message);
        }

        IEnumerator Run()
        {
            report.scenario = args.Scenario;
            report.unityVersion = Application.unityVersion;
            report.graphicsDevice = $"{SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType})";
            outputDirectory = string.IsNullOrEmpty(args.ShotsDirectory)
                ? Path.Combine(Application.persistentDataPath, "Shots", args.Scenario)
                : args.ShotsDirectory;
            Directory.CreateDirectory(outputDirectory);

            if (Application.isBatchMode)
            {
                report.errors.Add("Scenarios do not run in batchmode (cameras and the interface are not rendered); use tools/Shots.ps1.");
                Finish();
                yield break;
            }
            if (!Scenarios.TryGet(args.Scenario, out ScenarioScript script))
            {
                report.errors.Add($"Unknown scenario: {args.Scenario}. Available: {string.Join(", ", Scenarios.Names)}");
                Finish();
                yield break;
            }

            // The frame time measurement must not be capped by the screen refresh.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            InputReader.Simulate(PlayerInputState.None);

            target = new RenderTexture(args.ShotWidth, args.ShotHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "ScenarioTarget"
            };
            target.Create();
            player.WorldCamera.targetTexture = target;

            // The interface camera's canvas scales with the window size; if the sizes differ the layout looks broken.
            report.screenWidth = Screen.width;
            report.screenHeight = Screen.height;
            if (Screen.width != target.width || Screen.height != target.height)
                report.warnings.Add($"Window {Screen.width}x{Screen.height}, image {target.width}x{target.height}; the interface scale may differ.");

            yield return null;
            yield return script(new ScenarioContext(this));

            InputReader.Simulate(PlayerInputState.None);
            using (var probe = new PerfProbe())
            {
                for (int i = 0; i < TimingFrames; i++)
                {
                    yield return null;
                    probe.Sample();
                }
                PerfStats stats = probe.Result;
                report.averageFrameMs = stats.FrameMs;
                report.averageCpuMainMs = stats.CpuMainMs;
                report.averageCpuRenderMs = stats.CpuRenderMs;
                report.averageGpuMs = stats.GpuMs;
                report.averageBatches = stats.Batches;
            }

            Finish();
        }

        /// <summary>Pixels of the last screenshot (for comparison checks).</summary>
        internal Color32[] LastShotPixels { get; private set; }
        internal string OutputDirectory => outputDirectory;

        internal void Capture(string shotName, bool allowSame = false)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply(false);
            RenderTexture.active = previous;

            Color32[] pixels = texture.GetPixels32();
            float deviation = ShotCheck.LuminanceDeviation(pixels);
            Hash128 hash = Hash128.Compute(pixels);
            LastShotPixels = pixels;
            if (report.shots.Count > 0 && hash == previousShotHash && !allowSame)
                report.errors.Add($"'{shotName}' is pixel for pixel identical to the previous one; the camera may not be rendering.");
            previousShotHash = hash;

            string file = Path.Combine(outputDirectory, shotName + ".png");
            File.WriteAllBytes(file, texture.EncodeToPNG());
            Destroy(texture);

            bool blank = deviation < ShotCheck.MinLuminanceDeviation;
            if (blank)
                report.errors.Add($"Empty screenshot: {shotName}");
            Debug.Log($"[Opus] Screenshot saved: {file}");
            report.shots.Add(new ShotRecord { name = shotName, file = file, luminanceDeviation = deviation, blank = blank });
        }

        internal void AddCheck(string name, bool passed, string detail)
        {
            report.checks.Add(new CheckRecord { name = name, passed = passed, detail = detail });
            Debug.Log($"[Opus] Check {(passed ? "PASSED" : "FAILED")}: {name} — {detail}");
            if (!passed)
                report.errors.Add($"Check failed: {name} ({detail})");
        }

        void Finish()
        {
            report.passed = report.errors.Count == 0 && (report.shots.Count > 0 || report.checks.Count > 0);
            File.WriteAllText(Path.Combine(outputDirectory, "result.json"), JsonUtility.ToJson(report, true));
            Debug.Log($"[Opus] Scenario '{args.Scenario}' finished: {(report.passed ? "PASSED" : "FAILED")}");

            InputReader.StopSimulation();
            if (player != null)
                player.WorldCamera.targetTexture = null;
            if (target != null)
                target.Release();

            Application.Quit(report.passed ? 0 : 1);
        }
    }

    /// <summary>Steps used by the scenario scripts.</summary>
    public sealed class ScenarioContext
    {
        readonly ScenarioRunner runner;

        /// <summary>The current player (rebuilt with the scene when the garden changes).</summary>
        public PlayerController Player => runner.CurrentPlayer;

        internal ScenarioContext(ScenarioRunner runner)
        {
            this.runner = runner;

        }

        public IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
                yield return null;
        }

        /// <summary>Waits in game time; in real time if the game is paused (break menu).</summary>
        public IEnumerator Seconds(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime)
                yield return null;
        }

        /// <summary>Positions the player, releases the input and waits for the scene to settle.</summary>
        public IEnumerator Pose(Vector3 feet, float yaw, float pitch, int settleFrames = 10)
        {
            InputReader.Simulate(PlayerInputState.None);
            EndFreeCamera();
            Player.Teleport(feet, yaw, pitch);
            yield return Frames(settleFrames);
        }

        /// <summary>Switches tools without animation.</summary>
        public void Equip(int slot) => Player.Tools.EquipImmediate(slot, Player);

        /// <summary>
        /// Detaches the camera from the player and points it at the target from <paramref name="eye"/> (for close-ups below
        /// eye height); the player stands still meanwhile. <see cref="EndFreeCamera"/> or the next <see cref="Pose"/> reattaches it.
        /// </summary>
        public IEnumerator FreeCamera(Vector3 eye, Vector3 target, float fieldOfView, int settleFrames = 10)
        {
            InputReader.Simulate(PlayerInputState.None);
            Player.enabled = false;
            Camera camera = Player.WorldCamera;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
            camera.fieldOfView = fieldOfView;
            yield return Frames(settleFrames);
        }

        public void EndFreeCamera() => Player.enabled = true;

        /// <summary>
        /// Holds the input for the given time. In this call <see cref="PlayerInputState.LookDelta"/> is read as degrees/second
        /// (so it does not depend on the frame rate); one-shot presses are only sent on the first frame.
        /// </summary>
        public IEnumerator Hold(PlayerInputState input, float seconds, Action<float> eachFrame = null)
        {
            Vector2 lookPerSecond = input.LookDelta;
            PlayerInputState state = input;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                state.LookDelta = lookPerSecond * Time.deltaTime;
                InputReader.Simulate(state);
                state.UsePressed = false;
                state.SelectSlot = -1;
                state.CycleTool = 0;
                state.AltPressed = false;
                state.HighlightPressed = false;
                yield return null;
                eachFrame?.Invoke(Time.deltaTime);
            }
            InputReader.Simulate(PlayerInputState.None);
        }

        /// <summary>Sets the input indefinitely (so movement continues while taking a screenshot); <see cref="Pose"/> and <see cref="Hold"/> reset it.</summary>
        public void SetInput(PlayerInputState input) => InputReader.Simulate(input);

        /// <summary>Measures the average frame time (ms); vSync is off throughout the scenario.</summary>
        public IEnumerator MeasureFrames(int frames, Action<float> result) => MeasurePerf(frames, stats => result(stats.FrameMs));

        /// <summary>Measures the frame time with the CPU/GPU split and draw counts; also writes the result to the log.</summary>
        public IEnumerator MeasurePerf(int frames, Action<PerfStats> result)
        {
            using var probe = new PerfProbe();
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                probe.Sample();
            }
            PerfStats stats = probe.Result;
            Debug.Log($"[Opus] Measurement: {stats}");
            result(stats);
        }

        /// <param name="allowSame">A screenshot expected to equal the previous one (e.g. an occlusion on/off comparison).</param>
        public IEnumerator Shot(string name, int settleFrames = 4, bool allowSame = false)
        {
            yield return Frames(settleFrames);
            yield return new WaitForEndOfFrame();
            runner.Capture(name, allowSame);
        }

        public void Check(string name, bool passed, string detail) => runner.AddCheck(name, passed, detail);

        public Color32[] LastShotPixels => runner.LastShotPixels;
        /// <summary>Folder where the screenshots and the report are written.</summary>
        public string OutputDirectory => runner.OutputDirectory;

        /// <param name="lookPerSecond">Degrees/second within Hold.</param>
        public static PlayerInputState Input(Vector2 move = default, Vector2 lookPerSecond = default, int selectSlot = -1, bool use = false, bool alt = false,
            bool highlight = false)
        {
            PlayerInputState state = PlayerInputState.None;
            state.HighlightPressed = highlight;
            state.Move = move;
            state.LookDelta = lookPerSecond;
            state.SelectSlot = selectSlot;
            state.UseHeld = use;
            state.UsePressed = use;
            state.AltPressed = alt;
            return state;
        }
    }
}
