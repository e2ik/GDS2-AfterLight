using System.Collections;
using Enemies;
using UnityEngine;

public class PerfectDodgeSlowMotion : MonoBehaviour
{
    [SerializeField] private bool slowOnPerfectDodge = true;
    [SerializeField, Range(0.05f, 1f)] private float slowTimeScale = 0.3f;
    [SerializeField] private float slowDuration = 0.5f;
    [SerializeField] private float easeOutDuration = 0.15f;
    [SerializeField] private float cooldown = 1f;

    private Coroutine slowRoutine;
    private float baseFixedDeltaTime;
    private float lastTriggerTime = float.NegativeInfinity;
    private float appliedScale = 1f;

    private void Awake() => baseFixedDeltaTime = Time.fixedDeltaTime;

    private void OnEnable() => PlayerHurtBox.AnyAttackDodged += HandleDodge;

    private void OnDisable()
    {
        PlayerHurtBox.AnyAttackDodged -= HandleDodge;
        StopSlow();
    }

    private void HandleDodge(HitBox hitbox, bool isPerfect)
    {
        if (!slowOnPerfectDodge || !isPerfect) return;
        if (Time.unscaledTime - lastTriggerTime < cooldown) return;

        lastTriggerTime = Time.unscaledTime;

        if (slowRoutine != null) StopCoroutine(slowRoutine);
        slowRoutine = StartCoroutine(SlowRoutine());
    }

    private IEnumerator SlowRoutine()
    {
        SetScale(slowTimeScale);

        float t = 0f;
        while (t < slowDuration)
        {
            if (!StillOurs()) { slowRoutine = null; yield break; }
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < easeOutDuration)
        {
            if (!StillOurs()) { slowRoutine = null; yield break; }
            t += Time.unscaledDeltaTime;
            SetScale(Mathf.Lerp(slowTimeScale, 1f, t / easeOutDuration));
            yield return null;
        }

        if (StillOurs()) SetScale(1f);
        slowRoutine = null;
    }

    private bool StillOurs() => Mathf.Approximately(Time.timeScale, appliedScale);

    private void SetScale(float scale)
    {
        appliedScale = scale;
        Time.timeScale = scale;
        Time.fixedDeltaTime = baseFixedDeltaTime * scale;
    }

    private void StopSlow()
    {
        if (slowRoutine != null)
        {
            StopCoroutine(slowRoutine);
            slowRoutine = null;
        }

        if (StillOurs() && !Mathf.Approximately(Time.timeScale, 1f))
            SetScale(1f);
    }
}