using UnityEngine;
using UnityEngine.InputSystem;

namespace GameUI
{
    public class UIGlobalInput : MonoBehaviour
    {
        public static UIGlobalInput Instance { get; private set; }

        [SerializeField] private InputActionReference cancelAction;
        [SerializeField] private InputActionReference menuAction;
        [SerializeField] private PauseWindow pauseWindow;

        [Tooltip("If a window is open, should the menu button close it instead of doing nothing?")]
        [SerializeField] private bool menuClosesOpenWindows = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            cancelAction.action.Enable();
            cancelAction.action.performed += HandleCancel;

            menuAction.action.Enable();
            menuAction.action.performed += HandleMenu;
        }

        private void OnDisable()
        {
            cancelAction.action.performed -= HandleCancel;
            menuAction.action.performed -= HandleMenu;
        }

        private void HandleCancel(InputAction.CallbackContext context)
        {
            if (UIManager.Instance.SuppressCancel) { return; }

            if (UIManager.Instance.HasOpenWindows)
            {
                UIManager.Instance.CloseTopmost();
            }
        }

        private void HandleMenu(InputAction.CallbackContext context)
        {
            if (UIManager.Instance.SuppressCancel) { return; }

            if (UIManager.Instance.HasOpenWindows)
            {
                if (menuClosesOpenWindows)
                {
                    UIManager.Instance.CloseAll();
                }
            }
            else
            {
                UIManager.Instance.Open(pauseWindow);
            }
        }

        public void ToggleWindow(UIWindow window)
        {
            if (window.IsOpen)
            {
                UIManager.Instance.Close(window);
            }
            else if (!UIManager.Instance.HasOpenWindows)
            {
                UIManager.Instance.Open(window);
            }
        }
    }
}