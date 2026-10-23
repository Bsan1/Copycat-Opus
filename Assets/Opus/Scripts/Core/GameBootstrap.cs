using Opus.Audio;
using Opus.Lawn;
using Opus.Player;
using Opus.Tools;
using Opus.UI;
using Opus.Visual;
using Opus.World;
using UnityEngine;

namespace Opus.Core
{
    /// <summary>
    /// The only object in the scene. Builds the garden, the lawn, the player, the tools, the light and the interface from code;
    /// starts the automated scenario if -scenario is on the command line.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        public LaunchArgs Args { get; private set; }
        public PlayerController Player { get; private set; }
        public LawnState Lawn { get; private set; }
        public GrassField Grass { get; private set; }
        public Clippings Clippings { get; private set; }
        public WeedField Weeds { get; private set; }
        public UiRoot Ui { get; private set; }
        public YardSession Session { get; private set; }
        public GameMenu Menu { get; private set; }
        public AudioDirector Audio { get; private set; }
        public GamepadRumble Rumble { get; private set; }

        void Awake()
        {
            Instance = this;
            Args = LaunchArgs.FromEnvironment();
            var buildWatch = System.Diagnostics.Stopwatch.StartNew();
            // When the garden changes the scene reloads: the career says which garden to build; pausing must not carry over
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Career.Traveling = false;
            YardBuilder.Use(Career.CurrentYard());
            SaveSystem.Slot = Career.CurrentSlot;
            Debug.Log($"[Opus] Starting. Unity {Application.unityVersion}, screen {Screen.width}x{Screen.height}, scenario: {(Args.HasScenario ? Args.Scenario : "none")}, " +
                      $"garden: {YardBuilder.Definition.Id}");

            // The player's capsule must not collide with the tool in their own hand.
            Physics.IgnoreLayerCollision(Layers.Player, Layers.Tool);

            Atmosphere.Apply();
            PostFx.Create(transform);
            long t0 = buildWatch.ElapsedMilliseconds;
            PaintedBackdrop.Build(transform, YardBuilder.YardRect);
            long tBackdrop = buildWatch.ElapsedMilliseconds;
            Transform yard = YardBuilder.Build(transform);
            long tYard = buildWatch.ElapsedMilliseconds;
            BuildLawn(yard);
            long tLawn = buildWatch.ElapsedMilliseconds;

            Player = PlayerController.Create(YardBuilder.SpawnFeet, YardBuilder.SpawnYaw);
            Ui = UiRoot.Create(Player.UiCamera);

            ToolBelt tools = Player.Tools;
            Clippings = Clippings.Create(transform);
            MowerTool mower = MowerTool.Create(Player);
            mower.Lawn = Lawn;
            mower.Clippings = Clippings;
            TrimmerTool trimmer = TrimmerTool.Create(Player);
            trimmer.Lawn = Lawn;
            trimmer.Clippings = Clippings;
            trimmer.Weeds = Weeds;
            HoseTool hose = HoseTool.Create(Player);
            hose.Lawn = Lawn;
            hose.Splashes = Clippings;
            tools.Add(mower);
            tools.Add(trimmer);
            tools.Add(hose);
            tools.Add(HandTool.Create(Player));
            tools.EquipImmediate(Tool.MowerSlot, Player);

            Grass.ViewCamera = Player.WorldCamera;
            foreach (IGrassPusher pusher in new IGrassPusher[] { mower, trimmer, hose, Player })
            {
                Grass.AddPusher(pusher);
                Weeds.AddPusher(pusher);
            }

            long tTools = buildWatch.ElapsedMilliseconds;
            Session = YardSession.Create(transform, Player, Lawn, Weeds, Grass, Ui);
            long tSession = buildWatch.ElapsedMilliseconds;
            Menu = GameMenu.Create(Ui, Session);
            // The other area's grass and flowers are only visible through the gate opening
            Grass.IsHidden = YardVisibility.IsHidden;
            Session.Flowers.ViewCamera = Player.WorldCamera;
            Session.Flowers.IsHidden = YardVisibility.IsHidden;
            Audio = AudioDirector.Create(transform, Player, Session, Weeds, Menu);
            // Automated runs make no sound through the speakers (the mix is still measured and can be recorded)
            Audio.Tap.MuteOutput = Args.HasScenario;
            Rumble = GamepadRumble.Create(Player);
            Session.Tasks.PatchFinished += _ => Rumble.Pulse(0.35f, 0.2f, 0.18f);
            Session.ItemFound += (_, _) => Rumble.Pulse(0.2f, 0.4f, 0.22f);
            long tAudio = buildWatch.ElapsedMilliseconds;
            SetUpSaving();
            Session.PlayArrival();

            buildWatch.Stop();
            if (Career.TransitionStartedAt >= 0f)
            {
                Career.LastBuildSeconds = Time.realtimeSinceStartup - Career.TransitionStartedAt;
                Career.TransitionStartedAt = -1f;
                Debug.Log($"[Opus] Garden changed: {YardBuilder.Definition.Id}, built in {buildWatch.ElapsedMilliseconds} ms (with loading {Career.LastBuildSeconds * 1000f:0} ms; " +
                          $"scenery {tBackdrop - t0}, props {tYard - tBackdrop}, lawn {tLawn - tYard}, player and tools {tTools - tLawn}, session {tSession - tTools}, " +
                          $"menu and audio {tAudio - tSession}, save {buildWatch.ElapsedMilliseconds - tAudio} ms)");
            }

            // The scenario runner survives garden changes (when the scene reloads) and attaches to the new player
            if (Args.HasScenario)
            {
                if (ScenarioRunner.Instance == null)
                {
                    var runner = new GameObject("ScenarioRunner");
                    DontDestroyOnLoad(runner);
                    runner.AddComponent<ScenarioRunner>().Begin(Args, Player);
                }
                else
                {
                    ScenarioRunner.Instance.Rebind(Player);
                }
            }
        }

