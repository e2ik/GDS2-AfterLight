using UnityEngine;

namespace GameUI
{
    public class InteractionPopup : MonoBehaviour
    {
        [SerializeField] private GameObject iconPrefab;
        [SerializeField] private Vector2 iconOffset = new Vector2(0f, 0.15f);

        private InteractionManager interactionManager;
        private GameObject iconInstance;
        private Transform currentTarget;

        private void Awake()
        {
            if (iconPrefab != null)
            {
                iconInstance = Instantiate(iconPrefab);
                iconInstance.SetActive(false);
            }
        }

        private void Update()
        {
            if (iconInstance == null || currentTarget == null) return;

            Vector3 anchor = GetTopCenter(currentTarget);
            iconInstance.transform.position = (Vector2)anchor + iconOffset;
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

        private void HandleTargetChanged(Transform target)
        {
            currentTarget = target;
            if (iconInstance != null) iconInstance.SetActive(target != null);
        }

        private static Vector3 GetTopCenter(Transform target)
        {
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