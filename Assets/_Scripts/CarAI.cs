using UnityEngine;

/// <summary>
/// Drives a truck like a player would: steers at the next checkpoint (blending toward the one after it as it
/// gets close), slows for sharp turns, drifts through bends for a boost, detours for gems near its path and
/// backs up when stuck. It only fills CarController.AIMove / AIDrift; all driving physics stay in CarController.
/// </summary>
[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    const float LookAhead = 14f;                  // m from a checkpoint where the aim point starts sliding to the next one
    const float GemRange = 16f, GemPathWidth = 4f; // gems this close to the path (and ahead) are worth a detour
    const float StuckSpeed = 1.5f, StuckTime = 1.2f, ReverseTime = 1f, NoProgressRespawn = 12f;
    // Blocking: a player this close behind (and trying to pass) makes the AI slide over into their line
    const float BlockBehind = 16f, BlockSide = 7f, BlockMaxShift = 4.5f;

    CarController car;
    GemPickup gem;
    float speedFactor, laneOffset;                // per-AI skill and where on the road it likes to drive
    float aggression;                             // 0..1: how hard this AI blocks (each truck differs)
    float blockShift;                             // current sideways shift toward the passing player (m)
    float stuckTimer, reverseUntil, noGemUntil, nextGemScan, driftReadyAt, progressAt;
    int lastPassed;

    void Awake()
    {
        car = GetComponent<CarController>();
        speedFactor = Random.Range(0.88f, 0.97f);
        laneOffset = Random.Range(-3f, 3f);
        aggression = Random.Range(0.55f, 1f);
    }

    void FixedUpdate()
    {
        var race = RaceManager.Instance;
        if (race == null || !car.CanDrive) { progressAt = Time.time; stuckTimer = 0f; return; }

        var racer = race.Racers[car.PlayerIndex];
        var cps = race.Checkpoints;
        Transform a = cps[racer.nextCheckpoint], b = cps[(racer.nextCheckpoint + 1) % cps.Length];
        Vector3 pos = transform.position, fwd = Flat(transform.forward);

        // Aim at the next checkpoint, sliding toward the one after it as we get close (look-ahead)
        float dist = Flat(a.position - pos).magnitude;
        Vector3 aim = Vector3.Lerp(a.position, b.position, 1f - Mathf.Clamp01(dist / LookAhead)) + a.right * laneOffset;
        float bend = Vector3.Angle(Flat(a.position - pos), Flat(b.position - a.position)); // how sharp the road turns ahead

        // Block a player who is right behind and trying to pass: drift over into their line.
        // The shift eases in (reaction time) so a quick move the other way still gets past.
        float wantShift = 0f;
        var passer = FindPasser(race, pos);
        if (passer != null && bend < 45f)
        {
            float side = Vector3.Dot(passer.transform.position - pos, transform.right);
            wantShift = Mathf.Clamp(side, -BlockMaxShift, BlockMaxShift) * aggression;
        }
        blockShift = Mathf.MoveTowards(blockShift, wantShift, 3.5f * aggression * Time.fixedDeltaTime);
        aim += transform.right * blockShift;
        bool blocking = passer != null;

        // Detour for a gem near the path (not while blocking)
        if (Time.time >= nextGemScan) { nextGemScan = Time.time + 0.25f; gem = blocking ? null : FindGem(pos, aim); }
        if (gem != null && gem.Available && !car.Bed.Full) aim = gem.transform.position;

        float angle = Vector3.SignedAngle(fwd, Flat(aim - pos), Vector3.up);
        float steer = Mathf.Clamp(angle / 25f, -1f, 1f);

        // Speed: full speed on straights, down to half through sharp turns; the throttle value brakes when too fast
        float top = car.maxSpeed * car.Bed.WeightFactor * (car.ActiveNitroLevel == 2 ? car.nitroSpeedMultiplier : car.ActiveNitroLevel == 1 ? car.boostSpeedMultiplier : 1f);
        float sharp = Mathf.InverseLerp(20f, 70f, Mathf.Max(Mathf.Abs(angle), bend));
        float target = top * speedFactor * Mathf.Lerp(1f, 0.5f, sharp);
        float throttle = Mathf.Clamp((target - car.Speed) / 4f, -1f, 1f);

        // Drift through a bend (steering the same way) for about a second; drifting fills the nitro tank
        bool drift = car.Drifting ? car.DriftTime < 1f && Mathf.Abs(angle) > 10f
                                  : Mathf.Abs(angle) > 30f && car.Speed > car.minDriftSpeed + 3f && Time.time > driftReadyAt;
        if (car.Drifting) driftReadyAt = Time.time + 2f;

        // Stuck (against a wall, another truck...): reverse for a moment, steering the nose back toward the aim
        if (Time.time < reverseUntil)
        {
            throttle = -1f;
            steer = -Mathf.Sign(angle);
            drift = false;
        }
        else
        {
            stuckTimer = Mathf.Abs(car.Speed) < StuckSpeed ? stuckTimer + Time.fixedDeltaTime : 0f;
            if (stuckTimer > StuckTime)
            {
                stuckTimer = 0f;
                reverseUntil = Time.time + ReverseTime;
                noGemUntil = Time.time + 4f; // the gem may be the thing it is stuck on
            }
        }

        // Last resort: no checkpoint progress for a long time, so go back to the last checkpoint
        if (racer.passed != lastPassed || racer.finished) { lastPassed = racer.passed; progressAt = Time.time; }
        else if (Time.time - progressAt > NoProgressRespawn) { car.Respawn(); progressAt = Time.time; }

        // Nitro: wait for a full tank (red), fire it on a straight
        if (car.NitroReady && !drift && Mathf.Abs(angle) < 8f && bend < 15f && Time.time >= reverseUntil) car.TryFireNitro();

        car.AIMove = new Vector2(steer, throttle);
        car.AIDrift = drift;
    }

    /// <summary>The nearest player truck close behind us that is catching up (i.e. about to overtake).</summary>
    CarController FindPasser(RaceManager race, Vector3 pos)
    {
        CarController best = null;
        float bestDist = BlockBehind;
        foreach (var r in race.Racers)
        {
            var other = r.car;
            if (other == car || other.IsAI || r.finished) continue;
            Vector3 d = Flat(other.transform.position - pos);
            float behind = -Vector3.Dot(d, Flat(transform.forward));
            float side = Mathf.Abs(Vector3.Dot(d, transform.right));
            bool sameWay = Vector3.Dot(other.transform.forward, transform.forward) > 0.6f;
            bool catchingUp = other.Speed > car.Speed - 1f;
            if (behind > 1f && behind < bestDist && side < BlockSide && sameWay && catchingUp)
            {
                bestDist = behind;
                best = other;
            }
        }
        return best;
    }

    /// <summary>The most valuable nearby gem that is roughly ahead and close to the line from here to the aim point.</summary>
    GemPickup FindGem(Vector3 pos, Vector3 aim)
    {
        if (car.Bed.Full || Time.time < noGemUntil) return null;
        Vector3 path = Flat(aim - pos).normalized;
        GemPickup best = null;
        float bestScore = float.MinValue;
        foreach (var g in GemPickup.All)
        {
            if (!g.Available) continue;
            Vector3 d = Flat(g.transform.position - pos);
            float along = Vector3.Dot(d, path);
            if (d.magnitude > GemRange || along < 2f || Vector3.Cross(path, d).magnitude > GemPathWidth) continue;
            float score = g.Value * 4f - d.magnitude;
            if (score > bestScore) { bestScore = score; best = g; }
        }
        return best;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
