using UnityEngine;

/// <summary>
/// Drives a truck like a player would: steers at the next checkpoint (blending toward the one after it as it
/// gets close), brakes early enough to take the next corner, drifts through bends for a boost, detours for gems
/// near its path and backs up when stuck. Three whisker rays steer it away from walls and other trucks.
/// It only fills CarController.AIMove / AIDrift; all driving physics stay in CarController.
/// </summary>
[RequireComponent(typeof(CarController))]
public class CarAI : MonoBehaviour
{
    const float LookAhead = 14f;                  // m from a checkpoint where the aim point starts sliding to the next one
    const float GemRange = 16f, GemPathWidth = 4f; // gems this close to the path (and ahead) are worth a detour
    const float StuckSpeed = 1.5f, StuckTime = 1.2f, ReverseTime = 1f, NoProgressRespawn = 10f;
    const float WhiskerLength = 12f, WhiskerAngle = 30f;
    const int ObstacleMask = (1 << Layers.Wall) | (1 << Layers.Car);
    // Blocking is occasional: the AI races for the finish, but now and then, when a truck (player or AI) is
    // right behind trying to pass, it slides into that truck's line for a couple of seconds, then cools down.
    const float BlockBehind = 16f, BlockSide = 7f, BlockMaxShift = 4.5f;
    const float BlockChance = 0.3f, BlockDuration = 2.5f, BlockCooldownMin = 8f, BlockCooldownMax = 15f;

    CarController car;
    BoxCollider box;
    GemPickup gem;
    float speedFactor, laneOffset;                // per-AI skill and where on the road it likes to drive
    float aggression;                             // 0..1: how hard this AI blocks (each truck differs)
    float blockShift;                             // current sideways shift toward the truck being blocked (m)
    CarController blockTarget, lastRolled;        // who we're blocking / who we already rolled the dice for
    float blockUntil, nextBlockAllowed, lastPasserSeen;
    float stuckTimer, reverseUntil, noGemUntil, nextGemScan, driftReadyAt, progressAt;
    int lastPassed;

