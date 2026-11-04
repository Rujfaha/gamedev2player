using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Where the race happens: checkpoints (CP_00 = finish), start poses, gem spots.</summary>
public class TrackLayout
{
    public Transform[] checkpoints;
    public Pose[] starts = new Pose[2];
    public List<(Vector3 pos, GemColor color)> gems = new List<(Vector3, GemColor)>();
}

/// <summary>
/// Gets the track for the race. If the scene already has a hand-built track (objects tagged
/// Checkpoint named CP_00.., Start_P1/Start_P2, GemSpawn_<Color>_NN) that is used; otherwise a
/// test circuit is generated: white robot-arena road with a black line-tracing stripe.
/// </summary>
public static class TrackBuilder
{
    public const float RoadWidth = 14f;
    const float SampleSpacing = 2f, CheckpointSpacing = 30f, GemSpacing = 24f;

    // Closed loop, metres, driven in this order (~600 m)
    static readonly Vector2[] Control =
    {
        new Vector2(0, 0), new Vector2(70, 0), new Vector2(120, 15), new Vector2(140, 55),
        new Vector2(120, 95), new Vector2(80, 100), new Vector2(60, 75), new Vector2(30, 70),
        new Vector2(10, 100), new Vector2(-30, 110), new Vector2(-70, 90), new Vector2(-85, 50),
        new Vector2(-70, 10), new Vector2(-40, -15),
    };

    public static TrackLayout FromScene()
    {
        var cpObjects = GameObject.FindGameObjectsWithTag("Checkpoint");
        if (cpObjects.Length == 0) return null;

        var layout = new TrackLayout();
        layout.checkpoints = cpObjects.OrderBy(g => g.name).Select(g => g.transform).ToArray();
        for (int i = 0; i < layout.checkpoints.Length; i++)
        {
            var cp = layout.checkpoints[i];
            var col = cp.GetComponent<Collider>();
            if (col == null) { col = cp.gameObject.AddComponent<BoxCollider>(); ((BoxCollider)col).size = new Vector3(RoadWidth + 4, 6, 3); }
            col.isTrigger = true;
            (cp.GetComponent<Checkpoint>() ?? cp.gameObject.AddComponent<Checkpoint>()).index = i;
        }
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

    public static TrackLayout Build(GemDriftConfig cfg, Transform parent)
    {
        var root = new GameObject("GeneratedTrack").transform;
        root.SetParent(parent, false);

        var pts = SampleLoop();
        int n = pts.Count;
        var tangents = new Vector3[n];
        var rights = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            tangents[i] = (pts[(i + 1) % n] - pts[(i - 1 + n) % n]).normalized;
            rights[i] = Vector3.Cross(Vector3.up, tangents[i]);
        }

        // Ground: one big flat collider at y = 0 is the only driving surface
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.layer = Layers.Road;
        ground.transform.SetParent(root, false);
        ground.transform.localPosition = new Vector3(30, -0.5f, 50);
        ground.transform.localScale = new Vector3(1200, 1, 1200);
        ground.GetComponent<Renderer>().sharedMaterial = cfg.ground;

        Strip(root, "Road", pts, rights, RoadWidth, 0.02f, cfg.road);
        Strip(root, "LineTracingStripe", pts, rights, 0.7f, 0.035f, cfg.roadLine);
        BuildWalls(root, cfg, pts, rights);

        // Finish line (checker) at sample 0
        var finish = GameObject.CreatePrimitive(PrimitiveType.Quad);
        finish.name = "FinishLine";
        Object.Destroy(finish.GetComponent<Collider>());
        finish.transform.SetParent(root, false);
        finish.transform.position = pts[0] + Vector3.up * 0.045f;
        finish.transform.rotation = Quaternion.LookRotation(Vector3.down, tangents[0]);
        finish.transform.localScale = new Vector3(RoadWidth, 3f, 1);
        finish.GetComponent<Renderer>().sharedMaterial = cfg.checker;

        var layout = new TrackLayout();

        // Checkpoints every ~30 m; CP_00 sits on the finish line
        int cpStride = Mathf.RoundToInt(CheckpointSpacing / SampleSpacing);
        var cps = new List<Transform>();
        for (int i = 0, k = 0; i < n - cpStride / 2; i += cpStride, k++)
        {
            var go = new GameObject("CP_" + k.ToString("00")) { tag = "Checkpoint" };
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pts[i], Quaternion.LookRotation(tangents[i]));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(RoadWidth + 4f, 6f, 3f);
            box.center = new Vector3(0, 3f, 0);
            go.AddComponent<Checkpoint>().index = k;
            cps.Add(go.transform);
        }
        layout.checkpoints = cps.ToArray();

