using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Lets the player use StoryInteractable objects by looking at them and pressing Interact (E).
// Put this on the Main Camera (the child of Player).
// Interactable objects need a normal (non-trigger) collider; trigger zones are ignored by the ray.
public class PlayerInteractor : MonoBehaviour
{
    [Tooltip("How far away the player can reach objects, in metres.")]
    [SerializeField] private float reach = 3f;

    [Tooltip("Text that shows e.g. \"[E] You could stay here forever.\" while looking at something usable.")]
    [SerializeField] private TMP_Text promptText;

    [Tooltip("If Prompt Text is empty, look for a TextMeshPro object with this name under the StoryManager.")]
    [SerializeField] private string promptObjectName = "InteractPrompt";

    [SerializeField] private LayerMask layers = ~0;

    private InputAction interactAction;
    private FirstPersonController player;
    private StoryInteractable current;

    private void Start()
    {
        interactAction = InputSystem.actions.FindAction("Player/Interact");
        player = GetComponentInParent<FirstPersonController>();
        // The prompt lives on the StoryManager's canvas, which comes along from the first scene,
        // so a Player in a later scene can't reference it directly. Find it by name instead.
        if (promptText == null && StoryManager.Instance != null)
        {
            foreach (var text in StoryManager.Instance.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == promptObjectName) { promptText = text; break; }
        }

        if (promptText != null)
            promptText.text = "";
        else
            Debug.LogWarning("[PlayerInteractor] Prompt Text isn't set, so no \"[E] ...\" prompt will show. Drag the InteractPrompt text into it.", this);
    }

    private void Update()
    {
        // No interacting while the player is frozen (doorway fades, endings).
        if (player != null && player.InputLocked)
        {
            current = null;
            if (promptText != null) promptText.text = "";
            return;
        }

        current = FindTarget();

        if (promptText != null)
            promptText.text = current != null ? $"[E] {current.Prompt}" : "";

        // WasPressedThisFrame ignores the "Hold" interaction the default Interact action has.
        if (current != null && interactAction != null && interactAction.WasPressedThisFrame())
            current.Activate();
    }

    private StoryInteractable FindTarget()
    {
        if (!Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, reach, layers,
                QueryTriggerInteraction.Ignore))
            return null;

        var target = hit.collider.GetComponentInParent<StoryInteractable>();
        return target != null && target.UsesInteract && target.IsAvailable ? target : null;
    }

    private void OnDisable()
    {
        if (promptText != null) promptText.text = "";
    }
}