    void Awake()
    {
        car = GetComponent<CarController>();
        box = GetComponent<BoxCollider>();
        speedFactor = Random.Range(0.94f, 1f);
        laneOffset = Random.Range(-1.5f, 1.5f);
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
        float bend = Vector3.Angle(Flat(a.position - pos), Flat(b.position - a.position)); // how sharp the road turns ahead
        float sharpBend = Mathf.InverseLerp(20f, 80f, bend);
        // keep its lane on straights, but move to the middle for sharp corners so it doesn't clip the inside wall
        Vector3 aim = Vector3.Lerp(a.position, b.position, 1f - Mathf.Clamp01(dist / LookAhead)) + a.right * laneOffset * (1f - sharpBend);

        // Occasionally block a truck that's right behind and trying to pass. One dice roll per encounter,
        // a short block, then a long cooldown, so most of the time the AI just races for the finish.
        var passer = FindPasser(race, pos);
        if (passer != null) lastPasserSeen = Time.time;
        else if (Time.time - lastPasserSeen > 2f) lastRolled = null;
        if (blockTarget == null && passer != null && passer != lastRolled && Time.time >= nextBlockAllowed)
        {
            lastRolled = passer;
            if (Random.value < BlockChance * aggression) { blockTarget = passer; blockUntil = Time.time + BlockDuration; }
        }
        if (blockTarget != null && (Time.time > blockUntil || passer != blockTarget))
        {
            blockTarget = null;
            nextBlockAllowed = Time.time + Random.Range(BlockCooldownMin, BlockCooldownMax);
        }
        float wantShift = 0f;
        if (blockTarget != null && bend < 45f)
        {
            float side = Vector3.Dot(blockTarget.transform.position - pos, transform.right);
            wantShift = Mathf.Clamp(side, -BlockMaxShift, BlockMaxShift) * aggression;
        }
        // the shift eases in (reaction time), so a quick move the other way still gets past
        blockShift = Mathf.MoveTowards(blockShift, wantShift, 3.5f * aggression * Time.fixedDeltaTime);
        aim += transform.right * blockShift;
        bool blocking = blockTarget != null;

        // Detour for a gem near the path (not while blocking)
        if (Time.time >= nextGemScan) { nextGemScan = Time.time + 0.25f; gem = blocking ? null : FindGem(pos, aim); }
        if (gem != null && gem.Available && !car.Bed.Full) aim = gem.transform.position;

        float angle = Vector3.SignedAngle(fwd, Flat(aim - pos), Vector3.up);
        float steer = Mathf.Clamp(angle / 20f, -1f, 1f);

        // Whiskers: steer away from whatever is close on either side, and see how much room is straight ahead
        float left = Whisker(-WhiskerAngle), right = Whisker(WhiskerAngle), ahead = Whisker(0f);
        steer = Mathf.Clamp(steer - (left - right) * 1.2f, -1f, 1f);   // closer on the right (right < left) pushes left
        if (ahead < 0.35f) steer = Mathf.Clamp(steer + (left > right ? -0.8f : 0.8f), -1f, 1f); // wall dead ahead: turn to the open side

        // Speed: the corner speed for the bend ahead, braking early enough to reach it (v² = vc² + 2·a·d),
        // also capped by how far off-aim we are right now and by an obstacle close ahead
        float top = car.maxSpeed * car.Bed.WeightFactor * speedFactor
                  * (car.ActiveNitroLevel == 2 ? car.nitroSpeedMultiplier : car.ActiveNitroLevel == 1 ? car.boostSpeedMultiplier : 1f);
        float cornerSpeed = top * Mathf.Lerp(1f, 0.5f, sharpBend);
        float target = Mathf.Min(top, Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * car.brakeDecel * 0.6f * dist));
        target = Mathf.Min(target, top * Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(20f, 75f, Mathf.Abs(angle))));
        target = Mathf.Min(target, Mathf.Lerp(car.minDriftSpeed, top, ahead));
        float throttle = Mathf.Clamp((target - car.Speed) / 3f, -1f, 1f);

        // Drift through a bend (steering the same way) for about a second; drifting fills the nitro tank.
        // Only start one with room to slide

        bool drift = car.Drifting ? car.DriftTime < 1f && Mathf.Abs(angle) > 10f
                                  : Mathf.Abs(angle) > 30f && car.Speed > car.minDriftSpeed + 3f && Time.time > driftReadyAt
                                    && Mathf.Min(left, right, ahead) > 0.5f;
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

        // Nitro: wait for a full tank (red), fire it on a long clear straight
        if (car.NitroReady && !drift && Mathf.Abs(angle) < 8f && bend < 15f && dist > 25f && ahead > 0.9f && Time.time >= reverseUntil) car.TryFireNitro();

        car.AIMove = new Vector2(steer, throttle);
        car.AIDrift = drift;
    }

    /// <summary>The nearest truck (player or AI) close behind us that is catching up, i.e. about to overtake.</summary>
    CarController FindPasser(RaceManager race, Vector3 pos)
    {
        CarController best = null;
        float bestDist = BlockBehind;
        foreach (var r in race.Racers)
        {
            var other = r.car;
            if (other == car || r.finished) continue;
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

    /// <summary>The most valuable nearby gem that is roughly ahead, close to the line from here to the aim point, and not behind a wall.</summary>
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
            if (score <= bestScore) continue;
            if (Physics.Linecast(pos + Vector3.up, g.transform.position + Vector3.up * 0.5f, 1 << Layers.Wall, QueryTriggerInteraction.Ignore)) continue;
            bestScore = score; best = g;
        }
        return best;
    }

    /// <summary>Free distance along a ray from the truck's nose, turned by yaw degrees: 1 = clear, 0 = touching.</summary>
    float Whisker(float yaw)
    {
        Vector3 fwd = transform.forward;
        Vector3 nose = transform.TransformPoint(box.center) + fwd * (box.size.z * 0.5f + 0.1f); // start outside our own collider
        float len = yaw == 0f ? WhiskerLength * 1.5f : WhiskerLength;
        return Physics.Raycast(nose, Quaternion.Euler(0f, yaw, 0f) * fwd, out var hit, len, ObstacleMask, QueryTriggerInteraction.Ignore)
            ? hit.distance / len : 1f;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
