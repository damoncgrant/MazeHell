using System;
using UnityEngine;
using UnityEngine.Events;

// Lets a scene react when a passage plays, without writing code.
// Example in the field scene:
//   NeutralEnding -> FirstPersonController.enabled = false, ScreenFader.FadeOut()
//   MazeStart     -> SceneLoader.Load("Maze")
//
// Put one of these anywhere in the scene and add entries in the Inspector.
public class StoryPassageEvents : MonoBehaviour
{
    [Serializable]
    public class PassageEvent
    {
        public string passage;
        public UnityEvent onShown;
    }

    [SerializeField] private PassageEvent[] events;

    private void OnEnable() => StoryManager.PassageShown += Handle;
    private void OnDisable() => StoryManager.PassageShown -= Handle;

    private void Handle(string passageName, string text)
    {
        foreach (var e in events)
            if (string.Equals(e.passage, passageName, StringComparison.OrdinalIgnoreCase))
                e.onShown.Invoke();
    }
}
