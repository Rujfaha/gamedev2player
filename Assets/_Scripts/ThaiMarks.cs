using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Legacy uGUI Text does no OpenType shaping, so Thai marks that a font would normally move are drawn in the wrong place:
/// a tone mark after an upper vowel (ที่ กิ่ง ตั้ง วิ่ง) lands on the vowel and disappears, and a tone mark over a tall-stemmed
/// consonant (ป ฝ ฟ ฬ, as in ปุ่ม) lands on the stem. This effect fixes both, using the glyph boxes the text generator already
/// made, so it works for any font and size. Add it before any Shadow / Outline.
/// </summary>
public class ThaiMarks : BaseMeshEffect
{
    static bool IsTone(char c) => c >= '่' && c <= '๋';                                          // ่ ้ ๊ ๋
    static bool IsUpperVowel(char c) => c == 'ั' || (c >= 'ิ' && c <= 'ื') || c == '็'; // ั ิ ี ึ ื ็
    static bool IsLowerVowel(char c) => c == 'ุ' || c == 'ู' || c == 'ฺ';                   // ุ ู ฺ
    static bool IsConsonant(char c) => c >= 'ก' && c <= 'ฮ';
    static bool IsTall(char c) => c == 'ป' || c == 'ฝ' || c == 'ฟ' || c == 'ฬ';       // ป ฝ ฟ ฬ
    static bool HasQuad(char c) => !char.IsWhiteSpace(c) && c != '​' && c != '‌' && c != '‍';

    public override void ModifyMesh(VertexHelper vh)
    {
        var text = graphic as Text;
        if (!IsActive() || text == null || vh.currentVertCount == 0) return;
        string s = text.text;
        if (string.IsNullOrEmpty(s) || s.IndexOf('<') >= 0) return; // rich text changes which characters have quads

        int quads = 0;
        bool hasTone = false;
        foreach (char c in s)
        {
            if (HasQuad(c)) quads++;
            if (IsTone(c)) hasTone = true;
        }
        if (!hasTone || quads * 4 != vh.currentVertCount) return; // nothing to do, or not the mesh we expect: leave it alone

        char prev = '\0', baseChar = '\0';
        int q = -1, prevQuad = -1, baseQuad = -1;
        float gap = text.fontSize * 0.03f;
        foreach (char c in s)
        {
            if (!HasQuad(c)) { prev = baseChar = '\0'; continue; }
            q++;
            if (IsTone(c))
            {
                if (IsUpperVowel(prev)) Lift(vh, q, prevQuad, gap);
                else if (IsTall(baseChar) && (prev == baseChar || IsLowerVowel(prev))) Nudge(vh, q, baseQuad);
            }
            if (IsConsonant(c)) { baseChar = c; baseQuad = q; }
            prev = c;
            prevQuad = q;
        }
    }

    static void Bounds(VertexHelper vh, int quad, out float left, out float right, out float bottom, out float top)
    {
        var v = new UIVertex();
        left = bottom = float.MaxValue;
        right = top = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            vh.PopulateUIVertex(ref v, quad * 4 + i);
            left = Mathf.Min(left, v.position.x);
            right = Mathf.Max(right, v.position.x);
            bottom = Mathf.Min(bottom, v.position.y);
            top = Mathf.Max(top, v.position.y);
        }
    }

    static void Move(VertexHelper vh, int quad, float dx, float dy)
    {
        var v = new UIVertex();
        for (int i = 0; i < 4; i++)
        {
            vh.PopulateUIVertex(ref v, quad * 4 + i);
            v.position += new Vector3(dx, dy, 0f);
            vh.SetUIVertex(v, quad * 4 + i);
        }
    }

    // put the glyph box `mark` just above the glyph box `vowel`, centred on it
    static void Lift(VertexHelper vh, int mark, int vowel, float gap)
    {
        Bounds(vh, vowel, out float vl, out float vr, out _, out float vowelTop);
        Bounds(vh, mark, out float ml, out float mr, out float markBottom, out _);
        float dy = vowelTop + gap - markBottom;
        if (dy > 0f) Move(vh, mark, (vl + vr) * 0.5f - (ml + mr) * 0.5f, dy);
    }

    // over a tall stem (ป ฝ ฟ ฬ) the mark moves left, clear of the stem
    static void Nudge(VertexHelper vh, int mark, int consonant)
    {
        Bounds(vh, consonant, out float cl, out float cr, out _, out _);
        Move(vh, mark, -(cr - cl) * 0.28f, 0f);
    }
}
