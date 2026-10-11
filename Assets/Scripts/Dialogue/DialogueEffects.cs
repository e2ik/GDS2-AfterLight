using UnityEngine;
using System.Collections;
using TMPro;

public class DialogueEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Angry")]
    [SerializeField] private bool keepShakingBox = true; //. shake nonstop

    [SerializeField] private float angryShakeDuration = 0.35f;
    [SerializeField] private float angryBoxShakeStrength = 8f;
    [SerializeField] private float angryTextShakeStrength = 1.5f;
    [SerializeField] private float angryFontSizeMultiplier = 1.15f;
    

    [Header("Unstable")]
    [SerializeField] private float unstableStrength = 2f;
    [SerializeField] private float unstableSpeed = 8f;

    private Coroutine boxShakeCoroutine;
    private Coroutine textEffectCoroutine;

    private Vector2 originalPanelPosition;
    private RectTransform[] shakeTargets = new RectTransform[0];
    private Vector2[] shakeOrigins = new Vector2[0];
    private float originalFontSize;

    private DialogueEffect currentEffect = DialogueEffect.Default;

    private void Awake()
    {
        if (dialoguePanel != null)
            originalPanelPosition = dialoguePanel.anchoredPosition;

        if (dialogueText != null)
            originalFontSize = dialogueText.fontSize;
    }

    public void SetTargets(TMP_Text text, RectTransform textbox, RectTransform portrait)
    {
        StopEffects();

        if (text != null && text != dialogueText)
        {
            dialogueText = text;
            originalFontSize = text.fontSize;
        }

        int count = (textbox != null ? 1 : 0) + (portrait != null ? 1 : 0);
        shakeTargets = new RectTransform[count];
        shakeOrigins = new Vector2[count];

        int index = 0;
        if (textbox != null) shakeTargets[index++] = textbox;
        if (portrait != null) shakeTargets[index] = portrait;

        for (int i = 0; i < count; i++) shakeOrigins[i] = shakeTargets[i].anchoredPosition;
    }

    public void PlayEffect(DialogueEffect effect)
    {
        StopEffects();

        currentEffect = effect;

        switch (effect)
        {
            case DialogueEffect.Angry:
                dialogueText.fontSize = originalFontSize * angryFontSizeMultiplier;
                boxShakeCoroutine = StartCoroutine(ShakeBox());
                textEffectCoroutine = StartCoroutine(ShakeCharacters());
                break;

            case DialogueEffect.Unstable:
                textEffectCoroutine = StartCoroutine(WiggleCharacters());
                break;
        }
    }

    public void StopEffects()
    {
        if (boxShakeCoroutine != null)
        {
            StopCoroutine(boxShakeCoroutine);
            boxShakeCoroutine = null;
            ApplyShakeOffset(Vector2.zero);
        }

        if (textEffectCoroutine != null)
        {
            StopCoroutine(textEffectCoroutine);
            textEffectCoroutine = null;
        }

        currentEffect = DialogueEffect.Default;

        if (dialogueText != null)
        {
            dialogueText.fontSize = originalFontSize;
            dialogueText.ForceMeshUpdate();
        }
    }

    private IEnumerator ShakeBox()
    {
        float elapsed = 0f;

        while (currentEffect == DialogueEffect.Angry && (keepShakingBox || elapsed < angryShakeDuration))
        {
            elapsed += Time.unscaledDeltaTime;

            ApplyShakeOffset(Random.insideUnitCircle * angryBoxShakeStrength);

            yield return null;
        }

        ApplyShakeOffset(Vector2.zero);
        boxShakeCoroutine = null;
    }

    private void ApplyShakeOffset(Vector2 offset)
    {
        if (shakeTargets.Length > 0)
        {
            for (int i = 0; i < shakeTargets.Length; i++)
            {
                if (shakeTargets[i] != null) shakeTargets[i].anchoredPosition = shakeOrigins[i] + offset;
            }

            return;
        }

        if (dialoguePanel != null) dialoguePanel.anchoredPosition = originalPanelPosition + offset;
    }

    private IEnumerator ShakeCharacters()
    {
        while (currentEffect == DialogueEffect.Angry)
        {
            dialogueText.ForceMeshUpdate();

            TMP_TextInfo textInfo = dialogueText.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                Vector3 offset = new Vector3(Random.Range(-angryTextShakeStrength, angryTextShakeStrength),Random.Range(-angryTextShakeStrength, angryTextShakeStrength),0f);

                for (int j = 0; j < 4; j++) vertices[vertexIndex + j] += offset;
            }

            UpdateTextMesh(textInfo);

            yield return null;
        }
    }

    private IEnumerator WiggleCharacters()
    {
        while (currentEffect == DialogueEffect.Unstable)
        {
            dialogueText.ForceMeshUpdate();

            TMP_TextInfo textInfo = dialogueText.textInfo;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

                if (!charInfo.isVisible) continue;

                int materialIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;

                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

                float offsetX = Random.Range(-unstableStrength * 0.5f, unstableStrength * 0.5f);
                float offsetY = Random.Range(-unstableStrength, unstableStrength);

                Vector3 offset = new Vector3(offsetX, offsetY, 0f);

                for (int j = 0; j < 4; j++) vertices[vertexIndex + j] += offset;
            }

            UpdateTextMesh(textInfo);
            yield return new WaitForSecondsRealtime(1f / unstableSpeed);
        }
    }

    private void UpdateTextMesh(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            dialogueText.UpdateGeometry(meshInfo.mesh, i);
        }
    }
}