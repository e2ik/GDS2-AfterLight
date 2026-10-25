using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CreditsData", menuName = "UI/Credits Data")]
public class CreditsData : ScriptableObject
{
    public enum Layout { Terminal, Card }
    public enum LineStyle { RoleAndName, Name, SmallPrint, Spacer, Images }

    [System.Serializable]
    public class Line
    {
        public LineStyle style = LineStyle.RoleAndName;
        public string role = "";
        public string name = "";
        public Sprite[] images;
        [Min(1f)] public float imageHeight = 64f;
        [Min(0f)] public float imageSpacing = 24f;
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
}