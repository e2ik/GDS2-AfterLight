using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveButtonState : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private string saveFileName = "save.json";

    [Header("UI References")]
    [SerializeField] private Button primaryButton;
    [SerializeField] private Button newGameButton;

    private TMP_Text primaryButtonText;
    private TMP_Text newGameButtonText;

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private void Start()
    {
        if (primaryButton != null)
            primaryButtonText = primaryButton.GetComponentInChildren<TMP_Text>();

        if (newGameButton != null)
            newGameButtonText = newGameButton.GetComponentInChildren<TMP_Text>();

        SetupButtons();
    }

    private void SetupButtons()
    {
        bool saveExists = File.Exists(SaveFilePath);

        primaryButton.onClick.RemoveAllListeners();
        newGameButton.onClick.RemoveAllListeners();

        if (primaryButton != null)
        {
            primaryButton.gameObject.SetActive(true);

            if (saveExists)
            {
                if (primaryButtonText != null) primaryButtonText.text = "Continue";
                primaryButton.onClick.AddListener(OnContinuePressed);
            }
            else
            {
                if (primaryButtonText != null) primaryButtonText.text = "Play Game";
                primaryButton.onClick.AddListener(OnNewGamePressed);
            }
        }

        if (newGameButton != null)
        {
            newGameButton.gameObject.SetActive(saveExists);

            if (saveExists)
            {
                if (newGameButtonText != null) newGameButtonText.text = "New Game";
                newGameButton.onClick.AddListener(OnNewGamePressed);
            }
        }
    }

    private void OnContinuePressed()
    {
        GameManager.Instance.LoadGame();
    }

    private void OnNewGamePressed()
    {
        GameManager.Instance.NewGame();
    }
}