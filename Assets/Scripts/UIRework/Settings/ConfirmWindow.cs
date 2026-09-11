using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public class ConfirmWindow : UIWindow
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action onConfirm;
        private Action onCancel;

        protected override void Awake()
        {
            base.Awake();

            confirmButton.onClick.AddListener(HandleConfirmClicked);
            cancelButton.onClick.AddListener(HandleCancelClicked);
        }

        public void Show(string message, Action onConfirmCallback, Action onCancelCallback = null)
        {
            messageText.text = message;
            onConfirm = onConfirmCallback;
            onCancel = onCancelCallback;

            UIManager.Instance.Open(this);
        }

        private void HandleConfirmClicked()
        {
            UIManager.Instance.Close(this);
            onConfirm?.Invoke();
        }

        private void HandleCancelClicked()
        {
            UIManager.Instance.Close(this);
            onCancel?.Invoke();
        }
    }
}