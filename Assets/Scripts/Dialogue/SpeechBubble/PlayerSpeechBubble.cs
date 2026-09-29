using System.Collections;
using TMPro;
using UnityEngine;

public class PlayerSpeechBubble : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text bubbleText;

    [Header("Settings")]
    [SerializeField] private float textSpeed = 0.03f;
    [SerializeField] private float visibleDuration = 2f;
    [SerializeField] private float timeBetweenLines = 0.2f;

    private Coroutine speechCoroutine;
    private object currentSource;
    private string[] currentLines;
    private Vector3 originalScale;

    private void Awake()
    {
        originalScale = transform.localScale;

        if (bubbleText != null) bubbleText.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        float parentDirection = Mathf.Sign(transform.parent.lossyScale.x);
        transform.localScale = new Vector3(originalScale.x * parentDirection, originalScale.y, originalScale.z);
    }

    public void Show(PlayerSpeechData speech, object source)
    {
        if (speech == null) return;
        Show(speech.Lines, source);
    }

    public void Show(string text, object source)
    {
        Show(new string[] { text }, source);
    }

    public void Show(string[] lines, object source)
    {
        if (lines == null || lines.Length == 0) return;

        // not restart speech from the same object while it is still running
        if (speechCoroutine != null && currentSource == source) return;

        // speech from a different object interrupts the current speech
        if (speechCoroutine != null) StopCoroutine(speechCoroutine);

        currentSource = source;
        currentLines = lines;
        speechCoroutine = StartCoroutine(ShowSpeechRoutine());
    }

    private IEnumerator ShowSpeechRoutine()
    {
        bubbleText.gameObject.SetActive(true);

        for (int i = 0; i < currentLines.Length; i++)
        {
            bubbleText.text = "";

            foreach (char character in currentLines[i])
            {
                bubbleText.text += character;
                yield return new WaitForSeconds(textSpeed);
            }

            yield return new WaitForSeconds(visibleDuration);
            bubbleText.gameObject.SetActive(false);

            if (i < currentLines.Length - 1)
            {
                yield return new WaitForSeconds(timeBetweenLines);
                bubbleText.gameObject.SetActive(true);
            }
        }

        bubbleText.text = "";
        bubbleText.gameObject.SetActive(false);

        currentSource = null;
        currentLines = null;
        speechCoroutine = null;
    }
}