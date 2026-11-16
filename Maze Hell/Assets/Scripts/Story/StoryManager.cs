using System;
using System.Linq;
using System.Text;
using Cradle;
using UnityEngine;

// The one place the game talks to the Twine story (Cradle).
//
// Setup: put this on an empty "StoryManager" GameObject in the first scene, together with the
// generated LabyrinthStory component. It survives scene loads, so the story (and the stones the
// player has collected) carries over from the field into the maze.
//
// Other scripts use it like this:
//   StoryManager.Instance.TryLink("stay")        -> follow a link in the current passage (if it exists)
//   StoryManager.Instance.GoTo("StonePickup")    -> jump straight to a passage
//   StoryManager.Instance.SetVar("redStone", true)
//   StoryManager.PassageShown += (name, text) => ...   -> react when a passage finishes playing
public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }

    /// <summary>Fired when a passage has finished playing. Gives the passage name and its text (links included).</summary>
    public static event Action<string, string> PassageShown;

    [Tooltip("The generated story component (LabyrinthStory). Found automatically if it's on this GameObject.")]
    [SerializeField] private Story story;

    [Tooltip("Colour used for link text inside the narration, so players can tell what their options are.")]
    [SerializeField] private Color linkColor = new Color(0.95f, 0.8f, 0.45f);

    [Tooltip("Show each paragraph on its own line instead of leaving a blank line between them, so passages take less space.")]
    [SerializeField] private bool compactParagraphs = true;

    [Serializable]
    public class HiddenLink
    {
        [Tooltip("Passage name, e.g. \"disclaimer\". Leave empty to hide this link in every passage.")]
        public string passage;
        [Tooltip("Link name, e.g. \"enter\" (the name before the = in the twee file).")]
        public string link;
    }

    [Tooltip("Links that still work but are not shown in the narration text. Links whose name starts with \"hidden\" are always hidden.")]
    [SerializeField] private HiddenLink[] hiddenLinks = new HiddenLink[0];

    [Tooltip("Start the story automatically when the game starts.")]
    [SerializeField] private bool beginOnStart = true;

    public Story Story => story;
    public string CurrentPassage => story != null && story.CurrentPassage != null ? story.CurrentPassage.Name : null;
    public string CurrentText { get; private set; } = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // A StoryManager already came over from a previous scene; keep that one.
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (story == null)
            story = GetComponent<Story>();
        if (story == null)
        {
            Debug.LogError("[StoryManager] No story found. Add the LabyrinthStory component to this GameObject.");
            return;
        }

        // We start the story ourselves, after everything has subscribed.
        story.AutoPlay = false;
        story.OnPassageDone += HandlePassageDone;
    }

    private void Start()
    {
        if (beginOnStart && story != null)
            story.Begin();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            if (story != null)
                story.OnPassageDone -= HandlePassageDone;
        }
    }

    // ------------------------------------------------------------------
    // Links and passages

    /// <summary>Is there a link with this name in the current passage, and can it be used right now?</summary>
    public bool HasLink(string linkName)
    {
        return story != null && story.State == StoryState.Idle && story.HasLink(linkName);
    }

    /// <summary>The on-screen text of a link in the current passage, or null.</summary>
    public string GetLinkText(string linkName)
    {
        if (!HasLink(linkName)) return null;
        return story.GetLink(linkName).Text;
    }

    /// <summary>Follows a link in the current passage. Returns false if that link isn't available.</summary>
    public bool TryLink(string linkName)
    {
        if (!HasLink(linkName))
            return false;

        story.DoLink(linkName);
        return true;
    }

    /// <summary>Jumps straight to a passage, e.g. when the player picks up a stone or steps on a teleporter.</summary>
    public bool GoTo(string passageName)
    {
        if (story == null || story.State != StoryState.Idle)
            return false;
        if (!story.Passages.ContainsKey(passageName))
        {
            Debug.LogWarning($"[StoryManager] There is no passage called '{passageName}'.");
            return false;
        }

        story.GoTo(passageName);
        return true;
    }

    // ------------------------------------------------------------------
    // Variables

    public void SetVar(string name, bool value) => story.Vars[name] = value;
    public void SetVar(string name, int value) => story.Vars[name] = value;
    public void SetVar(string name, string value) => story.Vars[name] = value;

    public bool GetBool(string name) => story.Vars[name];
    public int GetInt(string name) => story.Vars[name];

    // ------------------------------------------------------------------

    private void HandlePassageDone(StoryPassage passage)
    {
        // Also fires when a link's own action finishes, just before moving on; ignore that one.
        if (story.CurrentLinkInAction != null)
            return;

        string text = BuildText();

        // Passages that only redirect (like Start -> Beginning) have no text; skip them.
        if (string.IsNullOrEmpty(text))
            return;

        CurrentText = text;
        PassageShown?.Invoke(passage.Name, CurrentText);
    }

    private bool IsHidden(string linkName)
    {
        if (string.IsNullOrEmpty(linkName)) return false;

        // Naming convention: links named "hidden..." in the twee file (e.g. [[hiddenContinue = :(|Maze]])
        // always work but are never shown.
        if (linkName.StartsWith("hidden", StringComparison.OrdinalIgnoreCase)) return true;

        string passage = CurrentPassage;
        foreach (var h in hiddenLinks)
        {
            if (!string.Equals(h.link, linkName, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrEmpty(h.passage) || string.Equals(h.passage, passage, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Turns the passage output into one rich-text string. Links are kept in place but coloured,
    // so "You could stay here forever." still shows up as an option, just not as a button.
    private string BuildText()
    {
        string color = ColorUtility.ToHtmlStringRGB(linkColor);
        var sb = new StringBuilder();

        foreach (StoryOutput output in story.Output)
        {
            if (output is StoryLink link)
            {
                if (!IsHidden(link.Name))
                    sb.Append($"<color=#{color}>{link.Text}</color>");
            }
            else if (output is StoryText text)
                sb.Append(text.Text);
            else if (output is LineBreak)
                sb.Append('\n');
        }

        // Collapse the blank lines Twine leaves around <<silently>> blocks etc.
        string result = sb.ToString().Trim();
        string gap = compactParagraphs ? "\n" : "\n\n";
        while (result.Contains(gap + "\n"))
            result = result.Replace(gap + "\n", gap);
        return result;
    }
}
