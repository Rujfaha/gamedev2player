using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The main menu (uGUI built in code, like RaceHUD, styled by MenuUI) over the live 3D background that GameBootstrap sets up in the Menu scene.
/// Flow: Main → Mode (1 / 2 players) → Racers (total trucks) → Colors (each human picks a truck colour) → Map → race scene.
/// Main also leads to How to Play and Settings. Esc / gamepad B goes back one screen.
/// Navigation: arrow keys / WASD / gamepad (uGUI Selectable navigation), mouse clicks; the colour screen has its own per-player keys.
/// Choices are written to MatchSettings, options to GameSettings, all text comes from Loc.
/// </summary>
public class MainMenu : MonoBehaviour
{
    enum View { Main, Mode, Racers, Colors, Map, HowTo, Settings }

    class ColorSlot
    {
        public Text header, colorName;
        public Image[] frames;
        public Button[] swatches;
        public Button readyButton;
    }

    GemDriftConfig cfg;
    Transform canvas;
    RectTransform content, hintBar;
    CanvasGroup contentGroup;
    Image dim, fadeLeft;
    AudioSource music;
    View view;
    int shownFrame;
    float shownTime;
    readonly List<Selectable> items = new List<Selectable>();
    GameObject lastSelected;

    // colour screen
    readonly int[] pick = { 0, 1 };
    readonly bool[] ready = new bool[2];
    ColorSlot[] slots;
    GameObject stage;
    Transform[] pivots;
    Renderer[][] truckRenderers;

    static Color InkA(float a) => new Color(MenuUI.Ink.r, MenuUI.Ink.g, MenuUI.Ink.b, a);

    void Awake()
    {
        cfg = GemDriftConfig.Load();
        MenuUI.EnsureEventSystem();
        canvas = MenuUI.CreateCanvas(transform, "MainMenu", 10);

        // screen-edge fades instead of a flat dim: the course stays visible in the middle
        dim = MenuUI.Fill(MenuUI.Stretch(canvas, "Dim", Vector2.zero, Vector2.one), MenuUI.Clear);
        fadeLeft = MenuUI.Gradient(MenuUI.Stretch(canvas, "FadeLeft", Vector2.zero, new Vector2(0.68f, 1f)), InkA(0.93f), false, true);
        MenuUI.Gradient(MenuUI.Stretch(canvas, "FadeTop", new Vector2(0f, 0.66f), Vector2.one), InkA(0.85f), true, false);
        MenuUI.Gradient(MenuUI.Stretch(canvas, "FadeBottom", Vector2.zero, new Vector2(1f, 0.4f)), InkA(0.92f), true, true);
        hintBar = MenuUI.Group(canvas, "Hints", false, 44, TextAnchor.MiddleCenter);
        MenuUI.Place(hintBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), Vector2.zero);
        MenuUI.FitToContent(hintBar, true);

