using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameUI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private InputActionAsset actionAsset;

        private readonly Stack<UIWindow> openWindows = new Stack<UIWindow>();
        private readonly List<UIWindowAnimator> closingAnimators = new List<UIWindowAnimator>();
        private Coroutine releaseRoutine;

        public bool HasOpenWindows => openWindows.Count > 0;
        public bool IsInputLocked => HasOpenWindows || releaseRoutine != null;
        public bool SuppressCancel { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InputRebindSaver.Load(actionAsset);
        }

        public void Open(UIWindow window)
        {
            if (window == null || window.IsOpen) { return; }

            bool wasEmpty = openWindows.Count == 0;

            if (!wasEmpty)
            {
                openWindows.Peek().SetInteractable(false);
            }

            openWindows.Push(window);
            window.transform.SetAsLastSibling();
            window.HandleOpened();
            UISFX.PlayOpen();

            if (wasEmpty)
            {
                CancelRelease();
                Time.timeScale = window.BlocksPlayerInput ? 0f : 1f;
                GameManager.Instance?.Player?.InteractionManager?.SetInteractionBlocked(true);
            }
        }

        public void Close(UIWindow window)
        {
            if (window == null || !window.IsOpen) { return; }

            if (openWindows.Count == 0 || openWindows.Peek() != window)
            {
                Debug.LogWarning($"[UIManager] Tried to close '{window.name}' but it isn't on top of the stack.");
                return;
            }

            openWindows.Pop();
            window.HandleClosed();
            UISFX.PlayClose();

            if (openWindows.Count == 0)
            {
                TrackClosing(window);
                ReleasePlayer();
            }
            else
            {
                UIWindow newTop = openWindows.Peek();
                newTop.SetInteractable(true);
                newTop.Reselect();
            }
        }

        public void CloseTopmost()
        {
            if (openWindows.Count > 0) { Close(openWindows.Peek()); }
        }

        public void CloseAll()
        {
            if (openWindows.Count == 0) { return; }

            while (openWindows.Count > 0)
            {
                UIWindow window = openWindows.Pop();
                window.HandleClosed();
                TrackClosing(window);
            }

            UISFX.PlayClose();
            ReleasePlayer();
        }

        private void TrackClosing(UIWindow window)
        {
            var animator = window.GetComponentInChildren<UIWindowAnimator>(true);
            if (animator != null) { closingAnimators.Add(animator); }
        }

        private bool AnyClosing()
        {
            foreach (var animator in closingAnimators)
            {
                if (animator != null && animator.IsAnimating) { return true; }
            }
            return false;
        }

        private void ReleasePlayer()
        {
            if (releaseRoutine != null) { return; }

            if (!AnyClosing())
            {
                FinishRelease();
                return;
            }

            releaseRoutine = StartCoroutine(ReleasePlayerRoutine());
        }

        private IEnumerator ReleasePlayerRoutine()
        {
            while (AnyClosing()) { yield return null; }
            FinishRelease();
        }

        private void FinishRelease()
        {
            releaseRoutine = null;
            closingAnimators.Clear();
            Time.timeScale = 1f;
            GameManager.Instance?.Player?.InteractionManager?.SetInteractionBlocked(false);
        }

        private void CancelRelease()
        {
            closingAnimators.Clear();
            if (releaseRoutine == null) { return; }
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }
    }
}