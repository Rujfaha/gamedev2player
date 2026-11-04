using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// uGUI builders shared by the main menu and the pause menu (same approach as RaceHUD: everything is made in code,
/// 1920x1080 reference canvas). Look: dark glass cards with rounded corners, pill buttons that light up gold on hover / focus,
/// keycap prompts. The rounded sprites are generated at runtime, so no art files are needed.
/// Also plays the placeholder menu sounds, which follow the SFX volume setting.
/// </summary>
public static class MenuUI
{
    // ---------- Palette ----------

    public static readonly Color Ink = new Color(0.035f, 0.05f, 0.09f);
    public static readonly Color Glass = new Color(0.06f, 0.08f, 0.14f, 0.86f);
    public static readonly Color GlassHot = new Color(0.12f, 0.15f, 0.25f, 0.96f);
    public static readonly Color Chip = new Color(1f, 1f, 1f, 0.09f);
    public static readonly Color ChipHot = new Color(1f, 1f, 1f, 0.2f);
    public static readonly Color Line = new Color(1f, 1f, 1f, 0.14f);
    public static readonly Color Accent = new Color(1f, 0.78f, 0.15f);        // gem gold
    public static readonly Color AccentHot = new Color(1f, 0.9f, 0.5f);
    public static readonly Color Cyan = new Color(0.3f, 0.8f, 1f);
    public static readonly Color Green = new Color(0.2f, 0.72f, 0.38f);
    public static readonly Color Clear = new Color(1f, 1f, 1f, 0f);
    public static readonly Color Muted = new Color(1f, 1f, 1f, 0.62f);
    public static readonly Color Soft = new Color(1f, 1f, 1f, 0.78f);
    public static readonly Color P1Color = new Color(1f, 0.45f, 0.4f), P2Color = new Color(0.45f, 0.7f, 1f);

    // ---------- Font ----------

    static Font font;
    static int fontLanguage = -1;
    static FontStyle fontStyle = FontStyle.Normal;
    static float lineSpacing = 1f; // Thai fonts have taller lines than Inter, so their text is tightened to keep the layout the same

