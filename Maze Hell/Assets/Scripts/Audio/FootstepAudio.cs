using UnityEngine;

// Plays footstep sounds while the player walks.
// Put it on the Player (next to FirstPersonController) and drag in your sound(s).
//
// Two ways to use it:
//   Loop  - one longer "walking on grass" recording. It loops while you move and fades out when you stop.
//   Steps - one or more short single-step sounds. A random one plays every step.
[RequireComponent(typeof(CharacterController))]
public class FootstepAudio : MonoBehaviour
{
    public enum Mode { Loop, Steps }

    [SerializeField] private Mode mode = Mode.Loop;
    [SerializeField] private AudioClip[] clips;

    [Range(0f, 1f)] [SerializeField] private float volume = 0.6f;

    [Tooltip("Speed (m/s) below which the player counts as standing still.")]
    [SerializeField] private float minSpeed = 0.5f;

    [Tooltip("Loop mode: how quickly the sound fades in/out when you start/stop walking.")]
    [SerializeField] private float fadeSpeed = 6f;

    [Tooltip("Loop mode: play the loop slightly faster when sprinting.")]
    [SerializeField] private float sprintPitch = 1.25f;

    [Tooltip("Steps mode: seconds between steps when walking (shorter when faster).")]
    [SerializeField] private float stepInterval = 0.5f;

    [Tooltip("Walking speed the step interval is for (match FirstPersonController's Walk Speed).")]
    [SerializeField] private float walkSpeed = 4f;

    private CharacterController controller;
    private FirstPersonController player;
    private AudioSource source;
    private float stepTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        player = GetComponent<FirstPersonController>();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // it's the player's own feet: no 3D panning
        source.loop = mode == Mode.Loop;
        source.volume = 0f;
        if (mode == Mode.Loop && clips != null && clips.Length > 0)
            source.clip = clips[0];
    }

    private void Update()
    {
        if (clips == null || clips.Length == 0) return;

        Vector3 v = controller.velocity;
        float speed = new Vector2(v.x, v.z).magnitude;
        bool locked = player != null && player.InputLocked;
        bool walking = !locked && controller.enabled && controller.isGrounded && speed > minSpeed;

        if (mode == Mode.Loop)
            UpdateLoop(walking, speed);
        else
            UpdateSteps(walking, speed);
    }

    private void UpdateLoop(bool walking, float speed)
    {
        float target = walking ? volume : 0f;
        source.volume = Mathf.MoveTowards(source.volume, target, fadeSpeed * Time.deltaTime);
        source.pitch = walking && speed > walkSpeed * 1.2f ? sprintPitch : 1f;

        if (walking && !source.isPlaying)
            source.Play();
        else if (!walking && source.isPlaying && source.volume <= 0.001f)
            source.Pause(); // pause (not stop) so it carries on from the same spot next time
    }

    private void UpdateSteps(bool walking, float speed)
    {
        if (!walking)
        {
            stepTimer = 0f; // first step plays straight away when you start moving
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        source.pitch = Random.Range(0.9f, 1.1f);
        source.PlayOneShot(clip, volume);
        stepTimer = stepInterval * Mathf.Clamp(walkSpeed / Mathf.Max(speed, 0.01f), 0.5f, 1.5f);
    }
}
