using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerSpeech", menuName = "Dialogue/Player Speech Data")]
public class PlayerSpeechData : ScriptableObject
{
    [TextArea(2, 4)]
    [SerializeField] private string[] lines;

    public string[] Lines => lines;
}