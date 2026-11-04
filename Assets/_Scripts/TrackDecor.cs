using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dresses a generated course with Kenney props (visual only, no colliders), all placed outside the walls:
/// start area with grandstands, pits, tents, banner towers and chequered flags; light posts along the road;
/// cones and flags on the outside of corners; billboards on straights; grass tufts in the forest.
/// Every prop is auto-scaled to a real-world height, so it doesn't matter how big the source model is.
/// </summary>
public static class TrackDecor
{
    static GemDriftConfig cfg;
    static Transform root;
    static List<Vector3> pts;
    static Vector3[] rights, tangents;
    static float[] widths;
    static float clearance;
    static List<Vector3> extraClear;
    static System.Random rng;
    static readonly Dictionary<string, float> rawHeight = new Dictionary<string, float>();

    public static void Dress(GemDriftConfig config, Transform parent, List<Vector3> path, Vector3[] r, Vector3[] t,
                             float[] w, bool loop, bool forest, int finishIdx, List<Vector3> keepClear = null)
    {
        extraClear = keepClear ?? new List<Vector3>();
        if (config.decorModels == null || config.decorModels.Length == 0) return;
        cfg = config; pts = path; rights = r; tangents = t; widths = w;
        rng = new System.Random(forest ? 77 : 33);
        float maxW = 0f;
        foreach (var x in w) maxW = Mathf.Max(maxW, x);
        clearance = maxW * 0.5f + 1.5f;
        root = new GameObject("Decor").transform;
        root.SetParent(parent, false);
        int n = pts.Count;

        // ---- start / finish area ----
        int grid = Mathf.RoundToInt((loop ? 10f : 8f) / 2f);
        for (int side = -1; side <= 1; side += 2)
        {
            CpeBoard(Beside(grid, side, 2.5f), Oncoming(grid), 7f, 4f);
            Prop(side < 0 ? "flagCheckers" : "flagCheckersSmall", Beside(finishIdx, side, 1.5f), Face(finishIdx, -side), 6f);
            CpeBoard(Beside(finishIdx + (loop ? -6 : -3), side, 2.5f), Oncoming(finishIdx), 7f, 4f);
        }
        // grandstands + pits along the start straight
        int standSide = forest ? 1 : -1;
        for (int k = 0; k < 3; k++)
        {
            int i = Wrap(grid + 6 + k * 9, n, loop);
            Prop(k == 1 ? "grandStandCovered" : "grandStand", Beside(i, standSide, 4f), Face(i, -standSide), 8f, maxSize: 16f);
        }
        for (int k = 0; k < 3; k++)
        {
            int i = Wrap(grid + 4 + k * 7, n, loop);
            Prop(k == 1 ? "tentLong" : "tent", Beside(i, -standSide, 5f), Face(i, standSide), 4f, maxSize: k == 1 ? 9f : 6f);
        }
        Prop("tentLong", Beside(Wrap(grid + 28, n, loop), -standSide, 6f), Face(grid, standSide), 4f, maxSize: 9f);
        Prop("tent", Beside(Wrap(grid + 33, n, loop), -standSide, 5f), Face(grid, standSide), 4f, maxSize: 6f);
        Prop("radarEquipment", Beside(Wrap(grid + 36, n, loop), standSide, 8f), Face(grid, -standSide), 5f, maxSize: 6f);
        // big CPE banner gantry over the road just after the grid
        Gantry(Wrap(grid + 14, n, loop));

        // ---- along the road ----
        int last = loop ? n : n - 2;
        for (int i = 20; i < last; i += 30)                       // light posts, alternating sides
            Prop("lightPostLarge", Beside(i, (i / 30) % 2 == 0 ? 1 : -1, 1.2f), Face(i, (i / 30) % 2 == 0 ? -1 : 1), 9f);

        for (int i = 45; i < last - 10; i += 85)                   // billboards on the outside
            CpeBoard(Beside(i, rng.Next(2) == 0 ? 1 : -1, 5f), Oncoming(i), 8f, 4.5f);

        // corners: cones + flags on the outside of every bend, a light rig over the sharpest ones
        for (int i = 6; i < last - 6; i++)
        {
            var tPrev = tangents[Wrap(i - 5, n, loop)];
            var tNext = tangents[Wrap(i + 5, n, loop)];
            float turn = Vector3.Angle(tPrev, tNext);
            if (turn < 35f || !IsPeak(i, n, loop)) continue;
            int outside = Vector3.Cross(tPrev, tNext).y > 0 ? -1 : 1;
            for (int c = -2; c <= 2; c++)
                Prop("cone", Beside(Wrap(i + c * 2, n, loop), outside, 0.8f), Quaternion.identity, 0.9f, carKit: true);
            Prop(turn > 70f ? "flagRed" : "flagGreen", Beside(i, outside, 3f), Face(i, -outside), 5f);
            if (turn > 70f) Prop("lightRedDouble", Beside(i, -outside, 1.5f), Face(i, outside), 3f);
        }

        // ---- forest floor ----
        if (forest)
        {
            for (int i = 0; i < n; i += 1)
            {
                if (rng.NextDouble() > 0.55) continue;
                int side = rng.Next(2) == 0 ? 1 : -1;
                float d = 0.6f + (float)rng.NextDouble() * 5f;
                Prop("grass", Beside(i, side, d) + tangents[i] * ((float)rng.NextDouble() * 2f - 1f),
                     Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), 0.5f + (float)rng.NextDouble() * 0.6f);
            }
        }

