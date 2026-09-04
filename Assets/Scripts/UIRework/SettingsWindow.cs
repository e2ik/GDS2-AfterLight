using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI
{
    public class SettingsWindow : UIWindow
    {
        [Header("Tabs")]
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button controlsTabButton;
        [SerializeField] private GameObject audioPanel;
        [SerializeField] private GameObject controlsPanel;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;

        [Header("Shared")]
        [SerializeField] private Button backButton;

        private Bus masterBus;

        protected override void Awake()
        {
            base.Awake();
            masterBus = RuntimeManager.GetBus("bus:/");
            backButton.onClick.AddListener(HandleBackClicked);
            masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);

            audioTabButton.onClick.AddListener(ShowAudioTab);
            controlsTabButton.onClick.AddListener(ShowControlsTab);
        }

        protected override void OnWindowOpened()
        {
            masterBus.getVolume(out float currentVolume);
            masterVolumeSlider.SetValueWithoutNotify(currentVolume);

            ShowAudioTab();
        }

        private void ShowAudioTab()
        {
            audioPanel.SetActive(true);
            controlsPanel.SetActive(false);
        }

        private void ShowControlsTab()
        {
            audioPanel.SetActive(false);
            controlsPanel.SetActive(true);
        }

        private void HandleMasterVolumeChanged(float value) => masterBus.setVolume(value);

        private void HandleBackClicked() => UIManager.Instance.Close(this);
    }
}