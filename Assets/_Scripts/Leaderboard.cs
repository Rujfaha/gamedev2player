using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// End-of-race leaderboard (built in code, MenuUI style): every racer ranked by score, with car, finish place / time,
/// gem points, finish bonus and total. Rows slide in one by one and the totals count up. The best total ever scored on
/// each course is kept (PlayerPrefs) and shown, with a NEW RECORD banner when it is beaten.
/// Keys are handled by RaceManager (R / Enter again, N next course, M / Esc menu); the buttons do the same for the mouse.
/// </summary>
public class Leaderboard : MonoBehaviour
{
    const float RowHeight = 66f, RowGap = 8f, Stagger = 0.14f, SlideTime = 0.35f, CountTime = 0.7f;
    static readonly float[] ColX = { 40f, 130f, 560f, 790f, 950f, 1110f };   // rank, racer, finish, gems, bonus, total
    const float CardWidth = 1300f;

    RaceManager race;
    RectTransform[] rows;
    Text[] totals;
    CanvasGroup[] fades;
    int[] order;
    float shownAt;

    public static Leaderboard Create(Transform parent, RaceManager race, System.Func<int, string> nameOf)
    {
        var lb = new GameObject("Leaderboard").AddComponent<Leaderboard>();
        lb.transform.SetParent(parent, false);
        lb.Build(race, nameOf);
        return lb;
    }

    static string Ordinal(int n) => n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";
    static string Time(float t) => Mathf.FloorToInt(t / 60f) + ":" + (t % 60f).ToString("00.00");

