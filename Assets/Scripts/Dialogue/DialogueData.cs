using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Header("Dialogue Settings")] 
    [SerializeField] private float textSpeed = 0.03f; // so some npc might talk fast, some slow
    [SerializeField] private DialogueLine[] lines;

    public DialogueLine[] Lines => lines;
    public float TextSpeed => textSpeed;
}

[System.Serializable]
public class DialogueLine
{
    [SerializeField] private string speakerName;
    [SerializeField] private Sprite portrait;

    [TextArea(2, 5)]
    [SerializeField] private string text;
    [SerializeField] private AudioClip typingSound;

    public string SpeakerName => speakerName;
    public Sprite Portrait => portrait;
    public string Text => text;
    public AudioClip TypingSound => typingSound;
}