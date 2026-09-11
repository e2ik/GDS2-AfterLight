using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameUI
{
    [System.Serializable]
    public class RebindEntry
    {
        public InputActionReference action;
        public int bindingIndex;
        public string displayLabel;
    }

    public class RebindListBuilder : MonoBehaviour
    {
        [SerializeField] private RebindRow rowPrefab;
        [SerializeField] private Transform rowContainer;
        [SerializeField] private CanvasGroup sharedWaitingPrompt;
        [SerializeField] private List<RebindEntry> entries;

        private void Awake()
        {
            PopulateRows();
        }

        private void PopulateRows()
        {
            foreach (Transform child in rowContainer)
            {
                Destroy(child.gameObject);
            }

            foreach (RebindEntry entry in entries)
            {
                RebindRow row = Instantiate(rowPrefab, rowContainer);
                row.gameObject.SetActive(false);
                row.RebindButton.Initialize(entry.action, entry.bindingIndex, entry.displayLabel, sharedWaitingPrompt);
                row.gameObject.SetActive(true);
            }
        }
    }
}