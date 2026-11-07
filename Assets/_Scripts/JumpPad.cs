using UnityEngine;

/// <summary>Jump ramp in front of a gap in the road: launches every car over it (CarController.Launch).
/// Too slow is impossible (the ramp sets a minimum speed), but steering off the road mid-air still drops you into space.</summary>
public class JumpPad : MonoBehaviour
{
    public float upSpeed = 8.5f, minForward = 24f;

    public static JumpPad Create(GemDriftConfig cfg, Transform parent, Vector3 pos, Vector3 forward, float width)
    {
        const float length = 6f, height = 0.9f;
        var go = new GameObject("JumpRamp");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward));

        // wedge: flat at the front edge, rising to `height` at the lip (visual only: the trigger does the launching)
        var ramp = new GameObject("Ramp");
        ramp.transform.SetParent(go.transform, false);
        float w = width * 0.5f;
        var v = new[]
        {
            new Vector3(-w, 0.02f, -length * 0.5f), new Vector3(w, 0.02f, -length * 0.5f),
            new Vector3(-w, height, length * 0.5f), new Vector3(w, height, length * 0.5f),
            new Vector3(-w, 0.02f, length * 0.5f), new Vector3(w, 0.02f, length * 0.5f),
        };
        var mesh = new Mesh
        {
            name = "Ramp",
            vertices = new[] { v[0], v[1], v[2], v[3], v[2], v[3], v[4], v[5], v[0], v[2], v[4], v[1], v[5], v[3] },
            uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0.85f), new Vector2(1, 0.85f),
                         new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.9f), new Vector2(1, 0), new Vector2(1, 0.9f), new Vector2(1, 1) },
            triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7, 8, 10, 9, 11, 13, 12 },
        };
        mesh.RecalculateNormals();
        ramp.AddComponent<MeshFilter>().sharedMesh = mesh;
        ramp.AddComponent<MeshRenderer>().sharedMaterial = cfg.jumpPad;

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(width + 2f, 4f, length);
        box.center = new Vector3(0f, 2f, 0f);
        return go.AddComponent<JumpPad>();
    }

    void OnTriggerEnter(Collider other)
    {
        var car = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<CarController>() : null;
        if (car != null) car.Launch(upSpeed, minForward);
    }
}
