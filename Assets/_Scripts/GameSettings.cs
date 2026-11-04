using UnityEngine;

/// <summary>
/// Options from the Settings screen, saved in PlayerPrefs and applied at startup.
/// Assets/_Audio has no AudioMixer yet, so music and SFX volumes (0..1) are plain values: the menu's own sounds
/// use them, and any AudioSource added later should multiply its volume by MusicVolume or SfxVolume
/// (or an AudioMixer can be hooked up in Apply).
/// </summary>
public static class GameSettings
{
    public static float MusicVolume = 0.7f, SfxVolume = 0.8f;
    public static int Language;           // index into Loc.Languages
    public static bool Fullscreen;
    public static int Quality;            // index into QualitySettings.names
    public static bool ShowMinimap = true, CameraShake = true;

    static bool loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Startup()
    {
        Load();
#if !UNITY_WEBGL
        if (PlayerPrefs.HasKey("gd_full")) Screen.fullScreen = Fullscreen; // browsers only allow fullscreen from a click
#endif
        if (PlayerPrefs.HasKey("gd_quality")) ApplyQuality();
    }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        MusicVolume = PlayerPrefs.GetFloat("gd_music", MusicVolume);
        SfxVolume = PlayerPrefs.GetFloat("gd_sfx", SfxVolume);
        Language = Mathf.Clamp(PlayerPrefs.GetInt("gd_lang", 0), 0, Loc.Languages.Length - 1);
        Fullscreen = PlayerPrefs.GetInt("gd_full", Screen.fullScreen ? 1 : 0) == 1;
        Quality = Mathf.Clamp(PlayerPrefs.GetInt("gd_quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        ShowMinimap = PlayerPrefs.GetInt("gd_minimap", 1) == 1;
        CameraShake = PlayerPrefs.GetInt("gd_shake", 1) == 1;
    }

    public static void ApplyFullscreen() => Screen.fullScreen = Fullscreen;

    public static void ApplyQuality()
    {
#if !UNITY_EDITOR // in the editor this would change the project's current quality level (a tracked file)
        QualitySettings.SetQualityLevel(Quality, true);
#endif
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("gd_music", MusicVolume);
        PlayerPrefs.SetFloat("gd_sfx", SfxVolume);
        PlayerPrefs.SetInt("gd_lang", Language);
        PlayerPrefs.SetInt("gd_full", Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("gd_quality", Quality);
        PlayerPrefs.SetInt("gd_minimap", ShowMinimap ? 1 : 0);
        PlayerPrefs.SetInt("gd_shake", CameraShake ? 1 : 0);
        PlayerPrefs.Save();
    }
}
