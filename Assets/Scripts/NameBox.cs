using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NameBox : MonoBehaviour
{
    [Tooltip("The DialogueBox that owns the lines this NameBox reads from. Auto-fills from a parent if left empty.")]
    public DialogueBox dialogueBox;

    [Tooltip("TextMeshProUGUI that displays the speaker's name. Auto-wires to the first TMP child if left empty.")]
    public TextMeshProUGUI nameText;

    [Tooltip("Image background behind the name text. Auto-wires to this GameObject's Image if left empty.")]
    public Image background;

    private void Awake()
    {
        if (dialogueBox == null)
            dialogueBox = GetComponentInParent<DialogueBox>();

        if (nameText == null)
            nameText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (background == null)
            background = GetComponent<Image>();

        SetVisible(false);
    }

    public void SetNameForLine(int index)
    {
        if (dialogueBox == null || dialogueBox.dialogueLines == null
            || index < 0 || index >= dialogueBox.dialogueLines.Count)
        {
            SetVisible(false);
            return;
        }

        string name = dialogueBox.dialogueLines[index].speakerName;
        if (string.IsNullOrWhiteSpace(name))
        {
            SetVisible(false);
            return;
        }

        if (nameText != null)
            nameText.text = name;

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (background != null)
            background.enabled = visible;

        if (nameText != null)
            nameText.gameObject.SetActive(visible);
    }
}
