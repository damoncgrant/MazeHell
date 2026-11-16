using UnityEngine;

// A misty doorway between two maze rooms. Walking into it fades the screen to black, moves the
// player just inside the room on the other side (keeping the way they're facing), and fades back in,
// so every room feels like its own separate space. Created by MazeBuilder on each inner doorway.
//
// The doorway's forward direction (blue arrow) points from one room into the other.
[RequireComponent(typeof(BoxCollider))]
public class MazeDoorway : MonoBehaviour
{
    [Tooltip("How far past the doorway the player is placed, in metres.")]
    public float exitDistance = 2f;

    private static bool transitioning;

    private void OnTriggerEnter(Collider other)
    {
        if (transitioning) return;
        var player = other.GetComponentInParent<FirstPersonController>();
        if (player == null) return;

        // Which side did the player come from? Send them to the other one.
        float side = Vector3.Dot(player.transform.position - transform.position, transform.forward);
        Vector3 exit = transform.position + transform.forward * (side < 0f ? exitDistance : -exitDistance);
        exit.y = player.transform.position.y;

        transitioning = true;
        player.InputLocked = true;

        if (ScreenFader.Instance != null)
            ScreenFader.Instance.Transition(() => Move(player, exit), done: () => Finish(player));
        else
        {
            Move(player, exit);
            Finish(player);
        }
    }

    private static void Move(FirstPersonController player, Vector3 position)
    {
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.position = position;
        if (controller != null) controller.enabled = true;
    }

    private static void Finish(FirstPersonController player)
    {
        if (player != null) player.InputLocked = false;
        transitioning = false;
    }

    /// <summary>Teleporters use this so a doorway and a teleport can't run at the same time.</summary>
    public static bool IsTransitioning => transitioning;
}
