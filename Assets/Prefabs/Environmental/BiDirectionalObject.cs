using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class BiDirectionalObject : MonoBehaviour
{
    private enum Side { None, Left, Right }

    public enum AccessMode
    {
        Normal,
        RequireKey
    }

    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool invertSides = false;

    [Header("Access")]
    [SerializeField] private AccessMode accessMode = AccessMode.Normal;
    [SerializeField] private KeyDefinition requiredKey;
    [SerializeField] private string missingKeyMessage = "I need the [{0}]...";
    [SerializeField] private Color keyNameColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField, Min(0f)] private float missingKeyMessageDuration = 2f;
    [SerializeField] private DialogueEffect missingKeyMessageEffect = DialogueEffect.Default;
    [SerializeField, Min(0f)] private float missingKeyMessageCooldown = 1f;

    [Header("Boss Lock")]
    [SerializeField] private bool lockDuringBossFights = true;
    [SerializeField] private string bossLockedMessage = "";
    [SerializeField] private FMODUnity.EventReference bossLockEvent;
    [SerializeField] private FMODUnity.EventReference bossUnlockEvent;

    [Header("Animator State Names")]
    [SerializeField] private string openLeftState = "OpenLeft";
    [SerializeField] private string openRightState = "OpenRight";
    [SerializeField] private string closeLeftState = "CloseLeft";
    [SerializeField] private string closeRightState = "CloseRight";

    [Header("Audio")]
    [SerializeField] private FMODUnity.EventReference openEvent;
    [SerializeField] private FMODUnity.EventReference closeEvent;
    [SerializeField] private FMODUnity.EventReference unlockEvent;
    [SerializeField] private FMODUnity.EventReference lockedEvent;

    private BoxCollider2D box;
    private Side openedFromSide = Side.None;
    private readonly HashSet<Collider2D> occupants = new HashSet<Collider2D>();
    private bool isLocked;
    private bool bossLocked;
    private bool solidPending;
    private float nextMissingKeyMessageTime = float.NegativeInfinity;

    public bool IsLocked => isLocked || bossLocked;
    public bool IsBossLocked => bossLocked;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        SetLocked(accessMode == AccessMode.RequireKey);
    }

    private void OnEnable()
    {
        Enemies.BossTrigger.OnBossEngagementChanged += HandleBossEngagementChanged;
        if (lockDuringBossFights && box != null) SetBossLocked(Enemies.BossTrigger.AnyBossEngaged);
    }

    private void HandleBossEngagementChanged(bool engaged)
    {
        if (lockDuringBossFights) SetBossLocked(engaged);
    }

    private void OnDisable()
    {
        Enemies.BossTrigger.OnBossEngagementChanged -= HandleBossEngagementChanged;
        occupants.Clear();
        openedFromSide = Side.None;
    }

    private void SetLocked(bool locked)
    {
        isLocked = locked;
        ApplyColliderState();
    }

    public void SetBossLocked(bool locked)
    {
        if (bossLocked == locked) return;

        bossLocked = locked;
        ApplyColliderState();
        AudioManager.PlaySFX(locked ? bossLockEvent : bossUnlockEvent, transform.position);
    }

    private void ApplyColliderState()
    {
        bool solid = isLocked || bossLocked;

        if (solid && occupants.Count > 0)
        {
            solidPending = true;
            box.isTrigger = true;
            return;
        }

        solidPending = false;
        box.isTrigger = !solid;
    }

    public void Unlock()
    {
        if (!isLocked) return;

        SetLocked(false);
        AudioManager.PlaySFX(unlockEvent, transform.position);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isLocked && !bossLocked) return;

        Collider2D other = collision.collider;
        if (!IsOnPlayerLayer(other)) return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;

        if (bossLocked)
        {
            ShowBossLockedMessage(player);
            return;
        }

        if (PlayerHasRequiredKey(player))
        {
            Unlock();
            return;
        }

        ShowMissingKeyMessage(player);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isLocked || bossLocked) return;
        if (!IsOnPlayerLayer(other)) return;

        occupants.Add(other);

        if (openedFromSide != Side.None) return;

        openedFromSide = GetSide(other.transform.position);
        Play(openedFromSide == Side.Left ? openLeftState : openRightState);
        AudioManager.PlaySFX(openEvent, transform.position);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!occupants.Remove(other)) return;

        if (occupants.Count > 0) return;
        if (solidPending) ApplyColliderState();
        if (openedFromSide == Side.None) return;

        if (!gameObject.activeInHierarchy)
        {
            openedFromSide = Side.None;
            return;
        }

        Play(openedFromSide == Side.Left ? closeLeftState : closeRightState);
        AudioManager.PlaySFX(closeEvent, transform.position);
        openedFromSide = Side.None;
    }

    private bool PlayerHasRequiredKey(Player player)
    {
        if (requiredKey == null)
        {
            Debug.LogWarning($"{name}: Access Mode is Require Key but no Required Key is assigned, unlocking.", this);
            return true;
        }

        PlayerInventorySO inv = player != null && player.Inventory != null ? player.Inventory.currentInventory : null;
        if (inv == null) return false;

        return inv.KeyInstances != null && inv.KeyInstances.Exists(k => k.InstItemID == requiredKey.ItemID);
    }

    private void ShowMissingKeyMessage(Player player)
    {
        if (Time.time < nextMissingKeyMessageTime) return;
        nextMissingKeyMessageTime = Time.time + missingKeyMessageDuration + missingKeyMessageCooldown;

        AudioManager.PlaySFX(lockedEvent, transform.position);

        string message = missingKeyMessage;

        if (requiredKey != null)
        {
            string coloredName = $"<color=#{ColorUtility.ToHtmlStringRGB(keyNameColor)}>{requiredKey.UIName}</color>";

            message = string.Format(missingKeyMessage, coloredName);
        }

        Tutorial.TutorialSpeechBubblePool.Instance?.Show(message, player.transform, missingKeyMessageDuration, missingKeyMessageEffect);
    }

    private void ShowBossLockedMessage(Player player)
    {
        if (Time.time < nextMissingKeyMessageTime) return;
        nextMissingKeyMessageTime = Time.time + missingKeyMessageDuration + missingKeyMessageCooldown;

        AudioManager.PlaySFX(lockedEvent, transform.position);

        if (!string.IsNullOrEmpty(bossLockedMessage))
            Tutorial.TutorialSpeechBubblePool.Instance?.Show(bossLockedMessage, player.transform, missingKeyMessageDuration, missingKeyMessageEffect);
    }

    private bool IsOnPlayerLayer(Collider2D other)
    {
        return ((1 << other.gameObject.layer) & playerLayer.value) != 0;
    }

    private Side GetSide(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        float x = local.x - box.offset.x;

        bool isLeft = x < 0f;
        if (invertSides) isLeft = !isLeft;

        return isLeft ? Side.Left : Side.Right;
    }

    private void Play(string stateName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"{name}: No Animator assigned to BiDirectionalObject.", this);
            return;
        }

        if (!animator.isActiveAndEnabled) return;

        animator.Play(stateName);
    }
}