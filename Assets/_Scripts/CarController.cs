using UnityEngine;

/// <summary>
/// Arcade truck physics: throttle/brake/steer, hold drift to slide, release for a boost.
/// Drifting also fills the NITRO tank; when it's full, double-tap the drift key to fire it.
/// Same code for both players; only the input bindings (PlayerControls) differ.
/// An AI truck uses the same physics: CarAI just fills AIMove / AIDrift instead of the keyboard.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Speed (m/s)")]
    public float maxSpeed = 26f;
    public float acceleration = 16f;
    public float brakeDecel = 32f;
    public float reverseMax = 8f;
    public float coastDecel = 5f;

    [Header("Steering (deg/s)")]
    public float turnRate = 115f;
    public float driftTurnRate = 105f;

    [Header("Grip (higher = less sliding)")]
    public float grip = 10f;
    public float driftGrip = 1.8f;

    [Header("Drift / boost")]
    public float minDriftSpeed = 9f;
    public float boostSpeedMultiplier = 1.35f;
    public const float MiniBoostTime = 0.8f, SuperBoostTime = 1.6f, SpillDriftTime = 2f;

    [Header("Nitro (charged by drifting, fired by double-tapping drift)")]
    public float nitroChargeSeconds = 3f;     // seconds of drifting to fill the tank
    public float nitroDuration = 2.5f;
    public float nitroSpeedMultiplier = 1.45f;
    public const float DoubleTapWindow = 0.3f;

    [Header("Crashes (ramming a car: see GemDriftConfig)")]
    public float wallCrashImpact = 12f;

    public int PlayerIndex { get; private set; }
    public bool CanDrive { get; set; }
    public bool IsAI => controls == null;
    /// <summary>Input for AI trucks (x = steer, y = throttle), written by CarAI.</summary>
    public Vector2 AIMove { get; set; }
    public bool AIDrift { get; set; }
    public GemBed Bed { get; private set; }
    public float Speed => Vector3.Dot(rb.linearVelocity, transform.forward);
    public bool Drifting => drifting;
    public float DriftTime => drifting ? driftTime : 0f;
    /// <summary>Velocity going into the latest physics step, i.e. before any collision changed it.</summary>
    public Vector3 PreImpactVelocity { get; private set; }
    public int DriftLevel => !drifting ? 0 : driftTime >= SuperBoostTime ? 2 : driftTime >= MiniBoostTime ? 1 : 0;
    public bool Boosting => boostTimer > 0f || nitroTimer > 0f;
    public float Nitro { get; private set; }          // 0..1
    public bool NitroReady => Nitro >= 1f;
    public bool NitroActive => nitroTimer > 0f;
    public float LastNitroTime { get; private set; } = -99f;
    public float LastBoostTime { get; private set; } = -99f;
    public int LastBoostLevel { get; private set; }

    PlayerControls controls;   // null for AI trucks
    GemDriftConfig cfg;
    Rigidbody rb;
    Transform body;
    TrailRenderer[] skids;
    ParticleSystem sparks;
    bool drifting;
    float driftDir, driftTime, nextSpillAt, boostTimer, crashCooldownUntil, steerVisual;
    float nitroTimer, lastDriftTap = -99f;
    Vector3 respawnPos;
    Quaternion respawnRot;

    /// <summary>Players are index 0 and 1. AI trucks (ai = true) use index 2 and up.</summary>
    public static CarController Create(GemDriftConfig cfg, int index, Vector3 pos, Quaternion rot, bool ai = false)
    {
        var root = new GameObject(ai ? "Car_AI" + (index - 1) : "Car_P" + (index + 1)) { layer = Layers.Car };
        root.transform.SetPositionAndRotation(pos, rot);

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.angularDamping = 3f;

        var size = cfg.carSize;
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, size.y * 0.35f, 0);
        col.size = new Vector3(size.x * 0.95f, size.y * 0.7f, size.z * 0.95f);
        col.material = new PhysicsMaterial("CarSlide")
        {
            dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum,
            bounciness = 0.2f, bounceCombine = PhysicsMaterialCombine.Maximum,
        };
        rb.centerOfMass = new Vector3(0, size.y * 0.2f, 0);

        var body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);
        var model = Instantiate(cfg.carModel, body);
        model.transform.localRotation = Quaternion.Euler(0, cfg.modelYaw, 0);
        model.transform.localScale = Vector3.one * cfg.modelScale;
        model.transform.localPosition = cfg.modelOffset;
        foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
        var block = new MaterialPropertyBlock();
        block.SetColor("_Color", ai ? cfg.aiTints[(index - 2) % cfg.aiTints.Length] : cfg.playerTints[index]);
        foreach (var r in model.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);

        var car = root.AddComponent<CarController>();
        car.Setup(cfg, index, body, ai);
        if (ai) root.AddComponent<CarAI>();
        return car;
    }

    void Setup(GemDriftConfig cfg, int index, Transform bodyTransform, bool ai)
    {
        this.cfg = cfg;
        PlayerIndex = index;
        rb = GetComponent<Rigidbody>();
        body = bodyTransform;
        controls = ai ? null : new PlayerControls(index);
        Bed = gameObject.AddComponent<GemBed>();
        Bed.Setup(cfg, body, this);
        SetRespawn(transform.position, transform.rotation);

        var s = cfg.carSize;
        skids = new TrailRenderer[2];
        for (int i = 0; i < 2; i++)
        {
            var t = new GameObject("Skid" + i).transform;
            t.SetParent(transform, false);
            t.localPosition = new Vector3((i == 0 ? -1 : 1) * s.x * 0.38f, 0.04f, -s.z * 0.33f);
            t.localRotation = Quaternion.Euler(90, 0, 0); // lay the trail flat on the road
            var tr = t.gameObject.AddComponent<TrailRenderer>();
            tr.time = 1.6f;
            tr.widthMultiplier = 0.35f;
            tr.minVertexDistance = 0.2f;
            tr.alignment = LineAlignment.TransformZ;
            tr.material = cfg.skid;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.emitting = false;
            skids[i] = tr;
        }

        var sp = new GameObject("DriftSparks").transform;
        sp.SetParent(transform, false);
        sp.localPosition = new Vector3(0, 0.2f, -s.z * 0.5f);
        sp.localRotation = Quaternion.Euler(-20, 180, 0);
        sparks = sp.gameObject.AddComponent<ParticleSystem>();
        var main = sparks.main;
        main.startLifetime = 0.35f;
        main.startSpeed = 5f;
        main.startSize = 0.22f;
        main.gravityModifier = 1.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = s.x * 0.4f;
        var emission = sparks.emission;
        emission.rateOverTime = 0f;
        sparks.GetComponent<ParticleSystemRenderer>().material = cfg.spark;
    }

    public void SetRespawn(Vector3 pos, Quaternion rot)
    {
        respawnPos = pos;
        respawnRot = rot;
    }

    public void Respawn()
    {
        rb.position = respawnPos + Vector3.up * 0.3f;
        rb.rotation = respawnRot;
        transform.SetPositionAndRotation(rb.position, rb.rotation);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        drifting = false;
        boostTimer = 0f;
        nitroTimer = 0f;
        foreach (var s in skids) s.Clear();
    }

    void Update()
    {
        if (CanDrive && !IsAI && controls.ResetPressed) Respawn();

        // double-tap drift with a full tank = NITRO (AI trucks call TryFireNitro instead)
        if (CanDrive && !IsAI && controls.DriftPressed)
        {
            if (Time.time - lastDriftTap <= DoubleTapWindow && NitroReady) FireNitro();
            lastDriftTap = Time.time;
        }
        if (transform.position.y < -5f) Respawn();

        // Visuals: lean into the slide, skid marks, sparks coloured by boost level
        float lat = Vector3.Dot(rb.linearVelocity, transform.right);
        steerVisual = Mathf.Lerp(steerVisual, drifting ? driftDir * 12f : 0f, 1f - Mathf.Exp(-8f * Time.deltaTime));
        body.localRotation = Quaternion.Euler(0, steerVisual, Mathf.Clamp(-lat * 0.6f, -6f, 6f));

        foreach (var s in skids) s.emitting = drifting;

        var emission = sparks.emission;
        var main = sparks.main;
        int level = DriftLevel;
        emission.rateOverTime = NitroActive ? 140f : drifting ? (level == 0 ? 15f : 60f) : (Boosting ? 30f : 0f);
        main.startColor = NitroActive ? new Color(0.75f, 0.35f, 1f)
                        : level == 2 || (Boosting && LastBoostLevel == 2) ? new Color(1f, 0.55f, 0.1f)
                        : level == 1 || Boosting ? new Color(0.3f, 0.8f, 1f)
                        : new Color(1f, 1f, 0.85f);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 input = !CanDrive ? Vector2.zero : IsAI ? AIMove : controls.Move;
        bool driftHeld = CanDrive && (IsAI ? AIDrift : controls.DriftHeld);
        float throttle = input.y, steer = input.x;

        Vector3 v = rb.linearVelocity;
        Vector3 fwd = transform.forward, right = transform.right;
        float fwdSpeed = Vector3.Dot(v, fwd);
        float latSpeed = Vector3.Dot(v, right);

        boostTimer = Mathf.Max(0f, boostTimer - dt);
        nitroTimer = Mathf.Max(0f, nitroTimer - dt);
        float speedMult = NitroActive ? nitroSpeedMultiplier : boostTimer > 0f ? boostSpeedMultiplier : 1f;
        float top = maxSpeed * Bed.WeightFactor * speedMult;

        // --- drift state ---
        if (!drifting && driftHeld && Mathf.Abs(steer) > 0.2f && fwdSpeed > minDriftSpeed)
        {
            drifting = true;
            driftDir = Mathf.Sign(steer);
            driftTime = 0f;
            nextSpillAt = SpillDriftTime;
        }
        else if (drifting && (!driftHeld || fwdSpeed < minDriftSpeed * 0.6f))
        {
            EndDrift(!driftHeld);
        }
        if (drifting)
        {
            driftTime += dt;
            if (!NitroActive) Nitro = Mathf.Min(1f, Nitro + dt / nitroChargeSeconds);
            if (driftTime >= nextSpillAt) // drifting too long shakes a gem loose
            {
                Bed.Spill(1);
                nextSpillAt += 1.5f;
            }
        }

        // --- forward / back ---
        float accel = acceleration * (NitroActive ? 2f : Boosting ? 1.6f : 1f);
        if (throttle > 0.1f)
        {
            if (fwdSpeed < top) fwdSpeed = Mathf.Min(top, fwdSpeed + accel * throttle * dt);
        }
        else if (throttle < -0.1f)
        {
            fwdSpeed = fwdSpeed > 0.5f
                ? fwdSpeed - brakeDecel * -throttle * dt
                : Mathf.Max(-reverseMax, fwdSpeed - acceleration * 0.6f * -throttle * dt);
        }
        else
        {
            fwdSpeed = Mathf.MoveTowards(fwdSpeed, 0f, coastDecel * dt);
        }
        if (fwdSpeed > top) fwdSpeed = Mathf.MoveTowards(fwdSpeed, top, 10f * dt);

        // --- sideways grip: low grip while drifting = the truck slides ---
        latSpeed *= Mathf.Exp(-(drifting ? driftGrip : grip) * dt);
        rb.linearVelocity = fwd * fwdSpeed + right * latSpeed + Vector3.up * v.y;

        // --- steering ---
        float yawRate;
        if (drifting)
        {
            // always turning into the drift; steer adjusts how tight (0.45×..1.15×)
            float tight = Mathf.Lerp(0.45f, 1.15f, (steer * driftDir + 1f) * 0.5f);
            yawRate = driftDir * driftTurnRate * tight;
        }
        else
        {
            float speedFactor = Mathf.Clamp01(Mathf.Abs(fwdSpeed) / 5f);
            float highSpeedDamp = Mathf.Lerp(1f, 0.75f, Mathf.Abs(fwdSpeed) / maxSpeed);
            yawRate = steer * turnRate * speedFactor * highSpeedDamp * (fwdSpeed < -0.1f ? -1f : 1f);
        }
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, yawRate * dt, 0f));
        rb.angularVelocity = new Vector3(0f, rb.angularVelocity.y * 0.85f, 0f);
        PreImpactVelocity = rb.linearVelocity; // collisions resolve after FixedUpdate, so this is the pre-hit velocity
    }

    void EndDrift(bool released)
    {
        drifting = false;
        if (!released) return;
        if (driftTime >= SuperBoostTime) Boost(2, 1.4f);
        else if (driftTime >= MiniBoostTime) Boost(1, 0.7f);
    }

    void Boost(int level, float duration)
    {
        boostTimer = Mathf.Max(boostTimer, duration);
        rb.linearVelocity += transform.forward * (level == 2 ? 5f : 3f);
        LastBoostTime = Time.time;
        LastBoostLevel = level;
    }

    /// <summary>For CarAI: fires the nitro if the tank is full.</summary>
    public bool TryFireNitro()
    {
        if (!CanDrive || !NitroReady || NitroActive) return false;
        FireNitro();
        return true;
    }

    void FireNitro()
    {
        Nitro = 0f;
        nitroTimer = nitroDuration;
        rb.linearVelocity += transform.forward * 6f;
        LastNitroTime = Time.time;
    }

    void OnCollisionEnter(Collision c)
    {
        if (Time.time < crashCooldownUntil || c.contactCount == 0) return;
        var other = c.rigidbody != null ? c.rigidbody.GetComponent<CarController>() : null;
        if (other != null)
        {
            // Ramming: only the truck that was driven into drops gems. Both trucks run this, and the
            // one whose pre-hit velocity points more toward the other is the attacker, so it returns.
            Vector3 toMe = Vector3.ProjectOnPlane(transform.position - other.transform.position, Vector3.up).normalized;
            float theirPush = Vector3.Dot(other.PreImpactVelocity, toMe);
            float myPush = Vector3.Dot(PreImpactVelocity, -toMe);
            if (theirPush + myPush < cfg.crashMinSpeed || theirPush <= myPush) return;
            Bed.Spill(Random.Range(cfg.crashDropMin, cfg.crashDropMax + 1), toMe);
        }
        else
        {
            float impact = Mathf.Abs(Vector3.Dot(c.relativeVelocity, c.GetContact(0).normal));
            if (c.gameObject.layer != Layers.Wall || impact <= wallCrashImpact) return;
            Bed.Spill(1);
        }
        crashCooldownUntil = Time.time + cfg.crashCooldown;
    }

    void OnDestroy() => controls?.Dispose();
}