        StaticBatchingUtility.Combine(root.gameObject);
    }

    // a point beside the road: sample i, side ±1, `extra` metres beyond the wall
    static Vector3 Beside(int i, int side, float extra)
    {
        i = Mathf.Clamp(i, 0, pts.Count - 1);
        return pts[i] + rights[i] * side * (widths[i] * 0.5f + 0.7f + extra);
    }

    // rotation facing across the road (towards side `toward`), or along it when toward == 0
    static Quaternion Face(int i, int toward, int along = 0)
    {
        i = Mathf.Clamp(i, 0, pts.Count - 1);
        if (toward == 0) return Quaternion.LookRotation(-rights[i] * (along >= 0 ? 1 : -1));
        return Quaternion.LookRotation(rights[i] * toward);
    }

    // faces trucks driving towards it (the panel is double-sided anyway)
    static Quaternion Oncoming(int i) => Quaternion.LookRotation(-tangents[Mathf.Clamp(i, 0, tangents.Length - 1)]);

    static int Wrap(int i, int n, bool loop) => loop ? ((i % n) + n) % n : Mathf.Clamp(i, 0, n - 1);

    static bool IsPeak(int i, int n, bool loop)
    {
        float Turn(int k) => Vector3.Angle(tangents[Wrap(k - 5, n, loop)], tangents[Wrap(k + 5, n, loop)]);
        float t = Turn(i);
        return t >= Turn(i - 1) && t > Turn(i + 1) && t >= Turn(i - 3) && t >= Turn(i + 3);
    }

    static bool Clear(Vector3 pos, float margin)
    {
        float nearest = float.MaxValue;
        foreach (var p in pts) nearest = Mathf.Min(nearest, (p.x - pos.x) * (p.x - pos.x) + (p.z - pos.z) * (p.z - pos.z));
        if (Mathf.Sqrt(nearest) < clearance - margin) return false;
        foreach (var p in extraClear)
            if (new Vector2(p.x - pos.x, p.z - pos.z).magnitude < 5f) return false;
        return true;
    }

    static void Prop(string name, Vector3 pos, Quaternion rot, float height, bool carKit = false, float maxSize = float.MaxValue)
    {
        var prefab = Find(name);
        if (prefab == null) return;
        // skip anything that would end up on (another part of) the road
        if (!Clear(pos, carKit ? 1.2f : 0f)) return;

        var go = Object.Instantiate(prefab, pos, rot, root);
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        if (!rawHeight.TryGetValue(name, out float h))
        {
            h = Mathf.Max(Bounds(go).size.y / Mathf.Max(go.transform.lossyScale.y, 1e-4f), 0.01f);
            rawHeight[name] = h;
        }
        go.transform.localScale = Vector3.one * (height / h);
        var b = Bounds(go);
        float wide = Mathf.Max(b.size.x, b.size.z);
        if (wide > maxSize)
        {
            go.transform.localScale *= maxSize / wide; // flat, wide models: cap the footprint
            b = Bounds(go);
        }
        go.transform.position += Vector3.up * -b.min.y;
    }

    // Original CPE · KMUTT sign: two posts and a double-sided panel (cfg.cpeBanner, 4:1)
    static void CpeBoard(Vector3 pos, Quaternion rot, float width, float bottom)
    {
        if (cfg.cpeBanner == null || !Clear(pos, 0f)) return;
        float h = width / 4f;
        var board = new GameObject("CPE_Board").transform;
        board.SetParent(root, false);
        board.SetPositionAndRotation(pos, rot);
        foreach (int s in new[] { -1, 1 })
            Post(board, new Vector3(s * width * 0.45f, 0f, 0f), bottom + h);
        Panel(board, new Vector3(0f, bottom + h * 0.5f, 0f), width, h);
    }

    // banner across the whole road on two tall posts
    static void Gantry(int i)
    {
        if (cfg.cpeBanner == null) return;
        float width = widths[i] + 2f, h = width / 4f, bottom = 6f;
        var g = new GameObject("CPE_Gantry").transform;
        g.SetParent(root, false);
        g.SetPositionAndRotation(pts[i], Quaternion.LookRotation(-tangents[i]));
        foreach (int s in new[] { -1, 1 })
            Post(g, new Vector3(s * (width * 0.5f + 0.3f), 0f, 0f), bottom + h + 0.3f);
        Panel(g, new Vector3(0f, bottom + h * 0.5f, 0f), width, h);
    }

    static void Post(Transform parent, Vector3 local, float height)
    {
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.Destroy(post.GetComponent<Collider>());
        post.transform.SetParent(parent, false);
        post.transform.localPosition = local + Vector3.up * height * 0.5f;
        post.transform.localScale = new Vector3(0.35f, height * 0.5f, 0.35f);
        post.GetComponent<Renderer>().sharedMaterial = cfg.nitroTankShell != null ? cfg.nitroTankShell : cfg.wallB;
    }

    static void Panel(Transform parent, Vector3 local, float width, float height)
    {
        foreach (int face in new[] { 0, 180 }) // two quads back to back = readable from both directions
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = local + Vector3.forward * (face == 0 ? -0.02f : 0.02f);
            q.transform.localRotation = Quaternion.Euler(0f, face, 0f);
            q.transform.localScale = new Vector3(width, height, 1f);
            q.GetComponent<Renderer>().sharedMaterial = cfg.cpeBanner;
        }
        var back = GameObject.CreatePrimitive(PrimitiveType.Cube); // thin frame behind the print
        Object.Destroy(back.GetComponent<Collider>());
        back.transform.SetParent(parent, false);
        back.transform.localPosition = local;
        back.transform.localScale = new Vector3(width + 0.3f, height + 0.3f, 0.02f);
        back.GetComponent<Renderer>().sharedMaterial = cfg.nitroTankShell != null ? cfg.nitroTankShell : cfg.wallB;
    }

    static GameObject Find(string name)
    {
        for (int i = 0; i < cfg.decorNames.Length && i < cfg.decorModels.Length; i++)
            if (cfg.decorNames[i] == name) return cfg.decorModels[i];
        return null;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
