using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Fades the screen to/from black, and can load a scene after fading out.
// Setup: a full-screen black Image on a Canvas with a CanvasGroup; put this on it.
// Call FadeOut / FadeIn / FadeOutAndLoad from StoryPassageEvents.
//
// Because it lives under the StoryManager (which survives scene loads), it also fades back in
// automatically after a new scene loads, e.g. when the player arrives in the labyrinth.
// Maze doorways and teleporters use ScreenFader.Instance.Transition(...) for their quick fades.
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private float duration = 2f;

    [Tooltip("Start fully black and fade in when the game starts (a nice 'waking up' effect).")]
    [SerializeField] private bool fadeInOnStart = true;

    [Tooltip("Fade back in after a scene finishes loading.")]
    [SerializeField] private bool fadeInOnSceneLoad = true;

    [Tooltip("Seconds to wait before fading out when loading a scene, so the player can read the passage.")]
    [SerializeField] private float delayBeforeLoad = 3f;

    private CanvasGroup group;
    private Coroutine routine;

    /// <summary>True while a fade is running.</summary>
    public bool IsBusy => routine != null;

    private void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = fadeInOnStart ? 1f : 0f;
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (fadeInOnStart) FadeIn();
    }

    public void FadeIn() => Run(Fade(0f, duration, 0f, null));
    public void FadeOut() => Run(Fade(1f, duration, 0f, null));
    public void FadeOutAndLoad(string sceneName) => Run(Fade(1f, duration, delayBeforeLoad, sceneName));

    /// <summary>Fade to black, run atBlack (e.g. move the player), hold, then fade back in.</summary>
    public void Transition(Action atBlack, float fadeOutTime = 0.35f, float holdTime = 0.15f, float fadeInTime = 0.45f, Action done = null)
    {
        Run(TransitionRoutine(atBlack, fadeOutTime, holdTime, fadeInTime, done));
    }

    private IEnumerator TransitionRoutine(Action atBlack, float outTime, float hold, float inTime, Action done)
    {
        yield return FadeTo(1f, outTime);
        atBlack?.Invoke();
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
        yield return FadeTo(0f, inTime);
        routine = null;
        done?.Invoke();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only fade in if we're currently dark (i.e. we faded out to load this scene).
        if (fadeInOnSceneLoad && group.alpha > 0.5f)
            FadeIn();
    }

    private void Run(IEnumerator fade)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(fade);
    }

    private IEnumerator Fade(float target, float time, float delay, string sceneToLoad)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        yield return FadeTo(target, time);
        routine = null;

        if (!string.IsNullOrEmpty(sceneToLoad))
            SceneManager.LoadScene(sceneToLoad);
    }

    private IEnumerator FadeTo(float target, float time)
    {
        float start = group.alpha;
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / time);
            yield return null;
        }
        group.alpha = target;
    }
}
