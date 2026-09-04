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

            openWindows.Push(window);
            window.transform.SetAsLastSibling();
            window.HandleOpened();

            if (wasEmpty && window.BlocksPlayerInput) { Time.timeScale = 0f; Debug.Log("TimeScale: " + Time.timeScale); }
        }

        public void Close(UIWindow window)
        {
            if (window == null || !window.IsOpen) { return; }

            if (openWindows.Count == 0 || openWindows.Peek() != window) { Debug.LogWarning($"[UIManager] Tried to close '{window.name}' but it isn't on top of the stack."); return; }

            openWindows.Pop();
            window.HandleClosed();

            if (openWindows.Count == 0) { Time.timeScale = 1f; Debug.Log("TimeScale: " + Time.timeScale); }

        }

        public void CloseTopmost()
        {
            if (openWindows.Count > 0) { Close(openWindows.Peek()); }
        }
    }
}