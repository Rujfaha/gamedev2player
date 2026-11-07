using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Which built-in course to generate.</summary>
public enum TrackId { Circuit, Forest, Space }

/// <summary>Where the race happens: checkpoints (in order), start poses, gem spots, laps.</summary>
public class TrackLayout
{
    public string name = "Track";
    public Transform[] checkpoints;
    public bool loop = true;   // loop: CP_00 is the start/finish line; point-to-point: the LAST checkpoint is the finish
    public int laps = 3;
    public Pose[] starts = new Pose[2];
    public List<(Vector3 pos, GemColor color)> gems = new List<(Vector3, GemColor)>();
    public List<Vector3> path = new List<Vector3>(); // centre line, for the minimap
    public List<Pose> aiStarts = new List<Pose>();   // grid slots for AI trucks (same row as the players)
    public List<List<Vector3>> shortcuts = new List<List<Vector3>>(); // centre lines of shortcut paths (minimap)
    public float roadWidth = 14f;
}

/// <summary>
/// Gets the track for the race. If the scene already has a hand-built track (objects tagged
/// Checkpoint named CP_00.., Start_P1/Start_P2, GemSpawn_&lt;Color&gt;_NN) that is used; otherwise a
/// built-in course is generated:
///   Circuit — 3-lap loop, white robot-arena road with a black line-tracing stripe.
///   Forest  — one-way switchback run through a forest with four U-turns (drift or lose time).
/// </summary>
public static class TrackBuilder
{
    public const float SampleSpacing = 2f;
    const float CheckpointSpacing = 30f, GemSpacing = 24f, RunOff = 30f;
    // The start area is widened so 5 trucks fit side by side, then tapers back to the normal width.
    const float GridWidth = 21f, GridLaneSpacing = 3.6f, WideZone = 30f, TaperZone = 35f;
    // Forest shortcut: a narrow dirt cut across the gap of hairpin 1, from 66 m on the first straight to
    // the same height on the way back (~147 m). Short, but two 90° turns and three barriers to dodge.
    static readonly Vector2[] ForestShortcuts = { new Vector2(66f, 147.4f) };
    const float ShortcutWidth = 7f;

    // ---------- Course shapes ----------

    // Circuit: closed Catmull-Rom loop through these points (metres), ~1050 m, 16 m wide so 4 trucks fit side by side.
    // Long start straight, fast sweeper, drift hairpin, S-bends, then a long left onto the straight (min radius ~15 m).
    static readonly Vector2[] CircuitControl =
    {
        new Vector2(0, 0), new Vector2(90, 0), new Vector2(170, 5), new Vector2(215, 35), new Vector2(225, 85),
        new Vector2(200, 125), new Vector2(155, 140), new Vector2(115, 120), new Vector2(90, 140), new Vector2(85, 180),
        new Vector2(55, 208), new Vector2(5, 210), new Vector2(-35, 185), new Vector2(-80, 205), new Vector2(-130, 190),
        new Vector2(-160, 145), new Vector2(-150, 95), new Vector2(-115, 60), new Vector2(-105, 20), new Vector2(-65, -5),
    };

    // Space (Nebula Ring): ~1.3 km loop floating in space. Two long straights carry the jump gaps, the rest is sweepers and an S.
    static readonly Vector2[] SpaceControl =
    {
        new Vector2(0, 0), new Vector2(80, 0), new Vector2(160, 0), new Vector2(220, 0), new Vector2(265, 30), new Vector2(270, 85),
        new Vector2(235, 115), new Vector2(185, 110), new Vector2(140, 90), new Vector2(95, 100), new Vector2(70, 140), new Vector2(80, 190),
        new Vector2(60, 235), new Vector2(0, 250), new Vector2(-70, 250), new Vector2(-140, 250), new Vector2(-195, 230), new Vector2(-220, 180),
        new Vector2(-200, 125), new Vector2(-150, 100), new Vector2(-120, 60), new Vector2(-90, 15), new Vector2(-45, 0),
    };

