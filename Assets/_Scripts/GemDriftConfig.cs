using UnityEngine;

/// <summary>Asset references the runtime needs (models, materials). Built by menu Gem Drift > Rebuild Config.</summary>
[CreateAssetMenu(menuName = "Gem Drift/Config")]
public class GemDriftConfig : ScriptableObject
{
    [Header("Car model (Kenney Car Kit)")]
    public GameObject carModel;
    public float modelYaw;          // rotates the model so its nose points +Z
    public float modelScale = 1f;   // scales the model to ~4.2 m long
    public Vector3 modelOffset;     // puts wheels on y = 0, centred on x/z
    public Vector3 carSize;         // size after yaw/scale (x = width, y = height, z = length)
    public Vector3 bedCenter;       // truck-bed floor centre in car space
    public Vector2 bedSize;         // bed floor width (x) and length (y)
    public Color[] playerTints = { new Color(1f, 0.55f, 0.5f), new Color(0.55f, 0.75f, 1f) };

    [Header("AI racers")]
    public int aiCount = 2;         // computer-driven trucks racing alongside the two players
    public Color[] aiTints =
    {
        new Color(1f, 0.85f, 0.4f), new Color(0.6f, 0.95f, 0.6f), new Color(0.8f, 0.6f, 1f), new Color(0.75f, 0.75f, 0.75f),
    };

    [Header("Crash attacks")]
    public float crashMinSpeed = 8f;    // closing speed (m/s) a ram needs before the rammed truck loses gems
    public int crashDropMin = 1;        // gems the rammed truck drops: random from min to max (inclusive)
    public int crashDropMax = 3;
    public float crashCooldown = 1.5f;  // seconds before the same truck can lose gems to a crash again

    [Header("Materials")]
    public Material road, roadLine, ground, wallA, wallB, checker;
    public Material gemRed, gemBlue, gemGold;
    public Material skid, spark;
    public Material flame;                     // additive soft dot for exhaust fire
    public Material nitroTankShell, nitroGlow; // little nitro tank on the bed

    [Header("Forest course")]
    public Material dirtRoad, forestGround, woodA, woodB;
    public GameObject[] treeModels;   // [0] large, [1] small (Kenney Racing Kit)
    public GameObject finishArch;     // Kenney overhead arch over the finish line

    public Material GemMaterial(GemColor c) => c == GemColor.Gold ? gemGold : c == GemColor.Blue ? gemBlue : gemRed;

    public static GemDriftConfig Load() => Resources.Load<GemDriftConfig>("GemDriftConfig");
}

/// <summary>Gem colour; the int value is the score it is worth.</summary>
public enum GemColor { Red = 1, Blue = 2, Gold = 3 }

/// <summary>Layer numbers set up in ProjectSettings/TagManager.</summary>
public static class Layers
{
    public const int Road = 6, Wall = 7, Car = 8, Gem = 9;
}
