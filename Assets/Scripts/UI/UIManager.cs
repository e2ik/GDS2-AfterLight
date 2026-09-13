using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("UI Canvas References")]
    [SerializeField] private GameObject deathScreenCanvas;


    private UIWindowAnimator deathScreenAnimator;

    public GameObject DeathScreenCanvas => deathScreenCanvas;

    private void Awake()
    {
        if (deathScreenCanvas != null)
        {
            deathScreenAnimator = deathScreenCanvas.GetComponentInChildren<UIWindowAnimator>();

            if (deathScreenAnimator != null)
            {
                deathScreenAnimator.InstantHide();
            }
            else
            {
                deathScreenCanvas.SetActive(false);
            }
        }
    }

    public void SetDeathScreenActive(bool state)
    {
        if (deathScreenCanvas == null)
        {
            Debug.LogWarning("[UIManager] Death Screen Canvas reference is missing!");
            return;
        }

        if (deathScreenAnimator != null)
        {
            if (state)
            {
                deathScreenAnimator.Show(freezeplayer: false);
            }
            else
            {
                deathScreenAnimator.Hide();
            }
        }
        else
        {
            deathScreenCanvas.SetActive(state);
        }
    }
}