using UnityEngine;

public class ObjectAnimOnOff : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool loadOn = true;
    [SerializeField] private string onState = "On";
    [SerializeField] private string offState = "Off";

    public bool LoadOn => loadOn;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        Apply();
    }

    public void SetLoad(bool on)
    {
        loadOn = on;
        Apply();
    }

    private void Apply()
    {
        if (animator == null || !isActiveAndEnabled) return;

        animator.Play(loadOn ? onState : offState);
    }

#if UNITY_EDITOR
    private bool dirty;

    private void OnValidate()
    {
        if (Application.isPlaying) dirty = true;
    }

    private void Update()
    {
        if (!dirty) return;
        dirty = false;
        Apply();
    }
#endif
}