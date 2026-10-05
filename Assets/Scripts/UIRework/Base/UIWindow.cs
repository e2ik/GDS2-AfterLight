using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI
{
    [RequireComponent(typeof(UIWindowAnimator))]
    public class UIWindow : MonoBehaviour
    {
        [SerializeField] private bool blocksPlayerInput = true;
        [SerializeField] private Selectable firstSelected;

        private UIWindowAnimator animator;
        private GameObject lastSelected;

        public bool BlocksPlayerInput => blocksPlayerInput;
        public bool IsOpen { get; private set; }
        public virtual bool CanClose => true;

        protected virtual void Awake()
        {
            animator = GetComponent<UIWindowAnimator>();
            animator.InstantHide();
        }

        internal void HandleOpened()
        {
            IsOpen = true;
            lastSelected = null;

            OnWindowOpened();
            animator.Show();

            SelectInitial();
        }

        internal void HandleClosed()
        {
            IsOpen = false;

            animator.Hide();
            OnWindowClosed();
        }

        internal void SetInteractable(bool interactable)
        {
            if (!interactable) RememberSelection();
            animator.SetInteractable(interactable);
        }

        internal void Reselect()
        {
            if (lastSelected != null && lastSelected.activeInHierarchy)
            {
                Selectable selectable = lastSelected.GetComponent<Selectable>();
                if (selectable == null || selectable.IsInteractable())
                {
                    EventSystem.current?.SetSelectedGameObject(lastSelected);
                    lastSelected = null;
                    return;
                }
            }

            lastSelected = null;
            SelectInitial();
        }

        private void RememberSelection()
        {
            GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            lastSelected = current != null && current.transform.IsChildOf(transform) ? current : null;
        }

        // need this because some windows are not selectable and they need their own implementation
        protected virtual Selectable GetInitialSelectable() => firstSelected;

        private void SelectInitial()
        {
            Selectable target = GetInitialSelectable();
            if (target != null)
            {
                EventSystem.current?.SetSelectedGameObject(target.gameObject);
            }
        }

        protected virtual void OnWindowOpened() { }
        protected virtual void OnWindowClosed() { }
    }
}