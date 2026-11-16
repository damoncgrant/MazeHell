using UnityEngine;
using UnityEngine.InputSystem;

// Simple first-person controller for Maze Hell.
// Put this on a "Player" object that has a CharacterController,
// and drag the camera (a child of Player) into the Camera Root field.
// Uses the project-wide input actions (Assets/InputSystem_Actions): Player/Move, Look, Jump, Sprint.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraRoot;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float sprintSpeed = 7f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private float maxPitch = 85f;

    private CharacterController controller;
    private InputAction moveAction, lookAction, jumpAction, sprintAction;
    private float verticalVelocity;
    private float pitch;

    /// <summary>While true the player can't move or look (used during doorway fades and cutscenes).</summary>
    public bool InputLocked { get; set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        moveAction   = InputSystem.actions.FindAction("Player/Move");
        lookAction   = InputSystem.actions.FindAction("Player/Look");
        jumpAction   = InputSystem.actions.FindAction("Player/Jump");
        sprintAction = InputSystem.actions.FindAction("Player/Sprint");
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (InputLocked) return;
        Look();
        Move();
    }

    private void Look()
    {
        Vector2 look = lookAction.ReadValue<Vector2>() * lookSensitivity;

        // Turn the whole body left/right, tilt only the camera up/down.
        transform.Rotate(Vector3.up, look.x);
        pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        float speed = sprintAction.IsPressed() ? sprintSpeed : walkSpeed;
        Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -2f; // keeps the player stuck to the ground
            if (jumpAction.WasPressedThisFrame())
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = horizontal + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}
