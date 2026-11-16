using TMPro;
using UnityEngine;

// Keeps a backdrop (e.g. a semi-transparent black Image) wrapped tightly around whatever a
// TextMeshPro text is currently showing, with some padding. Works with Auto Size, and hides the
// backdrop when the text is empty.
//
// Setup: add an Image as a SIBLING of the text, placed ABOVE the text in the Hierarchy so it draws
// behind it. Put this component on that Image and drag the text into "Text".
[RequireComponent(typeof(RectTransform))]
public class TextBackdrop : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private Vector2 padding = new Vector2(24f, 14f);

    private RectTransform rect;
    private UnityEngine.UI.Graphic graphic;

    private void Awake()
    {
        rect = (RectTransform)transform;
        graphic = GetComponent<UnityEngine.UI.Graphic>();

        // Size is driven from the centre.
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        if (graphic != null) graphic.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (text == null) return;

        bool hasText = !string.IsNullOrWhiteSpace(text.text);
        if (graphic != null) graphic.enabled = hasText;
        if (!hasText) return;

        text.ForceMeshUpdate();
        Bounds bounds = text.textBounds; // in the text's local space
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
        {
            if (graphic != null) graphic.enabled = false;
            return;
        }

        // Place the backdrop over the text's bounds, converting through world space so it works
        // regardless of how the two objects are anchored.
        RectTransform textRect = text.rectTransform;
        rect.position = textRect.TransformPoint(bounds.center);

        Vector3 textScale = textRect.lossyScale, myScale = rect.lossyScale;
        Vector2 size = new Vector2(
            bounds.size.x * textScale.x / myScale.x,
            bounds.size.y * textScale.y / myScale.y);
        rect.sizeDelta = size + padding * 2f;
    }
}
