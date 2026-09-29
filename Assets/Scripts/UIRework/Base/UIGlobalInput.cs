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

        [Tooltip("If a window is open, should the menu button close everything instead of doing nothing?")]
        [SerializeField] private bool menuClosesOpenWindows = true;

        private bool cancelRequested;
        private bool menuRequested;

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
            if (UIManager.Instance != null && UIManager.Instance.SuppressCancel) { return; }
            cancelRequested = true;
        }

        private void HandleMenu(InputAction.CallbackContext context)
        {
            if (UIManager.Instance != null && UIManager.Instance.SuppressCancel) { return; }
            menuRequested = true;
        }

        private void LateUpdate()
        {
            if (!cancelRequested && !menuRequested) { return; }

            bool cancel = cancelRequested;
            bool menu = menuRequested;
            cancelRequested = false;
            menuRequested = false;

            UIManager manager = UIManager.Instance;
            if (manager == null) { return; }

            if (manager.HasOpenWindows)
            {
                if (menu && menuClosesOpenWindows)
                {
                    manager.CloseAll();
                }
                else if (cancel)
                {
                    manager.CloseTopmost();
                }
            }
            else if (menu)
            {
                manager.Open(pauseWindow);
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