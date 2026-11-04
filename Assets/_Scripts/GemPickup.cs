using UnityEngine;

/// <summary>
/// A gem lying on the track. Track gems respawn 10 s after being taken;
/// spilled gems fly out of a truck bed, land, and stay until someone grabs them.
/// </summary>
public class GemPickup : MonoBehaviour
{
    const float RespawnDelay = 10f, RestHeight = 0.9f, OwnerIgnoreTime = 1.2f;

    GemColor color;
    bool respawns, flying, available = true;
    float respawnAt, ignoreUntil, phase;
    Vector3 velocity;
    CarController ignoreCar;
    Transform visual;
    Collider trigger;

    public static GemPickup CreateOnTrack(GemDriftConfig cfg, GemColor color, Vector3 groundPos)
    {
        var p = Create(cfg, color, groundPos + Vector3.up * RestHeight);
        p.respawns = true;
        return p;
    }

    public static GemPickup CreateDropped(GemDriftConfig cfg, GemColor color, Vector3 pos, CarController from)
    {
        var p = Create(cfg, color, pos);
        p.flying = true;
        var back = -from.transform.forward;
        var side = from.transform.right * Random.Range(-1f, 1f);
        p.velocity = from.GetComponent<Rigidbody>().linearVelocity * 0.4f + (back * 3f + side * 3f) + Vector3.up * 6f;
        p.ignoreCar = from;
        p.ignoreUntil = Time.time + OwnerIgnoreTime;
        return p;
    }

    static GemPickup Create(GemDriftConfig cfg, GemColor color, Vector3 pos)
    {
        var go = new GameObject("GemPickup_" + color) { layer = Layers.Gem };
        go.transform.position = pos;
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        var sc = go.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 1.4f;
        var p = go.AddComponent<GemPickup>();
        p.color = color;
        p.trigger = sc;
        p.visual = GemMesh.CreateVisual(cfg, color, go.transform, color == GemColor.Gold ? 1.3f : 1.05f).transform;
        p.phase = Random.value * 10f;
        return p;
    }

    void Update()
    {
        float t = Time.time;
        if (!available)
        {
            if (respawns && t >= respawnAt) SetAvailable(true);
            return;
        }

        visual.localRotation = Quaternion.Euler(0, (t + phase) * 90f, 0);
        visual.localPosition = flying ? Vector3.zero : Vector3.up * Mathf.Sin((t + phase) * 2.5f) * 0.15f;

        if (flying) Fly(Time.deltaTime);
    }

    void Fly(float dt)
    {
        velocity += Physics.gravity * dt;
        var step = velocity * dt;
        var flat = new Vector3(step.x, 0, step.z);
        // stop at walls so gems don't vanish through the barrier
        if (flat.sqrMagnitude > 0 &&
            Physics.Raycast(transform.position, flat.normalized, flat.magnitude + 0.3f, 1 << Layers.Wall, QueryTriggerInteraction.Ignore))
        {
            velocity.x *= -0.3f; velocity.z *= -0.3f;
            step = new Vector3(0, step.y, 0);
        }
        var next = transform.position + step;
        if (next.y <= RestHeight && velocity.y < 0)
        {
            next.y = RestHeight;
            flying = false;
        }
        transform.position = next;
    }

    void OnTriggerStay(Collider other)
    {
        if (!available) return;
        var car = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<CarController>() : null;
        if (car == null) return;
        if (car == ignoreCar && Time.time < ignoreUntil) return;
        if (!car.Bed.TryAdd(color)) return;

        if (respawns)
        {
            SetAvailable(false);
            respawnAt = Time.time + RespawnDelay;
        }
        else Destroy(gameObject);
    }

    void SetAvailable(bool on)
    {
        available = on;
        visual.gameObject.SetActive(on);
        trigger.enabled = on;
    }
}
