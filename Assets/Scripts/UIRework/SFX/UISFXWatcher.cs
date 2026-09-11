using System;
using FMODUnity;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameUI
{
    public class UISFXWatcher : MonoBehaviour
    {
        [SerializeField] private EventReference uiSelectEvent;

        private GameObject lastSelected;

        private void Update()
        {
            GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            if (current != null && current != lastSelected)
            {
                AudioManager.PlaySFX(uiSelectEvent);
            }

            lastSelected = current;
        }
    }
}