    // Forest: driven piece by piece from (0,0) heading north. R/L = right/left turn (degrees, radius).
    // ~850 m, 15 m wide, with 4 hairpins (180°), an S-bend and three 90° corners; the last 30 m is run-off past the finish.
    static readonly Seg[] ForestRoute =
    {
        S(80), R(180, 17),            // hairpin 1
        S(60), L(180, 17),            // hairpin 2
        S(60), R(180, 17),            // hairpin 3
        S(30), L(60, 25), R(60, 25),  // S-bend
        S(40), L(90, 30),
        S(50), L(180, 18),            // hairpin 4
        S(25), R(90, 20),
        S(60), R(90, 25),
        // --- extension: zig-zag climb, hairpin 5, long run home, final S ---
        S(60), L(90, 22),
        S(40), R(40, 30), L(80, 30), R(40, 30),   // zig-zag
        S(30), R(180, 18),                        // hairpin 5
        S(130), L(90, 30),
        S(40), L(70, 22), R(140, 20), L(70, 22),  // final S
        // --- extension 2 (one long run, ~2.4 km): ridge, double hairpin, sweeper home ---
        S(70), R(90, 30), S(80), L(120, 26), S(40), R(150, 22),
        S(60), L(60, 40), R(60, 40), S(90), L(90, 30), S(70), R(180, 20),
        S(80), L(90, 35),
        S(60),                                    // finish line at the end of this straight
        S(RunOff),
    };

    // CPE Arena (one way, ~2 km): white arena road through a drift hairpin, S-bends, two more hairpins and fast sweepers.
    static readonly Seg[] ArenaRoute =
    {
        S(120), R(90, 30), S(90), L(90, 25), S(60), R(180, 22),
        S(110), L(60, 40), R(120, 30), L(60, 40), S(140), L(90, 25), S(70), L(90, 25), S(90),
        R(180, 24), S(120), R(45, 40), L(90, 30), R(45, 40), S(130), R(90, 30), S(80), L(135, 26),
        S(100), L(45, 30), R(90, 30), S(120),     // finish line at the end of this straight
        S(RunOff),
    };

    // Nebula Ring (one way, ~2.5 km floating in space): long straights carry the jump gaps, sweepers and hairpins between.
    static readonly Seg[] SpaceRoute =
    {
        S(100), R(90, 40), S(150), L(120, 35), S(60), R(180, 32), S(160), L(90, 45), R(60, 40), L(60, 40),
        S(80), R(135, 35), S(170), L(90, 32), S(60), L(90, 32), S(130), R(70, 50), L(140, 32), R(70, 50),
        S(170), R(90, 40), S(100), L(90, 35), S(150), R(160, 30),
        S(120),                                   // finish line at the end of this straight
        S(RunOff),
    };

    struct Seg { public float length, turnDeg, radius; }
    static Seg S(float length) => new Seg { length = length };
    static Seg R(float deg, float radius) => new Seg { turnDeg = deg, radius = radius };
    static Seg L(float deg, float radius) => new Seg { turnDeg = -deg, radius = radius };

    class Style
    {
        public float width;
        public Material road, line, ground, wallA, wallB;
        public float wallHeight;
        public bool forest, space;
    }

    // ---------- Hand-built track from the scene ----------

