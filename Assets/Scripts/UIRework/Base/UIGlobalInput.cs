using UnityEngine;
using UnityEngine.InputSystem;

namespace GameUI
{
    public class UIGlobalInput : MonoBehaviour
    {
        public static UIGlobalInput Instance { get; private set; }

        [SerializeField] private InputActionReference cancelAction;
        [SerializeField] private PauseWindow pauseWindow;

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
        }

        private void OnDisable()
        {
            cancelAction.action.performed -= HandleCancel;
        }

        private void HandleCancel(InputAction.CallbackContext context)
        {
            if (UIManager.Instance.SuppressCancel) { return; }

            if (UIManager.Instance.HasOpenWindows)
            {
                UIManager.Instance.CloseTopmost();
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