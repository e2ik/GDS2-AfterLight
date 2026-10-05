using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameUI
{
    [RequireComponent(typeof(Button))]
    public class ResetBindingsButton : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actionAsset;
        [SerializeField] private ConfirmWindow confirmWindow;
        [SerializeField] private SettingsWindow settingsWindow;
        [SerializeField] private bool resetVolume = true;
        [SerializeField] private string confirmMessage = "Reset all controls and volume to their defaults? This cannot be undone.";

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(HandleClicked);
            if (settingsWindow == null) settingsWindow = GetComponentInParent<SettingsWindow>(true);
        }

        private void HandleClicked()
        {
            UISFX.PlayClick();

            if (RebindButton.AnyRebinding) return;

            if (confirmWindow != null)
                confirmWindow.Show(confirmMessage, ResetAll);
            else
                ResetAll();
        }

        private void ResetAll()
        {
            if (resetVolume && settingsWindow != null) settingsWindow.ResetAudioToDefault();

            var assets = new System.Collections.Generic.HashSet<InputActionAsset>();
            if (actionAsset != null) assets.Add(actionAsset);

            foreach (RebindListBuilder builder in FindObjectsByType<RebindListBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                InputActionAsset builderAsset = builder.ActionAsset;
                if (builderAsset != null) assets.Add(builderAsset);
            }

            if (assets.Count == 0)
            {
                Debug.LogWarning("[ResetBindingsButton] No Input Action Asset found to reset.", this);
                return;
            }

            foreach (InputActionAsset asset in assets)
            {
                asset.RemoveAllBindingOverrides();
                InputRebindSaver.Save(asset);
                Debug.Log($"[ResetBindingsButton] Reset all bindings on '{asset.name}'.", this);
            }

            RebindListBuilder.NotifyBindingsReset();
        }
    }
}