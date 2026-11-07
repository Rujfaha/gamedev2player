using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Nebula Ring (space course) pieces that TrackBuilder doesn't do for the other courses:
/// where the jump ramps / gaps and boost pads go, the floating road with colliders, and the space scenery
/// (portal rings over the road, support pylons, tumbling asteroids, a ringed gas giant and a moon).
/// </summary>
public static class SpaceTrack
{
    const int PadToGap = 3, GapSamples = 7;          // gap starts 6 m after the ramp and is 14 m long
    const int JumpClear = 45, MaxJumps = 2;          // a jump needs ~90 m of straight road after the ramp
    const int BoostSpacing = 55, MaxBoosts = 7;      // a boost pad every ~110 m or more

    public class Plan
    {
        public readonly List<int> jumps = new List<int>();
        public readonly List<int> boosts = new List<int>();
        public readonly HashSet<int> gap = new HashSet<int>();
    }

    /// <summary>Largest heading change (degrees) between sample i and the next `ahead` samples.</summary>
    static float Bend(Vector3[] t, int i, int ahead)
    {
        int n = t.Length;
        float max = 0f;
        for (int k = 1; k <= ahead; k++) max = Mathf.Max(max, Vector3.Angle(t[i], t[(i + k) % n]));
        return max;
    }

    public static bool NearGap(HashSet<int> gap, int i, int margin, int n)
    {
        if (gap.Count == 0) return false;
        for (int k = -margin; k <= margin; k++)
            if (gap.Contains(((i + k) % n + n) % n)) return true;
        return false;
    }

    public static Plan PlanFeatures(Vector3[] tangents)
    {
        int n = tangents.Length;
        var plan = new Plan();
        int last = -999;
        for (int i = 40; i < n - JumpClear - 10 && plan.jumps.Count < MaxJumps; i++)
        {
            if (i - last < 150) continue;
            if (Bend(tangents, i, JumpClear) > 5f || Bend(tangents, (i - 10 + n) % n, 10) > 8f) continue;
            plan.jumps.Add(i);
            for (int g = i + PadToGap; g < i + PadToGap + GapSamples; g++) plan.gap.Add(g % n);
            last = i;
        }

        last = -999;
        for (int i = 25; i < n - 15 && plan.boosts.Count < MaxBoosts; i++)
        {
            if (i - last < BoostSpacing) continue;
            bool nearJump = false;
            foreach (int j in plan.jumps) if (i > j - 25 && i < j + JumpClear) nearJump = true;
            if (nearJump || Bend(tangents, i, 12) > 12f) continue;
            plan.boosts.Add(i);
            last = i;
        }
        return plan;
    }

    /// <summary>The road surface (with mesh colliders on the Road layer), broken into pieces around the gaps.</summary>
    public static void RoadPieces(Transform root, List<Vector3> pts, Vector3[] rights, float[] widths, HashSet<int> gap, Material mat)
    {
        int n = pts.Count;
        // start right after a gap (or at 0 when there is none) and walk once round the loop
        int start = 0;
        for (int i = 0; i < n; i++) if (gap.Contains(i) && !gap.Contains((i + 1) % n)) { start = (i + 1) % n; break; }
        var piece = new List<int>();
        for (int k = 0; k <= n; k++)
        {
            int i = (start + k) % n;
            bool open = k == n || gap.Contains(i);
            if (!open) { piece.Add(i); continue; }
            if (k == n && gap.Count == 0) piece.Add(start); // closed loop: join the end to the start
            else if (!gap.Contains(i)) piece.Add(i);
            if (piece.Count >= 2) BuildPiece(root, pts, rights, widths, piece, mat);
            piece.Clear();
        }
    }