    public static TrackLayout FromScene()
    {
        var cpObjects = GameObject.FindGameObjectsWithTag("Checkpoint");
        if (cpObjects.Length == 0) return null;

        var layout = new TrackLayout { name = "Custom Track" };
        layout.checkpoints = cpObjects.OrderBy(g => g.name).Select(g => g.transform).ToArray();
        for (int i = 0; i < layout.checkpoints.Length; i++)
        {
            var cp = layout.checkpoints[i];
            var col = cp.GetComponent<Collider>();
            if (col == null) { var box = cp.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(18, 6, 3); col = box; }
            col.isTrigger = true;
            (cp.GetComponent<Checkpoint>() ?? cp.gameObject.AddComponent<Checkpoint>()).index = i;
        }
        // A track without a GameObject named "PointToPoint" is treated as a loop.
        layout.loop = GameObject.Find("PointToPoint") == null;
        layout.path = layout.checkpoints.Select(c => c.position).ToList();
        layout.laps = layout.loop ? 3 : 1;
        for (int p = 0; p < 2; p++)
        {
            var s = GameObject.Find("Start_P" + (p + 1));
            var cp0 = layout.checkpoints[0];
            layout.starts[p] = s != null
                ? new Pose(s.transform.position, s.transform.rotation)
                : new Pose(cp0.position + cp0.right * (p == 0 ? -3f : 3f) + cp0.forward * 8f, cp0.rotation);
        }
        foreach (var g in GameObject.FindGameObjectsWithTag("GemSpawn"))
        {
            var color = g.name.Contains("Gold") ? GemColor.Gold : g.name.Contains("Blue") ? GemColor.Blue : GemColor.Red;
            layout.gems.Add((g.transform.position, color));
        }
        return layout;
    }

    // ---------- Generated courses ----------

    public static TrackLayout Build(GemDriftConfig cfg, Transform parent, TrackId id)
    {
        bool loop = false; // every course is one long run from start to finish (no laps)
        var style = id == TrackId.Forest
            ? new Style { width = 15f, road = cfg.dirtRoad, line = cfg.roadLine, ground = cfg.forestGround, wallA = cfg.woodA, wallB = cfg.woodB, wallHeight = 0.8f, forest = true }
            : id == TrackId.Space
            ? new Style { width = 16f, road = cfg.spaceRoad, line = cfg.roadLine, wallA = cfg.spaceWallA, wallB = cfg.spaceWallB, wallHeight = 0.9f, space = true }
            : new Style { width = 16f, road = cfg.road, line = cfg.roadLine, ground = cfg.ground, wallA = cfg.wallA, wallB = cfg.wallB, wallHeight = 1.1f };

        var root = new GameObject("Generated_" + id).transform;
        root.SetParent(parent, false);

        var pts = SampleRoute(id == TrackId.Forest ? ForestRoute : id == TrackId.Space ? SpaceRoute : ArenaRoute);
        int n = pts.Count;
        var tangents = new Vector3[n];
        var rights = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 a = loop ? pts[(i - 1 + n) % n] : pts[Mathf.Max(i - 1, 0)];
            Vector3 b = loop ? pts[(i + 1) % n] : pts[Mathf.Min(i + 1, n - 1)];
            tangents[i] = (b - a).normalized;
            rights[i] = Vector3.Cross(Vector3.up, tangents[i]);
        }

        // Road width per sample: wide around the start grid, normal elsewhere
        float total = (n - 1) * SampleSpacing;
        var widths = new float[n];
        for (int i = 0; i < n; i++)
        {
            float d = i * SampleSpacing;
            if (loop) d = Mathf.Min(d, n * SampleSpacing - d); // the grid sits on both sides of sample 0
            float t = Mathf.Clamp01((d - WideZone) / TaperZone);
            widths[i] = Mathf.Lerp(Mathf.Max(GridWidth, style.width), style.width, t * t * (3f - 2f * t));
        }

