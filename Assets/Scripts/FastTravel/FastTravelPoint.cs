using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FastTravelPoint : MonoBehaviour, IInteractable
{
    [System.Serializable]
    private class LightStateSettings
    {
        [ColorUsage(false, true)] public Color color = Color.white;
        public float intensity = 1f;
        public bool pulse = true;
        public float pulseAmount = 0.3f;
        public float pulseSpeed = 1f;
    }

    private enum LightState { Deactivated, Idle, Activated }

    [Header("Fast Travel Config")]
    [SerializeField] private FastTravelNodeSO nodeData;
    [SerializeField] private WorldMapStateSO worldMapState;

    [Header("Interaction Settings")]
    [SerializeField] private bool canBeInteractedWith = true;
    [SerializeField] private float mapOpenDelay = 0.5f;

    [Header("Rest Fade")]
    [SerializeField, Min(0f)] private float restFadeOutDuration = 0.2f;
    [SerializeField, Min(0f)] private float restBlackHoldDuration = 0.1f;
    [SerializeField, Min(0f)] private float restFadeInDuration = 0.25f;

    [Header("VFX")]
    [SerializeField] private string fastTravelVFXKey = "FastTravel";
    [SerializeField] private Transform vfxSpawnPoint;

    [Header("Light")]
    [SerializeField] private Light2D[] lights;
    [SerializeField] private LightStateSettings deactivatedLight = new LightStateSettings { color = Color.gray, intensity = 0.2f, pulse = false };
    [SerializeField] private LightStateSettings idleLight = new LightStateSettings { color = Color.white, intensity = 0.6f, pulseAmount = 0.15f, pulseSpeed = 0.5f };
    [SerializeField] private LightStateSettings activatedLight = new LightStateSettings { color = Color.cyan, intensity = 1.2f, pulseAmount = 0.3f, pulseSpeed = 1f };
    [SerializeField] private float lightBlendSpeed = 4f;

    [SerializeField] private SpriteOutlineToggle outlineToggle;
    [SerializeField] private Collider2D promptCollider;

    private static readonly int IsDiscoveredHash = Animator.StringToHash("isDiscovered");
    private static readonly int IsIdleHash = Animator.StringToHash("isIdle");
    private static readonly int IsInteractedHash = Animator.StringToHash("isInteracted");
    private static readonly int IsInteractableHash = Animator.StringToHash("isInteractable");

    private Animator anim;
    private bool isInteracting = false;
    private Player interactingPlayer;

    private LightState lightState = LightState.Idle;
    private Color currentLightColor;
    private float currentLightIntensity;
    private float currentPulseAmount;
    private float currentPulseSpeed;
    private float pulsePhase;
    private bool lightInitialized;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        if (outlineToggle == null) outlineToggle = GetComponent<SpriteOutlineToggle>();
        if (promptCollider == null) promptCollider = GetComponent<Collider2D>();
        if (lights == null || lights.Length == 0) lights = GetComponentsInChildren<Light2D>(true);
    }

    private void OnEnable()
    {
        if (worldMapState != null)
        {
            worldMapState.OnNodeUnlocked += OnNodeUnlocked;
            worldMapState.OnStateLoaded += UpdateVisualState;
            worldMapState.OnStateReset += UpdateVisualState;
        }
        UpdateVisualState();
    }

    private void OnDisable()
    {
        if (worldMapState != null)
        {
            worldMapState.OnNodeUnlocked -= OnNodeUnlocked;
            worldMapState.OnStateLoaded -= UpdateVisualState;
            worldMapState.OnStateReset -= UpdateVisualState;
        }
    }

    private void Start()
    {
        UpdateVisualState();
        SnapLight();
    }

    private void Update()
    {
        UpdateLight();
    }

    private void OnNodeUnlocked(FastTravelNodeSO unlockedNode)
    {
        if (unlockedNode == nodeData && !isInteracting)
        {
            UpdateVisualState();
        }
    }

    public bool CanInteract
    {
        get
        {
            if (MapUIManager.Instance != null && MapUIManager.Instance.IsMapOpen)
                return false;

            return canBeInteractedWith && !isInteracting;
        }
    }

    public string InteractionPrompt => (worldMapState != null && worldMapState.IsUnlocked(nodeData))
        ? $"Travel from {nodeData.displayName}"
        : $"Unlock {nodeData.displayName}";

    public bool ShouldStopPlayerMovement => true;
    public SpriteOutlineToggle OutlineToggle => outlineToggle;
    public Collider2D PromptCollider => promptCollider;

    public void Interact(Player player)
    {
        if (!CanInteract) return;

        interactingPlayer = player;
        StartCoroutine(InteractionRoutine());
    }

    private IEnumerator InteractionRoutine()
    {
        isInteracting = true;

        StopPlayer(interactingPlayer);

        FastTravelManager.Instance?.SetLastInteractedNode(nodeData);

        RestorePlayer(interactingPlayer);

        if (anim != null)
        {
            anim.SetTrigger(IsInteractedHash);
        }

        bool isAlreadyUnlocked = worldMapState != null && worldMapState.IsUnlocked(nodeData);

        if (!isAlreadyUnlocked && worldMapState != null)
        {
            Vector3 vfxPosition = vfxSpawnPoint != null ? vfxSpawnPoint.position : transform.position;
            PSpawner.Spawn(fastTravelVFXKey, vfxPosition);

            worldMapState.UnlockNode(nodeData);
        }

        if (SaveManager.Instance != null)
        {
            string currentSceneName = gameObject.scene.name;
            SaveManager.Instance.SaveProgressAtLocation(currentSceneName, nodeData.spawnAnchorID, nodeData.destinationAreaSide);
        }

        if (!isAlreadyUnlocked && mapOpenDelay > 0f)
        {
            yield return new WaitForSeconds(mapOpenDelay);
        }

        UpdateVisualState();

        Player restingPlayer = interactingPlayer;

        if (MapUIManager.Instance != null)
        {
            MapUIManager.Instance.OpenMap(nodeData);

            while (MapUIManager.Instance != null && MapUIManager.Instance.IsMapOpen)
                yield return null;

            yield return null;
            yield return null;

            bool travelling = FastTravelManager.Instance != null && FastTravelManager.Instance.IsTravelling;
            if (!travelling)
                yield return RestFadeRoutine(restingPlayer);
        }
        else
        {
            yield return RestFadeRoutine(restingPlayer);
        }

        interactingPlayer = null;
        isInteracting = false;
    }

    private IEnumerator RestFadeRoutine(Player player)
    {
        PlayerController controller = player != null ? player.Controller : null;
        StopPlayer(player);

        FadeCanvasController fader = FadeCanvasController.Instance;
        CameraFollow2D cam = FindFirstObjectByType<CameraFollow2D>();
        bool camWasEnabled = cam != null && cam.enabled;
        if (cam != null) cam.enabled = false;

        if (fader != null)
            yield return fader.FadeOut(restFadeOutDuration);

        if (cam != null)
        {
            cam.enabled = camWasEnabled;
            cam.SettleToRest();
        }

        PrefabSpawner.ResetAllDefeated();

        if (restBlackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(restBlackHoldDuration);

        if (fader != null)
            yield return fader.FadeIn(restFadeInDuration);

        if (controller != null) controller.SetPhysicsSuspended(false);
    }

    private static void StopPlayer(Player player)
    {
        if (player == null) return;

        if (player.TryGetComponent(out Rigidbody2D body))
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        if (player.Controller != null) player.Controller.SetPhysicsSuspended(true);
    }

    private void RestorePlayer(Player player)
    {
        if (player == null) return;

        if (player.Stats != null) player.Stats.ReviveFull();
        if (player.Heals != null) player.Heals.ResetHeals();
    }

    public void UpdateVisualState()
    {
        bool isUnlocked = worldMapState != null && nodeData != null && worldMapState.IsUnlocked(nodeData);

        if (!canBeInteractedWith) lightState = LightState.Deactivated;
        else lightState = isUnlocked ? LightState.Activated : LightState.Idle;

        if (anim == null) return;

        anim.SetBool(IsInteractableHash, canBeInteractedWith);

        if (worldMapState == null || nodeData == null) return;

        anim.SetBool(IsDiscoveredHash, isUnlocked);
        anim.SetBool(IsIdleHash, !isUnlocked);
    }

    private LightStateSettings GetLightSettings()
    {
        switch (lightState)
        {
            case LightState.Deactivated: return deactivatedLight;
            case LightState.Activated: return activatedLight;
            default: return idleLight;
        }
    }

    private void SnapLight()
    {
        LightStateSettings settings = GetLightSettings();
        currentLightColor = settings.color;
        currentLightIntensity = settings.intensity;
        currentPulseAmount = settings.pulse ? settings.pulseAmount : 0f;
        currentPulseSpeed = settings.pulseSpeed;
        lightInitialized = true;
        ApplyLight();
    }

    private void UpdateLight()
    {
        if (lights == null || lights.Length == 0) return;
        if (!lightInitialized) SnapLight();

        LightStateSettings settings = GetLightSettings();
        float t = 1f - Mathf.Exp(-lightBlendSpeed * Time.deltaTime);

        currentLightColor = Color.Lerp(currentLightColor, settings.color, t);
        currentLightIntensity = Mathf.Lerp(currentLightIntensity, settings.intensity, t);
        currentPulseAmount = Mathf.Lerp(currentPulseAmount, settings.pulse ? settings.pulseAmount : 0f, t);
        currentPulseSpeed = Mathf.Lerp(currentPulseSpeed, settings.pulseSpeed, t);

        pulsePhase += currentPulseSpeed * Mathf.PI * 2f * Time.deltaTime;
        if (pulsePhase > Mathf.PI * 2f) pulsePhase -= Mathf.PI * 2f;

        ApplyLight();
    }

    private void ApplyLight()
    {
        if (lights == null) return;

        float intensity = Mathf.Max(0f, currentLightIntensity + Mathf.Sin(pulsePhase) * currentPulseAmount);

        foreach (Light2D light in lights)
        {
            if (light == null) continue;
            light.color = currentLightColor;
            light.intensity = intensity;
        }
    }
}