        /// <summary>
        /// Save folder and settings. Scenarios do not touch the user's save and settings: a temporary folder,
        /// a clean start and autosave off. In normal play the game continues from the last save if there is one.
        /// </summary>
        /// <remarks>
        /// On first launch: the save folder, deleting old saves in a scenario, continuing from the home save in normal play. After a garden
        /// change: if that garden's slot has a save (coming home or an unfinished job) continue from there; then
        /// the profile carried between gardens (day, points, upgrades, collection) is written.
        /// </remarks>
        void SetUpSaving()
        {
            bool firstBoot = !Career.Booted;
            Career.Booted = true;
            if (firstBoot)
            {
                if (!string.IsNullOrEmpty(Args.SaveDirectory))
                    SaveSystem.Directory = Args.SaveDirectory;
                else if (Args.HasScenario)
                    SaveSystem.Directory = System.IO.Path.Combine(Application.temporaryCachePath, "ScenarioRecord");
                if (Args.HasScenario)
                {
                    SaveSystem.DeleteSlot(SaveSystem.HomeSlot);
                    foreach (JobDef job in Jobs.All)
                        SaveSystem.DeleteSlot(job.Id);
                }
            }

            GameSettings.Persistent = !Args.HasScenario;
            GameSettings.Load(Grass);
            Session.AutosaveEnabled = !Args.HasScenario;
            bool resume = firstBoot ? !Args.HasScenario && SaveSystem.Exists : SaveSystem.Exists;
            if (resume && !Session.Load(out string error))
                Debug.LogWarning($"[Opus] Could not continue from the save: {error}");
            Career.ApplyProfile(Session);
        }

        void BuildLawn(Transform yard)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            PatchLayout layout = YardBuilder.GenerateLayout();
            long msLayout = watch.ElapsedMilliseconds;
            Lawn = new LawnState(layout, GrassVariety.All);
            long msState = watch.ElapsedMilliseconds;
            int cleared = Lawn.ClearUnderColliders(yard.GetComponentsInChildren<Collider>());
            long msClear = watch.ElapsedMilliseconds;
            Lawn.Apply();

            Shader groundShader = Shader.Find("Opus/LawnGround");
            if (groundShader != null)
                YardBuilder.LawnGround.sharedMaterial = new Material(groundShader) { name = "LawnGround" };
            else
                Debug.LogError("[Opus] Opus/LawnGround shader not found.");

            Grass = GrassField.Create(Lawn, transform);
            Weeds = WeedField.Create(transform, YardBuilder.WeedSpots, layout);
            Debug.Log($"[Opus] Lawn: {layout.Grid.Width}x{layout.Grid.Height} cells, {layout.PatchCount} patches, " +
                      $"{cleared} cells under obstacles, {Grass.TotalChunks} draw chunks, " +
                      $"{Weeds.TotalWeeds} weeds, {Weeds.TotalOvergrowth} ivy, {watch.ElapsedMilliseconds} ms (layout {msLayout}, state {msState - msLayout}, obstacles {msClear - msState})");
        }
    }
}
