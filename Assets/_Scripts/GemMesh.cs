using UnityEngine;

/// <summary>Builds a low-poly cut-gem mesh in code (about 1 unit tall) and gem visuals.</summary>
public static class GemMesh
{
    static Mesh cached;

    public static Mesh Get()
    {
        if (cached != null) return cached;
        const int sides = 8;
        const float tableR = 0.28f, girdleR = 0.5f, top = 0.32f, girdle = 0.12f, bottom = -0.5f;
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();

        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        Vector3 Ring(float r, float y, int k) =>
            new Vector3(Mathf.Cos(k * Mathf.PI * 2 / sides) * r, y, Mathf.Sin(k * Mathf.PI * 2 / sides) * r);

        var tableC = new Vector3(0, top, 0);
        var tip = new Vector3(0, bottom, 0);
        for (int k = 0; k < sides; k++)
        {
            Vector3 t0 = Ring(tableR, top, k), t1 = Ring(tableR, top, k + 1);
            Vector3 g0 = Ring(girdleR, girdle, k), g1 = Ring(girdleR, girdle, k + 1);
            Tri(tableC, t1, t0);   // table
            Tri(t0, t1, g1);       // crown
            Tri(t0, g1, g0);
            Tri(g0, g1, tip);      // pavilion
        }
        cached = new Mesh { name = "Gem" };
        cached.SetVertices(verts);
        cached.SetTriangles(tris, 0);
        cached.RecalculateNormals();
        cached.RecalculateBounds();
        return cached;
    }

    /// <summary>A gem with no collider, e.g. for the truck bed.</summary>
    public static GameObject CreateVisual(GemDriftConfig cfg, GemColor color, Transform parent, float size)
    {
        var go = new GameObject("Gem_" + color);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;
        go.AddComponent<MeshFilter>().sharedMesh = Get();
        go.AddComponent<MeshRenderer>().sharedMaterial = cfg.GemMaterial(color);
        return go;
    }
}