    /// <summary>
    /// English uses Inter SemiBold (Resources/MenuFont.ttf, SIL OFL, the font Unity itself ships). Thai uses Prompt SemiBold
    /// (Resources/MenuFontThai.ttf, SIL OFL); swap that file to change it. Without it Thai falls back to an installed OS font,
    /// which a WebGL build does not have.
    /// </summary>
    public static Font GetFont()
    {
        if (font != null && fontLanguage == GameSettings.Language) return font;
        fontLanguage = GameSettings.Language;
        font = Resources.Load<Font>(fontLanguage == 0 ? "MenuFont" : "MenuFontThai");
        fontStyle = font != null ? FontStyle.Normal : FontStyle.Bold; // custom fonts are already semi-bold
        lineSpacing = font != null && fontLanguage != 0 ? 0.8f : 1f;
#if !UNITY_WEBGL
        if (font == null && fontLanguage != 0)
            font = Font.CreateDynamicFontFromOSFont(new[] { "Leelawadee UI", "Tahoma", "Noto Sans Thai", "Arial Unicode MS" }, 32);
#endif
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    // ---------- Generated sprites ----------

    static Sprite round, roundRing, circle, circleRing, fadeV, fadeH;

    public static Sprite Round => round != null ? round : round = Rounded(false);
    public static Sprite RoundRing => roundRing != null ? roundRing : roundRing = Rounded(true);
    public static Sprite Circle => circle != null ? circle : circle = Disc(false);
    public static Sprite CircleRing => circleRing != null ? circleRing : circleRing = Disc(true);
    static Sprite FadeV => fadeV != null ? fadeV : fadeV = Fade(true);
    static Sprite FadeH => fadeH != null ? fadeH : fadeH = Fade(false);

    static Sprite MakeSprite(Color32[] pixels, int w, int h, float border)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    // coverage of a shape from its signed distance d (negative inside), 1 px antialiased; a ring keeps a 2 px band just inside
    static byte Cover(float d, bool ring) =>
        (byte)(255f * (ring ? Mathf.Clamp01(0.5f - d) - Mathf.Clamp01(0.5f - (d + 2f)) : Mathf.Clamp01(0.5f - d)));

    // 64 px rounded square, 24 px corners: used 9-sliced, so any size keeps the same corner radius (small ones become pills)
    static Sprite Rounded(bool ring)
    {
        const int n = 64;
        const float r = 24f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - n / 2f) - (n / 2f - r), qy = Mathf.Abs(y + 0.5f - n / 2f) - (n / 2f - r);
                float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                px[y * n + x] = new Color32(255, 255, 255, Cover(d, ring));
            }
        return MakeSprite(px, n, n, r);
    }

    static Sprite Disc(bool ring)
    {
        const int n = 64;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                px[y * n + x] = new Color32(255, 255, 255, Cover(new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude - n / 2f, ring));
        return MakeSprite(px, n, n, 0f);
    }

    // alpha ramps 0 → 1 along the axis (bottom → top, or left → right)
    static Sprite Fade(bool vertical)
    {
        int w = vertical ? 4 : 64, h = vertical ? 64 : 4;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(255f * (vertical ? y / (h - 1f) : x / (w - 1f))));
        return MakeSprite(px, w, h, 0f);
    }

    // ---------- Canvas / event system ----------

    public static Transform CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go.transform;
    }

    /// <summary>Keyboard, mouse and gamepad navigation for uGUI come from the Input System's default UI actions.</summary>
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    // ---------- Rects and images ----------

    public static RectTransform Stretch(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>Flat colour rectangle (full-screen dims).</summary>
    public static Image Fill(RectTransform rt, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>Rounded 9-sliced plate (cards, chips, pills).</summary>
    public static Image Plate(RectTransform rt, Color color, Sprite sprite = null)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite != null ? sprite : Round;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>A thin outline around a rounded plate. It sits on top and is ignored by layout groups.</summary>
    public static Image Border(Transform parent, Color color)
    {
        var rt = Stretch(parent, "Border", Vector2.zero, Vector2.one);
        rt.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return Plate(rt, color, RoundRing);
    }

    /// <summary>Dark-to-clear fade for the edges of the screen. The fade is opaque at the top (vertical) or the left (horizontal) edge.</summary>
    public static Image Gradient(RectTransform rt, Color color, bool vertical, bool flip)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = vertical ? FadeV : FadeH;
        img.color = color;
        img.raycastTarget = false;
        if (flip) rt.localScale = vertical ? new Vector3(1f, -1f, 1f) : new Vector3(-1f, 1f, 1f);
        return img;
    }

    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // ---------- Text ----------

    public static Text Label(Transform parent, string text, int size, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = GetFont();
        t.fontSize = size;
        t.fontStyle = fontStyle;
        t.lineSpacing = lineSpacing;
        t.alignment = align;
        t.color = color ?? Color.white;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        go.AddComponent<ThaiMarks>(); // Thai tone marks need help in uGUI Text (see ThaiMarks)
        if (lineSpacing != 1f) // Thai font: lay out with a normal line box (the text still overflows evenly)
        {
            int lines = 1;
            foreach (char c in text) if (c == '\n') lines++;
            go.AddComponent<LayoutElement>().preferredHeight = size * 1.3f * lines;
        }
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(0f, -2.5f);
        return t;
    }

    /// <summary>A label with a fixed width, so a row of labels and controls lines up.</summary>
    public static Text Label(Transform parent, string text, int size, float width, TextAnchor align, Color? color = null)
    {
        var t = Label(parent, text, size, align, color);
        t.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
        return t;
    }

    static bool HasThai(string s)
    {
        foreach (char c in s) if (c >= '฀' && c <= '๿') return true;
        return false;
    }

    /// <summary>Numbers, key names and Latin titles are always drawn in Inter, in both languages: Thai fonts have taller lines.</summary>
    public static Text Latin(this Text t)
    {
        var latin = Resources.Load<Font>("MenuFont");
        if (latin == null || HasThai(t.text)) return t; // a Thai key name (ซ้าย) needs the Thai font
        t.font = latin;
        t.fontStyle = FontStyle.Normal;
        t.lineSpacing = 1f;
        return t;
    }

    /// <summary>Racing-style lean for titles.</summary>
    public static Text Italic(this Text t, float amount = 0.2f)
    {
        t.gameObject.AddComponent<UISkew>().skew = amount;
        return t;
    }

    // ---------- Layout groups ----------

    public static RectTransform Group(Transform parent, string name, bool vertical, float spacing, TextAnchor align, RectOffset padding = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var g = vertical ? (HorizontalOrVerticalLayoutGroup)go.AddComponent<VerticalLayoutGroup>() : go.AddComponent<HorizontalLayoutGroup>();
        g.spacing = spacing;
        g.childAlignment = align;
        g.childControlWidth = g.childControlHeight = true;
        g.childForceExpandWidth = g.childForceExpandHeight = false;
        if (padding != null) g.padding = padding;
        return (RectTransform)go.transform;
    }

    public static void Space(Transform parent, float height)
    {
        var go = new GameObject("Space", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<LayoutElement>().preferredHeight = height;
    }

    public static void FitToContent(RectTransform rt, bool horizontal)
    {
        var fit = rt.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (horizontal) fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>A dark glass card; children are stacked top to bottom.</summary>
    public static RectTransform Card(Transform parent, float width, TextAnchor align = TextAnchor.UpperCenter, int padX = 56, int padY = 44)
    {
        var col = Group(parent, "Card", true, 16, align, new RectOffset(padX, padX, padY, padY));
        col.sizeDelta = new Vector2(width, 0f);
        col.gameObject.AddComponent<LayoutElement>().preferredWidth = width; // when the card sits inside another layout group
        Plate(col, Glass);
        Border(col, Line);
        FitToContent(col, false);
        return col;
    }

    // ---------- Buttons ----------

    /// <summary>
    /// Pill button. Idle it is a faint glass chip; on hover or keyboard focus it fills gold and grows a little.
    /// primary = the main action of a screen: gold from the start.
    /// </summary>
    public static Button MakeButton(Transform parent, string text, float width, float height, UnityAction onClick, int fontSize = 36, bool navigable = true, bool primary = false)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var bg = Plate((RectTransform)go.transform, Chip);
        bg.raycastTarget = true;
        var b = go.AddComponent<Button>();
        b.targetGraphic = bg;
        b.transition = Selectable.Transition.None; // MenuFx does the animation
        if (!navigable) b.navigation = new Navigation { mode = Navigation.Mode.None }; // mouse only, never takes keyboard focus
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        var ring = Border(go.transform, Line);
        var label = Label(go.transform, text, fontSize, TextAnchor.MiddleCenter, Color.white);
        var rt = (RectTransform)label.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var fx = go.AddComponent<MenuFx>();
        fx.Add(bg, Chip, Accent);   // 0
        fx.Add(ring, Line, Accent); // 1
        fx.Add(label, Color.white, Ink); // 2
        if (primary) SetLook(b, Accent, AccentHot, Ink, Ink);
        fx.onFocus = () => { if (Time.unscaledTime > quietUntil) Blip(520f); };

        b.onClick.AddListener(() => Blip(740f));
        b.onClick.AddListener(onClick);
        return b;
    }

    /// <summary>Recolour a MakeButton button: fill and label, idle and highlighted.</summary>
    public static void SetLook(Button b, Color fill, Color fillHot, Color text, Color textHot)
    {
        var fx = b.GetComponent<MenuFx>();
        fx.Set(0, fill, fillHot);
        fx.Set(2, text, textHot);
    }

    /// <summary>Big text menu entry for the main page: no box until it is highlighted, then a gold bar and the text slides right.</summary>
    public static Button MakeMenuItem(Transform parent, string text, float width, float height, UnityAction onClick)
    {
        var go = new GameObject("MenuItem", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var bg = Plate((RectTransform)go.transform, Clear);
        bg.raycastTarget = true;
        var b = go.AddComponent<Button>();
        b.targetGraphic = bg;
        b.transition = Selectable.Transition.None;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        var bar = Stretch(go.transform, "Bar", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        bar.pivot = new Vector2(0f, 0.5f);
        bar.sizeDelta = new Vector2(8f, height - 36f);
        bar.anchoredPosition = new Vector2(16f, 0f);
        var barImage = Plate(bar, Clear);

        var label = Label(go.transform, text, 50, TextAnchor.MiddleLeft, Soft).Italic(0.16f);
        var rt = (RectTransform)label.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(52f, 0f);
        rt.offsetMax = Vector2.zero;

        var fx = go.AddComponent<MenuFx>();
        fx.scale = 1f;
        fx.Add(bg, Clear, new Color(1f, 1f, 1f, 0.1f));
        fx.Add(barImage, new Color(Accent.r, Accent.g, Accent.b, 0f), Accent);
        fx.Add(label, Soft, Color.white);
        fx.Slide(rt, 14f);
        fx.onFocus = () => { if (Time.unscaledTime > quietUntil) Blip(520f); };

        b.onClick.AddListener(() => Blip(740f));
        b.onClick.AddListener(onClick);
        return b;
    }

    /// <summary>
    /// A clickable glass card (map and mode choice). Returns the button; the card's content goes into `body` (a vertical group).
    /// The border turns gold and the card grows a little on hover / focus.
    /// </summary>
    public static Button MakeCard(Transform parent, float width, float height, UnityAction onClick, out RectTransform body)
    {
        var go = new GameObject("CardButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var bg = Plate((RectTransform)go.transform, Glass);
        bg.raycastTarget = true;
        var b = go.AddComponent<Button>();
        b.targetGraphic = bg;
        b.transition = Selectable.Transition.None;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        body = Group(go.transform, "Body", true, 14, TextAnchor.UpperCenter, new RectOffset(24, 24, 24, 24));
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = body.offsetMax = Vector2.zero;
        var ring = Border(go.transform, Line);

        var fx = go.AddComponent<MenuFx>();
        fx.scale = 1.03f;
        fx.Add(bg, Glass, GlassHot);
        fx.Add(ring, Line, Accent);
        fx.onFocus = () => { if (Time.unscaledTime > quietUntil) Blip(520f); };
        b.onClick.AddListener(() => Blip(740f));
        b.onClick.AddListener(onClick);
        return b;
    }

    /// <summary>A static rounded label chip (stats, counters, step markers).</summary>
    public static RectTransform Pill(Transform parent, string text, int size, Color fill, Color textColor, int padX = 22, int padY = 8)
    {
        var pill = Group(parent, "Pill", false, 0, TextAnchor.MiddleCenter, new RectOffset(padX, padX, padY, padY + 2));
        Plate(pill, fill);
        Label(pill, text, size, TextAnchor.MiddleCenter, textColor);
        return pill;
    }

    /// <summary>A keyboard key cap, for control prompts.</summary>
    public static void Key(Transform parent, string text, int size = 24)
    {
        var cap = Group(parent, "Key", false, 0, TextAnchor.MiddleCenter, new RectOffset(14, 14, 6, 9));
        Plate(cap, new Color(1f, 1f, 1f, 0.13f));
        Border(cap, new Color(1f, 1f, 1f, 0.28f));
        cap.gameObject.AddComponent<LayoutElement>().minWidth = 46f;
        Label(cap, text, size, TextAnchor.MiddleCenter, Color.white).Latin();
    }

    /// <summary>Pick one of a few options (also used for ON / OFF). onPick runs after the choice changed.</summary>
    public static Button[] MakeSegmented(Transform parent, string[] options, int selected, float optionWidth, float height, UnityAction<int> onPick, int fontSize = 30)
    {
        var holder = Group(parent, "Segmented", false, 4, TextAnchor.MiddleCenter, new RectOffset(5, 5, 5, 5));
        Plate(holder, Chip);
        Border(holder, Line);
        var buttons = new Button[options.Length];
        int current = selected;

        void Paint()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                bool on = i == current;
                SetLook(buttons[i], on ? Accent : Clear, on ? AccentHot : ChipHot, on ? Ink : Color.white, on ? Ink : Color.white);
            }
        }

        for (int i = 0; i < options.Length; i++)
        {
            int index = i;
            buttons[i] = MakeButton(holder, options[i], optionWidth, height - 10f, () => { current = index; Paint(); onPick(index); }, fontSize);
            buttons[i].transform.Find("Border").gameObject.SetActive(false); // the group already has an outline
            buttons[i].GetComponent<MenuFx>().scale = 1f;
        }
        Paint();
        return buttons;
    }

    /// <summary>A round colour swatch inside a ring that lights up when it is the chosen one. Mouse only (colour select has its own keys).</summary>
    public static Button MakeChip(Transform parent, Color color, float size, UnityAction onClick, out Image frame)
    {
        var cell = new GameObject("Chip", typeof(RectTransform));
        cell.transform.SetParent(parent, false);
        var le = cell.AddComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = size + 28f;
        frame = cell.AddComponent<Image>();
        frame.sprite = CircleRing;
        frame.color = Clear;
        frame.raycastTarget = false;

        var go = new GameObject("Swatch", typeof(RectTransform));
        go.transform.SetParent(cell.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        img.sprite = Circle;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.None;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var fx = go.AddComponent<MenuFx>();
        fx.scale = 1.12f;
        fx.Add(img, color, Color.Lerp(color, Color.white, 0.3f));
        b.onClick.AddListener(() => Blip(740f));
        b.onClick.AddListener(onClick);
        return b;
    }

    /// <summary>Left / right (keyboard, gamepad) and dragging (mouse) all work: that is what a uGUI Slider does.</summary>
    public static Slider MakeSlider(Transform parent, float width, float height, float min, float max, bool whole, float value, UnityAction<float> onChange)
    {
        var go = new GameObject("Slider", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        // thin pill track, gold fill, round handle (the handle is as tall as the slider)
        var bar = Stretch(go.transform, "Background", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
        bar.sizeDelta = new Vector2(0f, 12f);
        var barImage = Plate(bar, new Color(1f, 1f, 1f, 0.18f));
        barImage.raycastTarget = true;
        var fillArea = Stretch(go.transform, "Fill Area", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
        fillArea.sizeDelta = new Vector2(0f, 12f);
        var fill = Stretch(fillArea, "Fill", Vector2.zero, Vector2.one);
        Plate(fill, Accent);
        var handleArea = Stretch(go.transform, "Handle Slide Area", Vector2.zero, Vector2.one);
        handleArea.offsetMin = new Vector2(height * 0.5f, 0f);
        handleArea.offsetMax = new Vector2(-height * 0.5f, 0f);
        var handle = Stretch(handleArea, "Handle", new Vector2(0f, 0f), new Vector2(0f, 1f));
        handle.sizeDelta = new Vector2(height, 0f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = Circle;

        var s = go.AddComponent<Slider>();
        s.fillRect = fill;
        s.handleRect = handle;
        s.targetGraphic = handleImage;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min;
        s.maxValue = max;
        s.wholeNumbers = whole;
        s.value = value;
        var cb = s.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = cb.selectedColor = AccentHot;
        cb.pressedColor = Accent;
        cb.fadeDuration = 0.08f;
        s.colors = cb;
        s.onValueChanged.AddListener(onChange);
        return s;
    }

    // ---------- Sounds ----------

    static AudioSource sfx;
    static readonly Dictionary<int, AudioClip> clips = new Dictionary<int, AudioClip>();
    public static float quietUntil;   // no selection ticks right after a screen is built

    /// <summary>Placeholder UI blip (generated sine, no audio file). Volume = SFX setting.</summary>
    public static void Blip(float freq)
    {
        if (GameSettings.SfxVolume <= 0.001f) return;
        if (sfx == null)
        {
            var go = new GameObject("UISfx");
            Object.DontDestroyOnLoad(go);
            sfx = go.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.ignoreListenerPause = true; // still audible while the race is paused
        }
        int key = (int)freq;
        if (!clips.TryGetValue(key, out var clip))
        {
            const int rate = 44100;
            int n = rate / 12;
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / rate) * (1f - (float)i / n);
            clip = AudioClip.Create("blip" + key, n, 1, rate, false);
            clip.SetData(data, 0);
            clips[key] = clip;
        }
        sfx.PlayOneShot(clip, GameSettings.SfxVolume * 0.5f);
    }
}
