using UnityEngine;
using UnityEngine.UI;

/// <summary>Leans a Text to the right (italic look for titles) by shearing its mesh. Add it after any Shadow / Outline.</summary>
public class UISkew : BaseMeshEffect
{
    public float skew = 0.2f;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        var v = new UIVertex();
        float middle = graphic.rectTransform.rect.center.y;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            v.position.x += (v.position.y - middle) * skew;
            vh.SetUIVertex(v, i);
        }
    }
}
