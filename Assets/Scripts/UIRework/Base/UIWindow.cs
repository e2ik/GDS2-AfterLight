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

        // Open & Close Only callable by UIManager
        internal void HandleOpened()
        {
            IsOpen = true;

            OnWindowOpened();
            animator.Show();

            if (firstSelected != null)
            {
                EventSystem.current?.SetSelectedGameObject(firstSelected.gameObject);
            }
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
            if (firstSelected != null)
            {
                EventSystem.current?.SetSelectedGameObject(firstSelected.gameObject);
            }
        }

        protected virtual void OnWindowOpened() { }
        protected virtual void OnWindowClosed() { }

    }
}