    static void BuildPiece(Transform root, List<Vector3> pts, Vector3[] rights, float[] widths, List<int> idx, Material mat)
    {
        var p = new List<Vector3>();
        var r = new Vector3[idx.Count];
        var w = new float[idx.Count];
        for (int k = 0; k < idx.Count; k++) { p.Add(pts[idx[k]]); r[k] = rights[idx[k]]; w[k] = widths[idx[k]]; }
        var go = TrackBuilder.Strip(root, "Road", p, r, w, 0.02f, mat, false);
        go.layer = Layers.Road;
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;

        // thick underside so the floating road doesn't look paper thin from the side
        var under = TrackBuilder.Strip(root, "RoadHull", p, r, w, -0.6f, mat, false);
        var mesh = under.GetComponent<MeshFilter>().sharedMesh;
        var tris = mesh.triangles;
        for (int t = 0; t < tris.Length; t += 3) (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]); // face down
        mesh.triangles = tris;
        mesh.RecalculateNormals();
    }

    // ---------- Scenery ----------

    public static void Dress(GemDriftConfig cfg, Transform root, List<Vector3> pts, Vector3[] rights, Vector3[] tangents, float[] widths, Plan plan, Bounds bounds)
    {
        int n = pts.Count;
        var deco = new GameObject("SpaceDecor").transform;
        deco.SetParent(root, false);

        // boost pads, alternating lanes so taking one is a choice
        for (int k = 0; k < plan.boosts.Count; k++)
        {
            int i = plan.boosts[k];
            float lane = (k % 3 - 1) * widths[i] * 0.26f;
            BoostPad.Create(cfg, deco, pts[i] + rights[i] * lane, tangents[i], widths[i] * 0.42f);
        }

        // jump ramps + glowing lips on both sides of each gap
        foreach (int j in plan.jumps)
        {
            JumpPad.Create(cfg, deco, pts[j], tangents[j], widths[j]);
            int g0 = (j + PadToGap) % n, g1 = (j + PadToGap + GapSamples - 1) % n;
            Lip(cfg, deco, pts[(g0 - 1 + n) % n], tangents[g0], widths[g0]);
            Lip(cfg, deco, pts[(g1 + 1) % n], tangents[g1], widths[g1]);
        }

        // portal rings over the road every ~120 m, away from the jumps
        var torus = Torus(1f, 0.035f, 48, 10);
        int ringCount = 0;
        for (int i = 20; i < n; i += 60)
        {
            if (NearGap(plan.gap, i, 30, n)) continue;
            float radius = widths[i] * 0.5f + 3f;
            var ring = new GameObject("PortalRing");
            ring.transform.SetParent(deco, false);
            ring.transform.SetPositionAndRotation(pts[i] + Vector3.up * radius * 0.55f, Quaternion.LookRotation(tangents[i]));
            ring.transform.localScale = Vector3.one * radius;
            ring.AddComponent<MeshFilter>().sharedMesh = torus;
            var mr = ring.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ringCount++ % 2 == 0 ? cfg.ringCyan : cfg.ringMagenta;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // support pylons under the road, fading down into the void
        for (int i = 10; i < n; i += 22)
        {
            if (NearGap(plan.gap, i, 4, n)) continue;
            foreach (int side in new[] { -1, 1 })
            {
                var pylon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(pylon.GetComponent<Collider>());
                pylon.name = "Pylon";
                pylon.transform.SetParent(deco, false);
                pylon.transform.position = pts[i] + rights[i] * side * widths[i] * 0.3f + Vector3.down * 40.6f;
                pylon.transform.localScale = new Vector3(0.9f, 40f, 0.9f);
                pylon.GetComponent<Renderer>().sharedMaterial = cfg.pylon;
                var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(band.GetComponent<Collider>());
                band.transform.SetParent(deco, false);
                band.transform.position = pts[i] + rights[i] * side * widths[i] * 0.3f + Vector3.down * 2.2f;
                band.transform.localScale = new Vector3(1.1f, 0.12f, 1.1f);
                band.GetComponent<Renderer>().sharedMaterial = side < 0 ? cfg.ringCyan : cfg.ringMagenta;
            }
        }

        // asteroids: tumbling rocks all around, kept clear of the road
        var rocks = new[] { Rock(1), Rock(2), Rock(3) };
        var rng = new System.Random(7);
        float R() => (float)rng.NextDouble();
        int placed = 0;
        for (int tries = 0; tries < 2000 && placed < 160; tries++)
        {
            var pos = new Vector3(Mathf.Lerp(bounds.min.x - 260f, bounds.max.x + 260f, R()), Mathf.Lerp(-70f, 60f, R()),
                                  Mathf.Lerp(bounds.min.z - 260f, bounds.max.z + 260f, R()));
            float size = Mathf.Lerp(1.5f, 9f, R() * R()) * (R() < 0.08f ? 3f : 1f);
            float clear = 0f;
            float best = float.MaxValue;
            for (int i = 0; i < n; i += 3) best = Mathf.Min(best, new Vector2(pos.x - pts[i].x, pos.z - pts[i].z).sqrMagnitude);
            clear = Mathf.Sqrt(best);
            // near the road only far above or below it
            if (clear < 22f + size && pos.y > -18f - size && pos.y < 14f + size) continue;
            var go = new GameObject("Asteroid");
            go.transform.SetParent(deco, false);
            go.transform.SetPositionAndRotation(pos, Random.rotationUniform);
            go.transform.localScale = new Vector3(size, size * Mathf.Lerp(0.6f, 1f, R()), size * Mathf.Lerp(0.7f, 1.1f, R()));
            go.AddComponent<MeshFilter>().sharedMesh = rocks[placed % rocks.Length];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cfg.asteroid;
            if (size < 4f) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<Spinner>().degPerSecond = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f) * 30f;
            placed++;
        }

        StarField(cfg, deco, bounds.center);

        // gas giant with a ring, and a moon, far away
        var c = bounds.center;
        var planet = Ball("Planet", deco, c + new Vector3(650f, -120f, 950f), 760f, cfg.planet);
        planet.transform.rotation = Quaternion.Euler(18f, 30f, -12f);
        planet.AddComponent<Spinner>().degPerSecond = new Vector3(0f, 0.6f, 0f);
        var ringGo = new GameObject("PlanetRing");
        ringGo.transform.SetParent(planet.transform, false);
        ringGo.transform.localScale = Vector3.one / 760f;
        ringGo.AddComponent<MeshFilter>().sharedMesh = Annulus(560f, 1000f, 96);
        var ringRenderer = ringGo.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = cfg.planetRing;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var moon = Ball("Moon", deco, c + new Vector3(-820f, 260f, -380f), 190f, cfg.moon);
        moon.AddComponent<Spinner>().degPerSecond = new Vector3(0f, 1.5f, 0f);
    }

    /// <summary>Crisp stars: particles on a huge shell around the course (a sky texture would blur them).</summary>
    static void StarField(GemDriftConfig cfg, Transform parent, Vector3 centre)
    {
        var go = new GameObject("Stars");
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = 3000;
        main.startLifetime = 1e6f;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.enabled = false;
        ps.GetComponent<ParticleSystemRenderer>().material = cfg.spark;
        var rng = new System.Random(99);
        var p = new ParticleSystem.Particle[2600];
        for (int i = 0; i < p.Length; i++)
        {
            var dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.up;
            float b = Mathf.Pow((float)rng.NextDouble(), 3f);
            var tint = Color.Lerp(new Color(0.7f, 0.82f, 1f), new Color(1f, 0.9f, 0.75f), (float)rng.NextDouble());
            p[i].position = centre + dir.normalized * 1250f;
            p[i].startSize = Mathf.Lerp(1.6f, 6f, b);
            p[i].startColor = Color.Lerp(tint * 0.55f, Color.white, b);
            p[i].remainingLifetime = p[i].startLifetime = 1e6f;
        }
        ps.SetParticles(p, p.Length);
        ps.Play();
    }

    static GameObject Ball(string name, Transform parent, Vector3 pos, float diameter, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * diameter;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    // a glowing bar across the road at the edge of a gap
    static void Lip(GemDriftConfig cfg, Transform parent, Vector3 pos, Vector3 tangent, float width)
    {
        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(bar.GetComponent<Collider>());
        bar.name = "GapLip";
        bar.transform.SetParent(parent, false);
        bar.transform.SetPositionAndRotation(pos + Vector3.down * 0.25f, Quaternion.LookRotation(tangent));
        bar.transform.localScale = new Vector3(width + 1.4f, 0.55f, 0.35f);
        bar.GetComponent<Renderer>().sharedMaterial = cfg.ringMagenta;
    }

    // ---------- Procedural meshes ----------

    static Mesh Torus(float radius, float tube, int seg, int sides)
    {
        var v = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            var centre = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
            for (int j = 0; j <= sides; j++)
            {
                float b = j * Mathf.PI * 2f / sides;
                v.Add(centre + (centre.normalized * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * tube);
            }
        }
        int row = sides + 1;
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * row + j, b = a + row;
                tris.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
            }
        var m = new Mesh { name = "Torus", vertices = v.ToArray(), triangles = tris.ToArray() };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Flat ring in the XZ plane, both faces, UV u = 0 inside .. 1 outside.</summary>
    static Mesh Annulus(float inner, float outer, int seg)
    {
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(d * inner); uv.Add(new Vector2(0f, 0.5f));
            v.Add(d * outer); uv.Add(new Vector2(1f, 0.5f));
        }
        // the underside gets its own copy of the vertices: shared vertices would average the two normals to zero
        int top = v.Count;
        for (int i = 0; i < top; i++) { v.Add(v[i]); uv.Add(uv[i]); }
        for (int i = 0; i < seg; i++)
        {
            int a = i * 2, b = top + i * 2;
            tris.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });   // one face
            tris.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });   // the other
        }
        var m = new Mesh { name = "Annulus", vertices = v.ToArray(), uv = uv.ToArray(), triangles = tris.ToArray() };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    /// <summary>Low-poly rock: a lumpy subdivided icosahedron with flat shading and spherical UVs.</summary>
    static Mesh Rock(int seed)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var verts = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        int[] faces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        // one subdivision
        var mid = new Dictionary<long, int>();
        int Mid(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (mid.TryGetValue(key, out int m)) return m;
            verts.Add((verts[a] + verts[b]) * 0.5f);
            return mid[key] = verts.Count - 1;
        }
        var tris = new List<int>();
        for (int f = 0; f < faces.Length; f += 3)
        {
            int a = faces[f], b = faces[f + 1], c = faces[f + 2];
            int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        var rng = new System.Random(seed * 977);
        for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized * (0.5f * (0.75f + 0.4f * (float)rng.NextDouble()));

        // unshare vertices for flat shading
        var fv = new Vector3[tris.Count];
        var fuv = new Vector2[tris.Count];
        var ft = new int[tris.Count];
        for (int i = 0; i < tris.Count; i++)
        {
            var p = verts[tris[i]];
            fv[i] = p;
            var d = p.normalized;
            fuv[i] = new Vector2(Mathf.Atan2(d.x, d.z) / (Mathf.PI * 2f) + 0.5f, d.y * 0.5f + 0.5f);
            ft[i] = i;
        }
        var mesh = new Mesh { name = "Rock" + seed, vertices = fv, uv = fuv, triangles = ft };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
