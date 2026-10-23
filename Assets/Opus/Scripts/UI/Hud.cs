using System.Collections.Generic;
using Opus.Core;
using Opus.Lawn;
using Opus.Player;
using Opus.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Opus.UI
{
    /// <summary>
    /// Game interface (layout from the reference): top left the garden badge + the active patch's strip (coloured pill, name,
    /// task rows), top right the area card, bottom left the toolbar, bottom centre the contextual prompt strip.
    /// F3 toggles the developer metrics.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        const float FinishedShowSeconds = 4f;
        const float BannerSeconds = 5f;
        /// <summary>Distance of the banner card from the top edge: below the top-left strip and the top-right card.</summary>
        const float BannerY = -205f;
        static readonly Color Sage = Palette.Hex("#A7B697");
        static readonly Color SlotFill = Palette.Hex("#EAE2D4");
        static readonly Color BarTrack = Palette.Hex("#E2DACB");
        static readonly Color BarFill = Palette.Hex("#8F8A70");

        sealed class TaskRow
        {
            public GameObject Root;
            public TextMeshProUGUI Label;
            public Image BarFill;
            public GameObject Bar;
            public TextMeshProUGUI Value;
        }

        sealed class Slot
        {
            public int ToolSlot;
            public Image Background;
            public RawImage Icon;
        }

        LawnTasks tasks;
        ToolBelt tools;
        RectTransform root;

        // Top left
        TextMeshProUGUI badgeCount;
        Image badgeArc;
        Image pill;
        TextMeshProUGUI patchName;
        TextMeshProUGUI highlightHint;
        GameObject checkIcon;
        TextMeshProUGUI doneLabel;
        readonly TaskRow[] rows = new TaskRow[3];

        // Top right
        TextMeshProUGUI areaTitle;
        TextMeshProUGUI areaCount;
        TextMeshProUGUI objective;
        TextMeshProUGUI dayLine;
        RectTransform patternSection;
        TextMeshProUGUI patternTitle;
        TextMeshProUGUI patternValue;
        Image patternFill;
        Image patternGoal;
        int shownDay = -1, shownPoints = -1;

        // Night (end of day) screen
        RectTransform night;
        CanvasGroup nightGroup;
        TextMeshProUGUI nightTitle;
        TextMeshProUGUI nightBody;

        // Toolbar and prompt
        TextMeshProUGUI toolTitle;
        TextMeshProUGUI toolbarHint;
        readonly List<Slot> slots = new List<Slot>();
        RectTransform promptCard;
        TextMeshProUGUI promptLabel;

        // Banner
        RectTransform banner;
        CanvasGroup bannerGroup;
        TextMeshProUGUI bannerTitle;
        TextMeshProUGUI bannerSubtitle;
        float bannerUntil;

        IReadOnlyList<LawnArea> areas;
        int shownArea = -1;
        TextMeshProUGUI devStats;
        PatchProgress shownPatch;
        PatchProgress recentlyFinished;
        float recentlyFinishedUntil;

        /// <summary>The patch shown in the strip (for tests and scenarios).</summary>
        public PatchProgress ShownPatch => shownPatch;
        public string PatchTitle => patchName != null ? patchName.text : string.Empty;
        public string ToolTitle => toolTitle != null ? toolTitle.text : string.Empty;
        public string Objective => objective != null ? objective.text : string.Empty;
        public bool DevStatsVisible => devStats != null && devStats.gameObject.activeSelf;
        public string AreaTitle => areaTitle != null ? areaTitle.text : string.Empty;
        public string BannerText => banner != null && banner.gameObject.activeSelf ? bannerTitle.text : string.Empty;
        /// <summary>The area shown on the card.</summary>
        public int ShownArea => shownArea;

        public static Hud Create(UiRoot ui, LawnTasks tasks, ToolBelt tools, IReadOnlyDictionary<int, Texture> icons, IReadOnlyList<LawnArea> areas = null)
        {
            Hud hud = ui.gameObject.AddComponent<Hud>();
            hud.tasks = tasks;
            hud.tools = tools;
            hud.areas = areas;
            hud.root = ui.Root;
            hud.BuildPatchStrip();
            hud.BuildAreaCard();
            hud.BuildToolbar(icons);
            hud.BuildPrompt();
            hud.BuildBanner();
            hud.BuildNight();
            hud.BuildDevStats();
            tasks.PatchFinished += hud.OnPatchFinished;
            tools.Changed += _ => hud.RefreshTool();
            foreach (Tool tool in tools.Tools)
                tool.DescriptionChanged += changed => { if (changed == tools.Current) hud.RefreshTool(); };
            hud.RefreshTool();
            InputReader.SchemeChanged += hud.OnSchemeChanged;
            return hud;
        }

        void OnSchemeChanged(InputScheme scheme)
        {
            highlightHint.text = $"{InputGlyphs.Highlight}: show remaining";
            toolbarHint.text = InputGlyphs.ToolbarHint;
            RefreshTool();
        }

        void OnDestroy() => InputReader.SchemeChanged -= OnSchemeChanged;

        void OnPatchFinished(PatchProgress patch)
        {
            recentlyFinished = patch;
            recentlyFinishedUntil = Time.unscaledTime + FinishedShowSeconds;
        }

        /// <summary>Removes temporary displays (the patch just finished, the banner); while loading a save.</summary>
        public void ResetTransient()
        {
            recentlyFinished = null;
            recentlyFinishedUntil = 0f;
            bannerUntil = 0f;
            banner.gameObject.SetActive(false);
            shownArea = -1;
        }

        /// <summary>Every frame: updates the active patch strip, the current area's card and the counters.</summary>
        public void Refresh(PatchProgress activePatch, int areaId)
        {
            PatchProgress patch = recentlyFinished != null && Time.unscaledTime < recentlyFinishedUntil ? recentlyFinished : activePatch;
            shownPatch = patch;
            RefreshArea(areaId);
            UpdateBanner();

            if (patch == null)
            {
                pill.color = Palette.InkSoft;
                patchName.text = "Walk to a patch";
                SetRows(null);
                return;
            }

            pill.color = patch.Color;
            patchName.text = patch.Finished ? $"{patch.DisplayName} – Done" : patch.DisplayName;
            SetRows(patch);
        }

        void RefreshArea(int areaId)
        {
            int total = tasks.TotalIn(areaId);
            int finished = tasks.FinishedIn(areaId);
            if (areaId != shownArea)
            {
                shownArea = areaId;
                areaTitle.text = $"{AreaName(areaId)}  <color=#B9B2A6>|</color>  Area {areaId}";
            }
            badgeCount.text = $"{finished}/{total}";
            badgeArc.fillAmount = total == 0 ? 0f : (float)finished / total;
            areaCount.text = $"Patches finished  <color=#4B4741>{finished}</color> / {total}";

            int next = tasks.NextArea(areaId);
            if (!tasks.IsAreaFinished(areaId))
                objective.text = tasks.IsAreaCompleted(areaId)
                    ? $"Upkeep: {total - finished} patches need work again."
                    : "Complete every patch to finish this area.";
            else if (tasks.AllFinished)
                objective.text = "The whole garden is done! The lawn is spotless.";
            else if (next != 0 && tasks.IsAreaUnlocked(next))
                objective.text = $"This area is done! The gate is open, {AreaName(next)} is waiting.";
            else
                objective.text = "This area is done!";
        }

        string AreaName(int areaId)
        {
            if (areas != null)
                foreach (LawnArea area in areas)
                    if (area.Id == areaId) return area.Name;
            return "Garden";
        }

        /// <summary>The card's day and points line (written when it changes).</summary>
        public void SetDayInfo(int day, int points)
        {
            if (day == shownDay && points == shownPoints)
                return;
            shownDay = day;
            shownPoints = points;
            dayLine.text = $"Day {day}  <color=#B9B2A6>·</color>  Points <color=#4B4741>{FormatPoints(points)}</color>";
        }

        /// <summary>Free text for the card's day line (the time on a client job); the day/points cache is reset.</summary>
        public void SetInfoLine(string text)
        {
            shownDay = -1;
            if (dayLine.text != text)
                dayLine.text = text;
        }

        public static string FormatPoints(int points) => points.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        public string DayLine => dayLine != null ? dayLine.text : string.Empty;
        public string PatternLine => patternSection != null && patternSection.gameObject.activeSelf ? patternTitle.text : string.Empty;

        /// <summary>The day's pattern section (null: hide).</summary>
        public void SetPattern(string title, float score, float goal, int reward, bool completed)
        {
            bool show = !string.IsNullOrEmpty(title);
            if (patternSection.gameObject.activeSelf != show)
                patternSection.gameObject.SetActive(show);
            if (!show)
                return;
            patternTitle.text = completed ? $"{title}  <color=#8C857B>– done!</color>" : title;
            patternFill.fillAmount = Mathf.Clamp01(score);
            patternGoal.rectTransform.anchorMin = patternGoal.rectTransform.anchorMax = new Vector2(goal, 0.5f);
            patternValue.text = completed ? $"+{reward}" : $"{Mathf.FloorToInt(score * 100f)}% / {Mathf.RoundToInt(goal * 100f)}%";
        }

        /// <summary>Content of the end-of-day screen.</summary>
        public void ShowNight(string title, string body)
        {
            nightTitle.text = title;
            nightBody.text = body;
        }

        /// <summary>Visibility of the end-of-day screen (0..1).</summary>
        public float NightAlpha
        {
            get => night != null && night.gameObject.activeSelf ? nightGroup.alpha : 0f;
            set
            {
                bool show = value > 0.001f;
                if (night.gameObject.activeSelf != show)
                    night.gameObject.SetActive(show);
                nightGroup.alpha = Mathf.Clamp01(value);
            }
        }

        public string NightText => night != null && night.gameObject.activeSelf ? nightTitle.text + "\n" + nightBody.text : string.Empty;

        void BuildNight()
        {
            night = UiFactory.Rect(root, "Night");
            night.anchorMin = Vector2.zero;
            night.anchorMax = Vector2.one;
            night.offsetMin = night.offsetMax = Vector2.zero;
            Image shade = night.gameObject.AddComponent<Image>();
            shade.color = Palette.Hex("#3E3A52");
            shade.raycastTarget = true;
            nightGroup = night.gameObject.AddComponent<CanvasGroup>();
            nightGroup.interactable = false;

            RectTransform card = UiFactory.Card(night, "Summary", 640f, new RectOffset(44, 44, 30, 30));
            UiFactory.Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero);
            card.GetComponent<VerticalLayoutGroup>().spacing = 10f;
            nightTitle = UiFactory.Text(card, "Title", string.Empty, 44f, Palette.Ink, TextAlignmentOptions.Center);
            UiFactory.Divider(card);
            nightBody = UiFactory.Text(card, "Summary", string.Empty, 23f, Palette.Ink, TextAlignmentOptions.Center);
            nightBody.lineSpacing = 8f;
            night.gameObject.SetActive(false);
        }

        /// <summary>A short banner at the top centre of the screen (a new area unlocked etc.).</summary>
        public void ShowBanner(string title, string subtitle)
        {
            bannerTitle.text = title;
            bannerSubtitle.text = subtitle;
            bannerSubtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            bannerUntil = Time.unscaledTime + BannerSeconds;
            banner.gameObject.SetActive(true);
            UpdateBanner();
        }

        void UpdateBanner()
        {
            if (!banner.gameObject.activeSelf)
                return;
            float left = bannerUntil - Time.unscaledTime;
            if (left <= 0f)
            {
                banner.gameObject.SetActive(false);
                return;
            }
            // Slides down as it appears, fades at the end
            float shown = BannerSeconds - left;
            float appear = Mathf.Clamp01(shown / 0.35f);
            float eased = 1f - (1f - appear) * (1f - appear);
            bannerGroup.alpha = Mathf.Clamp01(left / 0.6f) * eased;
            banner.anchoredPosition = new Vector2(0f, BannerY + (1f - eased) * 24f);
        }

        void SetRows(PatchProgress patch)
        {
            bool finished = patch != null && patch.Finished;
            checkIcon.SetActive(finished);
            highlightHint.gameObject.SetActive(!finished);
            doneLabel.gameObject.SetActive(finished);

            int used = 0;
            if (patch != null && !finished)
            {
                if (!patch.MowDone)
                    SetBarRow(rows[used++], "Mow", patch.MowFraction);
                if (!patch.WeedsDone)
                    SetCountRow(rows[used++], "Weeds", $"{patch.WeedsLeft} left");
                if (!patch.WaterDone)
                    SetBarRow(rows[used++], "Water", patch.WaterFraction);
            }
            for (int i = 0; i < rows.Length; i++)
                rows[i].Root.SetActive(i < used);
        }

        static void SetBarRow(TaskRow row, string label, float fraction)
        {
            row.Label.text = label;
            row.Bar.SetActive(true);
            row.BarFill.fillAmount = Mathf.Clamp01(fraction);
            // Values like 97.6% mown must not round to "98" and look finished
            row.Value.text = $"{Mathf.FloorToInt(fraction * 100f)}%";
        }

        static void SetCountRow(TaskRow row, string label, string value)
        {
            row.Label.text = label;
            row.Bar.SetActive(false);
            row.Value.text = value;
        }

        void RefreshTool()
        {
            Tool current = tools.Current;
            toolTitle.text = current != null ? current.DisplayName : string.Empty;
            foreach (Slot slot in slots)
            {
                bool selected = current != null && current.Slot == slot.ToolSlot;
                slot.Background.color = selected ? Sage : SlotFill;
                slot.Icon.color = selected ? new Color(0.24f, 0.25f, 0.2f, 1f) : new Color(1f, 1f, 1f, 0.55f);
            }
            string prompt = current != null ? current.Prompt : string.Empty;
            promptLabel.text = prompt;
            promptCard.gameObject.SetActive(!string.IsNullOrEmpty(prompt));
        }

        public void ShowDevStats(string text)
        {
            if (devStats.gameObject.activeSelf)
                devStats.text = text;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
                devStats.gameObject.SetActive(!devStats.gameObject.activeSelf);
        }

        // ---------- Building ----------

        void BuildPatchStrip()
        {
            const float badgeSize = 108f;
            var origin = new Vector2(30f, -26f);

            RectTransform strip = UiFactory.Card(root, "PatchStrip", 640f, new RectOffset(72, 22, 12, 12), tape: false);
            UiFactory.Anchor(strip, new Vector2(0f, 1f), new Vector2(0f, 1f), origin + new Vector2(badgeSize * 0.5f, -badgeSize * 0.5f + 40f));
            var layout = strip.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;

            // Row 1: pill, name, a hint or the done tick on the right
            RectTransform header = Row(strip, "Title", 34f);
            RectTransform pillRect = UiFactory.Rect(header, "Pill");
            pill = pillRect.gameObject.AddComponent<Image>();
            pill.sprite = UiSprites.RoundedRect(7);
            pill.type = Image.Type.Sliced;
            Fixed(pillRect, 46f, 15f);
            patchName = UiFactory.Text(header, "Name", "???", 27f, Palette.Ink, TextAlignmentOptions.Left);
            Flexible(patchName.rectTransform);
            highlightHint = UiFactory.Text(header, "Hint", $"{InputGlyphs.Highlight}: show remaining", 17f, Palette.InkSoft, TextAlignmentOptions.Right);
            Fixed(highlightHint.rectTransform, 170f, 30f);
            checkIcon = CheckIcon(header).gameObject;

            for (int i = 0; i < rows.Length; i++)
                rows[i] = TaskRowUi(strip, i);
            doneLabel = UiFactory.Text(strip, "OK", "All done", 20f, Palette.Ink);
            doneLabel.gameObject.SetActive(false);

            // Badge: sits over the left end of the strip
            RectTransform badge = UiFactory.Rect(root, "Badge");
            UiFactory.Anchor(badge, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), origin + new Vector2(badgeSize * 0.5f, -badgeSize * 0.5f));
            badge.sizeDelta = Vector2.one * badgeSize;
            Disc(badge, "Shadow", UiSprites.Circle(), badgeSize + 12f, new Color(0.25f, 0.2f, 0.3f, 0.16f), new Vector2(0f, -4f));
            Disc(badge, "Ground", UiSprites.Circle(), badgeSize, Palette.Paper, Vector2.zero);
            Disc(badge, "Track", UiSprites.Ring(0.05f), badgeSize - 12f, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.1f), Vector2.zero);
            badgeArc = Disc(badge, "Arc", UiSprites.Ring(0.05f), badgeSize - 12f, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.75f), Vector2.zero);
            badgeArc.type = Image.Type.Filled;
            badgeArc.fillMethod = Image.FillMethod.Radial360;
            badgeArc.fillOrigin = (int)Image.Origin360.Top;
            badgeArc.fillClockwise = true;
            TextMeshProUGUI title = UiFactory.Text(badge, "Label", "Garden", 16f, Palette.InkSoft, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0f, 20f), new Vector2(100f, 22f));
            badgeCount = UiFactory.Text(badge, "Counter", "0/0", 30f, Palette.Ink, TextAlignmentOptions.Center);
            Place(badgeCount.rectTransform, new Vector2(0f, -8f), new Vector2(100f, 36f));
        }

        TaskRow TaskRowUi(RectTransform parent, int index)
        {
            RectTransform row = Row(parent, $"Task{index}", 26f);
            var task = new TaskRow { Root = row.gameObject };
            task.Label = UiFactory.Text(row, "Label", "Mow", 20f, Palette.Ink);
            Fixed(task.Label.rectTransform, 64f, 26f);

            RectTransform bar = UiFactory.Rect(row, "Bar");
            Flexible(bar);
            bar.GetComponent<LayoutElement>().preferredHeight = 26f;
            task.Bar = bar.gameObject;
            Image track = BarImage(bar, "Track", BarTrack);
            Image fill = BarImage(bar, "Fill", BarFill);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            task.BarFill = fill;
            track.raycastTarget = false;

            task.Value = UiFactory.Text(row, "Value", "0%", 18f, Palette.InkSoft, TextAlignmentOptions.Right);
            Fixed(task.Value.rectTransform, 90f, 26f);
            return task;
        }

        static Image BarImage(RectTransform bar, string name, Color color)
        {
            RectTransform rect = UiFactory.Rect(bar, name);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(-12f, 9f);
            rect.anchoredPosition = new Vector2(0f, 0f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.RoundedRect(4);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        void BuildAreaCard()
        {
            RectTransform card = UiFactory.Card(root, "AreaCard", 450f, new RectOffset(26, 70, 20, 16));
            UiFactory.Anchor(card, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -26f));
            areaTitle = UiFactory.Text(card, "Title", string.Empty, 31f, Palette.Ink);
            UiFactory.Divider(card);
            areaCount = UiFactory.Text(card, "Counter", "Patches finished 0 / 0", 20f, Palette.InkSoft);
            objective = UiFactory.Text(card, "Target", string.Empty, 16f, Palette.InkSoft);
            dayLine = UiFactory.Text(card, "Day", string.Empty, 18f, Palette.Ink);

            // The day's pattern (if any)
            patternSection = UiFactory.Rect(card, "Pattern");
            var sectionLayout = patternSection.gameObject.AddComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 2f;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;
            UiFactory.Divider(patternSection);
            patternTitle = UiFactory.Text(patternSection, "Title", string.Empty, 18f, Palette.Ink);
            RectTransform bar = Row(patternSection, "Bar", 22f);
            RectTransform track = UiFactory.Rect(bar, "Track");
            Flexible(track);
            track.GetComponent<LayoutElement>().preferredHeight = 22f;
            BarImage(track, "Ground", BarTrack);
            patternFill = BarImage(track, "Fill", BarFill);
            patternFill.type = Image.Type.Filled;
            patternFill.fillMethod = Image.FillMethod.Horizontal;
            patternGoal = BarImage(track, "Target", Palette.PatchOutline);
            patternGoal.rectTransform.sizeDelta = new Vector2(3f, 14f);
            patternGoal.rectTransform.anchorMin = patternGoal.rectTransform.anchorMax = new Vector2(0.8f, 0.5f);
            patternValue = UiFactory.Text(bar, "Value", string.Empty, 16f, Palette.InkSoft, TextAlignmentOptions.Right);
            Fixed(patternValue.rectTransform, 110f, 22f);
            patternSection.gameObject.SetActive(false);
            Sprout(card);
        }

        void BuildToolbar(IReadOnlyDictionary<int, Texture> icons)
        {
            RectTransform card = UiFactory.Card(root, "Toolbar", 410f, new RectOffset(18, 18, 18, 12));
            UiFactory.Anchor(card, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(28f, 28f));
            toolTitle = UiFactory.Text(card, "Name", string.Empty, 24f, Palette.Ink, TextAlignmentOptions.Center);

            RectTransform row = Row(card, "Slots", 86f);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            foreach (Tool tool in tools.Tools)
            {
                RectTransform slotRect = UiFactory.Rect(row, "Slot");
                Fixed(slotRect, 84f, 84f);
                Image background = slotRect.gameObject.AddComponent<Image>();
                background.sprite = UiSprites.RoundedRect(12);
                background.type = Image.Type.Sliced;
                background.raycastTarget = false;
                RectTransform iconRect = UiFactory.Rect(slotRect, "Icon");
                Place(iconRect, Vector2.zero, new Vector2(76f, 76f));
                RawImage icon = iconRect.gameObject.AddComponent<RawImage>();
                icon.texture = icons != null && icons.TryGetValue(tool.Slot, out Texture texture) ? texture : null;
                icon.raycastTarget = false;
                slots.Add(new Slot { ToolSlot = tool.Slot, Background = background, Icon = icon });
            }
            toolbarHint = UiFactory.Text(card, "Hint", InputGlyphs.ToolbarHint, 16f, Palette.InkSoft, TextAlignmentOptions.Center);
        }

        void BuildPrompt()
        {
            promptCard = UiFactory.Card(root, "PromptStrip", 640f, new RectOffset(26, 26, 9, 9), tape: false);
            UiFactory.Anchor(promptCard, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f));
            promptLabel = UiFactory.Text(promptCard, "Prompt", string.Empty, 21f, Palette.Ink, TextAlignmentOptions.Center);
            promptCard.gameObject.SetActive(false);
        }

        void BuildBanner()
        {
            banner = UiFactory.Card(root, "Banner", 560f, new RectOffset(30, 30, 18, 16));
            UiFactory.Anchor(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, BannerY));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;
            bannerTitle = UiFactory.Text(banner, "Title", string.Empty, 32f, Palette.Ink, TextAlignmentOptions.Center);
            bannerSubtitle = UiFactory.Text(banner, "Bottom", string.Empty, 19f, Palette.InkSoft, TextAlignmentOptions.Center);
            banner.gameObject.SetActive(false);
        }

        void BuildDevStats()
        {
            devStats = UiFactory.Text(root, "Metrics", string.Empty, 16f, Palette.InkSoft, TextAlignmentOptions.Top);
            devStats.rectTransform.anchorMin = devStats.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            devStats.rectTransform.pivot = new Vector2(0.5f, 1f);
            devStats.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            devStats.rectTransform.sizeDelta = new Vector2(700f, 50f);
            devStats.gameObject.SetActive(false);
        }

        // ---------- Helpers ----------

        static RectTransform Row(Transform parent, string name, float height)
        {
            RectTransform row = UiFactory.Rect(parent, name);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return row;
        }

        static void Fixed(RectTransform rect, float width, float height)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = width;
            element.preferredHeight = element.minHeight = height;
            element.flexibleWidth = 0f;
        }

        static void Flexible(RectTransform rect)
        {
            LayoutElement element = rect.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            element.minWidth = 40f;
        }

        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static Image Disc(RectTransform parent, string name, Sprite sprite, float size, Color color, Vector2 offset)
        {
            RectTransform rect = UiFactory.Rect(parent, name);
            Place(rect, offset, Vector2.one * size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A white tick inside a dark circle (two rotated bars).</summary>
        static RectTransform CheckIcon(RectTransform parent)
        {
            RectTransform rect = UiFactory.Rect(parent, "Confirm");
            Fixed(rect, 30f, 30f);
            Image circle = rect.gameObject.AddComponent<Image>();
            circle.sprite = UiSprites.Circle();
            circle.color = Palette.InkSoft;
            circle.raycastTarget = false;
            Stroke(rect, new Vector2(-4.5f, -1.5f), 9f, 45f);
            Stroke(rect, new Vector2(3f, 1.5f), 15f, -50f);
            rect.gameObject.SetActive(false);
            return rect;
        }

        static void Stroke(RectTransform parent, Vector2 position, float length, float angle)
        {
            RectTransform bar = UiFactory.Rect(parent, "Line");
            Place(bar, position, new Vector2(length, 3.5f));
            bar.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image image = bar.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.RoundedRect(2);
            image.type = Image.Type.Sliced;
            image.color = Palette.Paper;
            image.raycastTarget = false;
        }

        /// <summary>A small sprout on the right of the card (stem + leaves), like the plant icon in the reference.</summary>
        static void Sprout(RectTransform card)
        {
            RectTransform icon = UiFactory.Rect(card, "Sprout");
            icon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            icon.anchorMin = icon.anchorMax = new Vector2(1f, 0.5f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = new Vector2(-36f, -10f);
            icon.sizeDelta = new Vector2(40f, 60f);
            Color ink = new Color(Palette.InkSoft.r, Palette.InkSoft.g, Palette.InkSoft.b, 0.9f);
            Leaf(icon, new Vector2(0f, -4f), new Vector2(3f, 44f), 0f, ink);
            Leaf(icon, new Vector2(-8f, 2f), new Vector2(9f, 20f), 40f, ink);
            Leaf(icon, new Vector2(8f, 8f), new Vector2(9f, 20f), -40f, ink);
            Leaf(icon, new Vector2(-7f, 15f), new Vector2(7f, 15f), 35f, ink);
            Leaf(icon, new Vector2(4f, 20f), new Vector2(7f, 14f), -25f, ink);
        }

        static void Leaf(RectTransform parent, Vector2 position, Vector2 size, float angle, Color color)
        {
            RectTransform rect = UiFactory.Rect(parent, "Leaf");
            Place(rect, position, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Circle();
            image.color = color;
            image.raycastTarget = false;
        }
    }
}
