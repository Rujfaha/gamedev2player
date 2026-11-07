using UnityEngine;

/// <summary>Glowing chevron pad on the road: any car driving over it gets a short speed boost (CarController.PadBoost).</summary>
public class BoostPad : MonoBehaviour
{
    public float seconds = 1.2f;
    Renderer glow;
    MaterialPropertyBlock block;

    public static BoostPad Create(GemDriftConfig cfg, Transform parent, Vector3 pos, Vector3 forward, float width, float length = 7f)
    {
        var go = new GameObject("BoostPad");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.LookRotation(forward));
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(go.transform, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, texture "up" = driving direction
        quad.transform.localScale = new Vector3(width, length, 1f);
        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = cfg.boostPad;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(width, 3f, length);
        box.center = new Vector3(0f, 1.5f, 0f);
        var pad = go.AddComponent<BoostPad>();
        pad.glow = r;
        return pad;
    }

    void OnTriggerEnter(Collider other)
    {
        var car = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<CarController>() : null;
        if (car != null) car.PadBoost(seconds);
    }

    // chevrons pulse so the pad reads as "go faster" from far away
    void Update()
    {
        if (glow == null) return;
        block ??= new MaterialPropertyBlock();
        float k = 1.1f + 0.6f * Mathf.Sin(Time.time * 6f);
        block.SetColor("_EmissionColor", Color.white * k);
        glow.SetPropertyBlock(block);
    }
}