    void Build(RaceManager raceManager, System.Func<int, string> nameOf)
    {
        race = raceManager;
        shownAt = UnityEngine.Time.unscaledTime;
        MenuUI.EnsureEventSystem();
        var canvas = MenuUI.CreateCanvas(transform, "LeaderboardCanvas", 30);
        MenuUI.Fill(MenuUI.Stretch(canvas, "Dim", Vector2.zero, Vector2.one), new Color(0.02f, 0.02f, 0.06f, 0.72f));

        order = race.Ranking();
        int n = order.Length;
        var winner = race.Racers[order[0]];

        // title: who won
        var title = MenuUI.Label(canvas, nameOf(order[0]) + " WINS!", 92, TextAnchor.MiddleCenter, winner.car.Tint).Italic();
        MenuUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1400f, 120f));
        var sub = MenuUI.Label(canvas, race.TrackName + "   ·   RESULTS", 30, TextAnchor.MiddleCenter, MenuUI.Muted);
        MenuUI.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(1400f, 40f));

        // card with header + rows
        float height = 70f + n * (RowHeight + RowGap) + 30f;
        var card = MenuUI.Stretch(canvas, "Card", Vector2.zero, Vector2.one);
        MenuUI.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 300f), new Vector2(CardWidth, height));
        MenuUI.Plate(card, MenuUI.Glass);
        MenuUI.Border(card, MenuUI.Line);
        string[] heads = { "#", "RACER", "FINISH", "GEMS", "BONUS", "TOTAL" };
        for (int c = 0; c < heads.Length; c++) Cell(card, heads[c], 24, ColX[c], -22f, MenuUI.Muted, c >= 3);

        rows = new RectTransform[n];
        totals = new Text[n];
        fades = new CanvasGroup[n];
        for (int k = 0; k < n; k++)
        {
            int i = order[k];
            var r = race.Racers[i];
            bool human = i < race.HumanCount;
            var row = MenuUI.Stretch(card, "Row" + k, new Vector2(0f, 1f), new Vector2(1f, 1f));
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(20f, -(70f + k * (RowHeight + RowGap) + RowHeight));
            row.offsetMax = new Vector2(-20f, -(70f + k * (RowHeight + RowGap)));
            MenuUI.Plate(row, k == 0 ? new Color(1f, 0.78f, 0.15f, 0.22f) : human ? new Color(1f, 1f, 1f, 0.12f) : new Color(1f, 1f, 1f, 0.05f));
            if (k == 0) MenuUI.Border(row, new Color(1f, 0.82f, 0.3f, 0.8f));

            // colour stripe = the car's paint
            var stripe = MenuUI.Stretch(row, "Paint", new Vector2(0f, 0f), new Vector2(0f, 1f));
            stripe.offsetMin = new Vector2(92f, 12f);
            stripe.offsetMax = new Vector2(100f, -12f);
            MenuUI.Plate(stripe, r.car.Tint);

            Cell(row, (k + 1).ToString(), 40, ColX[0] - 20f, 0f, k == 0 ? MenuUI.Accent : Color.white, false, true);
            var who = Cell(row, nameOf(i), 32, ColX[1] - 20f, 10f, human ? r.car.Tint : Color.white, false, true);
            who.fontStyle = human ? FontStyle.Bold : FontStyle.Normal;
            Cell(row, r.car.Spec != null ? r.car.Spec.name : "", 20, ColX[1] - 20f, -18f, MenuUI.Muted, false, true);
            string finish = r.dnf ? "DNF" : Ordinal(r.finishOrder) + "   " + Time(r.finishTime);
            Cell(row, finish, 28, ColX[2] - 20f, 0f, r.dnf ? new Color(1f, 0.45f, 0.4f) : Color.white, false, true);
            Cell(row, r.gemScore.ToString(), 32, ColX[3] - 20f, 0f, new Color(0.6f, 0.9f, 1f), true, true);
            string bonus = r.bonus > 0 ? "+" + r.bonus : r.bonus.ToString();
            Cell(row, bonus, 32, ColX[4] - 20f, 0f, r.bonus > 0 ? MenuUI.Green * 1.3f : r.bonus < 0 ? new Color(1f, 0.45f, 0.4f) : MenuUI.Muted, true, true);
            totals[k] = Cell(row, "0", 40, ColX[5] - 20f, 0f, k == 0 ? MenuUI.Accent : Color.white, true, true);
            rows[k] = row;
            fades[k] = row.gameObject.AddComponent<CanvasGroup>();
        }

        // course record
        string key = "gd_best_" + race.TrackName;
        int best = PlayerPrefs.GetInt(key, int.MinValue);
        string bestName = PlayerPrefs.GetString(key + "_who", "");
        string recordText;
        if (winner.Total > best)
        {
            string who = nameOf(order[0]) + " · " + (winner.car.Spec != null ? winner.car.Spec.name : "");
            PlayerPrefs.SetInt(key, winner.Total);
            PlayerPrefs.SetString(key + "_who", who);
            PlayerPrefs.Save();
            recordText = best == int.MinValue ? "FIRST COURSE RECORD:  " + winner.Total + "  (" + who + ")"
                                              : "NEW COURSE RECORD!  " + winner.Total + "  (old " + best + ")";
        }
        else recordText = "COURSE RECORD:  " + best + "  (" + bestName + ")";
        var record = MenuUI.Pill(canvas, recordText, 28, winner.Total > best ? MenuUI.Accent : MenuUI.Glass, winner.Total > best ? MenuUI.Ink : Color.white, 28, 8);
        MenuUI.FitToContent(record, true);
        MenuUI.Place(record, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 300f - height - 24f), Vector2.zero);

        // score rule + buttons
        var rule = MenuUI.Label(canvas, "Score = gem value  +" + RaceManager.FirstPlaceBonus + " for 1st place  −" + RaceManager.DnfPenalty + " if not finished",
                                22, TextAnchor.MiddleCenter, MenuUI.Muted);
        MenuUI.Place(rule.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1400f, 30f));
        var buttons = MenuUI.Group(canvas, "Buttons", false, 24, TextAnchor.MiddleCenter);
        MenuUI.Place(buttons, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(1200f, 80f));
        string scene = SceneManager.GetActiveScene().name;
        MenuUI.MakeButton(buttons, "PLAY AGAIN  (R)", 330, 70, () => SceneManager.LoadScene(scene), 28, false, true);
        MenuUI.MakeButton(buttons, "NEXT COURSE  (N)", 330, 70, () => SceneManager.LoadScene(GameBootstrap.NextScene(scene)), 28, false);
        MenuUI.MakeButton(buttons, "MENU  (M)", 260, 70, () => SceneManager.LoadScene(GameBootstrap.MenuScene), 28, false);

        Animate(0f);
    }

    /// <summary>A text cell: x from the left edge, y offset from the middle of the parent row (or from the top of the card for headers).</summary>
    Text Cell(RectTransform parent, string text, int size, float x, float y, Color color, bool right, bool inRow = false)
    {
        var t = MenuUI.Label(parent, text, size, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, color).Latin();
        var rt = t.rectTransform;
        float w = right ? 150f : 420f;
        var anchor = inRow ? new Vector2(0f, 0.5f) : new Vector2(0f, 1f);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(right ? 1f : 0f, 0.5f);
        rt.anchoredPosition = new Vector2(right ? x + w - 10f : x, inRow ? y : y - 18f);
        rt.sizeDelta = new Vector2(w, size * 1.3f);
        return t;
    }

    void Update() => Animate(UnityEngine.Time.unscaledTime - shownAt);

    void Animate(float t)
    {
        for (int k = 0; k < rows.Length; k++)
        {
            float local = Mathf.Clamp01((t - 0.3f - k * Stagger) / SlideTime);
            float e = 1f - Mathf.Pow(1f - local, 3f);
            var p = rows[k].anchoredPosition;
            rows[k].anchoredPosition = new Vector2((1f - e) * 500f, p.y);
            fades[k].alpha = e;
            float count = Mathf.Clamp01((t - 0.3f - k * Stagger - SlideTime) / CountTime);
            totals[k].text = Mathf.RoundToInt(race.Racers[order[k]].Total * count).ToString();
        }
    }
}
