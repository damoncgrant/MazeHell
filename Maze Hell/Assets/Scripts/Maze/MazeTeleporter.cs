using System.Collections;
using UnityEngine;

// A teleporter room (codes 11 and 21 in the Twine grid). Walking into it fades to black, moves the
// player to the destination room (12 / 22) facing north, and shows the "Teleport" passage.
// Created by MazeBuilder; needs a trigger collider covering the room.
public class MazeTeleporter : MonoBehaviour
{
    /// <summary>Time of the last teleport, so the compass room doesn't also fire on arrival.</summary>
    public static float LastTeleportTime = -100f;

    public Transform destination;
    public string passage = "Teleport";

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<FirstPersonController>();
        if (player == null || destination == null) return;
        StartCoroutine(TeleportWhenReady(player));
    }

    private IEnumerator TeleportWhenReady(FirstPersonController player)
    {
        // If the player arrived through a doorway, wait for that fade to finish first.
        while (MazeDoorway.IsTransitioning)
            yield return null;

        player.InputLocked = true;
        if (ScreenFader.Instance != null)
            ScreenFader.Instance.Transition(() => Teleport(player), 0.8f, 0.6f, 1.2f, () => player.InputLocked = false);
        else
        {
            Teleport(player);
            player.InputLocked = false;
        }
    }

    private void Teleport(FirstPersonController player)
    {
        // A CharacterController overrides position changes while it's enabled.
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        LastTeleportTime = Time.time;
        player.transform.SetPositionAndRotation(destination.position + Vector3.up * 1.1f,
            Quaternion.LookRotation(Vector3.forward)); // north

        if (controller != null) controller.enabled = true;

        if (StoryManager.Instance != null)
            StoryManager.Instance.GoTo(passage);
    }
}
