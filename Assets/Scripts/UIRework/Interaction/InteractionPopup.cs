using TMPro;
using UnityEngine;

namespace GameUI
{
    public class InteractionPopup : MonoBehaviour
    {
        [SerializeField] private GameObject popupRoot;
        [SerializeField] private TMP_Text promptText;

        private InteractionManager interactionManager;

        private void Awake()
        {
            if (popupRoot != null) { popupRoot.SetActive(false); }
        }

        public void Bind(InteractionManager manager)
        {
            Unbind();

            interactionManager = manager;

            if (interactionManager != null)
            {
                interactionManager.OnInteractionPromptChanged += HandlePromptChanged;
            }
        }

        public void Unbind()
        {
            if (interactionManager != null)
            {
                interactionManager.OnInteractionPromptChanged -= HandlePromptChanged;
            }

            interactionManager = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandlePromptChanged(string prompt)
        {
            bool hasPrompt = !string.IsNullOrEmpty(prompt);

            if (popupRoot != null) { popupRoot.SetActive(hasPrompt); }
            if (promptText != null) { promptText.text = prompt; }
        }
    }
}