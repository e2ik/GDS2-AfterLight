using FMOD.Studio;
using FMODUnity;
using System.Collections.Generic;
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
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider uiVolumeSlider;

        private readonly List<(Slider slider, Bus bus)> audioChannels = new List<(Slider, Bus)>();

        [Header("Shared")]
        [SerializeField] private Button backButton;

        protected override void Awake()
        {
            base.Awake();
            backButton.onClick.AddListener(HandleBackClicked);

            RegisterAudioChannel(masterVolumeSlider, "bus:/");
            RegisterAudioChannel(musicVolumeSlider, "bus:/Music");
            RegisterAudioChannel(sfxVolumeSlider, "bus:/SFX");
            RegisterAudioChannel(uiVolumeSlider, "bus:/UI");

            audioTabButton.onClick.AddListener(HandleAudioTabClicked);
            keyboardTabButton.onClick.AddListener(HandleKeyboardTabClicked);
            controllerTabButton.onClick.AddListener(HandleControllerTabClicked);
        }

        protected override void OnWindowOpened()
        {
            foreach ((Slider slider, Bus bus) in audioChannels)
            {
                bus.getVolume(out float currentVolume);
                slider.SetValueWithoutNotify(currentVolume);
            }

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

        private void RegisterAudioChannel(Slider slider, string busPath)
        {
            if (slider == null) return;

            try
            {
                Bus bus = RuntimeManager.GetBus(busPath);
                audioChannels.Add((slider, bus));
                slider.onValueChanged.AddListener(value => bus.setVolume(value));
            }
            catch (BusNotFoundException)
            {
                Debug.LogWarning($"[SettingsWindow] FMOD bus '{busPath}' not found - slider '{slider.name}' will do nothing", this);
                slider.interactable = false;
            }
        }
    }
}