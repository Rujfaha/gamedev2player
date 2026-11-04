using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Split-screen HUD built in code: per-player lap / position / gems / speed / drift bar,
/// centre countdown, last-call timer and the results screen.
/// Layout and colours are placeholders for the UI design (task A7).
/// </summary>
public class RaceHUD : MonoBehaviour
{
    const float MapSize = 260f;

    // The first race.HumanCount racers are the players (one HUD half each, or the whole screen for one player);
    // the rest are AI trucks, which only show up in positions and results. Colours are the trucks' own tints.
    string Name(int racer) => racer < race.HumanCount ? "P" + (racer + 1) : "AI " + (racer - race.HumanCount + 1);
    Color RacerColor(int racer) => race.Racers[racer].car.Tint;
    static string Ordinal(int n) => n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";

    class PlayerUI
    {
        public Text lap, position, gems, speed, popup, result;
        public RectTransform driftFill, nitroFill;
        public Image driftFillImage, nitroFillImage;
        public Text nitroLabel;
    }

    RaceManager race;
    PlayerUI[] players;
    Text center, centerSub, winner;
    GameObject resultsDim;
    Font font;

    public void Init(RaceManager raceManager, TrackLayout layout)
    {
        race = raceManager;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGo = new GameObject("HUD");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var root = canvasGo.transform;

        int humans = race.HumanCount;
        float share = 1f / humans; // screen share of each player: half each for two players, the whole screen for one

        // divider between the two halves
        if (humans == 2)
        {
            var divider = Panel(root, "Divider", new Vector2(0.5f, 0), new Vector2(0.5f, 1), Color.black);
            divider.sizeDelta = new Vector2(8, 0);
        }

        players = new PlayerUI[humans];
        for (int i = 0; i < humans; i++)
        {
            string label = Name(i);
            var half = Panel(root, label, new Vector2(i * share, 0), new Vector2((i + 1) * share, 1), Color.clear);
            var ui = new PlayerUI();
            var tag = MakeText(half, label, 44, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30, -20), new Vector2(120, 60));
            tag.color = RacerColor(i);
            ui.lap = MakeText(half, "", 40, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30, -75), new Vector2(400, 60));
            // minimap top-right (Settings > Show minimap), position (1st/2nd) just left of it
            var cars = System.Array.ConvertAll(race.Racers, r => r.car);
            if (GameSettings.ShowMinimap) Minimap.Create(half, new Vector2(MapSize, MapSize), layout, cars, i, System.Array.ConvertAll(cars, c => c.Tint));
            ui.position = MakeText(half, "", 90, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(GameSettings.ShowMinimap ? -MapSize - 44 : -30, -10), new Vector2(300, 110));
            ui.gems = MakeText(half, "", 40, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(30, 30), new Vector2(520, 60));
            ui.speed = MakeText(half, "", 40, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(-30, 30), new Vector2(300, 60));
            ui.popup = MakeText(half, "", 52, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(800, 80));
            ui.result = MakeText(half, "", 46, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.45f), Vector2.zero, new Vector2(800, 420));

            // drift bar (bottom centre)
            var bar = Panel(half, "DriftBar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Color(0, 0, 0, 0.5f));
            bar.sizeDelta = new Vector2(300, 18);
            bar.anchoredPosition = new Vector2(0, 110);
            ui.driftFill = Panel(bar, "Fill", new Vector2(0, 0), new Vector2(0, 1), Color.white);
            ui.driftFill.pivot = new Vector2(0, 0.5f);
            ui.driftFillImage = ui.driftFill.GetComponent<Image>();

            // nitro tank (above the drift bar)
            var nbar = Panel(half, "NitroBar", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Color(0, 0, 0, 0.55f));
            nbar.sizeDelta = new Vector2(300, 26);
            nbar.anchoredPosition = new Vector2(0, 150);
            ui.nitroFill = Panel(nbar, "Fill", new Vector2(0, 0), new Vector2(0, 1), new Color(0.75f, 0.35f, 1f));
            ui.nitroFill.pivot = new Vector2(0, 0.5f);
            ui.nitroFillImage = ui.nitroFill.GetComponent<Image>();
            // tick at the middle: from here BLUE nitro can be used
            var tick = Panel(nbar, "HalfMark", new Vector2(0.5f, 0), new Vector2(0.5f, 1), Color.white);
            tick.sizeDelta = new Vector2(4, 8);
            ui.nitroLabel = MakeText(nbar, "NITRO", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 30));
            bar.gameObject.SetActive(false); // drift bar no longer needed: the nitro tank shows the progress
            players[i] = ui;
        }

        resultsDim = Panel(root, "ResultsDim", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.6f)).gameObject;
        resultsDim.transform.SetSiblingIndex(humans == 2 ? 1 : 0); // under the player halves, so their text stays bright
        resultsDim.SetActive(false);

        center = MakeText(root, "", 160, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(1200, 200));
        centerSub = MakeText(root, "", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.38f), Vector2.zero, new Vector2(1400, 140));
        winner = MakeText(root, "", 90, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.85f), Vector2.zero, new Vector2(1600, 140));
    }

    void Update()
    {
        if (race == null || race.Racers == null) return;

        for (int i = 0; i < players.Length; i++) UpdatePlayer(i);

        switch (race.State)
        {
            case RaceManager.Phase.Countdown:
                center.text = Mathf.CeilToInt(race.CountdownLeft).ToString();
                centerSub.text = race.TrackName + (race.Loop ? "  ·  " + race.Laps + " LAPS" : "  ·  ONE WAY") +
                                 "\nDrift to fill NITRO, double-tap Shift to fire      ·      1 = Forest   2 = Arena";
                break;
            case RaceManager.Phase.Racing:
                center.text = Time.time - race.GoTime < 1f ? "GO!" : "";
                centerSub.text = "";
                break;
            case RaceManager.Phase.LastCall:
                center.text = "";
                centerSub.text = "LAST CALL  " + Mathf.CeilToInt(race.LastCallLeft) + "s";
                break;
            case RaceManager.Phase.Results:
                resultsDim.SetActive(true);
                center.text = "";
                centerSub.text = Standings() + "R / Enter = again      N = next      M / Esc = menu";
                centerSub.rectTransform.anchorMin = centerSub.rectTransform.anchorMax = new Vector2(0.5f, race.HumanCount == 1 ? 0.2f : 0.08f); // clear of the nitro bar a single player has at the bottom centre
                winner.text = Name(race.WinnerIndex) + " WINS!";
                winner.color = RacerColor(race.WinnerIndex);
                break;
        }
    }

    void UpdatePlayer(int i)
    {
        var ui = players[i];
        var r = race.Racers[i];
        var car = r.car;

        ui.lap.text = r.finished ? "FINISHED"
                    : race.Loop ? "LAP " + race.DisplayLap(i) + "/" + race.Laps
                    : "ROUTE " + Mathf.RoundToInt(100f * r.passed / race.CheckpointCount) + "%";
        ui.position.text = Ordinal(race.PositionOf(i));
        ui.gems.text = "GEMS " + car.Bed.Count + "/" + GemBed.Capacity + "   VALUE " + car.Bed.Value;
        ui.speed.text = Mathf.RoundToInt(Mathf.Abs(car.Speed) * 3.6f) + " km/h";


        ui.nitroFill.anchorMax = new Vector2(car.NitroActive ? 1f : car.Nitro, 1);
        ui.nitroFill.sizeDelta = Vector2.zero;
        bool flash = car.NitroReady && Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
        string key = i == 0 ? "L-SHIFT" : "R-SHIFT";
        ui.nitroFillImage.color = car.ActiveNitroLevel == 2 ? new Color(1f, 0.55f, 0.15f)
                                : car.ActiveNitroLevel == 1 ? new Color(0.4f, 0.85f, 1f)
                                : car.NitroReady ? (flash ? new Color(1f, 0.75f, 0.3f) : new Color(1f, 0.35f, 0.15f))
                                : car.BlueReady ? new Color(0.25f, 0.65f, 1f)
                                : new Color(0.75f, 0.35f, 1f);
        ui.nitroLabel.text = car.ActiveNitroLevel == 2 ? "RED NITRO!!"
                           : car.ActiveNitroLevel == 1 ? "BLUE NITRO!"
                           : car.NitroReady ? "RED NITRO READY · double-tap " + key
                           : car.BlueReady ? "BLUE NITRO READY · double-tap " + key
                           : "NITRO";

        // short pop-up messages
        string msg = "";
        if (Time.time - car.Bed.LastSpillTime < 1.2f) msg = "-" + car.Bed.LastSpillCount + " GEM" + (car.Bed.LastSpillCount > 1 ? "S" : "");
        else if (Time.time - car.LastNitroTime < 1.2f) msg = car.LastNitroLevel == 2 ? "RED NITRO!!" : "BLUE NITRO!";
        else if (car.Bed.Full && !r.finished) msg = "BED FULL";
        ui.popup.text = race.State == RaceManager.Phase.Results ? "" : msg;
        ui.popup.color = msg.StartsWith("-") ? new Color(1f, 0.4f, 0.4f) : Color.white;

        if (race.State == RaceManager.Phase.Results)
        {
            string finish = r.dnf ? "DID NOT FINISH  (-" + RaceManager.DnfPenalty + ")"
                          : r.finishOrder == 1 ? "FINISHED 1st  (+" + RaceManager.FirstPlaceBonus + ")"
                          : "FINISHED " + Ordinal(r.finishOrder) + "  (+0)";
            ui.result.text = Name(i) + "\n\nGems value  " + r.gemScore + "\n" + finish + "\n\nTOTAL  " + r.Total;
        }
        else if (r.finished)
        {
            ui.result.text = "FINISHED " + Ordinal(r.finishOrder) + (r.finishOrder == 1 ? "!" : "") + "\n" + FormatTime(r.finishTime);
        }
        else ui.result.text = "";
    }

    /// <summary>One line with every racer's total, e.g. "P1 12   P2 8   AI 1 15   AI 2 9", so AI scores are visible too.</summary>
    string Standings()
    {
        var line = "";
        for (int i = 0; i < race.Racers.Length; i++) line += Name(i) + " " + race.Racers[i].Total + "     ";
        return line + "\n";
    }

    static string FormatTime(float t) => Mathf.FloorToInt(t / 60f) + ":" + (t % 60f).ToString("00.00");

    RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    Text MakeText(Transform parent, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = Color.white;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 0.8f);
        outline.effectDistance = new Vector2(3, -3);
        return t;
    }
}
