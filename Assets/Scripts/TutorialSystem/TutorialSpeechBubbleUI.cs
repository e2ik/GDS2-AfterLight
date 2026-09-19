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

        [Header("Diagnostics")]
        [SerializeField] private bool logDiagnostics = true;

        private RectTransform selfRect;
        private RectTransform canvasRect;
        private Transform followTarget;
        private Coroutine autoHideRoutine;
        private Camera cachedCamera;
        private bool isHidden = true;

        private bool loggedCanvasWarning;
        private bool loggedCameraWarning;
        private bool loggedConversionWarning;

        private void Awake()
        {
            selfRect = GetComponent<RectTransform>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas != null) canvasRect = canvas.transform as RectTransform;
            if (layoutRoot == null && promptLabel != null) layoutRoot = promptLabel.transform.parent as RectTransform;

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

        public void Show(string text, Transform target, float duration = 0f)
        {
            isHidden = false;

            if (promptLabel != null) promptLabel.text = text;
            if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);

            followTarget = target;

            if (autoHideRoutine != null)
            {
                StopCoroutine(autoHideRoutine);
                autoHideRoutine = null;
            }

            windowAnimator?.Show(freezeplayer: false);

            if (duration > 0f)
                autoHideRoutine = StartCoroutine(AutoHideAfter(duration));
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
            windowAnimator?.Hide();
            followTarget = null;

            OnHidden?.Invoke(this);
        }

        private IEnumerator AutoHideAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            Hide();
        }
    }
}