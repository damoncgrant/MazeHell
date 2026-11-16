using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Handy actions to call from StoryPassageEvents in any scene, without needing references to objects
// that live on the StoryManager (the fader and narration come over from the first scene, so a later
// scene can't drag them into an event). Put one on the same object as StoryPassageEvents.
//
// Ending example (Labyrinth scene), passage "Ending":
//   GameActions.FreezePlayer, GameActions.PinNarration, GameActions.RiseFromAltar(0 = forever),
//   GameActions.FadeOutAfter(12), GameActions.QuitGameAfter(25) (or RestartGame(25)) or GameActions.QuitGameAfter(20)
// Note: all the actions in one event run at the same moment, so anything that should happen later
// (restarting, quitting) needs one of the versions that take a delay.
public class GameActions : MonoBehaviour
{
    [Tooltip("How fast the ground rises in RiseFromAltar, in metres per second.")]
    [SerializeField] private float riseSpeed = 1.2f;

    // ---------------- Screen ----------------

    public void FadeOut()
    {
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeOut();
    }

    /// <summary>Waits, then fades to black. Lets the player read the text / watch the rise first.</summary>
    public void FadeOutAfter(float delaySeconds)
    {
        StartCoroutine(FadeOutLater(delaySeconds));
    }

    private IEnumerator FadeOutLater(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        FadeOut();
    }

    public void FadeIn()
    {
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn();
    }

    public void FadeOutAndLoad(string sceneName)
    {
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeOutAndLoad(sceneName);
        else SceneManager.LoadScene(sceneName);
    }

    // ---------------- Player ----------------

    /// <summary>Stops the player moving, looking around and interacting (hides the [E] prompt).</summary>
    public void FreezePlayer()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        if (player != null) player.InputLocked = true;
    }

    public void UnfreezePlayer()
    {
        var player = FindFirstObjectByType<FirstPersonController>();
        if (player != null) player.InputLocked = false;
    }

    // ---------------- Narration ----------------

    /// <summary>Keeps the current passage text on screen instead of letting it fade after a few seconds.</summary>
    public void PinNarration()
    {
        if (StoryNarration.Instance != null) StoryNarration.Instance.Pin();
    }

    // ---------------- Ending ----------------

    /// <summary>
    /// "The part of the ground you are standing on begins to lift": raises the altar room's floor,
    /// the altar and the player by this many metres, and removes that room's ceiling so they rise out.
    /// 0 (or less) = keep rising forever ("how long will you go up for?").
    /// </summary>
    public void RiseFromAltar(float metres)
    {
        StartCoroutine(Rise(metres));
    }

    private IEnumerator Rise(float metres)
    {
        GameObject altar = GameObject.Find("Altar");
        var player = FindFirstObjectByType<FirstPersonController>();
        if (altar == null || player == null) yield break;

        Transform room = altar.transform.parent;
        Transform floor = room.Find("Floor");
        Transform ceiling = room.Find("Ceiling");
        if (ceiling != null) ceiling.gameObject.SetActive(false);

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false; // we move the player ourselves

        bool forever = metres <= 0f;
        float risen = 0f;
        while (forever || risen < metres)
        {
            float step = riseSpeed * Time.deltaTime;
            if (!forever) step = Mathf.Min(step, metres - risen);
            Vector3 up = Vector3.up * step;
            if (floor != null) floor.position += up;
            altar.transform.position += up;
            player.transform.position += up;
            risen += step;
            yield return null;
        }
    }

    /// <summary>Waits, then starts the game over from the first scene.</summary>
    public void RestartGame(float delaySeconds)
    {
        StartCoroutine(Restart(delaySeconds));
    }

    private IEnumerator Restart(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        // The StoryManager (story, narration, fader) survives scene loads; remove it so a fresh one starts.
        if (StoryManager.Instance != null)
            Destroy(StoryManager.Instance.gameObject);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(0);
    }

    /// <summary>Waits, then quits (or stops Play mode in the editor). Use this at the end of the ending.</summary>
    public void QuitGameAfter(float delaySeconds)
    {
        StartCoroutine(QuitLater(delaySeconds));
    }

    private IEnumerator QuitLater(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        QuitGame();
    }

    /// <summary>Quits immediately. Everything in an event runs at the same moment, so prefer QuitGameAfter.</summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
