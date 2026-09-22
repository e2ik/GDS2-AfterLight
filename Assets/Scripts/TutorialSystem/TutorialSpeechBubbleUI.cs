using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tutorial
{
    public class TutorialSpeechBubbleUI : MonoBehaviour
    {
        public event Action<TutorialSpeechBubbleUI> OnHidden;

        [Header("Follow Settings")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Vector3 worldOffset = new(0f, 1.75f, 0f);

        [Header("Content")]
        [SerializeField] private UIWindowAnimator windowAnimator;
        [SerializeField] private TMP_Text promptLabel;
        [Tooltip("The RectTransform with the Content Size Fitter (usually promptLabel's parent panel). Auto-detected if left empty.")]
        [SerializeField] private RectTransform layoutRoot;
        [Tooltip("Optional. Point its Dialogue Panel at layoutRoot and its Dialogue Text at promptLabel. Auto-detected if left empty.")]
        [SerializeField] private DialogueEffects dialogueEffects;

        [Header("Typewriter")]
        [SerializeField] private bool useTypewriter = false;
        [SerializeField, Min(0f)] private float typewriterCharDelay = 0.03f;
        [Tooltip("Only used when Use Typewriter is on. If ticked, the dialogue effect (Angry, Unstable, etc.) doesn't start until the text has finished typing out.")]
        [SerializeField] private bool playEffectAfterTypewriter = false;

        [Header("Diagnostics")]
        [SerializeField] private bool logDiagnostics = true;

        private RectTransform selfRect;
        private RectTransform canvasRect;
        private Transform followTarget;
        private Coroutine autoHideRoutine;
        private Coroutine typeCoroutine;
        private Camera cachedCamera;
        private bool isHidden = true;
        private DialogueEffect activeEffect = DialogueEffect.Default;
        private bool hasActiveEffect;
        private string lastShownText;
        private Transform lastShownTarget;

        private bool loggedCanvasWarning;
        private bool loggedCameraWarning;
        private bool loggedConversionWarning;

        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = canvas.transform as RectTransform;
            if (layoutRoot == null && promptLabel != null) layoutRoot = promptLabel.transform.parent as RectTransform;
            if (dialogueEffects == null) dialogueEffects = GetComponentInChildren<DialogueEffects>(true);

            if (logDiagnostics && canvasRect == null)
                Debug.LogWarning("[TutorialSpeechBubbleUI] No Canvas found in parents — this object needs to be a child of a Canvas.", this);

            TutorialDirector.Instance?.RegisterExcludedWindow(windowAnimator);

            windowAnimator?.InstantHide();
        }

        private Camera ResolveCamera()
        {
            if (worldCamera != null) return worldCamera;
            if (cachedCamera != null) return cachedCamera;

            cachedCamera = Camera.main;
            if (cachedCamera == null)
            {
                var follow = FindFirstObjectByType<CameraFollow2D>();
                if (follow != null)
                {
                    cachedCamera = follow.GetComponent<Camera>();
                    if (cachedCamera == null && logDiagnostics && !loggedCameraWarning)
                    {
                        Debug.LogWarning("[TutorialSpeechBubbleUI] Found a CameraFollow2D, but it has no Camera component on the same GameObject.", follow);
                        loggedCameraWarning = true;
                    }
                }
                else if (logDiagnostics && !loggedCameraWarning)
                {
                    Debug.LogWarning("[TutorialSpeechBubbleUI] Camera.main is null and no CameraFollow2D was found in any loaded scene.");
                    loggedCameraWarning = true;
                }
            }
            return cachedCamera;
        }

        private void LateUpdate()
        {
            if (followTarget == null) return;

            if (canvasRect == null)
            {
                if (logDiagnostics && !loggedCanvasWarning)
                {
                    Debug.LogWarning("[TutorialSpeechBubbleUI] canvasRect is null — following is disabled until this is parented under a Canvas.", this);
                    loggedCanvasWarning = true;
                }
                return;
            }

            Camera cam = ResolveCamera();
            if (cam == null) return;

            Vector3 worldPos = followTarget.position + worldOffset;
            Vector2 screenPoint = cam.WorldToScreenPoint(worldPos);

            Camera screenToLocalCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam;
            bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, screenToLocalCam, out Vector2 localPoint);

            if (converted)
            {
                selfRect.anchoredPosition = localPoint;
            }
            else if (logDiagnostics && !loggedConversionWarning)
            {
                Debug.LogWarning($"[TutorialSpeechBubbleUI] ScreenPointToLocalPointInRectangle failed. worldPos={worldPos}, screenPoint={screenPoint}, camera={cam.name}", this);
                loggedConversionWarning = true;
            }
        }

        public void Show(string text, Transform target, float duration = 0f, DialogueEffect effect = DialogueEffect.Default)
        {
            bool alreadyShowingSame = !isHidden && followTarget == target && lastShownText == text;

            isHidden = false;
            lastShownText = text;
            lastShownTarget = target;

            bool deferUntilTypewriterDone = useTypewriter && playEffectAfterTypewriter;

            if (!alreadyShowingSame)
            {
                if (typeCoroutine != null)
                {
                    StopCoroutine(typeCoroutine);
                    typeCoroutine = null;
                }

                if (promptLabel != null)
                {
                    if (useTypewriter)
                    {
                        typeCoroutine = StartCoroutine(TypeText(text, deferUntilTypewriterDone ? effect : (DialogueEffect?)null));
                    }
                    else
                    {
                        promptLabel.text = text;
                        promptLabel.maxVisibleCharacters = int.MaxValue;
                        if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
                    }
                }
            }

            followTarget = target;

            if (autoHideRoutine != null)
            {
                StopCoroutine(autoHideRoutine);
                autoHideRoutine = null;
            }

            windowAnimator?.Show(freezeplayer: false);

            if (!deferUntilTypewriterDone)
            {
                ApplyEffect(effect);
            }

            if (duration > 0f)
                autoHideRoutine = StartCoroutine(AutoHideAfter(duration));
        }

        private void ApplyEffect(DialogueEffect effect)
        {
            if (dialogueEffects == null) return;

            bool sameEffectAlreadyRunning = hasActiveEffect && activeEffect == effect;
            if (sameEffectAlreadyRunning) return;

            dialogueEffects.PlayEffect(effect);
            activeEffect = effect;
            hasActiveEffect = true;
        }

        public void Hide()
        {
            if (isHidden) return;
            isHidden = true;

            if (autoHideRoutine != null)
            {
                StopCoroutine(autoHideRoutine);
                autoHideRoutine = null;
            }

            if (typeCoroutine != null)
            {
                StopCoroutine(typeCoroutine);
                typeCoroutine = null;
            }

            if (dialogueEffects != null) dialogueEffects.StopEffects();
            hasActiveEffect = false;
            lastShownText = null;
            lastShownTarget = null;

            windowAnimator?.Hide();
            followTarget = null;

            OnHidden?.Invoke(this);
        }

        private IEnumerator TypeText(string text, DialogueEffect? effectWhenDone)
        {
            promptLabel.text = text;
            promptLabel.maxVisibleCharacters = int.MaxValue;
            if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            promptLabel.ForceMeshUpdate();
            int totalChars = promptLabel.textInfo.characterCount;
            promptLabel.maxVisibleCharacters = 0;

            for (int i = 0; i <= totalChars; i++)
            {
                promptLabel.maxVisibleCharacters = i;
                yield return new WaitForSecondsRealtime(typewriterCharDelay);
            }

            typeCoroutine = null;

            if (effectWhenDone.HasValue)
                ApplyEffect(effectWhenDone.Value);
        }

        private IEnumerator AutoHideAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            Hide();
        }
    }
}