using FMODUnity;
using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "Dialogue/Character Data")]
public class CharacterData : ScriptableObject
{
    [SerializeField] private string characterName;
    [SerializeField] private Sprite portrait;
    [SerializeField] private EventReference typingSound;
    [SerializeField] private float defaultTextSpeed = 0.03f;

    public string CharacterName => characterName;
    public Sprite Portrait => portrait;
    public EventReference TypingSound => typingSound;
    public float DefaultTextSpeed => defaultTextSpeed;
}