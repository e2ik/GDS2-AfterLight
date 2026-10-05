using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class SelectedColorTint : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
{
    [System.Serializable]
    private class TintTarget
    {
        public Graphic graphic;
        public Color normalColor = Color.white;
        public Color selectedColor = new Color(1f, 0.8f, 0.2f, 1f);
    }

    [SerializeField] private TintTarget[] targets;
    [SerializeField] private bool highlightOnHover = true;
    [SerializeField, Min(0f)] private float fadeDuration = 0.08f;

    private bool isSelected;
    private bool isHovered;
    private float blend;

    private void OnEnable()
    {
        isSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
        isHovered = false;
        blend = isSelected ? 1f : 0f;
        Apply();
    }

    private void Update()
    {
        float target = isSelected || (highlightOnHover && isHovered) ? 1f : 0f;
        if (Mathf.Approximately(blend, target)) return;

        blend = fadeDuration <= 0f ? target : Mathf.MoveTowards(blend, target, Time.unscaledDeltaTime / fadeDuration);
        Apply();
    }

    private void Apply()
    {
        if (targets == null) return;

        foreach (TintTarget t in targets)
        {
            if (t != null && t.graphic != null)
                t.graphic.color = Color.Lerp(t.normalColor, t.selectedColor, blend);
        }
    }

    public void OnSelect(BaseEventData eventData) => isSelected = true;
    public void OnDeselect(BaseEventData eventData) => isSelected = false;
    public void OnPointerEnter(PointerEventData eventData) => isHovered = true;
    public void OnPointerExit(PointerEventData eventData) => isHovered = false;
}