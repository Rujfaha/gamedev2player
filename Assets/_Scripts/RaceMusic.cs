using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Background music for a race: Resources/Music/&lt;scene name&gt; loops during the race, and when the leader
/// reaches the last 20 % of the route (or the last-call timer starts) it cross-fades into the faster
/// Resources/Music/&lt;scene name&gt;_Climax version. Volume follows Settings > Music.
/// Tracks: CC0 by MintoDog (OpenGameArt), see CREDITS.md.
/// </summary>
public class RaceMusic : MonoBehaviour
{
    const float FadeTime = 1.5f, ClimaxAt = 0.8f;

    AudioSource main, climax;
    RaceManager race;
    bool climaxOn;
    float mix; // 0 = main track, 1 = climax

    public static RaceMusic Create(GameObject host, RaceManager race)
    {
        string scene = SceneManager.GetActiveScene().name;
        var clip = Resources.Load<AudioClip>("Music/" + scene);
        if (clip == null) return null;
        var m = host.AddComponent<RaceMusic>();
        m.race = race;
        m.main = m.Source(clip);
        var hot = Resources.Load<AudioClip>("Music/" + scene + "_Climax");
        if (hot != null) m.climax = m.Source(hot);
        m.main.Play();
        return m;
    }

    AudioSource Source(AudioClip clip)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        s.volume = 0f;
        return s;
    }

    void Update()
    {
        if (!climaxOn && climax != null && race != null && race.Racers != null && LeaderProgress() >= ClimaxAt)
        {
            climaxOn = true;
            climax.Play();
        }
        mix = Mathf.MoveTowards(mix, climaxOn ? 1f : 0f, Time.unscaledDeltaTime / FadeTime);
        float vol = GameSettings.MusicVolume * (race != null && race.State == RaceManager.Phase.Results ? 0.6f : 1f);
        main.volume = vol * (1f - mix);
        if (climax != null) climax.volume = vol * mix;
        if (mix >= 1f && main.isPlaying) main.Stop();
    }

    float LeaderProgress()
    {
        if (race.State == RaceManager.Phase.LastCall || race.State == RaceManager.Phase.Results) return 1f;
        float best = 0f;
        foreach (var r in race.Racers)
            best = Mathf.Max(best, race.Loop ? (r.lap + (float)r.nextCheckpoint / race.CheckpointCount) / race.Laps
                                             : (float)r.passed / race.CheckpointCount);
        return best;
    }
}
