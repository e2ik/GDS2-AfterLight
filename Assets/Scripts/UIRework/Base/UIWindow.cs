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

        public bool BlocksPlayerInput => blocksPlayerInput;
        public bool IsOpen { get; private set; }

        protected virtual void Awake()
        {
            animator = GetComponent<UIWindowAnimator>();
            animator.InstantHide();
        }

        internal void HandleOpened()
        {
            IsOpen = true;

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
            animator.SetInteractable(interactable);
        }

        internal void Reselect()
        {
            SelectInitial();
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