using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class RandomTipDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string prefix = "<b>Did you know?</b>\n";
    [SerializeField] private bool highlightBrackets = true;
    [SerializeField] private Color highlightColor = new Color(1f, 0.84f, 0.3f);
    [SerializeField] private bool showOnEnable = true;
    [SerializeField] private bool showWhenWindowOpens = true;
    [SerializeField, Min(0f)] private float cycleInterval = 0f;

    [SerializeField, TextArea(2, 4)] private string[] tips =
    {
        "Equip [Recoil] to parry Heavy attacks.",
        "Equip [Whirlwind] with [Kinetic] and spin to win. You won't get staggered while spinning.",
        "Attack gems make your attacks faster, shorten their cooldown and extend their reach.",
        "With an Attack gem equipped, enemies can't stagger you mid-attack.",
        "Attack gems also boost dash attacks and plunges.",
        "Skill gems boost the damage of your skills.",
        "Some gems refill your skill meter whenever your hits land.",
        "Your Defense makes reflected damage hit harder.",
        "Defense reduces the damage you take from every hit.",
        "Hit enemies while they're vulnerable for bonus damage.",
        "Big hits can be resisted, but vulnerable hits are never capped.",
        "The last hit of a combo can stagger enemies.",
        "Each hit in a combo deals more damage than the last.",
        "Keep pressing attack right after a swing to continue your combo.",
        "Attack right after a parry for bonus counter damage.",
        "Attacking in the air hits everything around you.",
        "Hold down and attack while falling to plunge. The longer you fall, the harder it hits.",
        "Plunging onto an enemy bounces you back into the air.",
        "Dash without a direction to become briefly invulnerable.",
        "Dodge at the very last moment for a Perfect dodge.",
        "Hold your skill to charge it. A full charge deals much more damage.",
        "Held skills speed up and hit harder the longer you hold them.",
        "Your skill meter slowly refills on its own, up to half.",
        "Hold down and jump to drop through thin platforms.",
        "Higher rarity gear rolls better stats.",
        "Use [Delete Worse Gear] to clear out gear you've outgrown.",
        "New items always appear at the top of your inventory.",
        "Some gems roll your damage. Land a JACKPOT for a huge hit.",
        "Enemies rarely drop gear worse than what you already own."
    };

    private static readonly Regex BracketPattern = new Regex(@"\[([^\]]+)\]");
    private static readonly List<int> bag = new List<int>();
    private static int lastShown = -1;
    private static int bagTipCount = -1;

    private float nextCycleTime;
    private GameUI.UIWindow window;
    private bool wasWindowOpen;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        window = GetComponentInParent<GameUI.UIWindow>();

        if (label == null) Debug.LogWarning($"[RandomTipDisplay] '{name}' has no TMP label assigned.", this);
    }

    private void OnEnable()
    {
        if (showOnEnable) ShowNext();
    }

    private void Update()
    {
        if (showWhenWindowOpens && window != null)
        {
            bool open = window.IsOpen;
            if (open && !wasWindowOpen) ShowNext();
            wasWindowOpen = open;
        }

        if (cycleInterval <= 0f || Time.unscaledTime < nextCycleTime) return;
        ShowNext();
    }

    public void ShowNext()
    {
        nextCycleTime = Time.unscaledTime + cycleInterval;
        if (label == null || tips == null || tips.Length == 0) return;

        label.text = prefix + Format(tips[PickIndex()]);
    }

    private int PickIndex()
    {
        if (tips.Length == 1) return 0;

        if (bag.Count == 0 || bagTipCount != tips.Length)
        {
            bag.Clear();
            bagTipCount = tips.Length;
            for (int i = 0; i < tips.Length; i++) bag.Add(i);

            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }

            if (bag[bag.Count - 1] == lastShown)
                (bag[bag.Count - 1], bag[0]) = (bag[0], bag[bag.Count - 1]);
        }

        int index = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        lastShown = index;
        return index;
    }

    private string Format(string tip)
    {
        if (!highlightBrackets || string.IsNullOrEmpty(tip)) return tip;

        string hex = ColorUtility.ToHtmlStringRGB(highlightColor);
        return BracketPattern.Replace(tip, $"<color=#{hex}>[$1]</color>");
    }
}