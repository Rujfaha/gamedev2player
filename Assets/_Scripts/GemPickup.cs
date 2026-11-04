using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A physical gem: a Rigidbody with a convex gem-shaped collider, so it falls, rolls, bounces and settles
/// on the road. A bigger trigger sphere lets any truck (player or AI) pick it up by driving close.
/// Track gems respawn at their spot 10 s after being taken; spilled gems stay until someone grabs them.
/// </summary>
public class GemPickup : MonoBehaviour
{
    const float RespawnDelay = 10f, RestHeight = 0.9f, OwnerIgnoreTime = 1.2f, PickupRadius = 1.4f, KillY = -20f;

    /// <summary>Every gem in the scene (taken track gems included: check Available). The AI scans this.</summary>
    public static readonly List<GemPickup> All = new List<GemPickup>();
    static PhysicsMaterial material;

    public bool Available => available;
    public int Value => (int)color;

    GemColor color;
    bool respawns, available = true;
    float respawnAt, ignoreUntil;
    Vector3 home;
    CarController ignoreCar;
    Rigidbody rb;
    Transform visual;
    Collider trigger;

    public static GemPickup CreateOnTrack(GemDriftConfig cfg, GemColor color, Vector3 groundPos)
    {
        var p = Create(cfg, color, groundPos + Vector3.up * RestHeight);
        p.respawns = true;
        return p;
    }

    /// <summary>A gem thrown out of a truck bed: flies along push with an upward kick, and the owner can't re-grab it at once.</summary>
    public static GemPickup CreateDropped(GemDriftConfig cfg, GemColor color, Vector3 pos, CarController from, Vector3 push)
    {
        var p = Create(cfg, color, pos);
        var side = Vector3.Cross(Vector3.up, push) * Random.Range(-1f, 1f);
        p.rb.linearVelocity = from.GetComponent<Rigidbody>().linearVelocity * 0.4f
                            + push * Random.Range(3f, 6f) + side * 3f + Vector3.up * Random.Range(4f, 6f);
        p.rb.angularVelocity = Random.insideUnitSphere * 8f;
        p.ignoreCar = from;
        p.ignoreUntil = Time.time + OwnerIgnoreTime;
        return p;
    }

    static GemPickup Create(GemDriftConfig cfg, GemColor color, Vector3 pos)
    {
        if (material == null)
            material = new PhysicsMaterial("Gem")
            {
                dynamicFriction = 0.5f, staticFriction = 0.6f,
                bounciness = 0.35f, bounceCombine = PhysicsMaterialCombine.Maximum,
            };

        var go = new GameObject("GemPickup_" + color) { layer = Layers.Gem };
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.value * 360f, 0f));

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 1.5f;                                   // lets a rolling gem come to rest
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // thrown gems don't tunnel through the road
        rb.maxDepenetrationVelocity = 3f;                           // gems spawned overlapping ease apart instead of exploding

        var sc = go.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = PickupRadius;

        var p = go.AddComponent<GemPickup>();
        p.color = color;
        p.home = pos;
        p.rb = rb;
        p.trigger = sc;
        var vis = GemMesh.CreateVisual(cfg, color, go.transform, color == GemColor.Gold ? 1.3f : 1.05f);
        vis.layer = Layers.Gem;
        var mc = vis.AddComponent<MeshCollider>(); // the solid part, same shape as what you see
        mc.convex = true;
        mc.sharedMesh = GemMesh.Get();
        mc.sharedMaterial = material;
        mc.excludeLayers = 1 << Layers.Car;        // trucks drive through gems (the trigger picks them up) instead of bashing them
        p.visual = vis.transform;
        return p;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        if (!available)
        {
            if (respawns && Time.time >= respawnAt) SetAvailable(true);
            return;
        }
        if (transform.position.y < KillY) Take(); // fell off the world
    }

    void OnTriggerStay(Collider other)
    {
        if (!available) return;
        var car = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<CarController>() : null;
        if (car == null) return;
        if (car == ignoreCar && Time.time < ignoreUntil) return;
        if (car.Bed.TryAdd(color)) Take();
    }

    void Take()
    {
        SetAvailable(false);
        if (respawns) respawnAt = Time.time + RespawnDelay;
        else Destroy(gameObject);
    }

    void SetAvailable(bool on)
    {
        available = on;
        if (on) transform.SetPositionAndRotation(home, Quaternion.Euler(0f, Random.value * 360f, 0f));
        else rb.linearVelocity = rb.angularVelocity = Vector3.zero; // must happen before it goes kinematic
        rb.isKinematic = !on;
        visual.gameObject.SetActive(on); // also switches the solid collider
        trigger.enabled = on;
    }
}