        var bounds = new Bounds(pts[0], Vector3.zero);
        foreach (var p in pts) bounds.Encapsulate(p);
        var plan = style.space ? SpaceTrack.PlanFeatures(tangents, loop) : null;
        var gap = plan != null ? plan.gap : new HashSet<int>();
        if (style.space)
        {
            // no ground: the road itself is the floor, with gaps after the jump ramps. Off the edge = fall into space.
            SpaceTrack.RoadPieces(root, pts, rights, widths, gap, style.road, loop);
        }
        else
        {
        // Ground: one big flat collider at y = 0 is the only driving surface
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.layer = Layers.Road;
        ground.transform.SetParent(root, false);
        ground.transform.localPosition = new Vector3(bounds.center.x, -0.5f, bounds.center.z);
        ground.transform.localScale = new Vector3(bounds.size.x + 1000f, 1f, bounds.size.z + 1000f);
        var groundRenderer = ground.GetComponent<Renderer>();
        groundRenderer.sharedMaterial = style.ground;
        groundRenderer.material.mainTextureScale = new Vector2(ground.transform.localScale.x, ground.transform.localScale.z) / 8f; // one texture tile per 8 m

        Strip(root, "Road", pts, rights, widths, 0.02f, style.road, loop);
        var stripe = new float[n];
        for (int i = 0; i < n; i++) stripe[i] = 0.6f;
        Strip(root, "LineTracingStripe", pts, rights, stripe, 0.035f, style.line, loop);
        }
        var layoutShortcuts = new List<List<Vector3>>();
        // shortcut gates: wall gaps where a shortcut joins the road
        var gates = new List<Vector3>();
        var shortcutPts = new List<Vector3>();
        if (style.forest)
            foreach (var sc in ForestShortcuts)
                BuildShortcut(root, cfg, style, pts, rights, widths,
                              Mathf.RoundToInt(sc.x / SampleSpacing), Mathf.RoundToInt(sc.y / SampleSpacing), gates, shortcutPts, layoutShortcuts);
        BuildWalls(root, pts, rights, tangents, widths, style, loop, gates, gap);

        int finishIdx = loop ? 0 : Mathf.Clamp(Mathf.RoundToInt((total - RunOff) / SampleSpacing), 0, n - 1);
        FinishLine(root, cfg, pts[finishIdx], tangents[finishIdx], widths[finishIdx]);
        if (style.space) SpaceTrack.Dress(cfg, root, pts, rights, tangents, widths, plan, bounds);
        else TrackDecor.Dress(cfg, root, pts, rights, tangents, widths, loop, style.forest, finishIdx, shortcutPts);

        var layout = new TrackLayout
        {
            loop = loop, laps = loop ? 3 : 1, path = pts, roadWidth = style.width,
            name = id == TrackId.Forest ? "FOREST SWITCHBACK" : id == TrackId.Space ? "NEBULA RING" : "CPE ARENA CIRCUIT",
        };

