using UnityEngine;

/// <summary>
/// Exhaust fire and the little nitro tank on the truck bed.
///   RED nitro (full tank) → big red/orange fire from both exhausts.
///   BLUE nitro (half tank) → blue fire.
///   The tank's glowing core fills with the nitro meter and flashes when it's ready.
/// </summary>
public class CarEffects : MonoBehaviour
{
    enum Fire { None, Blue, Red }

    static Gradient redFire, blueFire;

    CarController car;
    ParticleSystem[] exhausts;
    Fire current = Fire.None;
    Transform tankCore;
    Renderer tankCoreRenderer;
    MaterialPropertyBlock block;
    float coreLength;

    public void Setup(GemDriftConfig cfg, CarController owner, Transform body)
    {
        car = owner;
        block = new MaterialPropertyBlock();
        BuildGradients();
        var s = cfg.carSize;

        // two exhaust pipes, low at the back, flames pointing backwards
        exhausts = new ParticleSystem[2];
        for (int i = 0; i < 2; i++)
        {
            var t = new GameObject("ExhaustFire" + i).transform;
            t.SetParent(body, false);
            t.localPosition = new Vector3((i == 0 ? -1f : 1f) * s.x * 0.28f, s.y * 0.22f, -s.z * 0.5f - 0.05f);
            t.localRotation = Quaternion.Euler(0f, 180f, 0f);
            exhausts[i] = Flame(t, cfg.flame);
        }

        // little nitro tank lying across the front of the bed
        if (cfg.nitroTankShell != null && cfg.nitroGlow != null)
        {
            float len = cfg.bedSize.x * 0.85f, radius = 0.17f;
            var tank = new GameObject("NitroTank").transform;
            tank.SetParent(body, false);
            tank.localPosition = cfg.bedCenter + new Vector3(0f, radius + 0.03f, cfg.bedSize.y * 0.5f + 0.05f);
            tank.localRotation = Quaternion.Euler(0f, 0f, 90f); // cylinder axis → across the truck

            var shell = Cylinder(tank, "Shell", cfg.nitroTankShell);
            shell.localScale = new Vector3(radius * 1.75f, len * 0.5f, radius * 1.75f); // dark "empty" glass, thinner than the core
            foreach (int end in new[] { -1, 1 }) // caps
            {
                var cap = Cylinder(tank, "Cap", cfg.nitroTankShell);
                cap.localScale = new Vector3(radius * 2.3f, 0.04f, radius * 2.3f);
                cap.localPosition = new Vector3(0f, end * len * 0.5f, 0f);
            }
            coreLength = len * 0.9f;
            tankCore = Cylinder(tank, "Core", cfg.nitroGlow);
            tankCoreRenderer = tankCore.GetComponent<Renderer>();
        }
    }

    void Update()
    {
        var want = car.ActiveNitroLevel == 2 ? Fire.Red : car.ActiveNitroLevel == 1 ? Fire.Blue : Fire.None;
        if (want != current) SetFire(want);

        if (tankCore != null)
        {
            float fill = car.NitroActive ? 1f : Mathf.Max(0.04f, car.Nitro);
            // grows from the left end of the tank
            tankCore.localScale = new Vector3(0.34f, coreLength * 0.5f * fill, 0.34f); // glowing nitro, drawn over the shell
            tankCore.localPosition = new Vector3(0f, -coreLength * 0.5f * (1f - fill), 0f);
            bool flash = car.NitroReady && Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
            Color glow = car.ActiveNitroLevel == 2 ? new Color(3f, 0.9f, 0.3f)
                       : car.ActiveNitroLevel == 1 ? new Color(0.4f, 1.4f, 3f)
                       : flash ? new Color(3f, 1.2f, 0.4f)                       // full: flashing red-orange
                       : car.BlueReady ? new Color(0.4f, 1.2f, 2.6f)             // half: blue
                       : new Color(0.9f, 0.35f, 1.6f) * (0.4f + car.Nitro);       // filling: purple
            block.SetColor("_EmissionColor", glow);
            tankCoreRenderer.SetPropertyBlock(block);
        }
    }

    void SetFire(Fire f)
    {
        current = f;
        foreach (var ps in exhausts)
        {
            var main = ps.main;
            var em = ps.emission;
            var col = ps.colorOverLifetime;
            if (f == Fire.None) { em.rateOverTime = 0f; continue; }
            bool red = f == Fire.Red;
            em.rateOverTime = red ? 160f : 90f;
            main.startSize = red ? 0.75f : 0.5f;
            main.startSpeed = red ? 9f : 6f;
            main.startLifetime = red ? 0.32f : 0.22f;
            col.color = new ParticleSystem.MinMaxGradient(red ? redFire : blueFire);
        }
    }

    static ParticleSystem Flame(Transform t, Material mat)
    {
        var ps = t.gameObject.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 0.25f;
        main.startSpeed = 6f;
        main.startSize = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // a jet stuck to the pipe
        main.maxParticles = 250;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 9f;
        shape.radius = 0.07f;
        var em = ps.emission;
        em.rateOverTime = 0f;
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    static Transform Cylinder(Transform parent, string name, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    static void BuildGradients()
    {
        if (redFire != null) return;
        redFire = new Gradient();
        redFire.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.3f), new GradientColorKey(new Color(0.9f, 0.1f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
        blueFire = new Gradient();
        blueFire.SetKeys(
            new[] { new GradientColorKey(new Color(0.9f, 1f, 1f), 0f), new GradientColorKey(new Color(0.3f, 0.75f, 1f), 0.35f), new GradientColorKey(new Color(0.1f, 0.2f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
    }
}