        // background music: drop an AudioClip named MenuMusic into any Resources folder (the volume follows Settings)
        var clip = Resources.Load<AudioClip>("MenuMusic");
        if (clip != null)
        {
            music = gameObject.AddComponent<AudioSource>();
            music.clip = clip;
            music.loop = true;
            music.Play();
        }
        Show(View.Main);
    }

    void Update()
    {
        if (music != null) music.volume = GameSettings.MusicVolume;

        // screens slide in and fade up
        float e = Mathf.Clamp01((Time.unscaledTime - shownTime) / 0.3f);
        e = 1f - (1f - e) * (1f - e) * (1f - e);
        contentGroup.alpha = e;
        content.anchoredPosition = new Vector2(40f * (1f - e), 0f);

        var kb = Keyboard.current;
        var gp = Gamepad.current;
        bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);
        if (back && view != View.Main)
        {
            MenuUI.Blip(440f);
            Back();
            return;
        }

        // a click on empty space deselects the button; keep one selected so the keyboard / gamepad still work
        var es = EventSystem.current;
        if (es != null)
        {
            if (es.currentSelectedGameObject != null) lastSelected = es.currentSelectedGameObject;
            else if (lastSelected != null) es.SetSelectedGameObject(lastSelected);
        }

        if (view == View.Colors)
        {
            if (Time.frameCount > shownFrame) UpdateColors(); // not on the frame a button press brought us here
            foreach (var p in pivots) p.Rotate(0f, 30f * Time.deltaTime, 0f);
        }
    }

    // ---------- Screen switching ----------

    void Show(View v, int select = 0)
    {
        if (view == View.Settings && v != View.Settings) GameSettings.Save();
        view = v;
        shownFrame = Time.frameCount;
        shownTime = Time.unscaledTime;
        MenuUI.quietUntil = Time.unscaledTime + 0.15f;
        if (content != null)
        {
            content.gameObject.SetActive(false);
            Destroy(content.gameObject);
        }
        if (stage != null) Destroy(stage);
        content = MenuUI.Stretch(canvas, "Screen", Vector2.zero, Vector2.one);
        contentGroup = content.gameObject.AddComponent<CanvasGroup>();
        items.Clear();

        fadeLeft.gameObject.SetActive(v == View.Main);
        dim.color = v == View.Main || v == View.Colors ? MenuUI.Clear : InkA(0.5f);
        BuildHints(v);
        switch (v)
        {
            case View.Main: BuildMain(); break;
            case View.Mode: BuildMode(); break;
            case View.Racers: BuildRacers(); break;
            case View.Colors: BuildColors(); break;
            case View.Map: BuildMap(); break;
            case View.HowTo: BuildHowTo(); break;
            case View.Settings: BuildSettings(); break;
        }

        lastSelected = items.Count > 0 ? items[Mathf.Clamp(select, 0, items.Count - 1)].gameObject : null;
        EventSystem.current.SetSelectedGameObject(lastSelected);
    }

    void Back()
    {
        switch (view)
        {
            case View.Racers: Show(View.Mode); break;
            case View.Colors: Show(View.Racers); break;
            case View.Map: Show(View.Colors); break;
            case View.Mode:
            case View.HowTo:
            case View.Settings: Show(View.Main); break;
        }
    }

    // ---------- Shared pieces ----------

    /// <summary>Key prompts along the bottom of the screen.</summary>
    void BuildHints(View v)
    {
        foreach (Transform child in hintBar)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        void Hint(string label, params string[] keys)
        {
            var group = MenuUI.Group(hintBar, "Hint", false, 12, TextAnchor.MiddleCenter);
            var caps = MenuUI.Group(group, "Keys", false, 6, TextAnchor.MiddleCenter);
            foreach (var k in keys) MenuUI.Key(caps, k, 20);
            MenuUI.Label(group, label, 24, TextAnchor.MiddleLeft, MenuUI.Muted);
        }

        if (v == View.Colors)
        {
            string choose = Loc.T("hint_choose"), go = Loc.T("hint_ready");
            if (MatchSettings.playerCount == 1) { Hint(choose, "A", "D"); Hint(go, "SPACE"); }
            else
            {
                Hint("P1 " + choose, "A", "D"); Hint("P1 " + go, "SPACE");
                Hint("P2 " + choose, Loc.T("key_left"), Loc.T("key_right")); Hint("P2 " + go, "ENTER");
            }
        }
        else
        {
            Hint(Loc.T("hint_move"), "W", "S");
            if (v == View.Racers || v == View.Settings) Hint(Loc.T("hint_adjust"), "A", "D");
            Hint(Loc.T("hint_select"), "ENTER");
        }
        if (v != View.Main) Hint(Loc.T("hint_back"), "ESC");
    }

    static void PlaceTopLeft(RectTransform rt, Vector2 pos, Vector2 size) =>
        MenuUI.Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, size);

    static void PlaceCentre(RectTransform rt, Vector2 anchor, Vector2 size) =>
        MenuUI.Place(rt, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, size);

    /// <summary>Top of every sub-screen: back pill, leaning title with a gold bar, and the step markers of the play flow.</summary>
    void Header(string titleKey, int step, int titleSize = 72)
    {
        var back = MenuUI.MakeButton(content, "<   " + Loc.T("back"), 190, 60, Back, 26);
        PlaceTopLeft((RectTransform)back.transform, new Vector2(96f, -52f), new Vector2(190f, 60f));
        var title = MenuUI.Label(content, Loc.T(titleKey), titleSize, TextAnchor.MiddleLeft).Italic();
        PlaceTopLeft(title.rectTransform, new Vector2(96f, -128f), new Vector2(1300f, 100f));
        var bar = MenuUI.Stretch(content, "AccentBar", Vector2.zero, Vector2.one);
        MenuUI.Plate(bar, MenuUI.Accent);
        PlaceTopLeft(bar, new Vector2(100f, -236f), new Vector2(120f, 8f));

        if (step == 0) return;
        var row = MenuUI.Group(content, "Steps", false, 10, TextAnchor.MiddleCenter);
        MenuUI.Place(row, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-96f, -58f), Vector2.zero);
        MenuUI.FitToContent(row, true);
        string[] names = { "step_mode", "step_racers", "step_color", "step_course" };
        for (int i = 0; i < names.Length; i++)
        {
            bool now = i + 1 == step, done = i + 1 < step;
            MenuUI.Pill(row, (i + 1) + "   " + Loc.T(names[i]), 22, now ? MenuUI.Accent : done ? new Color(1f, 1f, 1f, 0.16f) : MenuUI.Chip,
                        now ? MenuUI.Ink : done ? MenuUI.Soft : MenuUI.Muted, 20, 7);
        }
    }

    /// <summary>A dark glass card on the sub-screen, centred (y = vertical anchor).</summary>
    RectTransform Card(float width, float y = 0.45f)
    {
        var card = MenuUI.Card(content, width);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, y);
        card.pivot = new Vector2(0.5f, 0.5f);
        return card;
    }

    /// <summary>A selectable pill button; key is a Loc key (or the text itself when raw).</summary>
    Button Btn(Transform parent, string key, float width, UnityAction onClick, bool raw = false, float height = 76f, bool primary = false, int fontSize = 36)
    {
        var b = MenuUI.MakeButton(parent, raw ? key : Loc.T(key), width, height, onClick, fontSize, true, primary);
        items.Add(b);
        return b;
    }

    static string QualityName(int i)
    {
        string s = Loc.T("quality_" + i);
        return s == "quality_" + i ? QualitySettings.names[i] : s;
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Main ----------

    void BuildMain()
    {
        var col = MenuUI.Group(content, "Menu", true, 10, TextAnchor.UpperLeft);
        MenuUI.Place(col, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(130f, 0f), Vector2.zero);
        MenuUI.FitToContent(col, true);
        MenuUI.Label(col, "CPE102  ·  GAME JAM #40", 22, TextAnchor.MiddleLeft, MenuUI.Cyan).Latin();
        MenuUI.Label(col, "GEM", 128, TextAnchor.MiddleLeft).Latin().Italic();
        MenuUI.Label(col, "DRIFT", 128, TextAnchor.MiddleLeft, MenuUI.Accent).Latin().Italic();
        MenuUI.Label(col, Loc.T("tagline"), 26, TextAnchor.MiddleLeft, MenuUI.Muted);
        MenuUI.Space(col, 40);
        MenuItem(col, "play", () => Show(View.Mode));
        MenuItem(col, "howto", () => Show(View.HowTo));
        MenuItem(col, "settings", () => Show(View.Settings));
#if !UNITY_WEBGL
        MenuItem(col, "quit", Quit);
#endif
    }

    void MenuItem(Transform parent, string key, UnityAction onClick) => items.Add(MenuUI.MakeMenuItem(parent, Loc.T(key), 620f, 88f, onClick));

    // ---------- Mode, racers ----------

    void BuildMode()
    {
        Header("mode_title", 1);
        var row = MenuUI.Group(content, "Cards", false, 48, TextAnchor.MiddleCenter);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.45f);
        row.pivot = new Vector2(0.5f, 0.5f);
        MenuUI.FitToContent(row, true);
        ModeCard(row, 1, "mode_single", "mode_single_sub");
        ModeCard(row, 2, "mode_two", "mode_two_sub");
    }

    void ModeCard(Transform row, int players, string nameKey, string subKey)
    {
        var b = MenuUI.MakeCard(row, 500f, 430f, () => ChooseMode(players), out var body);
        items.Add(b);
        var dots = MenuUI.Group(body, "Dots", false, 12, TextAnchor.MiddleCenter);
        for (int i = 0; i < players; i++)
        {
            var dot = new GameObject("Dot", typeof(RectTransform));
            dot.transform.SetParent(dots, false);
            var image = dot.AddComponent<Image>();
            image.sprite = MenuUI.Circle;
            image.color = i == 0 ? MenuUI.P1Color : MenuUI.P2Color;
            var size = dot.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = 30f;
        }
        MenuUI.Space(body, 6);
        MenuUI.Label(body, players + "P", 140, TextAnchor.MiddleCenter, MenuUI.Accent).Latin().Italic();
        MenuUI.Label(body, Loc.T(nameKey), 38, TextAnchor.MiddleCenter);
        MenuUI.Label(body, Loc.T(subKey), 24, TextAnchor.MiddleCenter, MenuUI.Muted);
    }

    void ChooseMode(int players)
    {
        MatchSettings.playerCount = players;
        MatchSettings.totalRacers = Mathf.Clamp(MatchSettings.totalRacers, 2, MatchSettings.MaxRacers);
        Show(View.Racers);
    }

    void BuildRacers()
    {
        Header("racers_title", 2);
        var card = Card(1100f);
        MenuUI.Label(card, Loc.T("racers_label"), 24, TextAnchor.MiddleCenter, MenuUI.Muted);
        var number = MenuUI.Label(card, "", 180, TextAnchor.MiddleCenter).Latin().Italic();
        var chips = MenuUI.Group(card, "Chips", false, 16, TextAnchor.MiddleCenter);
        var playersChip = MenuUI.Pill(chips, "", 26, MenuUI.Chip, Color.white).GetComponentInChildren<Text>();
        var aiChip = MenuUI.Pill(chips, "", 26, MenuUI.Chip, MenuUI.Cyan).GetComponentInChildren<Text>();
        MenuUI.Space(card, 10);

        // slider with the numbers 2..6 under it; the track runs between the handle centres, so the numbers sit at the same spots
        const float width = 760f, height = 44f;
        int count = MatchSettings.MaxRacers - 1;
        var ticks = new GameObject("Ticks", typeof(RectTransform));
        var tickLabels = new Text[count];
        void Set(int total)
        {
            MatchSettings.totalRacers = total;
            number.text = total.ToString();
            playersChip.text = Loc.T("players_chip", MatchSettings.playerCount);
            aiChip.text = Loc.T("ai_chip", total - MatchSettings.playerCount);
            for (int i = 0; i < count; i++) tickLabels[i].color = i + 2 == total ? MenuUI.Accent : MenuUI.Muted;
        }
        var slider = MenuUI.MakeSlider(card, width, height, 2, MatchSettings.MaxRacers, true, MatchSettings.totalRacers, v => { Set((int)v); MenuUI.Blip(600f); });
        items.Add(slider);
        ticks.transform.SetParent(card, false);
        var tickSize = ticks.AddComponent<LayoutElement>();
        tickSize.preferredWidth = width;
        tickSize.preferredHeight = 40f;
        for (int i = 0; i < count; i++)
        {
            float x = (height * 0.5f + i * (width - height) / (count - 1)) / width;
            tickLabels[i] = MenuUI.Label(ticks.transform, (i + 2).ToString(), 28, TextAnchor.MiddleCenter, MenuUI.Muted).Latin();
            PlaceCentre(tickLabels[i].rectTransform, new Vector2(x, 0.5f), new Vector2(60f, 40f));
        }
        Set(MatchSettings.totalRacers);

        MenuUI.Label(card, Loc.T("racers_note"), 24, TextAnchor.MiddleCenter, MenuUI.Muted);
        MenuUI.Space(card, 6);
        Btn(card, "continue", 480, () => Show(View.Colors), false, 80f, true, 38);
    }

    // ---------- Colour select ----------

    static float ColumnX(int humans, int p) => humans == 1 ? 0.5f : 0.27f + 0.46f * p;

    void BuildColors()
    {
        int humans = MatchSettings.playerCount;
        var pal = MatchSettings.Palette(cfg);
        pick[0] = MatchSettings.colors[0];
        pick[1] = humans == 2 && MatchSettings.colors[1] == pick[0] ? (pick[0] + 1) % pal.Length : MatchSettings.colors[1];
        ready[0] = ready[1] = false;

        Header("color_title", 3, 56);
        BuildStage(humans);

        slots = new ColorSlot[humans];
        for (int i = 0; i < humans; i++)
        {
            int p = i;
            float cx = ColumnX(humans, p);
            var s = new ColorSlot();
            // dark glass pills behind the player name and the colour name, so they stay readable over the bright course
            var headerPill = MenuUI.Pill(content, Loc.T("player_n", p + 1), 40, MenuUI.Glass, Color.white, 34, 8);
            MenuUI.FitToContent(headerPill, true);
            PlaceCentre(headerPill, new Vector2(cx, 0.745f), Vector2.zero);
            s.header = headerPill.GetComponentInChildren<Text>().Italic();
            var namePill = MenuUI.Pill(content, "", 34, MenuUI.Glass, Color.white, 30, 6);
            MenuUI.FitToContent(namePill, true);
            PlaceCentre(namePill, new Vector2(cx, 0.295f), Vector2.zero);
            s.colorName = namePill.GetComponentInChildren<Text>();

            var row = MenuUI.Group(content, "Swatches", false, 4, TextAnchor.MiddleCenter);
            PlaceCentre(row, new Vector2(cx, 0.2f), new Vector2(840f, 120f));
            s.frames = new Image[pal.Length];
            s.swatches = new Button[pal.Length];
            for (int c = 0; c < pal.Length; c++)
            {
                int color = c;
                s.swatches[c] = MenuUI.MakeChip(row, pal[c], 84f, () => ClickSwatch(p, color), out s.frames[c]);
            }

            s.readyButton = MenuUI.MakeButton(content, Loc.T("ready"), 380, 72, () => ToggleReady(p), 34, false);
            PlaceCentre((RectTransform)s.readyButton.transform, new Vector2(cx, 0.115f), new Vector2(380f, 72f));
            slots[p] = s;
        }
        RefreshColors();
    }

    /// <summary>Truck models on a stage far above the course, each drawn by its own camera into the middle of the screen.</summary>
    void BuildStage(int humans)
    {
        stage = new GameObject("TruckPreview");
        stage.transform.position = new Vector3(0f, 800f, 0f);
        pivots = new Transform[humans];
        truckRenderers = new Renderer[humans][];
        for (int p = 0; p < humans; p++)
        {
            var pivot = new GameObject("Truck" + p).transform;
            pivot.SetParent(stage.transform, false);
            pivot.localPosition = new Vector3(p * 30f, 0f, 0f);
            pivot.localRotation = Quaternion.Euler(0f, 205f, 0f);
            pivots[p] = pivot;
            if (cfg.carModel != null)
            {
                var model = Instantiate(cfg.carModel, pivot);
                model.transform.localRotation = Quaternion.Euler(0f, cfg.modelYaw, 0f);
                model.transform.localScale = Vector3.one * cfg.modelScale;
                model.transform.localPosition = cfg.modelOffset;
                foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                truckRenderers[p] = model.GetComponentsInChildren<Renderer>();
            }
            else truckRenderers[p] = new Renderer[0];

            var camGo = new GameObject("Camera" + p);
            camGo.transform.SetParent(stage.transform, false);
            camGo.transform.localPosition = pivot.localPosition + new Vector3(0f, 2.4f, -8.5f);
            camGo.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Depth; // draw on top of the course background
            cam.depth = 5f;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 60f;
            float cx = ColumnX(humans, p);
            cam.rect = new Rect(cx - 0.2f, 0.34f, 0.4f, 0.37f);
        }
    }

    void RefreshColors()
    {
        var pal = MatchSettings.Palette(cfg);
        int humans = slots.Length;
        var block = new MaterialPropertyBlock();
        for (int p = 0; p < humans; p++)
        {
            var s = slots[p];
            int other = humans == 2 ? pick[1 - p] : -1; // the other player's colour is taken
            for (int c = 0; c < pal.Length; c++)
            {
                s.frames[c].color = c == pick[p] ? Color.white : MenuUI.Clear;
                s.swatches[c].interactable = c != other;
            }
            s.colorName.text = Loc.T(MatchSettings.ColorKeys[pick[p]]);
            s.header.color = pal[pick[p]];
            var label = s.readyButton.GetComponentInChildren<Text>();
            label.text = Loc.T(ready[p] ? "ready_on" : "ready");
            if (ready[p]) MenuUI.SetLook(s.readyButton, MenuUI.Green, MenuUI.Green * 1.2f, Color.white, Color.white);
            else MenuUI.SetLook(s.readyButton, MenuUI.Chip, MenuUI.Accent, Color.white, MenuUI.Ink);
            block.SetColor("_Color", pal[pick[p]]);
            foreach (var r in truckRenderers[p]) r.SetPropertyBlock(block);
        }
    }

    void ClickSwatch(int p, int color)
    {
        if (slots.Length == 2 && pick[1 - p] == color) return;
        pick[p] = color;
        ready[p] = false;
        RefreshColors();
    }

    void ToggleReady(int p)
    {
        ready[p] = !ready[p];
        RefreshColors();
        CheckAllReady();
    }

    void CheckAllReady()
    {
        for (int p = 0; p < slots.Length; p++) if (!ready[p]) return;
        for (int p = 0; p < slots.Length; p++) MatchSettings.colors[p] = pick[p];
        Show(View.Map);
    }

    /// <summary>Per-player keys: P1 A / D + Space or Shift, P2 arrows + Enter or Shift (a single player may use either), gamepad N for player N.</summary>
    void UpdateColors()
    {
        var kb = Keyboard.current;
        int humans = slots.Length;
        int n = MatchSettings.ColorKeys.Length;
        for (int p = 0; p < humans; p++)
        {
            bool left = false, right = false, confirm = false;
            if (kb != null)
            {
                if (p == 0 || humans == 1)
                {
                    left |= kb.aKey.wasPressedThisFrame;
                    right |= kb.dKey.wasPressedThisFrame;
                    confirm |= kb.spaceKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame;
                }
                if (p == 1 || humans == 1)
                {
                    left |= kb.leftArrowKey.wasPressedThisFrame;
                    right |= kb.rightArrowKey.wasPressedThisFrame;
                    confirm |= kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame || kb.slashKey.wasPressedThisFrame;
                }
            }
            var gp = p < Gamepad.all.Count ? Gamepad.all[p] : null;
            if (gp != null)
            {
                left |= gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame;
                right |= gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame;
                confirm |= gp.buttonSouth.wasPressedThisFrame;
            }

            if (confirm)
            {
                MenuUI.Blip(740f);
                ToggleReady(p);
                if (view != View.Colors) return; // everyone is ready: the map screen is up
            }
            else if (!ready[p] && (left || right))
            {
                int dir = right ? 1 : -1, other = humans == 2 ? pick[1 - p] : -1, c = pick[p];
                do c = (c + dir + n) % n; while (c == other);
                pick[p] = c;
                MenuUI.Blip(520f);
                RefreshColors();
            }
        }
    }

    // ---------- Map ----------

    void BuildMap()
    {
        Header("map_title", 4);
        var row = MenuUI.Group(content, "Cards", false, 48, TextAnchor.MiddleCenter);
        row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.44f);
        row.pivot = new Vector2(0.5f, 0.5f);
        MenuUI.FitToContent(row, true);
        CourseCard(row, 0, "01", new Color(0.17f, 0.52f, 0.34f), "course_forest", "course_forest_desc", "chip_oneway", "chip_forest_len", "chip_dirt");
        CourseCard(row, 1, "02", new Color(0.26f, 0.44f, 0.88f), "course_circuit", "course_circuit_desc", "chip_laps", "chip_circuit_len", "chip_arena");
    }

    void CourseCard(Transform row, int course, string number, Color banner, string nameKey, string descKey, params string[] chipKeys)
    {
        var b = MenuUI.MakeCard(row, 540f, 560f, () => StartRace(course), out var body);
        items.Add(b);
        var art = new GameObject("Banner", typeof(RectTransform));
        art.transform.SetParent(body, false);
        MenuUI.Plate((RectTransform)art.transform, banner);
        var artSize = art.AddComponent<LayoutElement>();
        artSize.preferredWidth = 492f;
        artSize.preferredHeight = 220f;
        var shade = MenuUI.Stretch(art.transform, "Shade", Vector2.zero, Vector2.one);
        MenuUI.Gradient(shade, new Color(0f, 0f, 0f, 0.45f), true, true);
        var big = MenuUI.Label(art.transform, number, 150, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.92f)).Latin().Italic();
        big.rectTransform.anchorMin = Vector2.zero;
        big.rectTransform.anchorMax = Vector2.one;
        big.rectTransform.offsetMin = big.rectTransform.offsetMax = Vector2.zero;

        MenuUI.Label(body, Loc.T(nameKey), 36, TextAnchor.MiddleCenter).Italic(0.12f);
        var desc = MenuUI.Label(body, Loc.T(descKey), 23, TextAnchor.UpperCenter, MenuUI.Muted);
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        var descSize = desc.gameObject.AddComponent<LayoutElement>();
        descSize.preferredWidth = 480f;
        descSize.preferredHeight = 76f;
        var chips = MenuUI.Group(body, "Chips", false, 10, TextAnchor.MiddleCenter);
        foreach (var key in chipKeys) MenuUI.Pill(chips, Loc.T(key), 21, MenuUI.Chip, MenuUI.Soft, 16, 6);
    }

    void StartRace(int course)
    {
        MatchSettings.course = course;
        MatchSettings.totalRacers = Mathf.Clamp(MatchSettings.totalRacers, 2, MatchSettings.MaxRacers);
        MatchSettings.FromMenu = true;
        GameSettings.Save();
        SceneManager.LoadScene(GameBootstrap.SceneForCourse(course));
    }

    // ---------- How to play ----------

    void BuildHowTo()
    {
        Header("howto_title", 0, 64);
        var page = MenuUI.Group(content, "Page", true, 10, TextAnchor.UpperCenter);
        MenuUI.Place(page, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -272f), Vector2.zero);
        MenuUI.FitToContent(page, true);

        var cards = MenuUI.Group(page, "Controls", false, 24, TextAnchor.UpperCenter);
        ControlsCard(cards, 0);
        ControlsCard(cards, 1);
        MenuUI.Label(page, Loc.T("ctl_solo"), 22, TextAnchor.MiddleCenter, MenuUI.Muted);

        // the keys are the real bindings from PlayerControls (and the README controls table)
        var rules = MenuUI.Card(page, 1324f, TextAnchor.UpperLeft, 40, 20);
        rules.GetComponent<VerticalLayoutGroup>().spacing = 7f;
        foreach (var key in new[] { "rule_gems", "rule_drift", "rule_nitro", "rule_crash", "rule_score" })
        {
            var line = MenuUI.Group(rules, "Rule", false, 16, TextAnchor.MiddleLeft);
            var dot = new GameObject("Dot", typeof(RectTransform));
            dot.transform.SetParent(line, false);
            var image = dot.AddComponent<Image>();
            image.sprite = MenuUI.Circle;
            image.color = MenuUI.Accent;
            var size = dot.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = 12f;
            MenuUI.Label(line, Loc.T(key), 22, TextAnchor.MiddleLeft, MenuUI.Soft);
        }
    }

    void ControlsCard(Transform parent, int p)
    {
        var card = MenuUI.Card(parent, 650f, TextAnchor.UpperLeft, 36, 22);
        card.GetComponent<VerticalLayoutGroup>().spacing = 6f;
        MenuUI.Label(card, Loc.T("player_n", p + 1), 32, TextAnchor.MiddleLeft, p == 0 ? MenuUI.P1Color : MenuUI.P2Color).Italic();
        string[][] keys = p == 0
            ? new[] { new[] { "W" }, new[] { "S" }, new[] { "A", "D" }, new[] { "L-SHIFT", "SPACE" }, new[] { "R" } }
            : new[] { new[] { Loc.T("key_up") }, new[] { Loc.T("key_down") }, new[] { Loc.T("key_left"), Loc.T("key_right") }, new[] { "R-SHIFT", "/" }, new[] { "ENTER" } };
        string[] actions = { "ctl_throttle", "ctl_brake", "ctl_steer", "ctl_drift", "ctl_respawn" };
        for (int i = 0; i < actions.Length; i++)
        {
            var row = MenuUI.Group(card, "Row", false, 16, TextAnchor.MiddleLeft);
            var caps = MenuUI.Group(row, "Keys", false, 8, TextAnchor.MiddleLeft);
            caps.gameObject.AddComponent<LayoutElement>().preferredWidth = 250f;
            foreach (var k in keys[i]) MenuUI.Key(caps, k, 20);
            MenuUI.Label(row, Loc.T(actions[i]), 24, TextAnchor.MiddleLeft, MenuUI.Soft);
        }
    }

    // ---------- Settings ----------

    void BuildSettings()
    {
#if !UNITY_EDITOR
        GameSettings.Fullscreen = Screen.fullScreen; // the player may have left fullscreen with Esc
#endif
        Header("settings_title", 0);
        var card = Card(1260f, 0.43f);
        card.GetComponent<VerticalLayoutGroup>().spacing = 12f;
        SliderRow(card, "set_music", GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);
        SliderRow(card, "set_sfx", GameSettings.SfxVolume, v => { GameSettings.SfxVolume = v; MenuUI.Blip(660f); });
        SegmentedRow(card, "set_lang", Loc.Languages, GameSettings.Language, 190f, i =>
        {
            GameSettings.Language = i;
            Show(View.Settings, 2 + i); // every label changes: rebuild the screen, keeping the focus on the language row
        });
        ToggleRow(card, "set_full", () => GameSettings.Fullscreen, v => { GameSettings.Fullscreen = v; GameSettings.ApplyFullscreen(); });
        QualityRow(card);
        ToggleRow(card, "set_minimap", () => GameSettings.ShowMinimap, v => GameSettings.ShowMinimap = v);
        ToggleRow(card, "set_shake", () => GameSettings.CameraShake, v => GameSettings.CameraShake = v);
    }

    Transform SettingsRow(Transform card, string labelKey)
    {
        var row = MenuUI.Group(card, "Row", false, 16, TextAnchor.MiddleLeft);
        row.gameObject.AddComponent<LayoutElement>().preferredWidth = 1140f; // same width for every row, so the labels and controls line up
        MenuUI.Label(row, Loc.T(labelKey), 32, 520f, TextAnchor.MiddleLeft, MenuUI.Soft);
        return row;
    }

    void SliderRow(Transform card, string labelKey, float value, UnityAction<float> set)
    {
        var row = SettingsRow(card, labelKey);
        Text percent = null;
        items.Add(MenuUI.MakeSlider(row, 420f, 36f, 0f, 1f, false, value, v =>
        {
            set(v);
            percent.text = Mathf.RoundToInt(v * 100f) + "%";
        }));
        percent = MenuUI.Label(row, Mathf.RoundToInt(value * 100f) + "%", 28, 150f, TextAnchor.MiddleRight).Latin();
    }

    void QualityRow(Transform card)
    {
        var row = SettingsRow(card, "set_quality");
        Text value = null;
        int last = QualitySettings.names.Length - 1;
        items.Add(MenuUI.MakeSlider(row, 420f, 36f, 0f, last, true, GameSettings.Quality, v =>
        {
            GameSettings.Quality = (int)v;
            GameSettings.ApplyQuality();
            value.text = QualityName((int)v);
        }));
        value = MenuUI.Label(row, QualityName(GameSettings.Quality), 28, 150f, TextAnchor.MiddleRight);
    }

    void SegmentedRow(Transform card, string labelKey, string[] options, int selected, float optionWidth, UnityAction<int> onPick)
    {
        var row = SettingsRow(card, labelKey);
        foreach (var b in MenuUI.MakeSegmented(row, options, selected, optionWidth, 62f, onPick, 28)) items.Add(b);
    }

    void ToggleRow(Transform card, string labelKey, System.Func<bool> get, UnityAction<bool> set) =>
        SegmentedRow(card, labelKey, new[] { Loc.T("off"), Loc.T("on") }, get() ? 1 : 0, 130f, i => set(i == 1));
}
