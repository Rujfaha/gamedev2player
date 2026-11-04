using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Split-screen HUD built in code: per-player lap / position / gems / speed / drift bar,
/// centre countdown, last-call timer and the results screen.
/// Layout and colours are placeholders for the UI design (task A7).
/// </summary>
public class RaceHUD : MonoBehaviour
{
    static readonly string[] Names = { "P1", "P2" };
    const float MapSize = 260f;
    static readonly Color[] PlayerColors = { new Color(1f, 0.45f, 0.4f), new Color(0.45f, 0.7f, 1f) };

    class PlayerUI
    {
        public Text lap, position, gems, speed, popup, result;
        public RectTransform driftFill;
        public Image driftFillImage;
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

        // divider between the two halves
        var divider = Panel(root, "Divider", new Vector2(0.5f, 0), new Vector2(0.5f, 1), Color.black);
        divider.sizeDelta = new Vector2(8, 0);

        players = new PlayerUI[2];
        for (int i = 0; i < 2; i++)
        {
            var half = Panel(root, Names[i], new Vector2(i * 0.5f, 0), new Vector2(i * 0.5f + 0.5f, 1), Color.clear);
            var ui = new PlayerUI();
            var tag = MakeText(half, Names[i], 44, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30, -20), new Vector2(120, 60));
            tag.color = PlayerColors[i];
            ui.lap = MakeText(half, "", 40, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(30, -75), new Vector2(400, 60));
            // minimap top-right, position (1st/2nd) just left of it
            var cars = System.Array.ConvertAll(race.Racers, r => r.car);
            Minimap.Create(half, new Vector2(MapSize, MapSize), layout, cars, i, PlayerColors);
            ui.position = MakeText(half, "", 90, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-MapSize - 44, -10), new Vector2(300, 110));
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
            players[i] = ui;
        }

        resultsDim = Panel(root, "ResultsDim", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0.6f)).gameObject;
        resultsDim.transform.SetSiblingIndex(1);
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
                                 "\nP1: WASD + Left Shift drift      P2: Arrows + Right Shift drift";
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
                centerSub.text = "R / Enter = race again      N = next track";
                centerSub.rectTransform.anchorMin = centerSub.rectTransform.anchorMax = new Vector2(0.5f, 0.08f);
                winner.text = Names[race.WinnerIndex] + " WINS!";
                winner.color = PlayerColors[race.WinnerIndex];
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
        int pos = race.PositionOf(i);
        ui.position.text = pos == 1 ? "1st" : "2nd";
        ui.gems.text = "GEMS " + car.Bed.Count + "/" + GemBed.Capacity + "   VALUE " + car.Bed.Value;
        ui.speed.text = Mathf.RoundToInt(Mathf.Abs(car.Speed) * 3.6f) + " km/h";

        float fill = Mathf.Clamp01(car.DriftTime / CarController.SuperBoostTime);
        ui.driftFill.anchorMax = new Vector2(fill, 1);
        ui.driftFill.sizeDelta = Vector2.zero;
        ui.driftFillImage.color = car.DriftLevel == 2 ? new Color(1f, 0.55f, 0.1f)
                                : car.DriftLevel == 1 ? new Color(0.3f, 0.8f, 1f) : Color.white;

        // short pop-up messages
        string msg = "";
        if (Time.time - car.Bed.LastSpillTime < 1.2f) msg = "-" + car.Bed.LastSpillCount + " GEM" + (car.Bed.LastSpillCount > 1 ? "S" : "");
        else if (Time.time - car.LastBoostTime < 1f) msg = car.LastBoostLevel == 2 ? "SUPER BOOST!" : "BOOST!";
        else if (car.Bed.Full && !r.finished) msg = "BED FULL";
        ui.popup.text = race.State == RaceManager.Phase.Results ? "" : msg;
        ui.popup.color = msg.StartsWith("-") ? new Color(1f, 0.4f, 0.4f) : Color.white;

        if (race.State == RaceManager.Phase.Results)
        {
            string finish = r.dnf ? "DID NOT FINISH  (-" + RaceManager.DnfPenalty + ")"
                          : r.finishOrder == 1 ? "FINISHED 1st  (+" + RaceManager.FirstPlaceBonus + ")"
                          : "FINISHED " + (r.finishOrder == 2 ? "2nd" : r.finishOrder + "th") + "  (+0)";
            ui.result.text = Names[i] + "\n\nGems value  " + r.gemScore + "\n" + finish + "\n\nTOTAL  " + r.Total;
        }
        else if (r.finished)
        {
            ui.result.text = "FINISHED " + (r.finishOrder == 1 ? "1st!" : "2nd") + "\n" + FormatTime(r.finishTime);
        }
        else ui.result.text = "";
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
