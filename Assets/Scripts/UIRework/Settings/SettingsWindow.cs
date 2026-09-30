using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GameUI
{
    public class SettingsWindow : UIWindow
    {
        [Header("Tabs")]
        [SerializeField] private Button audioTabButton;
        [FormerlySerializedAs("controlsTabButton")]
        [SerializeField] private Button keyboardTabButton;
        [SerializeField] private Button controllerTabButton;

        [SerializeField] private GameObject audioPanel;
        [FormerlySerializedAs("controlsPanel")]
        [SerializeField] private GameObject keyboardPanel;
        [SerializeField] private GameObject controllerPanel;

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

            audioTabButton.onClick.AddListener(HandleAudioTabClicked);
            keyboardTabButton.onClick.AddListener(HandleKeyboardTabClicked);
            controllerTabButton.onClick.AddListener(HandleControllerTabClicked);
        }

        protected override void OnWindowOpened()
        {
            masterBus.getVolume(out float currentVolume);
            masterVolumeSlider.SetValueWithoutNotify(currentVolume);

            ShowAudioTab();
        }

        protected override void OnWindowClosed()
        {
            foreach (RebindButton button in GetComponentsInChildren<RebindButton>(true))
            {
                button.CancelIfRebinding();
            }
        }

        private void ShowAudioTab()
        {
            audioPanel.SetActive(true);
            keyboardPanel.SetActive(false);
            controllerPanel.SetActive(false);
        }

        private void ShowKeyboardTab()
        {
            audioPanel.SetActive(false);
            keyboardPanel.SetActive(true);
            controllerPanel.SetActive(false);
        }

        private void ShowControllerTab()
        {
            audioPanel.SetActive(false);
            keyboardPanel.SetActive(false);
            controllerPanel.SetActive(true);
        }

        private void HandleMasterVolumeChanged(float value) => masterBus.setVolume(value);

        private void HandleAudioTabClicked()
        {
            UISFX.PlayClick();
            ShowAudioTab();
        }

        private void HandleKeyboardTabClicked()
        {
            UISFX.PlayClick();
            ShowKeyboardTab();
        }

        private void HandleControllerTabClicked()
        {
            UISFX.PlayClick();
            ShowControllerTab();
        }

        private void HandleBackClicked()
        {
            UISFX.PlayClick();
            UIManager.Instance.Close(this);
        }
    }
}