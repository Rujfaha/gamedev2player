using UnityEngine;

/// <summary>
/// Arcade truck physics: throttle/brake/steer, hold drift to slide.
/// Drifting fills the NITRO tank. Double-tap the drift key to fire it:
///   tank half full or more → BLUE nitro (uses half the tank), tank full → RED nitro (uses all of it).
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

    [Header("Drift")]
    public float minDriftSpeed = 9f;
    public const float SpillDriftTime = 2f;

    [Header("Nitro (charged by drifting, fired by double-tapping drift)")]
    public float nitroChargeSeconds = 3f;     // seconds of drifting to fill the tank
    public float nitroDuration = 2.5f;        // RED: full tank
    public float nitroSpeedMultiplier = 1.45f;
    public float blueDuration = 1.3f;         // BLUE: half tank
    public float boostSpeedMultiplier = 1.25f;
    public const float DoubleTapWindow = 0.3f;

    [Header("Crashes (ramming a car: see GemDriftConfig)")]
    public float wallCrashImpact = 12f;

    public int PlayerIndex { get; private set; }
    public Color Tint { get; private set; }   // body colour, also used for the HUD and minimap
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
    public bool Boosting => nitroTimer > 0f;
    public float Nitro { get; private set; }          // 0..1
    public bool BlueReady => Nitro >= 0.5f;           // half tank
    public bool NitroReady => Nitro >= 1f;            // full tank
    public bool NitroActive => nitroTimer > 0f;
    /// <summary>0 = none, 1 = blue (half tank), 2 = red (full tank).</summary>
    public int ActiveNitroLevel => nitroTimer > 0f ? nitroLevel : 0;
    public float LastNitroTime { get; private set; } = -99f;
    public int LastNitroLevel { get; private set; }

    PlayerControls controls;   // null for AI trucks
    GemDriftConfig cfg;
    Rigidbody rb;
    Transform body;
    TrailRenderer[] skids;
    ParticleSystem sparks;
    bool drifting;
    float driftDir, driftTime, nextSpillAt, crashCooldownUntil, steerVisual;
    float nitroTimer, lastDriftTap = -99f;
    int nitroLevel;
    Vector3 respawnPos;
    Quaternion respawnRot;

    /// <summary>Humans first (index 0, 1), then the AI trucks (ai = true). tint overrides the body colour from GemDriftConfig.</summary>
    public static CarController Create(GemDriftConfig cfg, int index, Vector3 pos, Quaternion rot, bool ai = false, Color? tint = null)
    {
        var root = new GameObject(ai ? "Car_AI" + index : "Car_P" + (index + 1)) { layer = Layers.Car };
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
        var bodyTint = tint ?? (ai ? cfg.aiTints[(index - 2) % cfg.aiTints.Length] : cfg.playerTints[index]);
        block.SetColor("_Color", bodyTint);
        foreach (var r in model.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);

        var car = root.AddComponent<CarController>();
        car.Tint = bodyTint;
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
        controls = ai ? null : new PlayerControls(index, MatchSettings.Humans == 1);
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

        gameObject.AddComponent<CarEffects>().Setup(cfg, this, body);
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
        nitroTimer = 0f;
        foreach (var s in skids) s.Clear();
    }

    void Update()
    {
        if (Time.timeScale == 0f) return; // paused
        if (CanDrive && !IsAI && controls.ResetPressed) Respawn();

        // double-tap drift = NITRO: red with a full tank, blue with half (AI trucks call TryFireNitro instead)
        if (CanDrive && !IsAI && controls.DriftPressed)
        {
            if (Time.time - lastDriftTap <= DoubleTapWindow) TryFireNitro();
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
        emission.rateOverTime = NitroActive ? 100f : drifting ? 35f : 0f;
        main.startColor = ActiveNitroLevel == 2 ? new Color(1f, 0.5f, 0.15f)
                        : ActiveNitroLevel == 1 ? new Color(0.3f, 0.8f, 1f)
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

        nitroTimer = Mathf.Max(0f, nitroTimer - dt);
        float speedMult = ActiveNitroLevel == 2 ? nitroSpeedMultiplier : ActiveNitroLevel == 1 ? boostSpeedMultiplier : 1f;
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
            drifting = false;
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
        float accel = acceleration * (ActiveNitroLevel == 2 ? 2f : ActiveNitroLevel == 1 ? 1.6f : 1f);
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

    /// <summary>Fires RED nitro with a full tank, else BLUE with at least half. Players double-tap; CarAI calls this.</summary>
    public bool TryFireNitro()
    {
        if (!CanDrive || NitroActive || !BlueReady) return false;
        bool red = NitroReady;
        nitroLevel = red ? 2 : 1;
        Nitro = red ? 0f : Nitro - 0.5f;
        nitroTimer = red ? nitroDuration : blueDuration;
        rb.linearVelocity += transform.forward * (red ? 6f : 3.5f);
        LastNitroTime = Time.time;
        LastNitroLevel = nitroLevel;
        return true;
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
