using System.Collections;
using UnityEngine;

// Background music for a scene: fades in when the scene starts, loops, and can fade out
// (e.g. call FadeOut(2) from StoryPassageEvents on MazeStart so the zen music fades as you go
// through the door, or on NeutralEnding).
// Put it on an empty "Music" object and drag your music clip in.
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip music;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;
    [SerializeField] private float fadeInTime = 3f;

    private AudioSource source;

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = music;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D: same volume everywhere
        source.volume = 0f;
    }

    private void Start()
    {
        if (music == null) return;
        source.Play();
        StartCoroutine(FadeTo(volume, fadeInTime));
    }

    /// <summary>Fades the music out over this many seconds, then stops it.</summary>
    public void FadeOut(float seconds)
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutRoutine(seconds));
    }

    private IEnumerator FadeOutRoutine(float seconds)
    {
        yield return FadeTo(0f, seconds);
        source.Stop();
    }

    private IEnumerator FadeTo(float target, float seconds)
    {
        float start = source.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            source.volume = Mathf.Lerp(start, target, t / seconds);
            yield return null;
        }
        source.volume = target;
    }
}