        // Start grid 10 m past the line, side by side
        int s = Mathf.RoundToInt(10f / SampleSpacing);
        for (int p = 0; p < 2; p++)
            layout.starts[p] = new Pose(pts[s] + rights[s] * (p == 0 ? -3f : 3f), Quaternion.LookRotation(tangents[s]));

        // Gems: pairs on straights (one per lane), a single gold on the inside of sharp corners
        int gemStride = Mathf.RoundToInt(GemSpacing / SampleSpacing);
        int look = 4;
        for (int i = Mathf.RoundToInt(30f / SampleSpacing); i < n - 6; i += gemStride)
        {
            var tPrev = tangents[(i - look + n) % n];
            var tNext = tangents[(i + look) % n];
            float turnDeg = Vector3.Angle(tPrev, tNext);
            if (turnDeg > 28f)
            {
                float inner = Mathf.Sign(Vector3.Cross(tPrev, tNext).y);
                layout.gems.Add((pts[i] + rights[i] * inner * 4.5f, GemColor.Gold));
            }
            else
            {
                var c = (i / gemStride) % 3 == 2 ? GemColor.Blue : GemColor.Red;
                layout.gems.Add((pts[i] - rights[i] * 3.5f, c));
                layout.gems.Add((pts[i] + rights[i] * 3.5f, c));
            }
        }
        return layout;
    }

    // Catmull-Rom through Control, resampled every SampleSpacing metres
    static List<Vector3> SampleLoop()
    {
        var dense = new List<Vector3>();
        int m = Control.Length;
        for (int i = 0; i < m; i++)
        {
            Vector3 p0 = V(Control[(i - 1 + m) % m]), p1 = V(Control[i]), p2 = V(Control[(i + 1) % m]), p3 = V(Control[(i + 2) % m]);
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

    static Vector3 V(Vector2 p) => new Vector3(p.x, 0, p.y);

    static void Strip(Transform parent, string name, List<Vector3> pts, Vector3[] rights, float width, float y, Material mat)
    {
        int n = pts.Count;
        var verts = new Vector3[(n + 1) * 2];
        var uvs = new Vector2[verts.Length];
        var tris = new int[n * 6];
        float dist = 0f;
        for (int i = 0; i <= n; i++)
        {
            int k = i % n;
            if (i > 0) dist += Vector3.Distance(pts[i - 1], pts[k]);
            var up = Vector3.up * y;
            verts[i * 2] = pts[k] - rights[k] * width * 0.5f + up;
            verts[i * 2 + 1] = pts[k] + rights[k] * width * 0.5f + up;
            uvs[i * 2] = new Vector2(0, dist / width);
            uvs[i * 2 + 1] = new Vector2(1, dist / width);
        }
        for (int i = 0; i < n; i++)
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
    }

    // Box colliders along both edges; visuals merged into two meshes (red / white blocks)
    static void BuildWalls(Transform parent, GemDriftConfig cfg, List<Vector3> pts, Vector3[] rights)
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cube = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(temp);

        var colliders = new GameObject("WallColliders") { layer = Layers.Wall };
        colliders.transform.SetParent(parent, false);
        var red = new List<CombineInstance>();
        var white = new List<CombineInstance>();
        const float height = 1.1f, thick = 0.7f;
        float offset = RoadWidth * 0.5f + thick * 0.5f;
        int n = pts.Count;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var a = pts[i] + rights[i] * offset * side;
                var b = pts[j] + rights[j] * offset * side;
                var center = (a + b) * 0.5f + Vector3.up * height * 0.5f;
                var rot = Quaternion.LookRotation(b - a);
                var size = new Vector3(thick, height, Vector3.Distance(a, b) + 0.3f);

                // a BoxCollider can't be rotated on its own, so each wall piece gets its own object
                var piece = new GameObject("Wall") { layer = Layers.Wall };
                piece.transform.SetParent(colliders.transform, false);
                piece.transform.SetPositionAndRotation(center, rot);
                piece.AddComponent<BoxCollider>().size = size;
                piece.isStatic = true;

                var ci = new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(center, rot, size) };
                if ((i / 3) % 2 == 0) red.Add(ci); else white.Add(ci);
            }
        }
        Merge(parent, "Walls_Red", red, cfg.wallA);
        Merge(parent, "Walls_White", white, cfg.wallB);
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
}