        // Checkpoints every ~30 m. Loop: CP_00 = finish line at sample 0. Point-to-point: last CP = finish line.
        int cpStride = Mathf.RoundToInt(CheckpointSpacing / SampleSpacing);
        var cpIndices = new List<int>();
        if (loop)
            for (int i = 0; i < n - cpStride / 2; i += cpStride)
            {
                // never over (or right before) a jump gap: a respawn there would drop the car into space
                int safe = i;
                while (safe > 0 && SpaceTrack.NearGap(gap, safe, 6, n)) safe--;
                if (cpIndices.Count == 0 || safe > cpIndices[cpIndices.Count - 1]) cpIndices.Add(safe);
            }
        else
        {
            for (int i = cpStride; i < finishIdx - cpStride / 2; i += cpStride)
            {
                int safe = i; // never over (or right before) a jump gap
                while (safe > 0 && SpaceTrack.NearGap(gap, safe, 6, n)) safe--;
                if (cpIndices.Count == 0 || safe > cpIndices[cpIndices.Count - 1]) cpIndices.Add(safe);
            }
            cpIndices.Add(finishIdx);
        }
        var cps = new List<Transform>();
        for (int k = 0; k < cpIndices.Count; k++)
        {
            int i = cpIndices[k];
            var go = new GameObject("CP_" + k.ToString("00")) { tag = "Checkpoint" };
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pts[i], Quaternion.LookRotation(tangents[i]));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(widths[i] + 4f, 6f, 3f);
            box.center = new Vector3(0, 3f, 0);
            go.AddComponent<Checkpoint>().index = k;
            cps.Add(go.transform);
        }
        layout.checkpoints = cps.ToArray();

        // Start grid: one row of 5 lanes, 10 m past the line on the circuit / 8 m from the start wall on
        // point-to-point. Lanes left→right: AI, P1, AI, P2, AI (players never stuck behind an AI truck).
        int s = Mathf.RoundToInt((loop ? 10f : 8f) / SampleSpacing);
        var gridRot = Quaternion.LookRotation(tangents[s]);
        Vector3 Lane(int k) => pts[s] + rights[s] * (k * GridLaneSpacing);
        layout.starts[0] = new Pose(Lane(-1), gridRot);
        layout.starts[1] = new Pose(Lane(1), gridRot);
        foreach (int k in new[] { 0, -2, 2 }) layout.aiStarts.Add(new Pose(Lane(k), gridRot));

        // Gems: pairs on straights (one per lane), a single gold on the inside of sharp corners
        int gemStride = Mathf.RoundToInt(GemSpacing / SampleSpacing);
        int look = 4;
        int lastGem = loop ? n - 6 : finishIdx - 4;
        float laneOff = style.width * 0.25f, apexOff = style.width * 0.32f;
        for (int i = Mathf.RoundToInt(30f / SampleSpacing); i < lastGem; i += gemStride)
        {
            if (SpaceTrack.NearGap(gap, i, 8, n)) continue;
            var tPrev = tangents[loop ? (i - look + n) % n : Mathf.Max(i - look, 0)];
            var tNext = tangents[loop ? (i + look) % n : Mathf.Min(i + look, n - 1)];
            float turnDeg = Vector3.Angle(tPrev, tNext);
            if (turnDeg > 28f)
            {
                float inner = Mathf.Sign(Vector3.Cross(tPrev, tNext).y);
                layout.gems.Add((pts[i] + rights[i] * inner * apexOff, GemColor.Gold));
            }
            else
            {
                var c = (i / gemStride) % 3 == 2 ? GemColor.Blue : GemColor.Red;
                layout.gems.Add((pts[i] - rights[i] * laneOff, c));
                layout.gems.Add((pts[i] + rights[i] * laneOff, c));
            }
        }

        layout.shortcuts.AddRange(layoutShortcuts);
        if (style.forest)
        {
            var clear = new List<Vector3>(pts);
            clear.AddRange(shortcutPts);
            Forest(root, cfg, clear, Mathf.Max(style.width, GridWidth), bounds);
        }
        return layout;
    }

    // ---------- Path sampling ----------

    // Catmull-Rom through the points, resampled every SampleSpacing metres
    static List<Vector3> SampleLoop(Vector2[] control)
    {
        var dense = new List<Vector3>();
        int m = control.Length;
        for (int i = 0; i < m; i++)
        {
            Vector3 p0 = V(control[(i - 1 + m) % m]), p1 = V(control[i]), p2 = V(control[(i + 1) % m]), p3 = V(control[(i + 2) % m]);
            for (int k = 0; k < 40; k++)
            {
                float t = k / 40f, t2 = t * t, t3 = t2 * t;
                dense.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        var outPts = new List<Vector3> { dense[0] };
        float carry = 0f;
        for (int i = 1; i <= dense.Count; i++)
        {
            Vector3 a = dense[i - 1], b = dense[i % dense.Count];
            float seg = Vector3.Distance(a, b), pos = SampleSpacing - carry;
            while (pos <= seg)
            {
                outPts.Add(Vector3.Lerp(a, b, pos / seg));
                pos += SampleSpacing;
            }
            carry = seg - (pos - SampleSpacing);
        }
        if (Vector3.Distance(outPts[outPts.Count - 1], outPts[0]) < SampleSpacing * 0.5f) outPts.RemoveAt(outPts.Count - 1);
        return outPts;
    }

    // Drives the straight/turn pieces in small steps and drops a sample every SampleSpacing metres
    static List<Vector3> SampleRoute(Seg[] route)
    {
        const float step = 0.1f;
        var pts = new List<Vector3> { Vector3.zero };
        Vector3 p = Vector3.zero;
        float heading = 0f, acc = 0f;
        foreach (var seg in route)
        {
            bool straight = seg.turnDeg == 0f;
            float length = straight ? seg.length : Mathf.Abs(seg.turnDeg) * Mathf.Deg2Rad * seg.radius;
            float degPerMetre = straight ? 0f : seg.turnDeg / length;
            for (float d = 0f; d < length - 1e-4f; d += step)
            {
                float st = Mathf.Min(step, length - d);
                heading += degPerMetre * st * 0.5f;
                p += new Vector3(Mathf.Sin(heading * Mathf.Deg2Rad), 0f, Mathf.Cos(heading * Mathf.Deg2Rad)) * st;
                heading += degPerMetre * st * 0.5f;
                acc += st;
                if (acc >= SampleSpacing) { acc -= SampleSpacing; pts.Add(p); }
            }
        }
        return pts;
    }

    static Vector3 V(Vector2 p) => new Vector3(p.x, 0, p.y);

    // ---------- Meshes ----------

    internal static GameObject Strip(Transform parent, string name, List<Vector3> pts, Vector3[] rights, float[] widths, float y, Material mat, bool loop)
    {
        int n = pts.Count;
        int rows = loop ? n + 1 : n;
        var verts = new Vector3[rows * 2];
        var uvs = new Vector2[verts.Length];
        var tris = new int[(rows - 1) * 6];
        float dist = 0f;
        for (int i = 0; i < rows; i++)
        {
            int k = i % n;
            if (i > 0) dist += Vector3.Distance(pts[i - 1], pts[k]);
            var up = Vector3.up * y;
            float width = widths[k];
            verts[i * 2] = pts[k] - rights[k] * width * 0.5f + up;
            verts[i * 2 + 1] = pts[k] + rights[k] * width * 0.5f + up;
            uvs[i * 2] = new Vector2(0, dist / width);
            uvs[i * 2 + 1] = new Vector2(1, dist / width);
        }
        for (int i = 0; i < rows - 1; i++)
        {
            int a = i * 2, t = i * 6;
            tris[t] = a; tris[t + 1] = a + 2; tris[t + 2] = a + 1;
            tris[t + 3] = a + 1; tris[t + 4] = a + 2; tris[t + 5] = a + 3;
        }
        var mesh = new Mesh { name = name, vertices = verts, uv = uvs, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // Box colliders along both edges (+ end caps on a one-way route); visuals merged into two meshes
    static void BuildWalls(Transform parent, List<Vector3> pts, Vector3[] rights, Vector3[] tangents, float[] widths, Style style, bool loop, List<Vector3> gates, HashSet<int> skip)
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cube = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(temp);

        var colliders = new GameObject("WallColliders") { layer = Layers.Wall };
        colliders.transform.SetParent(parent, false);
        var partsA = new List<CombineInstance>();
        var partsB = new List<CombineInstance>();
        float height = style.wallHeight, thick = 0.7f;
        int n = pts.Count;
        float Offset(int i) => widths[i] * 0.5f + thick * 0.5f;
        int segs = loop ? n : n - 1;

        void Piece(Vector3 center, Quaternion rot, Vector3 size, bool useA)
        {
            var piece = new GameObject("Wall") { layer = Layers.Wall, isStatic = true };
            piece.transform.SetParent(colliders.transform, false);
            piece.transform.SetPositionAndRotation(center, rot);
            piece.AddComponent<BoxCollider>().size = size;
            var ci = new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(center, rot, size) };
            (useA ? partsA : partsB).Add(ci);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < segs; i++)
            {
                int j = (i + 1) % n;
                var a = pts[i] + rights[i] * Offset(i) * side;
                var b = pts[j] + rights[j] * Offset(j) * side;
                if (InGate((a + b) * 0.5f, gates)) continue; // opening for a shortcut
                if (skip.Contains(i) || skip.Contains(j)) continue; // jump gap
                Piece((a + b) * 0.5f + Vector3.up * height * 0.5f, Quaternion.LookRotation(b - a),
                      new Vector3(thick, height, Vector3.Distance(a, b) + 0.3f), (i / 3) % 2 == 0);
            }
        }
        if (!loop)
        {
            // close both ends so nobody drives off the route
            foreach (int e in new[] { 0, n - 1 })
            {
                var c = pts[e] + tangents[e] * (e == 0 ? -1f : 1f) + Vector3.up * height * 0.5f;
                Piece(c, Quaternion.LookRotation(tangents[e]), new Vector3(widths[e] + thick * 2f, height, thick), true);
            }
        }
        Merge(parent, "Walls_A", partsA, style.wallA);
        Merge(parent, "Walls_B", partsB, style.wallB);
    }

    static bool InGate(Vector3 p, List<Vector3> gates)
    {
        foreach (var g in gates)
            if (new Vector2(p.x - g.x, p.z - g.z).magnitude < ShortcutWidth * 0.5f + 0.8f) return true;
        return false;
    }

    // A narrow straight cut from sample f to sample t (they must face each other across a gap):
    // road strip, its own side walls, three barriers to slalom through, and gates in the main walls.
    static void BuildShortcut(Transform parent, GemDriftConfig cfg, Style style, List<Vector3> pts, Vector3[] rights, float[] widths,
                              int f, int t, List<Vector3> gates, List<Vector3> outPts, List<List<Vector3>> outLines)
    {
        int sA = Vector3.Dot(rights[f], pts[t] - pts[f]) > 0 ? 1 : -1;
        int sB = Vector3.Dot(rights[t], pts[f] - pts[t]) > 0 ? 1 : -1;
        Vector3 edgeA = pts[f] + rights[f] * sA * (widths[f] * 0.5f + 0.35f);
        Vector3 edgeB = pts[t] + rights[t] * sB * (widths[t] * 0.5f + 0.35f);
        gates.Add(edgeA);
        gates.Add(edgeB);

        // road: from inside road A to inside road B (overlapping both so there's no seam)
        Vector3 a = pts[f] + rights[f] * sA * (widths[f] * 0.5f - 1.5f);
        Vector3 b = pts[t] + rights[t] * sB * (widths[t] * 0.5f - 1.5f);
        Vector3 dir = (b - a).normalized, side = Vector3.Cross(Vector3.up, dir);
        int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / SampleSpacing));
        var line = new List<Vector3>();
        var lineRights = new Vector3[steps + 1];
        var lineWidths = new float[steps + 1];
        for (int k = 0; k <= steps; k++)
        {
            line.Add(Vector3.Lerp(a, b, k / (float)steps));
            lineRights[k] = side;
            lineWidths[k] = ShortcutWidth;
        }
        Strip(parent, "ShortcutRoad", line, lineRights, lineWidths, 0.025f, style.road, false);
        outPts.AddRange(line);
        outLines.Add(line);

        // side walls only on the stretch between the two main roads
        var walls = new GameObject("ShortcutWalls") { layer = Layers.Wall };
        walls.transform.SetParent(parent, false);
        float len = Vector3.Distance(edgeA, edgeB);
        Vector3 mid = (edgeA + edgeB) * 0.5f;
        Quaternion rot = Quaternion.LookRotation(dir);
        foreach (int s in new[] { -1, 1 })
            Block(walls.transform, mid + side * s * (ShortcutWidth * 0.5f + 0.35f), rot, new Vector3(0.7f, style.wallHeight, len), (s < 0 ? style.wallA : style.wallB), style.wallHeight);

        // three barriers, alternating sides: slalom
        for (int k = 0; k < 3; k++)
        {
            Vector3 p = Vector3.Lerp(edgeA, edgeB, 0.3f + 0.2f * k) + side * (k % 2 == 0 ? -1.6f : 1.6f);
            Block(walls.transform, p, rot * Quaternion.Euler(0, 90, 0), new Vector3(3.2f, 1f, 0.8f), cfg.wallA, 1f);
        }
    }

    static void Block(Transform parent, Vector3 groundPos, Quaternion rot, Vector3 size, Material mat, float height)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Barrier";
        go.layer = Layers.Wall;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(groundPos + Vector3.up * height * 0.5f, rot);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    static void Merge(Transform parent, string name, List<CombineInstance> parts, Material mat)
    {
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.CombineMeshes(parts.ToArray(), true, true);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void FinishLine(Transform parent, GemDriftConfig cfg, Vector3 pos, Vector3 tangent, float width)
    {
        var finish = GameObject.CreatePrimitive(PrimitiveType.Quad);
        finish.name = "FinishLine";
        Object.Destroy(finish.GetComponent<Collider>());
        finish.transform.SetParent(parent, false);
        finish.transform.position = pos + Vector3.up * 0.045f;
        finish.transform.rotation = Quaternion.LookRotation(Vector3.down, tangent);
        finish.transform.localScale = new Vector3(width, 3f, 1);
        finish.GetComponent<Renderer>().sharedMaterial = cfg.checker;

        // Kenney overhead arch, scaled to span the road
        if (cfg.finishArch != null)
        {
            var arch = Object.Instantiate(cfg.finishArch, parent);
            arch.name = "FinishArch";
            foreach (var c in arch.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            arch.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(tangent));
            var b = RendererBounds(arch);
            float span = Mathf.Max(b.size.x, 0.01f);
            arch.transform.localScale = Vector3.one * ((width + 2.5f) / span);
            b = RendererBounds(arch);
            arch.transform.position += new Vector3(pos.x - b.center.x, -b.min.y, pos.z - b.center.z);
        }
    }

    // Kenney trees scattered everywhere except on/near the road, then static-batched
    static void Forest(Transform parent, GemDriftConfig cfg, List<Vector3> pts, float width, Bounds pathBounds)
    {
        if (cfg.treeModels == null || cfg.treeModels.Length == 0) return;
        var root = new GameObject("Trees").transform;
        root.SetParent(parent, false);

        // target heights so the Kenney trees look right next to a 4.2 m truck
        var scales = new float[cfg.treeModels.Length];
        for (int t = 0; t < cfg.treeModels.Length; t++)
        {
            var probe = Object.Instantiate(cfg.treeModels[t]);
            float h = Mathf.Max(RendererBounds(probe).size.y, 0.01f);
            Object.Destroy(probe);
            scales[t] = (t == 0 ? 11f : 7f) / h;
        }

        var rng = new System.Random(4102);
        float clear = width * 0.5f + 3.5f;
        const float grid = 8f;
        var area = pathBounds;
        area.Expand(new Vector3(90f, 0f, 90f));
        for (float x = area.min.x; x < area.max.x; x += grid)
        {
            for (float z = area.min.z; z < area.max.z; z += grid)
            {
                var p = new Vector3(x + (float)rng.NextDouble() * grid * 0.8f, 0f, z + (float)rng.NextDouble() * grid * 0.8f);
                float d = MinDistance(p, pts);
                if (d < clear) continue;
                // denser near the road, thinner far away
                if (rng.NextDouble() > (d < 30f ? 0.85 : 0.45)) continue;
                int t = rng.NextDouble() < 0.6 ? 0 : 1;
                var tree = Object.Instantiate(cfg.treeModels[t], p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), root);
                tree.transform.localScale = Vector3.one * scales[t] * (0.75f + (float)rng.NextDouble() * 0.5f);
                foreach (var c in tree.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
        }
        StaticBatchingUtility.Combine(root.gameObject);
    }

    static float MinDistance(Vector3 p, List<Vector3> pts)
    {
        float best = float.MaxValue;
        for (int i = 0; i < pts.Count; i++)
        {
            float dx = pts[i].x - p.x, dz = pts[i].z - p.z;
            float d = dx * dx + dz * dz;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    static Bounds RendererBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
