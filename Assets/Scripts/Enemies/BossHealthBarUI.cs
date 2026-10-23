using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Enemies
{
    public class BossHealthBarUI : MonoBehaviour
    {
        public static BossHealthBarUI Instance { get; private set; }
        
        [Header("References")] 
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform fillBar;
        [SerializeField] private RectTransform ghostBar;
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text bossNameText;

        [Header("Juice")] 
        [SerializeField] private float fillLerpSpeed = 3f;
        [SerializeField] private float ghostLerpSpeed = 1.2f;
        [SerializeField] private float ghostDelay = 0.35f;
        [SerializeField] private Color damageFlashColor = Color.white;
        [SerializeField] private float flashDuration = 0.1f;
        [SerializeField] private float punchScale = 1.15f;
        [SerializeField] private float punchDuration = 0.15f;
        [SerializeField] private float fadeDuration = 0.4f;

        [Header("Phase Refill")] 
        [SerializeField] private Color refillFlashColor = Color.white;
        private bool refilling;
        private Coroutine refillRoutine;
        
        private Color normalColor;
        private float targetFraction = 1f;
        private float currentFraction = 1f;
        private float ghostFraction = 1f;
        private float ghostDelayTimer;

        private Coroutine fadeRoutine;
        private Coroutine flashRoutine;
        private Coroutine punchRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            
            if (fillImage != null)
                normalColor = fillImage.color;

            canvasGroup.alpha = 0f;
            canvasGroup.gameObject.SetActive(false);
            SyncName(0f, false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Initialize(int currentHealth, int maxHealth, string bossName = null)
        {
            if (bossNameText != null)
                bossNameText.text = bossName ?? string.Empty;

            targetFraction = currentFraction = ghostFraction = Fraction(currentHealth, maxHealth);
            ApplyScale(fillBar, currentFraction);
            ApplyScale(ghostBar, ghostFraction);

            canvasGroup.gameObject.SetActive(true);
            SyncName(canvasGroup.alpha, true);

            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(Fade(1f));
        }
        
        public void SetHealth(int currentHealth, int maxHealth)
        {
            float newFraction = Fraction(currentHealth, maxHealth);

            if (newFraction < targetFraction)
            {
                ghostDelayTimer = ghostDelay;

                if (flashRoutine != null) StopCoroutine(flashRoutine);
                flashRoutine = StartCoroutine(Flash());

                if (punchRoutine != null) StopCoroutine(punchRoutine);
                punchRoutine = StartCoroutine(Punch());
            }

            targetFraction = newFraction;
        }
        
        public void Hide()
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(Fade(0f, deactivateOnEnd: true));
        }
        
        private void Update()
        {
            if (refilling) return;
            
            currentFraction = Mathf.MoveTowards(currentFraction, targetFraction, fillLerpSpeed * Time.deltaTime);
            ApplyScale(fillBar, currentFraction);

            if (ghostDelayTimer > 0f)
            {
                ghostDelayTimer -= Time.deltaTime;
            }
            else
            {
                ghostFraction = Mathf.MoveTowards(ghostFraction, targetFraction, ghostLerpSpeed * Time.deltaTime);
            }
            
            ghostFraction = Mathf.Max(ghostFraction, currentFraction);
            ApplyScale(ghostBar, ghostFraction);
        }
        
        private void SyncName(float alpha, bool visible)
        {
            if (bossNameText == null) return;

            bool hasName = !string.IsNullOrEmpty(bossNameText.text);
            bossNameText.gameObject.SetActive(visible && hasName);

            if (canvasGroup == null || !bossNameText.transform.IsChildOf(canvasGroup.transform))
                bossNameText.alpha = alpha;
        }

        private static float Fraction(int current, int max) => max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        
        private static void ApplyScale(RectTransform bar, float fraction)
        {
            if (bar == null) return;
            Vector3 scale = bar.localScale;
            scale.x = fraction;
            bar.localScale = scale;
        }
        
        private IEnumerator Fade(float targetAlpha, bool deactivateOnEnd = false)
        {
            float start = canvasGroup.alpha;
            float t = 0f;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(start, targetAlpha, t / fadeDuration);
                SyncName(canvasGroup.alpha, true);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            SyncName(targetAlpha, !deactivateOnEnd);
            if (deactivateOnEnd) canvasGroup.gameObject.SetActive(false);
            fadeRoutine = null;
        }
        
        private IEnumerator Flash()
        {
            if (fillImage == null) yield break;

            fillImage.color = damageFlashColor;
            yield return new WaitForSeconds(flashDuration);
            fillImage.color = normalColor;
            flashRoutine = null;
        }
        
        private IEnumerator Punch()
        {
            RectTransform root = (RectTransform)transform;
            Vector3 baseScale = root.localScale;
            float t = 0f;

            while (t < punchDuration)
            {
                t += Time.deltaTime;
                float curve = Mathf.Sin((t / punchDuration) * Mathf.PI);
                root.localScale = baseScale * (1f + (punchScale - 1f) * curve);
                yield return null;
            }

            root.localScale = baseScale;
            punchRoutine = null;
        }

        public void PlayRefill(int currentHealth, int maxHealth, float duration)
        {
            if(refillRoutine != null)
                StopCoroutine(refillRoutine);

            refillRoutine =
                StartCoroutine(RefillRoutine(Fraction(currentHealth, maxHealth), Mathf.Max(0.01f, duration)));
        }

        private IEnumerator RefillRoutine(float to, float duration)
        {
            refilling = true;
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            float from = currentFraction;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = p * p * (3f - 2f * p);

                currentFraction = ghostFraction = targetFraction = Mathf.Lerp(from, to, eased);
                ApplyScale(fillBar, currentFraction);
                ApplyScale(ghostBar, ghostFraction);

                if (fillImage != null)
                    fillImage.color = Color.Lerp(refillFlashColor, normalColor, p);
                
                yield return null;
            }

            currentFraction = ghostFraction = targetFraction = to;
            ApplyScale(fillBar, to);
            ApplyScale(ghostBar, to);
            
            if (fillImage != null)
                fillImage.color = normalColor;
            
            if(punchRoutine != null)
                StopCoroutine(punchRoutine);

            punchRoutine = StartCoroutine(Punch());

            refilling = false;
            refillRoutine = null;
        }
    }
}