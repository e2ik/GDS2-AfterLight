using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public class PauseWindow : UIWindow
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button returnToTitleButton;

        [SerializeField] private ConfirmWindow confirmWindow;

        [SerializeField] private Button settingsButton;
        [SerializeField] private SettingsWindow settingsWindow;

        protected override void Awake()
        {
            base.Awake();

            resumeButton.onClick.AddListener(HandleResumeClicked);
            returnToTitleButton.onClick.AddListener(HandleReturnToTitleClicked);
            quitButton.onClick.AddListener(HandleQuitClicked);
            settingsButton.onClick.AddListener(HandleSettingsClicked);
        }

        private void HandleResumeClicked()
        {
            UISFX.PlayButtonClick();
            UIManager.Instance.Close(this);
        }

        private void HandleReturnToTitleClicked()
        {
            UISFX.PlayButtonClick();
            confirmWindow.Show(
                "Return to the title screen? Unsaved progress will be lost.",
                onConfirmCallback: () =>
                {
                    UIManager.Instance.Close(this);
                    Debug.Log("Would return to title here.");
                }
            );
        }

        private void HandleQuitClicked()
        {
            UISFX.PlayButtonClick();
            confirmWindow.Show(
                "Quit the game? Unsaved progress will be lost.",
                onConfirmCallback: () =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
                }
            );
        }

        private void HandleSettingsClicked()
        {
            UISFX.PlayButtonClick();
            UIManager.Instance.Open(settingsWindow);
        }
    }
}