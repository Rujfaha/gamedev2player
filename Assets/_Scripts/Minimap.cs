using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// North-up minimap in the top-right of a player's half: the course is drawn once into a texture,
/// and each truck is an arrow in its player colour (own arrow bigger, with a white outline).
/// No extra camera, so it costs almost nothing in WebGL.
/// </summary>
public class Minimap : MonoBehaviour
{
    const int TexSize = 256;
    const float Pad = 14f; // pixels of border around the course

    static Texture2D mapTex;
    static Sprite arrowSprite, arrowOutlineSprite;
    static Rect worldRect;   // x/z area the texture covers
    static List<Vector3> cachedPath;

    RectTransform mapRect;
    RectTransform[] arrows;
    CarController[] cars;

    public static Minimap Create(RectTransform parent, Vector2 size, TrackLayout layout, CarController[] cars, int owner, Color[] playerColors)
    {
        Prepare(layout);

        var go = new GameObject("Minimap", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-24, -24);
        rt.sizeDelta = size;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.45f);
        bg.raycastTarget = false;

        var mapGo = new GameObject("Course", typeof(RectTransform));
        mapGo.transform.SetParent(rt, false);
        var map = mapGo.AddComponent<RawImage>();
        map.texture = mapTex;
        map.raycastTarget = false;
        var mr = (RectTransform)mapGo.transform;
        mr.anchorMin = Vector2.zero;
        mr.anchorMax = Vector2.one;
        mr.offsetMin = mr.offsetMax = Vector2.zero;

        var m = go.AddComponent<Minimap>();
        m.mapRect = mr;
        m.cars = cars;
        m.arrows = new RectTransform[cars.Length];
        // draw the other player first so your own arrow is on top
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < cars.Length; i++)
            {
                bool mine = i == owner;
                if ((pass == 0) == mine) continue;
                float s = mine ? 40f : i < 2 ? 30f : 22f; // AI trucks smallest
                if (mine) m.MakeArrow(mr, arrowOutlineSprite, Color.white, s + 10f, "Outline_P" + (i + 1));
                m.arrows[i] = m.MakeArrow(mr, arrowSprite, playerColors[i], s, "Arrow_P" + (i + 1));
                if (mine) m.arrows[i].GetComponent<Image>().transform.SetAsLastSibling();
            }
        }
        // keep each outline glued to its arrow
        m.outlineFor = owner;
        return m;
    }

    int outlineFor = -1;
    RectTransform outline;

    RectTransform MakeArrow(RectTransform parent, Sprite sprite, Color color, float size, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        if (name.StartsWith("Outline")) outline = rt;
        return rt;
    }

    void LateUpdate()
    {
        var size = mapRect.rect.size;
        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] == null) continue;
            var p = cars[i].transform.position;
            var uv = new Vector2(
                Mathf.InverseLerp(worldRect.xMin, worldRect.xMax, p.x),
                Mathf.InverseLerp(worldRect.yMin, worldRect.yMax, p.z));
            var pos = new Vector2((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y);
            var rot = Quaternion.Euler(0, 0, -cars[i].transform.eulerAngles.y);
            arrows[i].anchoredPosition = pos;
            arrows[i].localRotation = rot;
            if (i == outlineFor && outline != null)
            {
                outline.anchoredPosition = pos;
                outline.localRotation = rot;
            }
        }
    }

    // ---------- one-time drawing ----------

    static void Prepare(TrackLayout layout)
    {
        if (mapTex != null && cachedPath == layout.path) return;
        cachedPath = layout.path;
        if (arrowSprite == null)
        {
            arrowSprite = ArrowSprite(false);
            arrowOutlineSprite = ArrowSprite(true);
        }

        var path = layout.path;
        var b = new Bounds(path[0], Vector3.zero);
        foreach (var p in path) b.Encapsulate(p);
        // square area so the map isn't stretched
        float side = Mathf.Max(b.size.x, b.size.z);
        float metresPerPixel = side / (TexSize - Pad * 2f);
        float half = side * 0.5f + Pad * metresPerPixel;
        worldRect = Rect.MinMaxRect(b.center.x - half, b.center.z - half, b.center.x + half, b.center.z + half);

        if (mapTex == null)
            mapTex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[TexSize * TexSize];
        float roadPx = Mathf.Max(2.5f, layout.roadWidth * 0.5f / metresPerPixel);

        Stroke(px, path, layout.loop, roadPx + 2.5f, new Color32(20, 20, 20, 230));     // dark edge
        Stroke(px, path, layout.loop, roadPx, new Color32(235, 235, 225, 255));          // road
        foreach (var sc in layout.shortcuts)                                             // shortcuts: thin orange
            Stroke(px, sc, false, Mathf.Max(1.5f, roadPx * 0.45f), new Color32(255, 150, 40, 255));

        // finish line (yellow bar) and, on a one-way route, a green start dot
        if (layout.checkpoints != null && layout.checkpoints.Length > 0)
        {
            var fin = layout.loop ? layout.checkpoints[0] : layout.checkpoints[layout.checkpoints.Length - 1];
            var a = fin.position - fin.right * layout.roadWidth * 0.7f;
            var c = fin.position + fin.right * layout.roadWidth * 0.7f;
            Stroke(px, new List<Vector3> { a, c }, false, 2.2f, new Color32(255, 210, 40, 255));
            if (!layout.loop) Disc(px, ToPx(path[0]), roadPx + 2f, new Color32(80, 220, 90, 255));
        }

        mapTex.SetPixels32(px);
        mapTex.Apply();
    }

    static Vector2 ToPx(Vector3 w) => new Vector2(
        Mathf.InverseLerp(worldRect.xMin, worldRect.xMax, w.x) * TexSize,
        Mathf.InverseLerp(worldRect.yMin, worldRect.yMax, w.z) * TexSize);

    static void Stroke(Color32[] px, List<Vector3> pts, bool loop, float radius, Color32 col)
    {
        int n = pts.Count;
        int segs = loop ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            Vector2 a = ToPx(pts[i]), b = ToPx(pts[(i + 1) % n]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(radius * 0.5f, 0.5f)));
            for (int s = 0; s <= steps; s++) Disc(px, Vector2.Lerp(a, b, s / (float)steps), radius, col);
        }
    }

    static void Disc(Color32[] px, Vector2 c, float r, Color32 col)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r)), x1 = Mathf.Min(TexSize - 1, Mathf.CeilToInt(c.x + r));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r)), y1 = Mathf.Min(TexSize - 1, Mathf.CeilToInt(c.y + r));
        float r2 = r * r;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - c.x, dy = y + 0.5f - c.y;
                if (dx * dx + dy * dy <= r2) px[y * TexSize + x] = col;
            }
    }

    // Arrow pointing up (= forward on a north-up map)
    static Sprite ArrowSprite(bool outlineOnly)
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        Vector2 tip = new Vector2(32, 60), left = new Vector2(8, 6), right = new Vector2(56, 6), notch = new Vector2(32, 20);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = InTri(p, tip, left, notch) || InTri(p, tip, notch, right);
                px[y * S + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
}
