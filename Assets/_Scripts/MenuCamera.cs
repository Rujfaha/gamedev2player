using UnityEngine;

/// <summary>Main-menu background camera: slowly orbits an AI truck as it drives around the live course.</summary>
public class MenuCamera : MonoBehaviour
{
    const float Radius = 13f, Height = 4.5f, DegPerSecond = 9f, Sharpness = 3f;

    Transform target;
    float angle = 150f;

    public static MenuCamera Create(Transform target)
    {
        var go = new GameObject("Camera_Menu");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1500f;
        go.AddComponent<AudioListener>();
        var m = go.AddComponent<MenuCamera>();
        m.target = target;
        m.transform.position = m.Wanted();
        return m;
    }

    Vector3 Wanted() => target.position + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, -Radius) + Vector3.up * Height;

    void LateUpdate()
    {
        angle += DegPerSecond * Time.deltaTime;
        var wanted = Wanted();
        // the one-way course sends the truck back to the start: cut instead of flying across the map
        if ((transform.position - wanted).sqrMagnitude > 2500f) transform.position = wanted;
        transform.position = Vector3.Lerp(transform.position, wanted, 1f - Mathf.Exp(-Sharpness * Time.deltaTime));
        transform.rotation = Quaternion.LookRotation(target.position + Vector3.up - transform.position);
    }
}
