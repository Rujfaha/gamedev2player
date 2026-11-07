using UnityEngine;

/// <summary>Slowly tumbles a decoration (asteroids, planet rings).</summary>
public class Spinner : MonoBehaviour
{
    public Vector3 degPerSecond = new Vector3(0f, 10f, 0f);

    void Update() => transform.Rotate(degPerSecond * Time.deltaTime, Space.Self);
}
