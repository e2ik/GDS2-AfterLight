using System;
using System.Collections;
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

        [Header("Juice")] 
        [SerializeField] private float fillLerpSpeed = 3f;
        [SerializeField] private float ghostLerpSpeed = 1.2f;
        [SerializeField] private float ghostDelay = 0.35f;
        [SerializeField] private Color damageFlashColor = Color.white;
        [SerializeField] private float flashDuration = 0.1f;
        [SerializeField] private float punchScale = 1.15f;
        [SerializeField] private float punchDuration = 0.15f;
        [SerializeField] private float fadeDuration = 0.4f;
        
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
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Initialize(int currentHealth, int maxHealth)
        {
            targetFraction = currentFraction = ghostFraction = Fraction(currentHealth, maxHealth);
            ApplyScale(fillBar, currentFraction);
            ApplyScale(ghostBar, ghostFraction);

            canvasGroup.gameObject.SetActive(true);

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
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
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
    }
}