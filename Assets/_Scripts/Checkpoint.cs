using UnityEngine;

/// <summary>Trigger across the road. CP_00 is the finish line; cars must pass them in order.</summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    public int index;

    void OnTriggerEnter(Collider other)
    {
        var car = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<CarController>() : null;
        if (car != null && RaceManager.Instance != null) RaceManager.Instance.OnCheckpoint(car, index);
    }

    // A grid slot right behind the line overlaps CP_00 before the race starts (Enter is ignored then); Stay catches it.
    void OnTriggerStay(Collider other) => OnTriggerEnter(other);
}
