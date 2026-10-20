using System.Collections.Generic;
using Enemies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public class InteractionPopup : MonoBehaviour
    {
        [SerializeField] private GameObject iconPrefab;
        [SerializeField] private Vector2 iconOffset = new Vector2(0f, 0.15f);

        [Header("Hover")]
        [SerializeField] private bool hover = true;
        [SerializeField] private float hoverHeight = 0.05f;
        [SerializeField] private float hoverSpeed = 3f;

        [Header("Combat Sorting")]
        [SerializeField] private bool belowPlayerInCombat = true;
        [SerializeField] private int combatOrderOffset = -2;

        [Header("Fade")]
        [SerializeField, Min(0f)] private float fadeDuration = 0.12f;
        [SerializeField, Range(0f, 1f)] private float combatAlpha = 0.7f;
        [SerializeField] private bool removeIconGlowInCombat = true;
        [SerializeField] private Color combatIconTint = new Color(1f, 0.7f, 0.7f, 1f);

        private InteractionManager interactionManager;
        private GameObject iconInstance;
        private Transform currentTarget;
        private Collider2D currentPromptCollider;

        private readonly Dictionary<Renderer, Vector2Int> rendererOriginals = new();
        private readonly Dictionary<Canvas, (bool overrideSorting, int layer, int order)> canvasOriginals = new();
        private SpriteRenderer playerSprite;
        private bool combatSortingApplied;

        private readonly Dictionary<SpriteRenderer, float> spriteAlphas = new();
        private readonly Dictionary<Graphic, float> graphicAlphas = new();
        private bool visible;
        private float currentAlpha;
        private InputActionPrompt[] prompts;
        private float combatBlend;

        private void Awake()
        {
            if (iconPrefab != null)
            {
                iconInstance = Instantiate(iconPrefab);
                iconInstance.SetActive(false);

                CaptureOriginals();
                CaptureAlphas();
                ApplyAlpha(0f);
                foreach (InputActionPrompt prompt in prompts) prompt.SetFade(0f, false);
            }
        }

        private void LateUpdate()
        {
            UpdateIconPosition();
            UpdateSorting();
            UpdateFade();
        }

        private void UpdateSorting()
        {
            if (iconInstance == null || !iconInstance.activeSelf) return;

            bool inCombat = belowPlayerInCombat && EnemyCombatTracker.TargetingCount > 0;
            if (!combatSortingApplied) CaptureOriginals();

            if (inCombat && TryGetPlayerSprite(out SpriteRenderer player))
            {
                CaptureOriginals();
                int order = player.sortingOrder + combatOrderOffset;

                foreach (Renderer r in rendererOriginals.Keys)
                {
                    if (r == null) continue;
                    r.sortingLayerID = player.sortingLayerID;
                    r.sortingOrder = order;
                }

                foreach (Canvas c in canvasOriginals.Keys)
                {
                    if (c == null) continue;
                    c.overrideSorting = true;
                    c.sortingLayerID = player.sortingLayerID;
                    c.sortingOrder = order;
                }

                combatSortingApplied = true;
            }
            else if (combatSortingApplied)
            {
                foreach (var pair in rendererOriginals)
                {
                    if (pair.Key == null) continue;
                    pair.Key.sortingLayerID = pair.Value.x;
                    pair.Key.sortingOrder = pair.Value.y;
                }

                foreach (var pair in canvasOriginals)
                {
                    if (pair.Key == null) continue;
                    pair.Key.overrideSorting = pair.Value.overrideSorting;
                    pair.Key.sortingLayerID = pair.Value.layer;
                    pair.Key.sortingOrder = pair.Value.order;
                }

                combatSortingApplied = false;
            }
        }

        private void CaptureOriginals()
        {
            if (iconInstance == null) return;

            foreach (Renderer r in iconInstance.GetComponentsInChildren<Renderer>(true))
            {
                if (!rendererOriginals.ContainsKey(r))
                    rendererOriginals[r] = new Vector2Int(r.sortingLayerID, r.sortingOrder);
            }

            foreach (Canvas c in iconInstance.GetComponentsInChildren<Canvas>(true))
            {
                if (!canvasOriginals.ContainsKey(c))
                    canvasOriginals[c] = (c.overrideSorting, c.sortingLayerID, c.sortingOrder);
            }
        }

        private bool TryGetPlayerSprite(out SpriteRenderer sprite)
        {
            if (playerSprite == null && GameManager.Instance != null && GameManager.Instance.Player != null)
                playerSprite = GameManager.Instance.Player.GetComponentInChildren<SpriteRenderer>();

            sprite = playerSprite;
            return sprite != null;
        }

        private void UpdateFade()
        {
            if (iconInstance == null || !iconInstance.activeSelf) return;

            bool inCombat = EnemyCombatTracker.TargetingCount > 0;
            float target = visible ? (inCombat ? combatAlpha : 1f) : 0f;

            currentAlpha = fadeDuration <= 0f
                ? target
                : Mathf.MoveTowards(currentAlpha, target, Time.unscaledDeltaTime / fadeDuration);

            combatBlend = fadeDuration <= 0f
                ? (inCombat ? 1f : 0f)
                : Mathf.MoveTowards(combatBlend, inCombat ? 1f : 0f, Time.unscaledDeltaTime / fadeDuration);

            CaptureAlphas();
            ApplyAlpha(currentAlpha);

            foreach (InputActionPrompt prompt in prompts)
            {
                if (prompt == null) continue;
                prompt.SetFade(currentAlpha, inCombat && removeIconGlowInCombat);
                prompt.SetTint(combatIconTint, combatBlend);
            }

            if (!visible && currentAlpha <= 0f)
                iconInstance.SetActive(false);
        }

        private void CaptureAlphas()
        {
            if (iconInstance == null) return;

            prompts ??= iconInstance.GetComponentsInChildren<InputActionPrompt>(true);

            foreach (SpriteRenderer sr in iconInstance.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (IsPromptIcon(sr)) continue;
                if (!spriteAlphas.ContainsKey(sr)) spriteAlphas[sr] = sr.color.a;
            }

            foreach (Graphic g in iconInstance.GetComponentsInChildren<Graphic>(true))
            {
                if (g is TMP_SubMeshUI || IsPromptIcon(g)) continue;
                if (!graphicAlphas.ContainsKey(g)) graphicAlphas[g] = g.color.a;
            }
        }

        private bool IsPromptIcon(Component component)
        {
            foreach (InputActionPrompt prompt in prompts)
            {
                if (prompt != null && prompt.Owns(component)) return true;
            }
            return false;
        }

        private void ApplyAlpha(float alpha)
        {
            foreach (var pair in spriteAlphas)
            {
                if (pair.Key == null) continue;
                Color c = pair.Key.color;
                c.a = pair.Value * alpha;
                pair.Key.color = c;
            }

            foreach (var pair in graphicAlphas)
            {
                if (pair.Key == null) continue;
                Color c = pair.Key.color;
                c.a = pair.Value * alpha;
                pair.Key.color = c;
            }
        }

        private void UpdateIconPosition()
        {
            if (iconInstance == null || currentTarget == null) return;

            Vector3 anchor = GetAnchor(currentTarget, currentPromptCollider);
            float bob = hover ? Mathf.Sin(Time.unscaledTime * hoverSpeed) * hoverHeight : 0f;
            iconInstance.transform.position = (Vector2)anchor + iconOffset + Vector2.up * bob;
        }

        public void Bind(InteractionManager manager)
        {
            Unbind();
            interactionManager = manager;
            if (interactionManager != null) interactionManager.OnInteractionTargetChanged += HandleTargetChanged;
        }

        public void Unbind()
        {
            if (interactionManager != null) interactionManager.OnInteractionTargetChanged -= HandleTargetChanged;
            interactionManager = null;
        }

        private void OnDestroy()
        {
            Unbind();
            if (iconInstance != null) Destroy(iconInstance);
        }

        private void HandleTargetChanged(Transform target, Collider2D promptCollider)
        {
            currentTarget = target;
            currentPromptCollider = promptCollider;
            UpdateIconPosition();
            if (iconInstance == null) return;

            visible = target != null;
            if (visible) iconInstance.SetActive(true);
        }

        private static Vector3 GetAnchor(Transform target, Collider2D promptCollider)
        {
            if (promptCollider != null)
            {
                Bounds colliderBounds = promptCollider.bounds;
                return new Vector3(colliderBounds.center.x, colliderBounds.max.y, target.position.z);
            }

            if (TryGetBounds(target, out Bounds bounds))
                return new Vector3(bounds.center.x, bounds.max.y, target.position.z);

            return target.position;
        }

        private static bool TryGetBounds(Transform target, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;

                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            if (found) return true;

            foreach (var col in target.GetComponentsInChildren<Collider2D>())
            {
                if (!col.enabled) continue;

                if (!found) { bounds = col.bounds; found = true; }
                else bounds.Encapsulate(col.bounds);
            }

            return found;
        }
    }
}