using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI
{
    public class RebindRow : MonoBehaviour
    {
        [SerializeField] private RebindButton rebindButton;
        public RebindButton RebindButton => rebindButton;

        [Header("Background")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Color selectedColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color rebindingColor = new Color(1f, 0.8f, 0.2f, 0.3f);
        [SerializeField, Min(0f)] private float colorFadeSpeed = 15f;

        private Color normalColor = Color.white;

        private void Awake()
        {
            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            if (backgroundImage != null) normalColor = backgroundImage.color;
        }

        private void OnDisable()
        {
            if (backgroundImage != null) backgroundImage.color = normalColor;
        }

        private void Update()
        {
            if (backgroundImage == null) return;

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            bool isSelected = selected != null && selected.transform.IsChildOf(transform);

            bool isRebinding = rebindButton != null && rebindButton.IsRebinding;

            Color target = isRebinding ? rebindingColor : isSelected ? selectedColor : normalColor;
            backgroundImage.color = colorFadeSpeed <= 0f
                ? target
                : Color.Lerp(backgroundImage.color, target, 1f - Mathf.Exp(-colorFadeSpeed * Time.unscaledDeltaTime));
        }
    }
}