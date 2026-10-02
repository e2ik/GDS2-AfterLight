using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputPromptTextSlots : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private InputActionPrompt promptPrefab;
    [SerializeField] private string slotText = "MM";
    [SerializeField] private string partSeparator = " ";
    [SerializeField] private float promptScale = 1.3f;
    [SerializeField] private Vector2 promptOffset = Vector2.zero;

    private static readonly Regex TokenPattern = new Regex(@"\{([A-Za-z0-9_ ]+(?:/[A-Za-z0-9_ ]+)?)(?::([A-Za-z0-9_]+))?\}");

    private string rawText;
    private readonly List<InputActionPrompt> activePrompts = new List<InputActionPrompt>();
    private readonly Stack<InputActionPrompt> pool = new Stack<InputActionPrompt>();

    private void Awake()
    {
        if (text == null) text = GetComponent<TMP_Text>();
    }

    public bool HasTarget => text != null || GetComponent<TMP_Text>() != null;

    public void SetTargetIfMissing(TMP_Text target)
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (text == null) text = target;
    }

    public void SetText(string raw)
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (text == null)
        {
            Debug.LogWarning("[InputPromptTextSlots] No TMP text assigned — drag the text into the Text slot.", this);
            return;
        }

        rawText = raw;
        text.text = BuildSlotText(raw);
        RebuildPrompts();
    }

    public void RebuildPrompts()
    {
        ReleaseAll();
        if (text == null || string.IsNullOrEmpty(rawText) || promptPrefab == null) return;

        InputActionAsset actions = ResolveActions();
        if (actions == null) return;

        text.ForceMeshUpdate();
        TMP_TextInfo info = text.textInfo;

        for (int i = 0; i < info.linkCount; i++)
        {
            TMP_LinkInfo link = info.linkInfo[i];
            string id = link.GetLinkID();
            if (!id.StartsWith("input:")) continue;

            string token = id.Substring(6);
            string actionName = token;
            string part = null;

            int colon = token.IndexOf(':');
            if (colon >= 0)
            {
                actionName = token.Substring(0, colon);
                part = token.Substring(colon + 1);
            }

            InputAction action = actions.FindAction(actionName);
            if (action == null || link.linkTextLength == 0) continue;

            TMP_CharacterInfo first = info.characterInfo[link.linkTextfirstCharacterIndex];
            TMP_CharacterInfo last = info.characterInfo[link.linkTextfirstCharacterIndex + link.linkTextLength - 1];

            Vector2 min = new Vector2(first.bottomLeft.x, first.descender);
            Vector2 max = new Vector2(last.topRight.x, first.ascender);
            Vector2 center = (min + max) * 0.5f + promptOffset;
            float height = (max.y - min.y) * promptScale;

            InputActionPrompt prompt = GetPrompt();
            RectTransform rect = (RectTransform)prompt.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localPosition = center;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(Mathf.Max(height, max.x - min.x), height);

            prompt.SetAction(action, part);
            activePrompts.Add(prompt);
        }
    }

    private string BuildSlotText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        InputActionAsset actions = ResolveActions();
        bool wantGamepad = Application.isPlaying && InputManager.CurrentDevice != InputDeviceType.KeyboardMouse;

        StringBuilder sb = new StringBuilder(raw.Length);
        int lastEnd = 0;
        string lastCollapsedAction = null;

        foreach (Match match in TokenPattern.Matches(raw))
        {
            string between = raw.Substring(lastEnd, match.Index - lastEnd);
            lastEnd = match.Index + match.Length;

            string actionName = match.Groups[1].Value.Trim();
            string part = match.Groups[2].Success ? match.Groups[2].Value : null;
            InputAction action = actions != null ? actions.FindAction(actionName) : null;

            if (action == null)
            {
                sb.Append(between);
                sb.Append(SlotLink(part != null ? actionName + ":" + part : actionName));
                lastCollapsedAction = null;
                continue;
            }

            if (part != null)
            {
                bool partFound = InputBindingUtility.TryFindBinding(action, wantGamepad, part, out int partIndex, out _, out _)
                    && action.bindings[partIndex].isPartOfComposite;

                if (!partFound)
                {
                    if (lastCollapsedAction == actionName && string.IsNullOrWhiteSpace(between)) continue;

                    sb.Append(between);
                    sb.Append(SlotLink(actionName));
                    lastCollapsedAction = actionName;
                    continue;
                }

                sb.Append(between);
                sb.Append(SlotLink(actionName + ":" + part));
                lastCollapsedAction = null;
                continue;
            }

            sb.Append(between);
            sb.Append(BuildWholeAction(action, actionName, wantGamepad));
            lastCollapsedAction = null;
        }

        sb.Append(raw.Substring(lastEnd));
        return sb.ToString();
    }

    private string BuildWholeAction(InputAction action, string actionName, bool wantGamepad)
    {
        if (!InputBindingUtility.TryFindBinding(action, wantGamepad, null, out int index, out _, out _))
            return SlotLink(actionName);

        var bindings = action.bindings;
        if (!bindings[index].isComposite) return SlotLink(actionName);

        List<string> parts = new List<string>();
        for (int i = index + 1; i < bindings.Count && bindings[i].isPartOfComposite; i++)
        {
            string partName = bindings[i].name;
            if (!string.IsNullOrEmpty(partName) && !parts.Contains(partName)) parts.Add(partName);
        }

        if (parts.Count == 0) return SlotLink(actionName);

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0) sb.Append(partSeparator);
            sb.Append(SlotLink(actionName + ":" + parts[i]));
        }
        return sb.ToString();
    }

    private string SlotLink(string token)
    {
        return $"<link=\"input:{token}\"><color=#00000000>{slotText}</color></link>";
    }

    private InputActionAsset ResolveActions()
    {
        InputIconDatabase icons = InputManager.Icons;
        if (icons != null && icons.Actions != null) return icons.Actions;

        PlayerInput playerInput = FindFirstObjectByType<PlayerInput>();
        return playerInput != null ? playerInput.actions : null;
    }

    private InputActionPrompt GetPrompt()
    {
        InputActionPrompt prompt = pool.Count > 0 ? pool.Pop() : Instantiate(promptPrefab, text.transform);
        prompt.gameObject.SetActive(true);
        prompt.transform.SetAsLastSibling();
        return prompt;
    }

    private void ReleaseAll()
    {
        foreach (InputActionPrompt prompt in activePrompts)
        {
            if (prompt == null) continue;
            prompt.gameObject.SetActive(false);
            pool.Push(prompt);
        }
        activePrompts.Clear();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && !string.IsNullOrEmpty(rawText)) RebuildPrompts();
    }
}