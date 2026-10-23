using System;
using System.Collections.Generic;
using Opus.Core;
using Opus.Lawn;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Opus.UI
{
    /// <summary>
    /// Break menu (Esc / Start on the gamepad): the game stops and the cursor is released. Pages: main menu (continue, save,
    /// load, settings, new game, quit), settings and the new game confirmation. A paper card like the interface cards; navigated with the
    /// mouse and the gamepad (D-pad + A). Opening it autosaves.
    /// </summary>
    public sealed class GameMenu : MonoBehaviour
    {
        public enum Page { Main, Settings, ConfirmNew, Workshop, Collection, JobBoard, JobResult, ConfirmLeave }

        static readonly Color StarOn = Palette.Hex("#E2A83A");
        static readonly Color StarOff = Palette.Hex("#DCD3C4");

        static readonly Color ButtonFill = Palette.Hex("#EAE2D4");
        static readonly Color ButtonHover = Palette.Hex("#D9E0CB");
        static readonly Color ButtonPressed = Palette.Hex("#A7B697");
        static readonly Color Dim = new Color(0.22f, 0.18f, 0.28f, 0.42f);

        YardSession session;
        RectTransform root;
        RectTransform card;
        TextMeshProUGUI title;
        TextMeshProUGUI status;
        readonly Dictionary<Page, RectTransform> pages = new Dictionary<Page, RectTransform>();
        readonly Dictionary<Page, Selectable> firstSelectable = new Dictionary<Page, Selectable>();
        Button loadButton;
        // Buttons on the main page shown at home or on a job
        readonly List<Button> homeOnly = new List<Button>();
        readonly List<Button> jobOnly = new List<Button>();
        TextMeshProUGUI sensitivityValue;
        TextMeshProUGUI invertValue;
        TextMeshProUGUI densityValue;
        TextMeshProUGUI fullscreenValue;
        TextMeshProUGUI vsyncValue;
        TextMeshProUGUI rumbleValue;
        TextMeshProUGUI masterValue;
        TextMeshProUGUI effectsValue;
        TextMeshProUGUI ambientValue;
        float previousTimeScale = 1f;

        public bool IsOpen { get; private set; }
        public Page CurrentPage { get; private set; }
        public string StatusText => status != null ? status.text : string.Empty;

        /// <summary>The menu opened / closed / a button was pressed (for the sounds).</summary>
        public event Action Opened;
        public event Action Closed;
        public event Action Clicked;
        /// <summary>An upgrade was bought in the workshop.</summary>
        public event Action<UpgradeId> Purchased;

        public static GameMenu Create(UiRoot ui, YardSession session)
        {
            if (ui.GetComponent<GraphicRaycaster>() == null)
                ui.gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            GameMenu menu = ui.gameObject.AddComponent<GameMenu>();
            menu.session = session;
            menu.Build(ui.Root);
            // When a client garden is finished the delivery card opens by itself
            session.JobFinished += menu.ShowJobResult;
            return menu;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // ---------- Open / close ----------

        public void Open()
        {
            if (IsOpen)
                return;
            IsOpen = true;
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            if (!ScenarioRunner.IsRunning)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (session.AutosaveEnabled)
                session.Save();
            ShowPage(Page.Main);
            SetStatus(SaveSummary());
            Opened?.Invoke();
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            Time.timeScale = previousTimeScale;
            root.gameObject.SetActive(false);
            if (!ScenarioRunner.IsRunning)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            EventSystem.current?.SetSelectedGameObject(null);
            Closed?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void ShowPage(Page page)
        {
            CurrentPage = page;
            foreach (KeyValuePair<Page, RectTransform> entry in pages)
                entry.Value.gameObject.SetActive(entry.Key == page);
            title.text = page switch
            {
                Page.Settings => "Settings",
                Page.ConfirmNew => "Start a new game?",
                Page.Workshop => "Workshop",
                Page.Collection => "Collection",
                Page.JobBoard => "Job board",
                Page.JobResult => session.Tasks.AllFinished ? "Job done!" : "Deliver the job?",
                Page.ConfirmLeave => "Leave the job?",
                _ => session.Job != null ? session.Job.Title : "Paused",
            };
            if (page == Page.Main)
            {
                bool job = session.Job != null;
                foreach (Button button in homeOnly) button.gameObject.SetActive(!job);
                foreach (Button button in jobOnly) button.gameObject.SetActive(job);
                loadButton.interactable = SaveSystem.Exists;
                SetStatus(job ? $"{session.Job.Client}  ·  time {Jobs.FormatTime(session.PlaySeconds)} / {session.Job.ParMinutes:0}:00" : SaveSummary());
            }
            else if (page == Page.JobBoard)
            {
                RefreshJobBoard();
            }
            else if (page == Page.JobResult)
            {
                RefreshJobResult();
            }
            else if (page == Page.ConfirmLeave)
            {
                SetStatus("The work done is saved; payment on delivery. The day ends when you get home.");
            }
            else if (page == Page.ConfirmNew)
            {
                SetStatus("All progress is deleted and the garden starts over.");
            }
            else if (page == Page.Workshop)
            {
                RefreshWorkshop();
            }
            else if (page == Page.Collection)
            {
                RefreshCollection();
            }
            else
            {
                SetStatus(string.Empty);
                RefreshSettings();
            }
            if (firstSelectable.TryGetValue(page, out Selectable first) && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        }

        void Update()
        {
            bool toggle = false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                toggle = true;
            Gamepad pad = Gamepad.current;
            if (pad != null && (pad.startButton.wasPressedThisFrame || (IsOpen && pad.buttonEast.wasPressedThisFrame)))
                toggle = true;
            if (!toggle || session.IsNight || Career.Traveling)
                return;
            // Go back to the main menu before leaving a sub page
            if (IsOpen && CurrentPage != Page.Main)
                ShowPage(Page.Main);
            else
                Toggle();
        }

        // ---------- Actions ----------

        void OnSave() => SetStatus(session.Save() ? $"Saved  ·  {session.LastSaveTime}" : "Could not save (disk error).");

        void OnLoad()
        {
            bool loaded = session.Load(out string error);
            SetStatus(loaded ? "Save loaded." : error);
        }

        void OnNewGame()
        {
            session.NewGame();
            ShowPage(Page.Main);
            Close();
        }

        void OnQuit()
        {
            if (session.AutosaveEnabled)
                session.Save();
            Time.timeScale = 1f;
            Application.Quit();
        }

        string SaveSummary() => string.IsNullOrEmpty(session.LastSaveTime) ? (SaveSystem.Exists ? "Save found" : "No save yet") : $"Last save  {session.LastSaveTime}";

        void SetStatus(string text)
        {
            status.text = text;
            status.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        void RefreshSettings()
        {
            sensitivityValue.text = GameSettings.LookSensitivity.ToString("0.0");
            invertValue.text = OnOff(GameSettings.InvertY);
            densityValue.text = GameSettings.GrassDensityNames[GameSettings.GrassDensity];
            fullscreenValue.text = OnOff(GameSettings.Fullscreen);
            vsyncValue.text = OnOff(GameSettings.VSync);
            rumbleValue.text = OnOff(GameSettings.Rumble);
            masterValue.text = Percent(GameSettings.MasterVolume);
            effectsValue.text = Percent(GameSettings.EffectsVolume);
            ambientValue.text = Percent(GameSettings.AmbientVolume);
        }

        static string OnOff(bool value) => value ? "On" : "Off";
        static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        // ---------- Building ----------

        void Build(RectTransform canvas)
        {
            root = UiFactory.Rect(canvas, "BreakMenu");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            Image dim = root.gameObject.AddComponent<Image>();
            dim.color = Dim;
            dim.raycastTarget = true;

            card = UiFactory.Card(root, "BreakCard", 560f, new RectOffset(40, 40, 30, 30));
            UiFactory.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            card.GetComponent<VerticalLayoutGroup>().spacing = 8f;
            title = UiFactory.Text(card, "Title", "Paused", 44f, Palette.Ink, TextAlignmentOptions.Center);
            UiFactory.Divider(card);
            status = UiFactory.Text(card, "Status", string.Empty, 19f, Palette.InkSoft, TextAlignmentOptions.Center);

            RectTransform main = PageRoot(Page.Main);
            firstSelectable[Page.Main] = MakeButton(main, "Continue", Close);
            homeOnly.Add(MakeButton(main, "End the day", OnEndDay));
            homeOnly.Add(MakeButton(main, "Job board", () => ShowPage(Page.JobBoard)));
            jobOnly.Add(MakeButton(main, "Deliver the job", () => ShowPage(Page.JobResult)));
            jobOnly.Add(MakeButton(main, "Leave the job, continue later", () => ShowPage(Page.ConfirmLeave)));
            homeOnly.Add(MakeButton(main, "Workshop", () => ShowPage(Page.Workshop)));
            homeOnly.Add(MakeButton(main, "Collection", () => ShowPage(Page.Collection)));
            homeOnly.Add(MakeButton(main, "Save", OnSave));
            homeOnly.Add(loadButton = MakeButton(main, "Load", OnLoad));
            MakeButton(main, "Settings", () => ShowPage(Page.Settings));
            homeOnly.Add(MakeButton(main, "New game", () => ShowPage(Page.ConfirmNew)));
            MakeButton(main, "Quit", OnQuit);
            UiFactory.Text(main, "Hint", "Esc: continue  ·  F5 quick save  ·  F9 quick load", 16f, Palette.InkSoft, TextAlignmentOptions.Center);

            RectTransform settings = PageRoot(Page.Settings);
            firstSelectable[Page.Settings] = Stepper(settings, "Look sensitivity", out sensitivityValue,
                () => GameSettings.SetLookSensitivity(Mathf.Round((GameSettings.LookSensitivity - 0.1f) * 10f) / 10f),
                () => GameSettings.SetLookSensitivity(Mathf.Round((GameSettings.LookSensitivity + 0.1f) * 10f) / 10f));
            Switch(settings, "Invert vertical look", out invertValue, () => GameSettings.SetInvertY(!GameSettings.InvertY));
            Stepper(settings, "Grass density", out densityValue,
                () => GameSettings.SetGrassDensity(GameSettings.GrassDensity - 1),
                () => GameSettings.SetGrassDensity(GameSettings.GrassDensity + 1));
            Switch(settings, "Fullscreen", out fullscreenValue, () => GameSettings.SetFullscreen(!GameSettings.Fullscreen));
            Switch(settings, "Vertical sync", out vsyncValue, () => GameSettings.SetVSync(!GameSettings.VSync));
            Switch(settings, "Gamepad rumble", out rumbleValue, () => GameSettings.SetRumble(!GameSettings.Rumble));
            Stepper(settings, "Master volume", out masterValue,
                () => GameSettings.SetMasterVolume(GameSettings.MasterVolume - 0.1f), () => GameSettings.SetMasterVolume(GameSettings.MasterVolume + 0.1f));
            Stepper(settings, "Effects", out effectsValue,
                () => GameSettings.SetEffectsVolume(GameSettings.EffectsVolume - 0.1f), () => GameSettings.SetEffectsVolume(GameSettings.EffectsVolume + 0.1f));
            Stepper(settings, "Ambience", out ambientValue,
                () => GameSettings.SetAmbientVolume(GameSettings.AmbientVolume - 0.1f), () => GameSettings.SetAmbientVolume(GameSettings.AmbientVolume + 0.1f));
            MakeButton(settings, "Back", () => ShowPage(Page.Main));

            RectTransform confirm = PageRoot(Page.ConfirmNew);
            firstSelectable[Page.ConfirmNew] = MakeButton(confirm, "Cancel", () => ShowPage(Page.Main));
            MakeButton(confirm, "Yes, start over", OnNewGame);

            BuildWorkshop();
            BuildCollection();
            BuildJobBoard();
            BuildJobResult();
            RectTransform leave = PageRoot(Page.ConfirmLeave);
            firstSelectable[Page.ConfirmLeave] = MakeButton(leave, "Cancel", () => ShowPage(Page.Main));
            MakeButton(leave, "Yes, go home", LeaveJob);
            root.gameObject.SetActive(false);
        }

        // ---------- Jobs ----------

        sealed class JobRow
        {
            public JobDef Job;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Detail;
            public TextMeshProUGUI Record;
            public Button Accept;
            public TextMeshProUGUI AcceptLabel;
        }

        readonly List<JobRow> jobRows = new List<JobRow>();
        readonly List<Image> resultStars = new List<Image>();
        TextMeshProUGUI resultText;
        Button deliverButton;
        Button keepWorkingButton;

        /// <summary>Delivery card (the game stops): opens by itself when all patches are finished, otherwise from the menu.</summary>
        public void ShowJobResult()
        {
            if (session.Job == null)
                return;
            Open();
            ShowPage(Page.JobResult);
        }

        /// <summary>Opens the job board (at home).</summary>
        public void OpenJobBoard()
        {
            if (session.Job != null)
                return;
            Open();
            ShowPage(Page.JobBoard);
        }

        /// <summary>Accepts the job: home is saved and you travel to the client's garden.</summary>
        public void AcceptJob(JobDef job)
        {
            if (session.Job != null || session.IsNight)
                return;
            HideForTravel();
            Career.GoToJob(session, job);
        }

        /// <summary>Delivers the job: back home with stars and payment.</summary>
        public void DeliverJob()
        {
            if (session.Job == null)
                return;
            JobResult result = session.EvaluateJob(delivered: true);
            HideForTravel();
            Career.ReturnHome(session, result);
        }

        /// <summary>Leaves the job half done: the state is saved to its own slot and you go home without payment.</summary>
        public void LeaveJob()
        {
            if (session.Job == null)
                return;
            JobResult result = session.EvaluateJob(delivered: false);
            HideForTravel();
            Career.ReturnHome(session, result);
        }

        /// <summary>The transition card must not stay under the menu: the menu closes and the game stays paused.</summary>
        void HideForTravel()
        {
            IsOpen = false;
            root.gameObject.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(null);
            Closed?.Invoke();
        }

        void BuildJobBoard()
        {
            RectTransform page = PageRoot(Page.JobBoard);
            foreach (JobDef job in Jobs.All)
            {
                RectTransform row = UiFactory.Rect(page, "Job");
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 12f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                row.gameObject.AddComponent<LayoutElement>().minHeight = 92f;

                RectTransform texts = UiFactory.Rect(row, "Text");
                var textLayout = texts.gameObject.AddComponent<VerticalLayoutGroup>();
                textLayout.childControlWidth = true;
                textLayout.childControlHeight = true;
                textLayout.childForceExpandWidth = true;
                textLayout.childForceExpandHeight = false;
                var textElement = texts.gameObject.AddComponent<LayoutElement>();
                textElement.flexibleWidth = 1f;
                textElement.minWidth = 260f;
                var entry = new JobRow
                {
                    Job = job,
                    Name = UiFactory.Text(texts, "Name", $"{job.Title}  <color=#8C857B>· {job.Client}</color>", 23f, Palette.Ink),
                    Detail = UiFactory.Text(texts, "Description", job.Summary, 15f, Palette.InkSoft),
                    Record = UiFactory.Text(texts, "Record", string.Empty, 15f, Palette.InkSoft),
                };
                JobDef chosen = job;
                entry.Accept = MakeButton(row, "Accept", () => AcceptJob(chosen), 48f, 150f);
                entry.AcceptLabel = entry.Accept.GetComponentInChildren<TextMeshProUGUI>();
                jobRows.Add(entry);
                if (!firstSelectable.ContainsKey(Page.JobBoard))
                    firstSelectable[Page.JobBoard] = entry.Accept;
            }
            MakeButton(page, "Back", () => ShowPage(Page.Main));
        }

        void RefreshJobBoard()
        {
            SetStatus("A job takes a day; when you get back it is night at home.");
            foreach (JobRow row in jobRows)
            {
                JobRecord record = Career.RecordOf(row.Job.Id);
                bool resume = SaveSystem.SlotExists(row.Job.Id);
                string pattern = row.Job.Pattern.HasValue ? "  ·  wants a pattern" : string.Empty;
                row.Record.text = $"Target {row.Job.ParMinutes:0} min  ·  pay {Hud.FormatPoints(row.Job.PayByStars[1])}–{Hud.FormatPoints(row.Job.MaxPay)}{pattern}" +
                                  (record.delivered > 0 ? $"  ·  best {record.bestStars}/3 stars" : string.Empty);
                row.AcceptLabel.text = resume ? "Continue" : "Accept";
                row.Accept.interactable = !session.IsNight;
            }
        }

        void BuildJobResult()
        {
            RectTransform page = PageRoot(Page.JobResult);
            RectTransform stars = UiFactory.Rect(page, "Stars");
            var layout = stars.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            stars.gameObject.AddComponent<LayoutElement>().minHeight = 76f;
            for (int i = 0; i < 3; i++)
            {
                RectTransform star = UiFactory.Rect(stars, "Star");
                star.sizeDelta = new Vector2(70f, 70f);
                Image image = star.gameObject.AddComponent<Image>();
                image.sprite = UiSprites.Star();
                image.color = StarOff;
                resultStars.Add(image);
            }
            resultText = UiFactory.Text(page, "Result", string.Empty, 21f, Palette.Ink, TextAlignmentOptions.Center);
            firstSelectable[Page.JobResult] = deliverButton = MakeButton(page, "Deliver and go home", DeliverJob);
            keepWorkingButton = MakeButton(page, "Keep working", () => ShowPage(Page.Main));
        }

        void RefreshJobResult()
        {
            JobResult result = session.EvaluateJob(delivered: true);
            JobDef job = session.Job;
            for (int i = 0; i < resultStars.Count; i++)
                resultStars[i].color = i < result.Stars ? StarOn : StarOff;
            string pattern = job.Pattern.HasValue ? $"\nRequested pattern: {(result.PatternDone ? "done" : "missing")}" : string.Empty;
            resultText.text = $"Completion {Mathf.FloorToInt(result.Completion * 100f)}%  ·  time {Jobs.FormatTime(result.Seconds)} (target {job.ParMinutes:0}:00){pattern}\n" +
                              $"Payment: <b>{Hud.FormatPoints(result.Payment)}</b> points";
            bool finished = session.Tasks.AllFinished;
            keepWorkingButton.gameObject.SetActive(!finished);
            SetStatus(result.Stars < 3
                ? finished ? "For the third star: the target time" + (job.Pattern.HasValue ? " and the requested pattern" : string.Empty) + "." : "Finish every patch for two stars."
                : "The client is delighted!");
        }

        // ---------- Workshop and collection ----------

        sealed class UpgradeRow
        {
            public UpgradeId Id;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Detail;
            public Button Buy;
            public TextMeshProUGUI BuyLabel;
        }

        readonly List<UpgradeRow> upgradeRows = new List<UpgradeRow>();
        readonly List<TextMeshProUGUI> collectionCells = new List<TextMeshProUGUI>();

        void BuildWorkshop()
        {
            RectTransform page = PageRoot(Page.Workshop);
            foreach (UpgradeDef def in Upgrades.All)
            {
                RectTransform row = UiFactory.Rect(page, "Upgrade");
                var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 12f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                row.gameObject.AddComponent<LayoutElement>().minHeight = 62f;

                RectTransform texts = UiFactory.Rect(row, "Text");
                var textLayout = texts.gameObject.AddComponent<VerticalLayoutGroup>();
                textLayout.childControlWidth = true;
                textLayout.childControlHeight = true;
                textLayout.childForceExpandWidth = true;
                textLayout.childForceExpandHeight = false;
                var textElement = texts.gameObject.AddComponent<LayoutElement>();
                textElement.flexibleWidth = 1f;
                textElement.minWidth = 200f;
                var entry = new UpgradeRow
                {
                    Id = def.Id,
                    Name = UiFactory.Text(texts, "Name", def.Name, 23f, Palette.Ink),
                    Detail = UiFactory.Text(texts, "Description", string.Empty, 16f, Palette.InkSoft),
                };
                UpgradeId id = def.Id;
                entry.Buy = MakeButton(row, string.Empty, () => OnBuy(id), 48f, 150f);
                entry.BuyLabel = entry.Buy.GetComponentInChildren<TextMeshProUGUI>();
                upgradeRows.Add(entry);
                if (!firstSelectable.ContainsKey(Page.Workshop))
                    firstSelectable[Page.Workshop] = entry.Buy;
            }
            MakeButton(page, "Back", () => ShowPage(Page.Main));
        }

        void RefreshWorkshop()
        {
            SetStatus($"Your points  {Hud.FormatPoints(session.Wallet.Points)}");
            foreach (UpgradeRow row in upgradeRows)
            {
                UpgradeDef def = Upgrades.Def(row.Id);
                int level = session.Upgrades.Level(row.Id);
                bool maxed = session.Upgrades.IsMaxed(row.Id);
                row.Name.text = def.MaxLevel > 1 ? $"{def.Name}  <color=#8C857B>{level}/{def.MaxLevel}</color>" : def.Name;
                row.Detail.text = maxed ? (level > 0 ? def.LevelTexts[level - 1] : string.Empty) + "  (top level)" : def.LevelTexts[level];
                int cost = session.Upgrades.NextCost(row.Id);
                row.BuyLabel.text = maxed ? "Bought" : $"Buy · {Hud.FormatPoints(cost)}";
                row.Buy.interactable = !maxed && session.Wallet.Points >= cost;
            }
        }

        void OnBuy(UpgradeId id)
        {
            if (session.Upgrades.TryBuy(id, session.Wallet))
            {
                Purchased?.Invoke(id);
                RefreshWorkshop();
                UpgradeDef def = Upgrades.Def(id);
                SetStatus($"Bought: {def.Name}  ·  Your points {Hud.FormatPoints(session.Wallet.Points)}");
            }
        }

        void BuildCollection()
        {
            RectTransform page = PageRoot(Page.Collection);
            RectTransform grid = UiFactory.Rect(page, "Grill");
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(232f, 36f);
            layout.spacing = new Vector2(8f, 4f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            int rows = (LostItems.Catalog.Length + 1) / 2;
            grid.gameObject.AddComponent<LayoutElement>().minHeight = rows * 40f;
            foreach (FindDef def in LostItems.Catalog)
                collectionCells.Add(UiFactory.Text(grid, "Item", "???", 21f, Palette.InkSoft));
            firstSelectable[Page.Collection] = MakeButton(page, "Back", () => ShowPage(Page.Main));
        }

        void RefreshCollection()
        {
            int found = session.Finds.Collected.Count;
            SetStatus($"Found  {found} / {LostItems.Catalog.Length}  ·  Watch for items sparkling in the grass");
            for (int i = 0; i < LostItems.Catalog.Length; i++)
            {
                FindDef def = LostItems.Catalog[i];
                bool has = ContainsId(session.Finds.Collected, def.Id);
                collectionCells[i].text = has ? def.Name : "???";
                collectionCells[i].color = has ? Palette.Ink : Palette.InkSoft;
            }
        }

        static bool ContainsId(IReadOnlyCollection<string> ids, string id)
        {
            foreach (string value in ids)
                if (value == id) return true;
            return false;
        }

        void OnEndDay()
        {
            Close();
            session.BeginEndDay();
        }

        RectTransform PageRoot(Page page)
        {
            RectTransform rect = UiFactory.Rect(card, "Page" + page);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(0, 0, 6, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            pages[page] = rect;
            return rect;
        }

        Button MakeButton(Transform parent, string label, Action onClick, float height = 54f, float width = -1f)
        {
            RectTransform rect = UiFactory.Rect(parent, "Button");
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = height;
            if (width > 0f)
            {
                element.preferredWidth = element.minWidth = width;
                element.flexibleWidth = 0f;
            }
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.RoundedRect(12);
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonFill;
            colors.highlightedColor = ButtonHover;
            colors.selectedColor = ButtonHover;
            colors.pressedColor = ButtonPressed;
            colors.disabledColor = new Color(ButtonFill.r, ButtonFill.g, ButtonFill.b, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                Clicked?.Invoke();
                onClick();
            });

            TextMeshProUGUI text = UiFactory.Text(rect, "Label", label, 27f, Palette.Ink, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return button;
        }

        RectTransform SettingRow(Transform parent, string label)
        {
            RectTransform row = UiFactory.Rect(parent, "Setting");
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = 50f;
            TextMeshProUGUI text = UiFactory.Text(row, "Label", label, 23f, Palette.Ink, TextAlignmentOptions.Left);
            var textElement = text.gameObject.AddComponent<LayoutElement>();
            textElement.flexibleWidth = 1f;
            textElement.minWidth = 120f;
            return row;
        }

        TextMeshProUGUI ValueLabel(RectTransform row, float width)
        {
            TextMeshProUGUI value = UiFactory.Text(row, "Value", string.Empty, 23f, Palette.Ink, TextAlignmentOptions.Center);
            var element = value.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = width;
            element.flexibleWidth = 0f;
            return value;
        }

        /// <summary>"Label   [-] value [+]" row.</summary>
        Selectable Stepper(Transform parent, string label, out TextMeshProUGUI value, Action minus, Action plus)
        {
            RectTransform row = SettingRow(parent, label);
            Button down = MakeButton(row, "-", () => { minus(); RefreshSettings(); }, 46f, 50f);
            value = ValueLabel(row, 92f);
            MakeButton(row, "+", () => { plus(); RefreshSettings(); }, 46f, 50f);
            return down;
        }

        /// <summary>"Label   [On]" row; the button toggles the value.</summary>
        Selectable Switch(Transform parent, string label, out TextMeshProUGUI value, Action toggle)
        {
            RectTransform row = SettingRow(parent, label);
            Button button = MakeButton(row, string.Empty, () => { toggle(); RefreshSettings(); }, 46f, 160f);
            value = button.GetComponentInChildren<TextMeshProUGUI>();
            return button;
        }
    }
}
