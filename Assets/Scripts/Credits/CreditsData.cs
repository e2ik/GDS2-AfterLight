using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CreditsData", menuName = "UI/Credits Data")]
public class CreditsData : ScriptableObject
{
    public enum Layout { Terminal, Card }
    public enum LineStyle { RoleAndName, Name, SmallPrint, Spacer }

    [System.Serializable]
    public class Line
    {
        public LineStyle style = LineStyle.RoleAndName;
        public string role = "";
        public string name = "";
    }

    [System.Serializable]
    public class Section
    {
        public string heading = "SECTION";
        public Layout layout = Layout.Terminal;
        [Min(0f)] public float holdTime = 3f;
        public bool clearTerminal = false;
        public List<Line> lines = new List<Line>();
    }

    public List<Section> sections = new List<Section>();

    [Header("Stats Card")]
    public bool showStats = true;
    public string statsHeading = "MISSION REPORT";
    public string timeLabel = "TIME";
    public string enemiesLabel = "ENEMIES DEFEATED";
    public string deathsLabel = "DEATHS";
    public string epicsLabel = "EPICS FOUND";
    public string legendariesLabel = "LEGENDARIES FOUND";
    [Min(0f)] public float statsHoldTime = 5f;

    [Header("Ending")]
    [TextArea] public string stingerLine = "";
    [Min(0f)] public float stingerDelay = 1.5f;
    [Min(0f)] public float stingerHoldTime = 3f;
}