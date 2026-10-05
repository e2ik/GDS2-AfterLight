using FMOD.Studio;
using FMODUnity;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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

        [SerializeField] private Color activeTabColor = Color.white;
        [SerializeField] private Color inactiveTabColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private bool tintTabText = true;

        [SerializeField] private InputActionReference previousTabAction;
        [SerializeField] private InputActionReference nextTabAction;

        [Header("Audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider uiVolumeSlider;

        private readonly List<(Slider slider, Bus bus)> audioChannels = new List<(Slider, Bus)>();
        private int currentTab;

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

            DisableNavigation(audioTabButton);
            DisableNavigation(keyboardTabButton);
            DisableNavigation(controllerTabButton);
        }

        private void OnEnable()
        {
            SubscribeTabAction(previousTabAction, HandlePreviousTab);
            SubscribeTabAction(nextTabAction, HandleNextTab);
        }

        private void OnDisable()
        {
            if (previousTabAction != null) previousTabAction.action.performed -= HandlePreviousTab;
            if (nextTabAction != null) nextTabAction.action.performed -= HandleNextTab;
        }

        private static void SubscribeTabAction(InputActionReference reference, System.Action<InputAction.CallbackContext> handler)
        {
            if (reference == null || reference.action == null) return;
            if (!reference.action.enabled) reference.action.Enable();
            reference.action.performed += handler;
        }

        private static void DisableNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            selectable.navigation = new Navigation { mode = Navigation.Mode.None };
        }

        private bool CanSwitchTabs =>
            IsOpen
            && !RebindButton.AnyRebinding
            && UIManager.Instance != null
            && UIManager.Instance.IsTopmost(this);

        private void HandlePreviousTab(InputAction.CallbackContext _)
        {
            if (!CanSwitchTabs) return;
            UISFX.PlayClick();
            ShowTab((currentTab + 2) % 3);
        }

        private void HandleNextTab(InputAction.CallbackContext _)
        {
            if (!CanSwitchTabs) return;
            UISFX.PlayClick();
            ShowTab((currentTab + 1) % 3);
        }

        private void ShowTab(int index)
        {
            switch (index)
            {
                case 1: ShowKeyboardTab(); break;
                case 2: ShowControllerTab(); break;
                default: ShowAudioTab(); break;
            }
        }

        private void UpdateTabHighlight()
        {
            TintTab(audioTabButton, currentTab == 0);
            TintTab(keyboardTabButton, currentTab == 1);
            TintTab(controllerTabButton, currentTab == 2);
        }

        private void TintTab(Button tab, bool active)
        {
            if (tab == null) return;

            Color color = active ? activeTabColor : inactiveTabColor;
            if (tab.targetGraphic != null) tab.targetGraphic.color = color;

            if (!tintTabText) return;
            foreach (TMP_Text text in tab.GetComponentsInChildren<TMP_Text>(true))
                text.color = color;
        }

        private void SelectFirstIn(GameObject panel)
        {
            if (panel == null || EventSystem.current == null) return;

            foreach (Selectable selectable in panel.GetComponentsInChildren<Selectable>())
            {
                if (selectable.IsInteractable() && selectable.navigation.mode != Navigation.Mode.None)
                {
                    EventSystem.current.SetSelectedGameObject(selectable.gameObject);
                    return;
                }
            }
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
            currentTab = 0;
            audioPanel.SetActive(true);
            keyboardPanel.SetActive(false);
            controllerPanel.SetActive(false);
            UpdateTabHighlight();
            SelectFirstIn(audioPanel);
        }

        private void ShowKeyboardTab()
        {
            currentTab = 1;
            audioPanel.SetActive(false);
            keyboardPanel.SetActive(true);
            controllerPanel.SetActive(false);
            UpdateTabHighlight();
            SelectFirstIn(keyboardPanel);
        }

        private void ShowControllerTab()
        {
            currentTab = 2;
            audioPanel.SetActive(false);
            keyboardPanel.SetActive(false);
            controllerPanel.SetActive(true);
            UpdateTabHighlight();
            SelectFirstIn(controllerPanel);
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

        public void ResetAudioToDefault()
        {
            foreach ((Slider slider, Bus bus) in audioChannels)
            {
                bus.setVolume(1f);
                slider.SetValueWithoutNotify(1f);
            }
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