using UnityEngine;

// A compass room (codes 12 and 22). Walking in on foot shows the "Compass" passage.
// Arriving by teleporter shows "Teleport" instead, like in the Twine game.
// Created by MazeBuilder; needs a trigger collider covering the room.
public class MazeCompassRoom : MonoBehaviour
{
    public string passage = "Compass";

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<FirstPersonController>() == null) return;
        if (Time.time - MazeTeleporter.LastTeleportTime < 0.5f) return;

        if (StoryManager.Instance != null)
            StoryManager.Instance.GoTo(passage);
    }
}
