using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [SerializeField] private DialogueLine[] lines;
    public DialogueLine[] Lines => lines;
}

[System.Serializable]
public class DialogueLine
{
    [SerializeField] private string speakerName;

    [SerializeField] private Sprite portrait;

    [TextArea(2, 5)]
    [SerializeField] private string text;

    public string SpeakerName => speakerName;
    public Sprite Portrait => portrait;
    public string Text => text;
}