using UnityEngine;

/// <summary>Chase camera for one player, drawn into its half of the split screen.</summary>
public class FollowCamera : MonoBehaviour
{
    public float distance = 9f, height = 4.2f, lookAhead = 5f;
    public float positionSharpness = 6f, rotationSharpness = 9f;

    CarController target;
    Rigidbody targetRb;
    Camera cam;

    public static FollowCamera Create(CarController car, Rect viewport)
    {
        var go = new GameObject("Camera_P" + (car.PlayerIndex + 1));
        var cam = go.AddComponent<Camera>();
        cam.rect = viewport;
        cam.fieldOfView = 62f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1500f;
        if (car.PlayerIndex == 0) go.AddComponent<AudioListener>();

        var f = go.AddComponent<FollowCamera>();
        f.cam = cam;
        f.target = car;
        f.targetRb = car.GetComponent<Rigidbody>();
        f.Snap();
        return f;
    }

    Vector3 ChaseDirection()
    {
        var t = target.transform;
        var fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
        var vel = targetRb.linearVelocity;
        vel.y = 0f;
        // during a drift, look partly along where the truck is actually going
        if (vel.magnitude > 4f && Vector3.Dot(vel, fwd) > 0f) return Vector3.Slerp(fwd, vel.normalized, 0.45f).normalized;
        return fwd;
    }

    void Snap()
    {
        var dir = ChaseDirection();
        transform.position = target.transform.position - dir * distance + Vector3.up * height;
        transform.rotation = Quaternion.LookRotation(target.transform.position + dir * lookAhead - transform.position);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return; // paused
        var dir = ChaseDirection();
        var carPos = target.transform.position;
        var desired = carPos - dir * distance + Vector3.up * height;
        if ((transform.position - desired).sqrMagnitude > 900f) { Snap(); return; } // after a respawn
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionSharpness * dt));
        var look = Quaternion.LookRotation(carPos + dir * lookAhead + Vector3.up - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-rotationSharpness * dt));
        float speed01 = Mathf.Clamp01(Mathf.Abs(target.Speed) / target.maxSpeed);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f + speed01 * 12f + (target.Boosting ? 6f : 0f), 1f - Mathf.Exp(-4f * dt));

        // Settings > Camera shake: a little rumble on nitro, a bigger jolt when gems spill
        if (GameSettings.CameraShake)
        {
            float shake = (target.NitroActive ? 0.05f : 0f) + (Time.time - target.Bed.LastSpillTime < 0.25f ? 0.15f : 0f);
            if (shake > 0f) transform.position += Random.insideUnitSphere * shake;
        }
    }
}
