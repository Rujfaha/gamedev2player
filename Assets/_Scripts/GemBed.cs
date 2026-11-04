using System.Collections.Generic;
using UnityEngine;

/// <summary>The truck bed: holds up to 12 gems, shows them stacked, and spills them on crashes.</summary>
public class GemBed : MonoBehaviour
{
    public const int Capacity = 12;
    const float MaxWeightSlowdown = 0.15f;

    readonly List<GemColor> gems = new List<GemColor>();
    readonly List<GameObject> visuals = new List<GameObject>();
    GemDriftConfig cfg;
    Transform anchor;
    CarController car;
    float gemSize;

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

    /// <summary>1 when empty, 0.85 when full: heavier truck = slower.</summary>
    public float WeightFactor => 1f - MaxWeightSlowdown * gems.Count / Capacity;

    public void Setup(GemDriftConfig config, Transform body, CarController owner)
    {
        cfg = config;
        car = owner;
        anchor = new GameObject("Bed").transform;
        anchor.SetParent(body, false);
        anchor.localPosition = cfg.bedCenter;
        gemSize = Mathf.Min(cfg.bedSize.x / 3f, cfg.bedSize.y / 2f) * 0.85f;
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

    // 3 across × 2 rows per layer, 2 layers = 12 slots
    Vector3 Slot(int i)
    {
        int layer = i / 6, k = i % 6, col = k % 3, row = k / 3;
        float sx = cfg.bedSize.x / 3f, sz = cfg.bedSize.y / 2f;
        return new Vector3((col - 1) * sx, gemSize * 0.5f + layer * gemSize * 0.85f, (row - 0.5f) * sz);
    }
}
