using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Loads a scene, optionally after a short delay so the player can read the passage first.
// Call Load("Maze") from StoryPassageEvents (e.g. on the MazeStart passage).
// The scene must be listed in File > Build Profiles > Scene List.
public class SceneLoader : MonoBehaviour
{
    [Tooltip("Seconds to wait before loading.")]
    [SerializeField] private float delay = 3f;

    public void Load(string sceneName)
    {
        StartCoroutine(LoadAfterDelay(sceneName));
    }

    private IEnumerator LoadAfterDelay(string sceneName)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }
}
