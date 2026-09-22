using UnityEngine;

public class FadeCanvasController : MonoBehaviour
{
    public static FadeCanvasController Instance { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;

    public CanvasGroup Group => canvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }
}