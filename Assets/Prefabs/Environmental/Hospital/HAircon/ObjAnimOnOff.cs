using System.Collections;
using UnityEngine;

public class ObjectAnimOnOff : MonoBehaviour, IOnOff
{
    [SerializeField] private Animator animator;
    [SerializeField] private bool loadOn = true;
    [SerializeField] private string onState = "On";
    [SerializeField] private string offState = "Off";
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField, Min(0.01f)] private float onAnimationSpeed = 1f;
    [SerializeField, Min(0.01f)] private float offAnimationSpeed = 1f;
    [SerializeField, Min(0f)] private float speedRampDuration = 0.4f;
    [SerializeField] private AnimationCurve speedRampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private float currentSpeed;
    private Coroutine transitionRoutine;

    public bool LoadOn => loadOn;
    public bool IsOn => loadOn;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void OnDisable()
    {
        transitionRoutine = null;
    }

    public void SetLoad(bool on)
    {
        if (!isActiveAndEnabled)
        {
            loadOn = on;
            return;
        }

        if (loadOn == on) return;

        loadOn = on;

        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        transitionRoutine = StartCoroutine(TransitionRoutine(on));
    }

    public void SetOn(bool on)
    {
        SetLoad(on);
    }

    private IEnumerator TransitionRoutine(bool turningOn)
    {
        if (animator == null) yield break;

        if (turningOn)
        {
            animator.Play(onState, 0, 0f);
            yield return RampSpeed(currentSpeed, onAnimationSpeed);
        }
        else
        {
            yield return RampSpeed(currentSpeed, 0f);
            animator.Play(offState);
            currentSpeed = offAnimationSpeed;
            animator.SetFloat(speedParameter, offAnimationSpeed);
        }

        transitionRoutine = null;
    }

    private IEnumerator RampSpeed(float from, float to)
    {
        if (speedRampDuration <= 0f)
        {
            currentSpeed = to;
            animator.SetFloat(speedParameter, to);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / speedRampDuration;
            float eased = speedRampCurve.Evaluate(Mathf.Clamp01(t));
            currentSpeed = Mathf.LerpUnclamped(from, to, eased);
            animator.SetFloat(speedParameter, currentSpeed);
            yield return null;
        }

        currentSpeed = to;
        animator.SetFloat(speedParameter, to);
    }

    private void Apply()
    {
        if (animator == null || !isActiveAndEnabled) return;

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        currentSpeed = loadOn ? onAnimationSpeed : offAnimationSpeed;
        animator.SetFloat(speedParameter, currentSpeed);
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