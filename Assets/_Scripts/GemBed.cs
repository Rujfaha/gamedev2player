using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The car's gem storage: the truck bed, or on the sports cars a gem pod (dark tray, metal rails, glowing trim) on the rear deck.
/// Holds up to the car's capacity (CarSpec.capacity), shows the gems stacked, and spills them on crashes.
/// </summary>
public class GemBed : MonoBehaviour
{
    const float MaxWeightSlowdown = 0.15f;

    readonly List<GemColor> gems = new List<GemColor>();
    readonly List<GameObject> visuals = new List<GameObject>();
    GemDriftConfig cfg;
    Transform anchor;
    CarController car;
    float gemSize;
    int cols, rows;
    Vector2 floor;

    public int Capacity { get; private set; } = 12;
    public int Count => gems.Count;
    public bool Full => gems.Count >= Capacity;
    /// <summary>When locked (car finished) the bed can't gain or lose gems.</summary>
    public bool Locked { get; set; }
    public float LastSpillTime { get; private set; } = -99f;
    public int LastSpillCount { get; private set; }

    public int Value
    {
        get { int v = 0; foreach (var g in gems) v += (int)g; return v; }
    }

    /// <summary>1 when empty, 0.85 when full: heavier car = slower.</summary>
    public float WeightFactor => 1f - MaxWeightSlowdown * gems.Count / Capacity;

    public void Setup(GemDriftConfig config, CarSpec spec, Transform body, CarController owner)
    {
        cfg = config;
        car = owner;
        Capacity = Mathf.Max(1, spec.capacity);
        floor = spec.bedSize;
        anchor = new GameObject("Bed").transform;
        anchor.SetParent(body, false);
        anchor.localPosition = spec.bedCenter;

        Layout(spec, out cols, out rows, out gemSize);
        if (spec.pod) BuildPod(cfg, anchor, spec, cols, rows, gemSize);
    }

    // gems per layer: 3 x 2 on a wide bed, 2 x 2 on a small deck
    static void Layout(CarSpec spec, out int cols, out int rows, out float gemSize)
    {
        cols = spec.bedSize.x > 1.1f ? 3 : 2;
        rows = 2;
        gemSize = Mathf.Min(spec.bedSize.x / cols, spec.bedSize.y / rows) * 0.85f;
    }

    /// <summary>Garage preview: the car's gem storage (pod or bed) under parent, filled with a few sample gems.</summary>
    public static void BuildPreview(GemDriftConfig cfg, CarSpec spec, Transform parent)
    {
        var anchor = new GameObject("Bed").transform;
        anchor.SetParent(parent, false);
        anchor.localPosition = spec.bedCenter;
        Layout(spec, out int cols, out int rows, out float size);
        if (spec.pod) BuildPod(cfg, anchor, spec, cols, rows, size);
        GemColor[] sample = { GemColor.Gold, GemColor.Red, GemColor.Blue, GemColor.Red, GemColor.Blue, GemColor.Gold };
        int n = Mathf.Min(spec.capacity, cols * rows);
        for (int i = 0; i < n; i++)
        {
            var vis = GemMesh.CreateVisual(cfg, sample[i % sample.Length], anchor, size);
            vis.transform.localPosition = SlotOf(i, spec.bedSize, cols, rows, size);
            vis.transform.localRotation = Quaternion.Euler(0, i * 37f, 0);
        }
    }

    /// <summary>Tray + corner posts + top rails, with a glowing strip so the storage reads at a glance.</summary>
    static void BuildPod(GemDriftConfig cfg, Transform anchor, CarSpec spec, int cols, int rows, float gemSize)
    {
        var floor = spec.bedSize;
        int layers = Mathf.CeilToInt(spec.capacity / (float)(cols * rows));
        float w = floor.x + 0.12f, l = floor.y + 0.12f, h = gemSize * (0.6f + 0.85f * layers);
        var pod = new GameObject("GemPod").transform;
        pod.SetParent(anchor, false);
        var dark = cfg.podShell != null ? cfg.podShell : cfg.nitroTankShell;
        var glow = cfg.podGlow != null ? cfg.podGlow : cfg.nitroGlow;
        Box(pod, "Tray", dark, new Vector3(0f, 0.04f, 0f), new Vector3(w, 0.08f, l));
        Box(pod, "Glow", glow, new Vector3(0f, 0.085f, 0f), new Vector3(w + 0.04f, 0.025f, l + 0.04f));
        float px = w * 0.5f - 0.03f, pz = l * 0.5f - 0.03f;
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
                Box(pod, "Post", dark, new Vector3(sx * px, h * 0.5f, sz * pz), new Vector3(0.06f, h, 0.06f));
        foreach (var sx in new[] { -1f, 1f }) Box(pod, "RailSide", dark, new Vector3(sx * px, h, 0f), new Vector3(0.06f, 0.06f, l));
        foreach (var sz in new[] { -1f, 1f }) Box(pod, "RailEnd", glow, new Vector3(0f, h, sz * pz), new Vector3(w, 0.05f, 0.05f));
    }

    static void Box(Transform parent, string name, Material m, Vector3 pos, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public bool TryAdd(GemColor color)
    {
        if (Locked || Full) return false;
        var vis = GemMesh.CreateVisual(cfg, color, anchor, gemSize);
        vis.transform.localPosition = Slot(gems.Count);
        vis.transform.localRotation = Quaternion.Euler(0, gems.Count * 37f, 0);
        gems.Add(color);
        visuals.Add(vis);
        return true;
    }

    /// <summary>Drops the top n gems as free physics gems. They fly out along push (default: backwards).</summary>
    public void Spill(int n, Vector3? push = null)
    {
        if (Locked || n <= 0 || gems.Count == 0) return;
        var dir = push ?? -car.transform.forward;
        int spilled = 0;
        for (int i = 0; i < n && gems.Count > 0; i++)
        {
            int last = gems.Count - 1;
            var color = gems[last];
            // lift each gem a bit more so gems dropped together don't spawn inside each other
            var pos = visuals[last].transform.position + Vector3.up * (0.4f + 0.6f * spilled);
            Destroy(visuals[last]);
            gems.RemoveAt(last);
            visuals.RemoveAt(last);
            GemPickup.CreateDropped(cfg, color, pos, car, dir);
            spilled++;
        }
        LastSpillTime = Time.time;
        LastSpillCount = spilled;
    }

    Vector3 Slot(int i) => SlotOf(i, floor, cols, rows, gemSize);

    // cols x rows per layer, layers stack upwards
    static Vector3 SlotOf(int i, Vector2 floor, int cols, int rows, float gemSize)
    {
        int per = cols * rows, layer = i / per, k = i % per, col = k % cols, row = k / cols;
        float sx = floor.x / cols, sz = floor.y / rows;
        return new Vector3((col - (cols - 1) * 0.5f) * sx, gemSize * 0.5f + 0.09f + layer * gemSize * 0.85f, (row - 0.5f) * sz);
    }
}
