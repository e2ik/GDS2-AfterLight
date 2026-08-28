using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("UI Canvas References")]
    [SerializeField] private GameObject pauseCanvas;
    
    private UIWindowAnimator pauseAnimator;

    public GameObject PauseCanvas => pauseCanvas;

    private void Awake()
    {
        if (pauseCanvas != null)
        {
            pauseAnimator = pauseCanvas.GetComponentInChildren<UIWindowAnimator>();

            if (pauseAnimator != null)
            {
                pauseAnimator.InstantHide();
            }
            else
            {
                pauseCanvas.SetActive(false);
            }
        }
    }

    public void SetPauseCanvasActive(bool state)
    {
        if (pauseCanvas == null)
        {
            Debug.LogWarning("[UIManager] Pause Canvas reference is missing!");
            return;
        }

        if (pauseAnimator != null)
        {
            if (state)
            {
                pauseAnimator.Show(freezeplayer: false);
            }
            else
            {
                pauseAnimator.Hide();
            }
        }
        else
        {
            pauseCanvas.SetActive(state);
        }
    }

    public void TogglePauseCanvas()
    {
        if (pauseCanvas != null)
        {
            bool isCurrentlyActive = pauseCanvas.activeSelf;
            SetPauseCanvasActive(!isCurrentlyActive);
        }
    }
}