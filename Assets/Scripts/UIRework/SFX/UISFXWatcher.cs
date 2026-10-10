using System;
using FMODUnity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameUI
{
    public class UISFXWatcher : MonoBehaviour
    {
        [SerializeField] private EventReference uiSelectEvent;
        [SerializeField] private bool onlyPlayWhenNavigatingWithinWindow = true;

        [Header("Tab Switching")]
        [SerializeField] private EventReference uiTabSwitchEvent;
        [SerializeField] private InputActionReference[] tabSwitchActions;

        [Header("Submit")]
        [SerializeField] private EventReference uiSubmitEvent;
        [SerializeField] private InputActionReference[] submitActions;

        [Header("Cancel")]
        [SerializeField] private EventReference uiCancelEvent;
        [SerializeField] private InputActionReference[] cancelActions;

        private static float suppressUntil = -1f;
        private static bool suppressNext;
        private static int suppressSubmitFrame = -1;

        private GameObject lastSelected;
        private GameObject anchorSelected;
        private bool submitPending;

        public static void SuppressSelectSound(float duration = 0.15f)
        {
            suppressNext = true;
            suppressUntil = Time.unscaledTime + duration;
            suppressSubmitFrame = Time.frameCount;
        }

        public static void SuppressSubmitSound()
        {
            suppressSubmitFrame = Time.frameCount;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            suppressUntil = -1f;
            suppressNext = false;
            suppressSubmitFrame = -1;
        }

        private static bool ConsumeSuppression()
        {
            if (!suppressNext) return false;

            suppressNext = false;
            return Time.unscaledTime <= suppressUntil;
        }

        private void OnEnable()
        {
            Subscribe(tabSwitchActions, HandleTabSwitch);
            Subscribe(submitActions, HandleSubmit);
            Subscribe(cancelActions, HandleCancel);
        }

        private void OnDisable()
        {
            Unsubscribe(tabSwitchActions, HandleTabSwitch);
            Unsubscribe(submitActions, HandleSubmit);
            Unsubscribe(cancelActions, HandleCancel);
            submitPending = false;
        }

        private static void Subscribe(InputActionReference[] references, Action<InputAction.CallbackContext> handler)
        {
            if (references == null) return;

            foreach (InputActionReference reference in references)
            {
                if (reference != null && reference.action != null)
                    reference.action.performed += handler;
            }
        }

        private static void Unsubscribe(InputActionReference[] references, Action<InputAction.CallbackContext> handler)
        {
            if (references == null) return;

            foreach (InputActionReference reference in references)
            {
                if (reference != null && reference.action != null)
                    reference.action.performed -= handler;
            }
        }

        private void PlayOrFallback(EventReference sound)
        {
            EventReference toPlay = sound.IsNull ? uiSelectEvent : sound;
            if (!toPlay.IsNull) AudioManager.PlaySFX(toPlay, this);
        }

        private void HandleTabSwitch(InputAction.CallbackContext context)
        {
            if (UIManager.Instance == null || !UIManager.Instance.HasOpenWindows) return;
            PlayOrFallback(uiTabSwitchEvent);
        }

        private void HandleSubmit(InputAction.CallbackContext context)
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || !selected.activeInHierarchy) return;

            Selectable selectable = selected.GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable()) return;

            submitPending = true;
        }

        private void HandleCancel(InputAction.CallbackContext context)
        {
            if (UIManager.Instance == null || !UIManager.Instance.HasOpenWindows) return;
            if (UIManager.Instance.SuppressCancel) return;
            PlayOrFallback(uiCancelEvent);
        }

        private static bool IsTransient(GameObject current)
        {
            UIManager manager = UIManager.Instance;
            if (manager == null || !manager.HasOpenWindows) return false;

            UIWindow window = current.GetComponentInParent<UIWindow>();
            return window == null || !manager.IsTopmost(window);
        }

        private bool IsNavigation(GameObject current)
        {
            if (!onlyPlayWhenNavigatingWithinWindow) return true;
            if (anchorSelected == null || !anchorSelected.activeInHierarchy) return false;

            UIWindow previousWindow = anchorSelected.GetComponentInParent<UIWindow>();
            UIWindow currentWindow = current.GetComponentInParent<UIWindow>();
            if (previousWindow != currentWindow) return false;

            return currentWindow == null || currentWindow.IsOpen;
        }

        private void Update()
        {
            GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            if (current != lastSelected)
            {
                lastSelected = current;

                if (current == null || IsTransient(current)) return;

                if (current != anchorSelected && IsNavigation(current) && !ConsumeSuppression())
                {
                    AudioManager.PlaySFX(uiSelectEvent);
                }

                anchorSelected = current;
            }
        }

        private void LateUpdate()
        {
            if (!submitPending) return;

            submitPending = false;
            if (suppressSubmitFrame == Time.frameCount) return;

            PlayOrFallback(uiSubmitEvent);
        }
    }
}