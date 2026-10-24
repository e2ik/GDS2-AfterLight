using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TitlePlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference attackAction;

    [Header("Menu Hooks")]
    [SerializeField] private bool jumpOnSelectionChange = true;
    [SerializeField] private bool attackOnButtonClick = true;
    [SerializeField] private bool attackBeforeSubmit = true;
    [SerializeField] private float submitDelayAfterAttack = 0f;
    [SerializeField] private bool overrideInputActions = true;
    [SerializeField] private Transform menuRoot;
    [SerializeField] private bool keepSelection = true;
    [SerializeField] private Selectable defaultSelection;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 4f;
    [SerializeField, Range(0f, 1f)] private float moveJumpChance = 0.25f;
    [SerializeField] private Transform movePoint;
    [SerializeField] private bool canTurn = true;

    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpHeight = 3f;
    [SerializeField, Min(0.01f)] private float riseGravity = 3f;
    [SerializeField, Min(0.01f)] private float fallGravity = 5f;

    [Header("Attack")]
    [SerializeField, Min(0.05f)] private float attackDuration = 0.35f;
    [SerializeField] private string groundAttackState = "p_attack";
    [SerializeField] private string airAttackState = "p_airattack";

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Intro Reveal")]
    [SerializeField] private bool playIntroOnStart = true;
    [SerializeField] private bool fadeFromBlack = true;
    [SerializeField, Min(0f)] private float screenFadeDuration = 1f;
    [SerializeField] private bool waitForScreenFade = true;
    [SerializeField, Min(0f)] private float introDelay = 0.5f;
    [SerializeField, Min(0.01f)] private float introDuration = 2f;
    [SerializeField] private AnimationCurve introCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool blockInputDuringIntro = true;
    [SerializeField] private int revealSortingOffset = 1;
    [SerializeField] private UnityEvent onIntroFinished;

    [Header("Cutout Match")]
    [SerializeField] private SpriteRenderer cutoutTarget;
    [SerializeField] private bool useTargetSprite = true;
    [SerializeField] private SpriteRenderer[] cutoutRenderers;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int YVelocityHash = Animator.StringToHash("yVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");

    private bool enabledMove;
    private bool enabledJump;
    private bool enabledAttack;

    private float horizontal;
    private GameObject lastSelected;
    private GameObject rememberedSelection;
    private readonly Dictionary<Button, Button.ButtonClickedEvent> originalClicks = new Dictionary<Button, Button.ButtonClickedEvent>();
    private Coroutine hookRoutine;
    private bool submitPending;
    private float speedX;
    private float startX;
    private bool grounded = true;
    private float groundY;
    private float velocityY;

    private bool attacking;
    private bool attackGrounded;
    private float attackTimer;

    private SpriteRenderer revealOverlay;
    private Material revealMaterial;
    private bool introPlaying;
    private Coroutine introRoutine;

    private static readonly int UseRectId = Shader.PropertyToID("_UseRect");
    private static readonly int CutoutRectId = Shader.PropertyToID("_CutoutRect");
    private static readonly int CutoutUVRectId = Shader.PropertyToID("_CutoutUVRect");
    private static readonly int CutoutTexId = Shader.PropertyToID("_CutoutTex");
    private MaterialPropertyBlock cutoutBlock;

    public bool IntroPlaying => introPlaying;

    private void Awake()
    {
        groundY = transform.position.y;
        startX = transform.position.x;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (cutoutRenderers == null || cutoutRenderers.Length == 0) cutoutRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        CreateRevealOverlay();

        if (playIntroOnStart)
        {
            ApplyIntro(0f);
            if (fadeFromBlack) SetScreenBlack();
        }
        UpdateCutout();
    }

    private void Start()
    {
        if (playIntroOnStart) PlayIntro();
    }

    public void PlayIntro()
    {
        if (introRoutine != null) StopCoroutine(introRoutine);
        introRoutine = StartCoroutine(IntroRoutine());
    }

    public void SkipIntro()
    {
        if (introRoutine != null) StopCoroutine(introRoutine);
        introRoutine = null;
        FinishIntro();
    }

    private IEnumerator IntroRoutine()
    {
        introPlaying = true;
        ApplyIntro(0f);

        if (fadeFromBlack && FadeCanvasController.Instance != null)
        {
            SetScreenBlack();
            FadeCanvasController.Instance.FadeIn(screenFadeDuration);
            if (waitForScreenFade && screenFadeDuration > 0f) yield return new WaitForSecondsRealtime(screenFadeDuration);
        }

        if (introDelay > 0f) yield return new WaitForSeconds(introDelay);

        float elapsed = 0f;
        while (elapsed < introDuration)
        {
            elapsed += Time.deltaTime;
            ApplyIntro(introCurve.Evaluate(Mathf.Clamp01(elapsed / introDuration)));
            yield return null;
        }

        introRoutine = null;
        FinishIntro();
    }

    private static void SetScreenBlack()
    {
        FadeCanvasController fade = FadeCanvasController.Instance;
        if (fade != null && fade.Group != null) fade.Group.alpha = 1f;
    }

    private void FinishIntro()
    {
        ApplyIntro(1f);
        introPlaying = false;
        onIntroFinished?.Invoke();
    }

    private void ApplyIntro(float t)
    {
        if (revealOverlay == null) return;

        float alpha = 1f - Mathf.Clamp01(t);
        revealOverlay.enabled = alpha > 0f;
        Color color = revealOverlay.color;
        color.a = alpha;
        revealOverlay.color = color;
    }

    private void CreateRevealOverlay()
    {
        if (cutoutTarget == null || revealOverlay != null) return;

        Material source = null;
        foreach (SpriteRenderer renderer in cutoutRenderers)
        {
            if (renderer != null && renderer.sharedMaterial != null)
            {
                source = renderer.sharedMaterial;
                break;
            }
        }
        if (source == null) return;

        revealMaterial = new Material(source);
        revealMaterial.SetFloat("_Mode", 1f);
        revealMaterial.SetFloat("_Outline", 0f);

        GameObject overlay = new GameObject("Cutout Reveal");
        overlay.SetActive(false);
        overlay.transform.SetParent(cutoutTarget.transform, false);

        revealOverlay = overlay.AddComponent<SpriteRenderer>();
        revealOverlay.sprite = cutoutTarget.sprite;
        revealOverlay.drawMode = cutoutTarget.drawMode;
        if (cutoutTarget.drawMode != SpriteDrawMode.Simple) revealOverlay.size = cutoutTarget.size;
        revealOverlay.flipX = cutoutTarget.flipX;
        revealOverlay.flipY = cutoutTarget.flipY;
        revealOverlay.sortingLayerID = cutoutTarget.sortingLayerID;
        revealOverlay.sortingOrder = cutoutTarget.sortingOrder + revealSortingOffset;
        revealOverlay.sharedMaterial = revealMaterial;
        revealOverlay.enabled = false;

        overlay.SetActive(true);
    }

    private void OnDestroy()
    {
        if (revealMaterial != null) Destroy(revealMaterial);
    }

    private void LateUpdate()
    {
        UpdateCutout();
    }

    private void UpdateCutout()
    {
        if (cutoutTarget == null || cutoutTarget.sprite == null || cutoutRenderers == null) return;
        if (!TryGetCutoutRect(out Vector4 rect, out Vector4 uvRect)) return;

        if (cutoutBlock == null) cutoutBlock = new MaterialPropertyBlock();

        foreach (SpriteRenderer renderer in cutoutRenderers) ApplyCutout(renderer, rect, uvRect);
        ApplyCutout(revealOverlay, rect, uvRect);
    }

    private void ApplyCutout(SpriteRenderer renderer, Vector4 rect, Vector4 uvRect)
    {
        if (renderer == null) return;

        renderer.GetPropertyBlock(cutoutBlock);
        cutoutBlock.SetFloat(UseRectId, 1f);
        cutoutBlock.SetVector(CutoutRectId, rect);

        if (useTargetSprite)
        {
            cutoutBlock.SetTexture(CutoutTexId, cutoutTarget.sprite.texture);
            cutoutBlock.SetVector(CutoutUVRectId, uvRect);
        }

        renderer.SetPropertyBlock(cutoutBlock);
    }

    private bool TryGetCutoutRect(out Vector4 rect, out Vector4 uvRect)
    {
        rect = Vector4.zero;
        uvRect = new Vector4(0f, 0f, 1f, 1f);

        Sprite sprite = cutoutTarget.sprite;
        if (sprite.texture == null) return false;

        Vector2[] vertices = sprite.vertices;
        Vector2[] uvs = sprite.uv;
        if (vertices == null || uvs == null || vertices.Length == 0 || vertices.Length != uvs.Length) return false;

        Vector2 vMin = vertices[0], vMax = vertices[0], uMin = uvs[0], uMax = uvs[0];
        for (int i = 1; i < vertices.Length; i++)
        {
            vMin = Vector2.Min(vMin, vertices[i]);
            vMax = Vector2.Max(vMax, vertices[i]);
            uMin = Vector2.Min(uMin, uvs[i]);
            uMax = Vector2.Max(uMax, uvs[i]);
        }

        Vector2 localMin = vMin;
        Vector2 localSize = vMax - vMin;

        if (cutoutTarget.drawMode != SpriteDrawMode.Simple)
        {
            localSize = cutoutTarget.size;
            localMin = -Vector2.Scale(sprite.pivot / sprite.rect.size, localSize);
        }

        Transform target = cutoutTarget.transform;
        Vector3 a = target.TransformPoint(localMin);
        Vector3 b = target.TransformPoint(localMin + localSize);
        rect = new Vector4(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));

        uvRect = new Vector4(uMin.x, uMin.y, uMax.x - uMin.x, uMax.y - uMin.y);

        bool flipX = cutoutTarget.flipX ^ (b.x < a.x);
        bool flipY = cutoutTarget.flipY ^ (b.y < a.y);
        if (flipX) { uvRect.x += uvRect.z; uvRect.z = -uvRect.z; }
        if (flipY) { uvRect.y += uvRect.w; uvRect.w = -uvRect.w; }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (cutoutTarget == null || cutoutTarget.sprite == null) return;
        if (!TryGetCutoutRect(out Vector4 rect, out _)) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(new Vector3(rect.x + rect.z * 0.5f, rect.y + rect.w * 0.5f, 0f), new Vector3(rect.z, rect.w, 0f));
    }

    private void OnEnable()
    {
        enabledMove = EnableIfNeeded(moveAction);
        enabledJump = EnableIfNeeded(jumpAction);
        enabledAttack = EnableIfNeeded(attackAction);
        hookRoutine = StartCoroutine(HookButtonsNextFrame());
    }

    private void OnDisable()
    {
        if (enabledMove) moveAction.action.Disable();
        if (enabledJump) jumpAction.action.Disable();
        if (enabledAttack) attackAction.action.Disable();
        enabledMove = enabledJump = enabledAttack = false;
        if (hookRoutine != null) StopCoroutine(hookRoutine);
        hookRoutine = null;
        UnhookButtons();
        submitPending = false;
    }

    private IEnumerator HookButtonsNextFrame()
    {
        yield return null;
        hookRoutine = null;
        HookButtons();
    }

    private void HookButtons()
    {
        UnhookButtons();
        if (!attackOnButtonClick) return;

        Button[] buttons = menuRoot != null
            ? menuRoot.GetComponentsInChildren<Button>(true)
            : FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button == null || originalClicks.ContainsKey(button)) continue;

            Button.ButtonClickedEvent original = button.onClick;
            originalClicks[button] = original;

            Button.ButtonClickedEvent wrapper = new Button.ButtonClickedEvent();
            wrapper.AddListener(() => OnMenuClick(original));
            button.onClick = wrapper;
        }
    }

    private void UnhookButtons()
    {
        foreach (KeyValuePair<Button, Button.ButtonClickedEvent> pair in originalClicks)
            if (pair.Key != null) pair.Key.onClick = pair.Value;

        originalClicks.Clear();
    }

    public void TriggerJump()
    {
        if (introPlaying && blockInputDuringIntro) return;
        if (grounded && !(attacking && attackGrounded)) Jump();
    }

    public void TriggerAttack()
    {
        if (introPlaying && blockInputDuringIntro) return;
        if (!attacking) StartAttack();
    }

    private void OnMenuClick(Button.ButtonClickedEvent original)
    {
        if (!attackBeforeSubmit)
        {
            TriggerAttack();
            original.Invoke();
            return;
        }

        if (submitPending) return;
        if (introPlaying && blockInputDuringIntro)
        {
            original.Invoke();
            return;
        }

        StartCoroutine(AttackThenSubmit(original));
    }

    private IEnumerator AttackThenSubmit(Button.ButtonClickedEvent original)
    {
        submitPending = true;

        while (attacking) yield return null;
        StartAttack();

        float wait = Mathf.Max(0f, attackDuration + submitDelayAfterAttack);
        if (wait > 0f) yield return new WaitForSeconds(wait);

        submitPending = false;
        original.Invoke();
    }

    private void KeepSelection()
    {
        if (!keepSelection) return;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (IsValidSelection(selected))
        {
            rememberedSelection = selected;
            return;
        }

        GameObject target = IsValidSelection(rememberedSelection) ? rememberedSelection : FindDefaultSelection();
        if (target == null) return;

        eventSystem.SetSelectedGameObject(target);
        rememberedSelection = target;
        lastSelected = target;
    }

    private bool IsValidSelection(GameObject target)
    {
        if (target == null || !target.activeInHierarchy) return false;
        if (menuRoot != null && !target.transform.IsChildOf(menuRoot)) return false;
        return target.TryGetComponent(out Selectable selectable) && selectable.IsInteractable();
    }

    private GameObject FindDefaultSelection()
    {
        if (defaultSelection != null && IsValidSelection(defaultSelection.gameObject)) return defaultSelection.gameObject;

        Selectable[] candidates = menuRoot != null ? menuRoot.GetComponentsInChildren<Selectable>() : Selectable.allSelectablesArray;
        foreach (Selectable candidate in candidates)
            if (candidate != null && IsValidSelection(candidate.gameObject)) return candidate.gameObject;

        return null;
    }

    private void CheckSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        if (selected == lastSelected) return;

        GameObject previous = lastSelected;
        lastSelected = selected;

        if (!jumpOnSelectionChange || previous == null || selected == null) return;
        if (menuRoot != null && !selected.transform.IsChildOf(menuRoot)) return;

        TriggerJump();
    }

    private static bool EnableIfNeeded(InputActionReference reference)
    {
        if (reference == null || reference.action == null || reference.action.enabled) return false;
        reference.action.Enable();
        return true;
    }

    private static bool Pressed(InputActionReference reference) =>
        reference != null && reference.action != null && reference.action.WasPressedThisFrame();

    private void Update()
    {
        float raw = moveAction != null && moveAction.action != null ? moveAction.action.ReadValue<Vector2>().x : 0f;
        float previousHorizontal = horizontal;
        horizontal = Mathf.Abs(raw) > 0.2f ? Mathf.Sign(raw) : 0f;
        bool movePressed = horizontal != 0f && horizontal != previousHorizontal;

        KeepSelection();

        if (introPlaying && blockInputDuringIntro)
        {
            horizontal = 0f;
            lastSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            UpdateAnimator();
            return;
        }

        CheckSelection();

        if (movePressed && moveJumpChance > 0f && Random.value < moveJumpChance) TriggerJump();

        if (!overrideInputActions)
        {
            if (Pressed(jumpAction)) TriggerJump();
            if (Pressed(attackAction)) TriggerAttack();
        }

        UpdateMovement();
        UpdateJump();
        UpdateAttack();
        UpdateAnimator();
    }

    private void UpdateMovement()
    {
        speedX = 0f;
        if (horizontal == 0f) return;

        if (canTurn && !attacking)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * horizontal;
            transform.localScale = scale;
        }

        if (movePoint == null || (attacking && attackGrounded)) return;

        float minX = Mathf.Min(startX, movePoint.position.x);
        float maxX = Mathf.Max(startX, movePoint.position.x);

        Vector3 position = transform.position;
        float newX = Mathf.Clamp(position.x + horizontal * moveSpeed * Time.deltaTime, minX, maxX);
        if (Time.deltaTime > 0f) speedX = (newX - position.x) / Time.deltaTime;
        position.x = newX;
        transform.position = position;
    }

    private void Jump()
    {
        velocityY = Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y) * riseGravity * jumpHeight);
        grounded = false;
    }

    private void UpdateJump()
    {
        if (grounded) return;

        float scale = velocityY > 0f ? riseGravity : fallGravity;
        velocityY -= Mathf.Abs(Physics2D.gravity.y) * scale * Time.deltaTime;

        Vector3 position = transform.position;
        position.y += velocityY * Time.deltaTime;

        if (position.y <= groundY)
        {
            position.y = groundY;
            velocityY = 0f;
            grounded = true;
        }

        transform.position = position;
    }

    private void StartAttack()
    {
        attacking = true;
        attackGrounded = grounded;
        attackTimer = attackDuration;

        if (animator == null) return;

        int hash = Animator.StringToHash(attackGrounded ? groundAttackState : airAttackState);
        if (animator.HasState(0, hash)) animator.Play(hash, 0, 0f);
    }

    private void UpdateAttack()
    {
        if (!attacking) return;

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f) attacking = false;
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        animator.SetFloat(SpeedHash, Mathf.Abs(speedX));
        animator.SetFloat(YVelocityHash, velocityY);
        animator.SetBool(IsGroundedHash, attacking ? attackGrounded : grounded);
        animator.SetBool(IsAttackingHash, attacking);
        animator.SetInteger(AttackIndexHash, attacking ? 1 : 0);
    }

    public void EndAttack() { }
    public void OpenComboWindow() { }
    public void CloseComboWindow() { }
    public void OnAttack() { }
    public void OnParry() { }
    public void EndSkill() { }
    public void EndSkillAnimation() { }
    public void TriggerSpinSkillEffect() { }
    public void TriggerDashEffect() { }
    public void TriggerLandingEffect() { }
    public void TriggerPlungeLandEffect() { }
    public void StopWallSlideEffect() { }
    public void PlayHealingEffect() { }
    public void FlashOnHeal() { }
    public void FlashGreenOnParrySuccess() { }
    public void FinishHealing() { }
    public void OnDeathAnimationFinished() { }
    public void PlaySFXEvent(string id) { }
    public void PlaySFXEventAttached(string id) { }
}