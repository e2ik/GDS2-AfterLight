using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI
{
    public class UIButtonStroke : MonoBehaviour
    {
        [SerializeField] private Shader strokeShader;

        [Header("Finding Buttons")]
        [SerializeField] private Transform[] panels;
        [SerializeField, Min(0.1f)] private float rescanInterval = 0.5f;
        [SerializeField] private bool skipCustomMaterials = true;
        [SerializeField] private Button[] excludedButtons;

        [Header("Stroke")]
        [SerializeField, Range(1, 4)] private int strokeWidth = 1;
        [SerializeField, Range(0.01f, 1f)] private float alphaThreshold = 0.5f;

        [Header("Brighten Mode")]
        [SerializeField] private bool brightenInsteadOfColor = false;
        [SerializeField, Range(1f, 4f)] private float brightness = 1.5f;
        [SerializeField, Range(0f, 1f)] private float brightenLift = 0f;
        [SerializeField] private bool strokeWhenUnselected = true;
        [SerializeField] private Color normalStrokeColor = new Color(1f, 1f, 1f, 0.5f);

        [Header("Selected")]
        [SerializeField] private Color selectedStrokeColor = Color.white;
        [SerializeField, Min(0f)] private float pulseSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float pulseMinAlpha = 0.3f;

        private static readonly int StrokeColorId = Shader.PropertyToID("_StrokeColor");
        private static readonly int StrokeWidthId = Shader.PropertyToID("_StrokeWidth");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int PulseMinAlphaId = Shader.PropertyToID("_PulseMinAlpha");
        private static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");
        private static readonly int BrightenModeId = Shader.PropertyToID("_BrightenMode");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int BrightenLiftId = Shader.PropertyToID("_BrightenLift");
        private static readonly int UnscaledTimeId = Shader.PropertyToID("_UIUnscaledTime");

        private static readonly HashSet<Button> activeButtons = new HashSet<Button>();

        private readonly Dictionary<Image, Material> originalMaterials = new Dictionary<Image, Material>();
        private readonly HashSet<Image> highlightedImages = new HashSet<Image>();
        private readonly HashSet<Image> desiredHighlights = new HashSet<Image>();
        private readonly List<Image> highlightChanges = new List<Image>();
        private readonly List<Image> staleImages = new List<Image>();
        private readonly List<Button> foundButtons = new List<Button>();
        private Material normalMaterial;
        private Material selectedMaterial;
        private float rescanTimer;

        public static void SetButtonActive(Button button, bool active)
        {
            if (button == null) return;

            if (active) activeButtons.Add(button);
            else activeButtons.Remove(button);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeButtons.Clear();
        }

        private void Awake()
        {
            if (strokeShader == null) strokeShader = Shader.Find("UI/Inner Stroke");
            if (strokeShader == null)
            {
                Debug.LogWarning("[UIButtonStroke] Inner stroke shader not found.");
                enabled = false;
                return;
            }

            normalMaterial = new Material(strokeShader) { name = "UI Inner Stroke (Normal)" };
            selectedMaterial = new Material(strokeShader) { name = "UI Inner Stroke (Selected)" };
            ApplyMaterialSettings();
        }

        private void OnEnable()
        {
            rescanTimer = 0f;
        }

        private void OnDisable()
        {
            RestoreAll();
        }

        private void OnDestroy()
        {
            if (normalMaterial != null) Destroy(normalMaterial);
            if (selectedMaterial != null) Destroy(selectedMaterial);
        }

        private void OnValidate()
        {
            if (normalMaterial != null && selectedMaterial != null) ApplyMaterialSettings();
        }

        private void ApplyMaterialSettings()
        {
            Color normal = normalStrokeColor;
            if (!strokeWhenUnselected) normal.a = 0f;

            normalMaterial.SetColor(StrokeColorId, normal);
            normalMaterial.SetFloat(StrokeWidthId, strokeWidth);
            normalMaterial.SetFloat(PulseSpeedId, 0f);
            normalMaterial.SetFloat(AlphaThresholdId, alphaThreshold);

            selectedMaterial.SetColor(StrokeColorId, selectedStrokeColor);
            selectedMaterial.SetFloat(StrokeWidthId, strokeWidth);
            selectedMaterial.SetFloat(PulseSpeedId, pulseSpeed);
            selectedMaterial.SetFloat(PulseMinAlphaId, pulseMinAlpha);
            selectedMaterial.SetFloat(AlphaThresholdId, alphaThreshold);

            foreach (Material material in new[] { normalMaterial, selectedMaterial })
            {
                material.SetFloat(BrightenModeId, brightenInsteadOfColor ? 1f : 0f);
                material.SetFloat(BrightnessId, brightness);
                material.SetFloat(BrightenLiftId, brightenLift);
            }
        }

        private void Update()
        {
            Shader.SetGlobalFloat(UnscaledTimeId, Time.unscaledTime);

            rescanTimer -= Time.unscaledDeltaTime;
            if (rescanTimer <= 0f)
            {
                rescanTimer = rescanInterval;
                Rescan();
            }

            UpdateSelected();
        }

        private void Rescan()
        {
            staleImages.Clear();
            foreach (Image image in originalMaterials.Keys)
            {
                if (image == null) staleImages.Add(image);
            }
            foreach (Image image in staleImages) originalMaterials.Remove(image);

            if (panels == null || panels.Length == 0)
            {
                ScanPanel(transform);
                return;
            }

            foreach (Transform panel in panels)
            {
                if (panel != null) ScanPanel(panel);
            }
        }

        private void ScanPanel(Transform panel)
        {
            panel.GetComponentsInChildren(true, foundButtons);
            foreach (Button button in foundButtons)
            {
                if (IsExcluded(button)) continue;

                Image image = button.targetGraphic as Image;
                if (image == null || originalMaterials.ContainsKey(image)) continue;

                Material current = image.material;
                bool isDefault = current == null || current == image.defaultMaterial;
                if (skipCustomMaterials && !isDefault) continue;

                originalMaterials.Add(image, image.material);
                image.material = highlightedImages.Contains(image) ? selectedMaterial : normalMaterial;
            }
        }

        private bool IsExcluded(Button button)
        {
            if (excludedButtons == null) return false;

            foreach (Button excluded in excludedButtons)
            {
                if (excluded == button) return true;
            }
            return false;
        }

        private void AddDesiredHighlight(Button button)
        {
            if (button == null) return;

            Image image = button.targetGraphic as Image;
            if (image != null && originalMaterials.ContainsKey(image)) desiredHighlights.Add(image);
        }

        private void UpdateSelected()
        {
            desiredHighlights.Clear();

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null) AddDesiredHighlight(selected.GetComponent<Button>());

            foreach (Button button in activeButtons)
                AddDesiredHighlight(button);

            highlightChanges.Clear();
            foreach (Image image in highlightedImages)
            {
                if (!desiredHighlights.Contains(image)) highlightChanges.Add(image);
            }

            foreach (Image image in highlightChanges)
            {
                highlightedImages.Remove(image);
                if (image != null && originalMaterials.ContainsKey(image)) image.material = normalMaterial;
            }

            foreach (Image image in desiredHighlights)
            {
                if (highlightedImages.Add(image)) image.material = selectedMaterial;
            }
        }

        private void RestoreAll()
        {
            foreach (KeyValuePair<Image, Material> entry in originalMaterials)
            {
                if (entry.Key != null) entry.Key.material = entry.Value;
            }

            originalMaterials.Clear();
            highlightedImages.Clear();
        }
    }
}