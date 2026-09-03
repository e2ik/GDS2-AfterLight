using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public class PauseWindow : UIWindow
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button returnToTitleButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private ConfirmWindow confirmWindow;

        protected override void Awake()
        {
            base.Awake();

            resumeButton.onClick.AddListener(HandleResumeClicked);
            returnToTitleButton.onClick.AddListener(HandleReturnToTitleClicked);
            quitButton.onClick.AddListener(HandleQuitClicked);
        }

        private void HandleResumeClicked()
        {
            UIManager.Instance.Close(this);
        }

        private void HandleReturnToTitleClicked()
        {
            confirmWindow.Show(
                "Return to the title screen? Unsaved progress will be lost.",
                onConfirmCallback: () =>
                {
                    UIManager.Instance.Close(this);
                    // TODO: GameManager.Instance.ReturnToTitle() once this is wired into the real game.
                    // Left as a log for now since there's no GameManager in this sandbox scene.
                    Debug.Log("Would return to title here.");
                }
            );
        }

        private void HandleQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}