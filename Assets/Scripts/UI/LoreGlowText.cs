using TMPro;
using UnityEngine;

public class LoreGlowText : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color glowColor = new Color(1f, 0.92f, 0.65f);
    [SerializeField, Min(0f)] private float pulseSpeed = 1.2f;

    private void Awake()
    {
        if (text == null) text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        Apply(0f);
    }

    private void Update()
    {
        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        Apply(t);
    }

    private void Apply(float t)
    {
        if (text == null) return;
        text.color = Color.Lerp(baseColor, glowColor, t);
    }
}