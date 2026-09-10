using UnityEngine;

public class PauseMenuController : MonoBehaviour
{
    public void OnResumeClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TogglePause();
        }
    }

    public void OnReturnToTitleClicked()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnToTitle();
        }
    }

    public void OnQuitClicked()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}