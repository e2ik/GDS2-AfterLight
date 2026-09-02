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
    [SerializeField] private CharacterData speaker;

    [TextArea(2, 5)]
    [SerializeField] private string text;

    [Header("Optional Overrides")]
    [SerializeField] private bool overrideTextSpeed;
    [SerializeField] private float textSpeed = 0.03f; // will be ignored unless overrideTextSpeed is true

    [Header("Dialogue Effects")]
    [SerializeField] private DialogueEffect effect;

    public CharacterData Speaker => speaker;
    public string Text => text;

    public bool OverrideTextSpeed => overrideTextSpeed;

    public float TextSpeed
    {
        get
        {
            if (overrideTextSpeed) return textSpeed;
            if (speaker != null) return speaker.DefaultTextSpeed;
            
            return 0.03f;
        }
    }

    public DialogueEffect Effect => effect;
}

public enum DialogueEffect
{ // for later dynamic dialogue???? 
    Default,
    Scary,
    Angry,
    Shout
}