using UnityEngine;

// Turns an object in the world into a story link.
//
// Examples for the field scene:
//   Bed          -> Links: "stay", "leave"            Activation: Interact   (go back to sleep)
//   Spawn zone   -> Links: "getUp"                    Activation: Exit       (walk away from the bed)
//   Door zone    -> Links: "keepMoving"               Activation: Enter      (reach the door)
//   Sign         -> Links: "disclaimer"               Activation: Interact   (read the disclaimer)
//   Door         -> Links: "enter"                    Activation: Interact
//
// Link names are the names before the "=" in the twee file, e.g. [[stay = You could stay here forever.|NeutralEnding]].
// The object only reacts while one of its links exists in the current passage, so the bed does nothing
// once you've left the Beginning passage, and the door only opens after you've reached the Entrance.
//
// "Go To Passage" is for things that aren't links, like stones in the maze: if none of the links are
// available, it jumps straight to that passage (optionally setting "Set Bool Var" to true first).
// The altar uses both: Links "placeStones" (only exists once you have all 5) + Go To Passage "Altar".
public class StoryInteractable : MonoBehaviour
{
    public enum Activation
    {
        Interact, // look at it and press E (needs a Collider; PlayerInteractor on the camera)
        Enter,    // walk into it (needs a Collider with Is Trigger ticked)
        Exit      // walk out of it (needs a Collider with Is Trigger ticked)
    }

    [Tooltip("Link names to try, in order. The first one that exists in the current passage is used.")]
    [SerializeField] private string[] links;

    [Tooltip("Optional: if none of the links are available, jump to this passage instead.")]
    [SerializeField] private string goToPassage;

    [Tooltip("Optional: set this story variable to true first, e.g. \"redStone\".")]
    [SerializeField] private string setBoolVar;

    [SerializeField] private Activation activation = Activation.Interact;

    [Tooltip("Prompt shown when looking at it, e.g. \"Go back to sleep\". Empty = use the link's own text.")]
    [SerializeField] private string promptOverride;

    [Tooltip("Prompt used when no link is available and Go To Passage would be used, e.g. \"Examine the altar\".")]
    [SerializeField] private string promptWhenNoLink;

    [Tooltip("Can only be used once (good for stones).")]
    [SerializeField] private bool singleUse;

    [Tooltip("Disable this GameObject after use (e.g. a stone being picked up).")]
    [SerializeField] private bool hideAfterUse;

    private bool used;

    /// <summary>Used by MazeBuilder to set up generated objects.</summary>
    public void Configure(Activation activation, string[] links = null, string goToPassage = null,
        string setBoolVar = null, string prompt = null, string promptWhenNoLink = null,
        bool singleUse = false, bool hideAfterUse = false)
    {
        this.activation = activation;
        this.links = links ?? new string[0];
        this.goToPassage = goToPassage;
        this.setBoolVar = setBoolVar;
        this.promptOverride = prompt;
        this.promptWhenNoLink = promptWhenNoLink;
        this.singleUse = singleUse;
        this.hideAfterUse = hideAfterUse;
    }

    /// <summary>True if interacting right now would do something.</summary>
    public bool IsAvailable
    {
        get
        {
            var story = StoryManager.Instance;
            if (story == null || (singleUse && used)) return false;
            return FindLink() != null || !string.IsNullOrEmpty(goToPassage);
        }
    }

    public bool UsesInteract => activation == Activation.Interact;

    /// <summary>Text for the on-screen prompt.</summary>
    public string Prompt
    {
        get
        {
            string link = FindLink();
            if (link == null)
                return !string.IsNullOrEmpty(promptWhenNoLink) ? promptWhenNoLink
                     : !string.IsNullOrEmpty(promptOverride) ? promptOverride
                     : name;

            return !string.IsNullOrEmpty(promptOverride) ? promptOverride : StoryManager.Instance.GetLinkText(link);
        }
    }

    public bool Activate()
    {
        if (!IsAvailable) return false;

        var story = StoryManager.Instance;

        if (!string.IsNullOrEmpty(setBoolVar))
            story.SetVar(setBoolVar, true);

        string link = FindLink();
        bool done = link != null ? story.TryLink(link) : story.GoTo(goToPassage);

        if (done)
        {
            used = true;
            if (hideAfterUse) gameObject.SetActive(false);
        }
        return done;
    }

    private string FindLink()
    {
        var story = StoryManager.Instance;
        if (story == null || links == null) return null;
        foreach (string link in links)
            if (!string.IsNullOrEmpty(link) && story.HasLink(link))
                return link;
        return null;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (activation == Activation.Enter && IsPlayer(other)) Activate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (activation == Activation.Exit && IsPlayer(other)) Activate();
    }

    private static bool IsPlayer(Collider other)
    {
        return other.GetComponentInParent<FirstPersonController>() != null;
    }
}
