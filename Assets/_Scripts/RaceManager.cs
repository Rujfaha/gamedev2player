using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Countdown → race (3 laps through checkpoints in order) → when the first truck finishes the other
/// has 10 s → results. Score = value of gems in the bed + 5 for finishing first (−3 if not finished).
/// </summary>
public class RaceManager : MonoBehaviour
{
    public const int Laps = 3;
    public const float CountdownTime = 3f, LastCallTime = 10f;
    public const int FirstPlaceBonus = 5, DnfPenalty = 3;

    public enum Phase { Countdown, Racing, LastCall, Results }

    public class Racer
    {
        public CarController car;
        public int lap;            // completed laps
        public int nextCheckpoint;
        public int passed;         // checkpoints passed in total (for positions)
        public bool finished, dnf;
        public int finishOrder;    // 1 = first
        public float finishTime;
        public int gemScore, bonus;
        public int Total => gemScore + bonus;
    }

    public static RaceManager Instance { get; private set; }
    public Phase State { get; private set; } = Phase.Countdown;
    public float CountdownLeft { get; private set; } = CountdownTime;
    public float LastCallLeft { get; private set; }
    public float RaceTime => State == Phase.Countdown ? 0f : Time.time - raceStart;
    public Racer[] Racers { get; private set; }
    public int WinnerIndex { get; private set; } = -1;
    public float GoTime { get; private set; } = -99f;

    Transform[] checkpoints;
    int finishCount;
    float raceStart, resultsTime;

    public void Init(CarController[] cars, Transform[] cps)
    {
        Instance = this;
        checkpoints = cps;
        Racers = new Racer[cars.Length];
        for (int i = 0; i < cars.Length; i++)
        {
            var r = new Racer { car = cars[i] };
            // Cars starting behind the finish line must cross it once before lap 1 counts.
            var cp0 = cps[0];
            bool behindLine = Vector3.Dot(cp0.forward, cars[i].transform.position - cp0.position) < 0f;
            r.nextCheckpoint = behindLine ? 0 : 1 % cps.Length;
            r.lap = behindLine ? -1 : 0;
            cars[i].CanDrive = false;
            Racers[i] = r;
        }
    }

    void Update()
    {
        switch (State)
        {
            case Phase.Countdown:
                CountdownLeft -= Time.deltaTime;
                if (CountdownLeft <= 0f)
                {
                    State = Phase.Racing;
                    raceStart = GoTime = Time.time;
                    foreach (var r in Racers) r.car.CanDrive = true;
                }
                break;
            case Phase.LastCall:
                LastCallLeft -= Time.deltaTime;
                if (LastCallLeft <= 0f) EndRace();
                break;
            case Phase.Results:
                var kb = Keyboard.current;
                if (Time.time - resultsTime > 1f && kb != null &&
                    (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                break;
        }
    }

    public void OnCheckpoint(CarController car, int index)
    {
        if (State != Phase.Racing && State != Phase.LastCall) return;
        var r = Racers[car.PlayerIndex];
        if (r.finished || index != r.nextCheckpoint) return;

        r.passed++;
        var cp = checkpoints[index];
        car.SetRespawn(cp.position + cp.right * (car.PlayerIndex == 0 ? -3f : 3f), cp.rotation);
        r.nextCheckpoint = (index + 1) % checkpoints.Length;
        if (index == 0)
        {
            r.lap++;
            if (r.lap >= Laps) Finish(r);
        }
    }

    /// <summary>Lap shown on the HUD, 1-based and clamped to Laps.</summary>
    public int DisplayLap(int player) => Mathf.Clamp(Racers[player].lap + 1, 1, Laps);

    /// <summary>1 = leading.</summary>
    public int PositionOf(int player)
    {
        var me = Racers[player];
        int pos = 1;
        foreach (var other in Racers)
            if (other != me && Ahead(other, me)) pos++;
        return pos;
    }

    bool Ahead(Racer a, Racer b)
    {
        if (a.finished != b.finished) return a.finished;
        if (a.finished) return a.finishOrder < b.finishOrder;
        if (a.passed != b.passed) return a.passed > b.passed;
        var target = checkpoints[a.nextCheckpoint].position;
        return (a.car.transform.position - target).sqrMagnitude < (b.car.transform.position - target).sqrMagnitude;
    }

    void Finish(Racer r)
    {
        r.finished = true;
        r.finishOrder = ++finishCount;
        r.finishTime = RaceTime;
        r.car.CanDrive = false;
        r.car.Bed.Locked = true;
        r.gemScore = r.car.Bed.Value;
        r.bonus = r.finishOrder == 1 ? FirstPlaceBonus : 0;

        if (finishCount >= Racers.Length) EndRace();
        else if (r.finishOrder == 1)
        {
            State = Phase.LastCall;
            LastCallLeft = LastCallTime;
        }
    }

    void EndRace()
    {
        foreach (var r in Racers)
        {
            if (r.finished) continue;
            r.dnf = true;
            r.car.CanDrive = false;
            r.car.Bed.Locked = true;
            r.gemScore = r.car.Bed.Value;
            r.bonus = -DnfPenalty;
        }
        // highest total wins; a tie goes to whoever finished first
        int best = 0;
        for (int i = 1; i < Racers.Length; i++)
        {
            var a = Racers[i];
            var b = Racers[best];
            if (a.Total > b.Total || (a.Total == b.Total && Ahead(a, b))) best = i;
        }
        WinnerIndex = best;
        State = Phase.Results;
        resultsTime = Time.time;
    }
}
