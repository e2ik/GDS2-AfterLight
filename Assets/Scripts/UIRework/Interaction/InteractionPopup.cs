using UnityEngine;

namespace GameUI
{
    public class InteractionPopup : MonoBehaviour
    {
        [SerializeField] private GameObject iconPrefab;
        [SerializeField] private Vector2 iconOffset = new Vector2(0f, 0.75f);

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
            iconInstance.transform.position = (Vector2)currentTarget.position + iconOffset;
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
    }
}