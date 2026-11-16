using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Shows the current passage's text on screen as narration, without stopping the player.
// Links appear in their own colour so players know their options, but they are NOT buttons:
// the player acts on them by interacting with objects in the world (see StoryInteractable).
//
// Setup: a Canvas (Screen Space - Overlay) with a TextMeshPro text for the narration and a CanvasGroup
// on the panel. Put this component on the panel. Making the Canvas a child of the StoryManager keeps
// the narration working after the maze scene loads.
[RequireComponent(typeof(CanvasGroup))]
public class StoryNarration : MonoBehaviour
{
    [SerializeField] private TMP_Text narrationText;

    [Tooltip("Seconds to fade the text in/out.")]
    [SerializeField] private float fadeTime = 0.6f;

    [Tooltip("Hide the text after this many seconds (0 = keep it up until the next passage). Tab shows it again.")]
    [SerializeField] private float hideAfter = 10f;

    [Tooltip("Show the current passage again after a new scene loads (e.g. arriving in the labyrinth).")]
    [SerializeField] private bool reshowOnSceneLoad = true;

    [Tooltip("Seconds to wait after a scene loads before showing the text again (lets the fade-in happen first).")]
    [SerializeField] private float reshowDelay = 1f;

    [Tooltip("Passages that lead into a new scene. Their text is held back and shown once the next scene has loaded, instead of twice.")]
    [SerializeField] private string[] showAfterNextSceneLoad = { "MazeStart" };

    [Tooltip("Key that hides/shows the narration (uses the Input System key name).")]
    [SerializeField] private UnityEngine.InputSystem.Key toggleKey = UnityEngine.InputSystem.Key.Tab;

    public static StoryNarration Instance { get; private set; }

    private CanvasGroup group;
    private bool pinned;
    private Coroutine routine;

    private void Awake()
    {
        Instance = this;
        group = GetComponent<CanvasGroup>();

        // Use the TextMeshPro text under this panel if none was dragged in.
        if (narrationText == null)
            narrationText = GetComponentInChildren<TMP_Text>(true);
        if (narrationText == null)
            Debug.LogError("[StoryNarration] No TextMeshPro text found. Add one as a child of this panel.", this);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        StoryManager.PassageShown += Show;
        SceneManager.sceneLoaded += OnSceneLoaded;

        // If a passage already played before this object existed, show it.
        if (StoryManager.Instance != null && !string.IsNullOrEmpty(StoryManager.Instance.CurrentText))
            Show(StoryManager.Instance.CurrentPassage, StoryManager.Instance.CurrentText);
    }

    private void OnDisable()
    {
        StoryManager.PassageShown -= Show;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!reshowOnSceneLoad || StoryManager.Instance == null) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ReshowAfterLoad());
    }

    private IEnumerator ReshowAfterLoad()
    {
        group.alpha = 0f;
        yield return new WaitForSecondsRealtime(reshowDelay);
        var story = StoryManager.Instance;
        if (story != null && !string.IsNullOrEmpty(story.CurrentText))
            yield return ShowRoutine(story.CurrentText);
    }

    private void Update()
    {
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
        {
            // Tab: hide the text if it's showing, or bring the current passage back if it isn't.
            if (group.alpha > 0.5f)
                StartFade(0f);
            else if (StoryManager.Instance != null)
                Show(StoryManager.Instance.CurrentPassage, StoryManager.Instance.CurrentText);
        }
    }

    private void Show(string passageName, string text)
    {
        if (System.Array.Exists(showAfterNextSceneLoad,
                p => string.Equals(p, passageName, System.StringComparison.OrdinalIgnoreCase)))
        {
            // Clear the old text now; ReshowAfterLoad shows this passage in the new scene.
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Fade(0f));
            return;
        }

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowRoutine(text));
    }

    /// <summary>Show the current passage and keep it on screen (no auto-hide), e.g. for endings.</summary>
    public void Pin()
    {
        pinned = true;
        if (StoryManager.Instance != null)
            Show(StoryManager.Instance.CurrentPassage, StoryManager.Instance.CurrentText);
    }

    private IEnumerator ShowRoutine(string text)
    {
        // Fade the old text out, swap, fade the new text in.
        if (group.alpha > 0f)
            yield return Fade(0f);

        if (narrationText != null)
            narrationText.text = text;
        yield return Fade(1f);

        if (hideAfter > 0f && !pinned)
        {
            yield return new WaitForSeconds(hideAfter);
            yield return Fade(0f);
        }
    }

    private void StartFade(float target)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Fade(target));
    }

    private IEnumerator Fade(float target)
    {
        float start = group.alpha;
        for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        group.alpha = target;
    }
}
