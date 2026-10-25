using System;
using System.Collections;
using Enemies.Cutscene;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace Enemies
{
    [RequireComponent(typeof(Collider2D))]
    public class BossTrigger : MonoBehaviour
    {
        [SerializeField] private DialogueData introDialogue;
        [SerializeField] private DialogueData deathDialogue;
        [SerializeField] private PrefabSpawner bossSpawner;
        [SerializeField] private BossBounds arenaBounds;
        [SerializeField] private GameObject arenaColliders;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private string bossID = "boss_intro_unique_id";
        [SerializeField] private string bossName = "";
        [SerializeField] private GameObject requiredGround;

        [Header("Prerequisite")] 
        [SerializeField] private string requiredBossID = "";

        [Header("Cutscens")] 
        [SerializeField] private string introCutscene = "";
        [SerializeField] private string deathCutscene = "";
        [SerializeField, Min(0f)] private float cutsceneFadeOutDuration = 1f;
        [SerializeField, Min(0f)] private float cutsceneFadeInDuration = 1f;

        [Header("Phases")] 
        [SerializeField, Min(1)] private int phaseCount = 1;
        [SerializeField] private DialogueData phaseTransitionDialogue;
        [SerializeField] private EventReference phaseTransitionSFX;
        [SerializeField, Min(0f)] private float transitionBeat = 0.6f;
        [SerializeField, Min(0.1f)] private float refillDuration = 2.5f;
        [SerializeField, Min(0f)] private float resumeDelay = 0.5f;
        

        [Header("Rematch")]
        [SerializeField] private bool rematchPlaysDeathDialogue = false;
        [SerializeField] private bool rematchPlaysPhaseDialogue = false;
        [SerializeField] private bool rematchLocksArena = true;
        [SerializeField] private bool rematchPlaysBossMusic = true;
        [SerializeField] private bool rematchShowsHealthBar = true;

        [Header("Camera")]
        [SerializeField] private bool lockCameraToArena = true;

        [Header("End Credits")]
        [SerializeField] private bool playEndCreditsOnDefeat = false;
        [SerializeField] private bool endCreditsOnRematch = false;
        [SerializeField, Min(0f)] private float endCreditsDelay = 1.5f;
        private bool endCreditsPending;
        private bool endCreditsStarted;
        private bool ShouldPlayEndCredits => playEndCreditsOnDefeat && (!isRematch || endCreditsOnRematch);

        [Header("Background")]
        [SerializeField] private List<ParallaxBackground.LayerColor> engagedLayerColors = new List<ParallaxBackground.LayerColor>();
        [SerializeField, Min(0f)] private float backgroundBlendTime = 3f;
        [SerializeField] private SpriteRenderer[] fadeWithBackground;
        private Coroutine backgroundFadeRoutine;
        private float backgroundFade;
        private float starsStartTime = float.PositiveInfinity;
        private MaterialPropertyBlock fadeBlock;
        private static readonly int UseCustomTimeId = Shader.PropertyToID("_UseCustomTime");
        private static readonly int CustomTimeId = Shader.PropertyToID("_CustomTime");

        [Header("Music")] 
        [SerializeField] private EventReference bossMusicEvent;

        [Header("Rematch Interactable")]
        [SerializeField] private bool useRematchInteractable = false;
        [SerializeField] private BossRematch rematchInteractable;
        private bool rematchInteractableShown = true;
        
        private bool hasTriggered;
        private bool bossDefeated;
        private GameObject defeatedBossObject;
        private bool isRematch;

        private Enemy activeBossEnemy;
        private Transform engagedPlayer;
        private bool cameraLocked;
        private bool doorsLockedByThis;
        private bool deathSequenceRunning;
        private Collider2D playerInside;

        private static int engagedTriggerCount;
        public static bool AnyBossEngaged => engagedTriggerCount > 0;
        public static event Action<bool> OnBossEngagementChanged;
        private CameraFollow2D lockedCamera;
        private PlayerStats engagedStats;
        private Action<int, int, bool> onBossDamaged;
        private Action onBossDeath;
        private Action onHealthDepleted;

        private void Update()
        {
            UpdateFadeTime();
            UpdateRematchInteractable();
            if (playerInside != null && activeBossEnemy == null) TryStart(playerInside);

            if (activeBossEnemy == null || deathSequenceRunning) return;

            bool bossGone = !activeBossEnemy.isActiveAndEnabled;
            if (bossGone || engagedPlayer == null) Disengage();
        }

        private bool IsOnRequiredGround(Player player)
        {
            if (requiredGround == null) return true;
            if (player == null || player.Controller == null) return false;

            Collider2D ground = player.Controller.GroundCollider;
            return ground != null && ground.transform.IsChildOf(requiredGround.transform);
        }

        private void Awake()
        {
            if (arenaColliders != null) arenaColliders.SetActive(false);
            SetFadeAlpha(0f);
            if (rematchInteractable != null) rematchInteractable.SetOwner(this);
            UpdateRematchInteractable();
        }

        private void UpdateRematchInteractable()
        {
            if (rematchInteractable == null) return;

            bool show = useRematchInteractable
                        && activeBossEnemy == null
                        && !deathSequenceRunning
                        && SaveManager.Instance != null && SaveManager.Instance.HasStoryFlag(DefeatedFlag)
                        && !BossPresent();

            if (show == rematchInteractableShown) return;
            rematchInteractableShown = show;
            rematchInteractable.gameObject.SetActive(show);
        }

        private bool BossPresent()
        {
            GameObject boss = bossSpawner != null ? bossSpawner.SpawnedEnemy : null;
            if (boss == null || !boss.activeInHierarchy) return false;

            EnemyHealth health = boss.GetComponentInChildren<EnemyHealth>();
            return health == null || health.CurrentHealth > 0;
        }

        public bool RequestRematch()
        {
            if (bossSpawner == null || activeBossEnemy != null || deathSequenceRunning || BossPresent()) return false;

            bossSpawner.ForceRespawn();
            UpdateRematchInteractable();
            return true;
        }

        private void OnEnable()
        {
            PlayerStats.OnRespawnStarted += HandleRespawnStarted;
        }

        private void OnDisable()
        {
            PlayerStats.OnRespawnStarted -= HandleRespawnStarted;
            if (activeBossEnemy != null) Disengage();
        }

        private void HandleRespawnStarted()
        {
            if (activeBossEnemy != null) Disengage();
        }

        private void Disengage()
        {
            ReleaseCamera();
            SetDoorsLocked(false);
            BossHealthBarUI.Instance?.Hide();
            
            if (MusicManager.Instance != null && MusicManager.Instance.IsBossMusicActive)
                MusicManager.Instance.ExitBossMusicToPrevious();

            CleanupFightSubscriptions();
            hasTriggered = false;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag(playerTag)) playerInside = other;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == playerInside) playerInside = null;
        }

        private void TryStart(Collider2D other)
        {
            if (!other.enabled || !other.gameObject.activeInHierarchy)
            {
                playerInside = null;
                return;
            }

            if (activeBossEnemy != null) return;
            if (hasTriggered && !bossDefeated) return;
            if (!PrerequisiteMet) return;
            if (bossDefeated && !HasFreshBoss()) return;

            Player player = other.GetComponentInParent<Player>();
            if (player == null || !IsOnRequiredGround(player)) return;

            if (bossDefeated)
            {
                if (!HasFreshBoss()) return;

                bossDefeated = false;
                hasTriggered = false;
                defeatedBossObject = null;
                isRematch = true;
            }

            if (SaveManager.Instance != null && SaveManager.Instance.HasStoryFlag(bossID))
            {
                TriggerBossFight(player);
                return;
            }

            if (DialogueManager.Instance == null)
            {
                Debug.LogWarning("[BossTrigger] DialogueManager not found");
                return;
            }

            hasTriggered = true;
            if(SaveManager.Instance != null)
                SaveManager.Instance.SetStoryFlag(bossID);

            if (!isRematch || rematchLocksArena) LockCamera();

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            
            DialogueManager.Instance.StartDialogue(introDialogue, player);

            if (DialogueManager.Instance.IsDialogueActive)
            {
                StartCoroutine(WaitForDialogueEnd(player));
            }
            else
            {
                hasTriggered = false;
                ReleaseCamera();
            }
        }


        private bool HasFreshBoss()
        {
            if (deathSequenceRunning || bossSpawner == null) return false;

            GameObject spawned = bossSpawner.SpawnedEnemy;
            return spawned != null && spawned.activeInHierarchy && spawned != defeatedBossObject;
        }

        private IEnumerator WaitForDialogueEnd(Player player)
        {
            yield return new WaitUntil(() => !DialogueManager.Instance.IsDialogueActive);

            yield return PlayCutscene(introCutscene, player);
            
            TriggerBossFight(player);
        }

        public static string DefeatedFlagFor(string id) => id + "_defeated";
        private string DefeatedFlag => DefeatedFlagFor(bossID);

        private bool PrerequisiteMet => string.IsNullOrEmpty(requiredBossID) || (SaveManager.Instance != null &&
            SaveManager.Instance.HasStoryFlag(DefeatedFlagFor(requiredBossID)));

        private void TriggerBossFight(Player player)
        {
            if (SaveManager.Instance != null && SaveManager.Instance.HasStoryFlag(DefeatedFlag)) isRematch = true;

            Enemy bossEnemy = bossSpawner != null ? bossSpawner.SpawnedEnemy?.GetComponent<Enemy>() : null;

            if (bossEnemy != null)
            {
                bossEnemy.SetBossBounds(arenaBounds);
                bossEnemy.Context.Health.SetEngaged(true);
                bossEnemy.TriggerLockOn(player.transform);
                
                bool playMusic = !isRematch || rematchPlaysBossMusic;
                bool showHealthBar = !isRematch || rematchShowsHealthBar;
                bool lockArena = !isRematch || rematchLocksArena;
                bool playDeathDialogue = !isRematch || rematchPlaysDeathDialogue;

                if (playMusic) MusicManager.Instance?.SwitchMusic(bossMusicEvent, isBossMusic: true);
                if (showHealthBar) BossHealthBarUI.Instance?.Initialize(bossEnemy.Context.Health.CurrentHealth, bossEnemy.Context.Health.MaxHealth, bossName);

                activeBossEnemy = bossEnemy;
                engagedPlayer = player.transform;
                SetEngagedBackground(true);
                engagedStats = player.Stats;
                if (engagedStats != null) engagedStats.OnDied += HandlePlayerDied;

                if (lockArena)
                {
                    LockCamera();
                    SetDoorsLocked(true);
                }

                bool playPhaseDialogue = !isRematch || rematchPlaysPhaseDialogue;

                if (playDeathDialogue || phaseCount > 1)
                {
                    bossEnemy.Context.Health.DeferDeath = true;

                    onHealthDepleted = () => HandleHealthDepleted(bossEnemy, player, playMusic, showHealthBar,
                        playDeathDialogue, playPhaseDialogue);
                    
                    bossEnemy.Context.Health.OnHealthDepleted += onHealthDepleted;
                }

                onBossDamaged = (amount, currentHealth, isCrit) =>
                {
                    if (playMusic) MusicManager.Instance?.SetBossIntensity((float)currentHealth / bossEnemy.Context.Health.MaxHealth);
                    if (showHealthBar) BossHealthBarUI.Instance?.SetHealth(currentHealth, bossEnemy.Context.Health.MaxHealth);
                };
                
                GameObject bossObject = bossEnemy.gameObject;
                onBossDeath = () => 
                {
                    bossDefeated = true;
                    hasTriggered = false;
                    defeatedBossObject = bossObject;
                    if (SaveManager.Instance != null) SaveManager.Instance.SetStoryFlag(DefeatedFlag);
                    ReleaseCamera();
                    SetDoorsLocked(false);
                    
                    if (playMusic) 
                        MusicManager.Instance?.ExitBossMusicToPrevious();
                    
                    BossHealthBarUI.Instance?.Hide();
                    CleanupFightSubscriptions();

                    if (ShouldPlayEndCredits && !endCreditsPending) StartEndCredits(endCreditsDelay);
                };

                bossEnemy.Context.Health.OnDamaged += onBossDamaged;
                bossEnemy.Context.Health.OnDeath += onBossDeath;
            }
            else
            {
                if (bossSpawner != null && bossSpawner.IsDefeated)
                {
                    bossDefeated = true;
                }
                else
                {
                    Debug.LogWarning("[BossTrigger] No spawned boss Enemy found to trigger lock-on");
                }
            }
        }

        private void HandleHealthDepleted(Enemy boss, Player player, bool playMusic, bool showHealthBar,
            bool playDeathDialogue, bool playPhaseDialogue)
        {
            if (boss.Phase < phaseCount)
            {
                StartCoroutine(PlayPhaseTransition(boss, player, playMusic, showHealthBar, playPhaseDialogue));
                return;
            }

            if (!playDeathDialogue)
            {
                boss.Context.Health.FinalizeDeath();
                return;
            }
            
            if(playMusic)
                MusicManager.Instance?.ExitBossMusicToPrevious();

            endCreditsPending = ShouldPlayEndCredits;
            StartCoroutine(PlayDeathDialogueThenFinalize(boss, player));
        }

        private IEnumerator PlayPhaseTransition(Enemy boss, Player player, bool playMusic, bool showHealthBar,
            bool playDialogue)
        {
            EnemyHealth health = boss.Context.Health;
            
            boss.FreezeForPhaseTransition();
            player.Controller.FreezeMovement(true);

            try
            {
                yield return new WaitForSeconds(transitionBeat);

                if (activeBossEnemy != boss)
                    yield break;

                if (playDialogue && phaseTransitionDialogue != null && DialogueManager.Instance != null)
                {
                    DialogueManager.Instance.StartDialogue(phaseTransitionDialogue, player);

                    if (DialogueManager.Instance.IsDialogueActive)
                        yield return new WaitUntil(() => !DialogueManager.Instance.IsDialogueActive);
                }

                if (activeBossEnemy != boss)
                    yield break;

                yield return new WaitForSeconds(transitionBeat);

                if (activeBossEnemy != boss)
                    yield break;

                if (!phaseTransitionSFX.IsNull)
                    AudioManager.PlaySFX(phaseTransitionSFX, boss.transform.position);

                if (showHealthBar)
                    BossHealthBarUI.Instance?.PlayRefill(health.MaxHealth, health.MaxHealth, refillDuration);

                if (playMusic)
                    MusicManager.Instance?.SetBossIntensity(1f);

                yield return new WaitForSeconds(refillDuration + resumeDelay);
                

                if (activeBossEnemy != boss)
                    yield break;

                boss.SetPhase(boss.Phase + 1);
                health.RefillHealth();
                boss.ResumeAfterPhaseTransition();
            }
            finally
            {
                if (player != null && player.Controller != null)
                    player.Controller.FreezeMovement(false);
            }
        }

        private void HandlePlayerDied()
        {
            ReleaseCamera();
            SetEngagedBackground(false);
            SetDoorsLocked(false);
        }

        private void SetDoorsLocked(bool locked)
        {
            if (locked == doorsLockedByThis) return;
            doorsLockedByThis = locked;

            if (arenaColliders != null) arenaColliders.SetActive(locked);

            bool wasEngaged = AnyBossEngaged;
            engagedTriggerCount = Mathf.Max(0, engagedTriggerCount + (locked ? 1 : -1));

            if (AnyBossEngaged != wasEngaged) OnBossEngagementChanged?.Invoke(AnyBossEngaged);
        }

        private void LockCamera()
        {
            if (cameraLocked || !lockCameraToArena || arenaBounds == null) return;

            lockedCamera = FindFirstObjectByType<CameraFollow2D>();
            if (lockedCamera == null) return;

            lockedCamera.LockToBounds(arenaBounds.WorldBounds);
            cameraLocked = true;
        }

        private void ReleaseCamera()
        {
            if (!cameraLocked) return;
            cameraLocked = false;

            if (lockedCamera != null) lockedCamera.ReturnToNormalFollow();
            lockedCamera = null;
        }

        private bool backgroundOverridden;

        private bool backgroundFadedIn;

        private void SetEngagedBackground(bool engaged)
        {
            if (engaged != backgroundFadedIn)
            {
                backgroundFadedIn = engaged;
                StartBackgroundFade(engaged ? 1f : 0f);
            }

            ParallaxBackground background = GameManager.Instance != null ? GameManager.Instance.Background : null;
            if (background == null) return;

            if (engaged && engagedLayerColors != null && engagedLayerColors.Count > 0)
            {
                background.SetOverrideColors(engagedLayerColors, backgroundBlendTime);
                backgroundOverridden = true;
            }
            else if (!engaged && backgroundOverridden)
            {
                background.ClearOverrideColors(backgroundBlendTime);
                backgroundOverridden = false;
            }
        }

        private void UpdateFadeTime()
        {
            if (fadeWithBackground == null || fadeWithBackground.Length == 0 || backgroundFade <= 0f) return;

            fadeBlock ??= new MaterialPropertyBlock();
            float elapsed = Time.unscaledTime - starsStartTime;

            foreach (SpriteRenderer sprite in fadeWithBackground)
            {
                if (sprite == null) continue;
                sprite.GetPropertyBlock(fadeBlock);
                fadeBlock.SetFloat(UseCustomTimeId, 1f);
                fadeBlock.SetFloat(CustomTimeId, elapsed);
                sprite.SetPropertyBlock(fadeBlock);
            }
        }

        private void StartBackgroundFade(float target)
        {
            if (fadeWithBackground == null || fadeWithBackground.Length == 0) return;

            if (target > 0f) starsStartTime = Time.unscaledTime + backgroundBlendTime;

            if (backgroundFadeRoutine != null) StopCoroutine(backgroundFadeRoutine);

            if (backgroundBlendTime <= 0f || !isActiveAndEnabled)
            {
                SetFadeAlpha(target);
                return;
            }

            backgroundFadeRoutine = StartCoroutine(FadeBackgroundObjects(target));
        }

        private IEnumerator FadeBackgroundObjects(float target)
        {
            while (!Mathf.Approximately(backgroundFade, target))
            {
                SetFadeAlpha(Mathf.MoveTowards(backgroundFade, target, Time.unscaledDeltaTime / backgroundBlendTime));
                yield return null;
            }

            backgroundFadeRoutine = null;
        }

        private void SetFadeAlpha(float alpha)
        {
            backgroundFade = alpha;
            if (fadeWithBackground == null) return;

            foreach (SpriteRenderer sprite in fadeWithBackground)
            {
                if (sprite == null) continue;

                Color color = sprite.color;
                color.a = alpha;
                sprite.color = color;
                sprite.enabled = alpha > 0f;
            }
        }

        private void CleanupFightSubscriptions()
        {
            SetEngagedBackground(false);

            if (engagedStats != null) engagedStats.OnDied -= HandlePlayerDied;
            engagedStats = null;

            if (activeBossEnemy != null)
            {
                if (!deathSequenceRunning)
                    activeBossEnemy.Context.Health.DeferDeath = false;

                if (activeBossEnemy.Context.Health.CurrentHealth > 0)
                    activeBossEnemy.Context.Health.SetEngaged(false);

                if (onBossDamaged != null)
                    activeBossEnemy.Context.Health.OnDamaged -= onBossDamaged;

                if (onBossDeath != null)
                    activeBossEnemy.Context.Health.OnDeath -= onBossDeath;

                if (onHealthDepleted != null)
                    activeBossEnemy.Context.Health.OnHealthDepleted -= onHealthDepleted;
            }

            activeBossEnemy = null;
            engagedPlayer = null;
            onBossDamaged = null;
            onBossDeath = null;
            onHealthDepleted = null;
        }

        private IEnumerator PlayDeathDialogueThenFinalize(Enemy bossEnemy, Player player)
        {
            deathSequenceRunning = true;
            bossEnemy.FreezeForDeathSequence();
            
            player.Controller.FreezeMovement(true);
            

            if (deathDialogue != null && DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(deathDialogue, player);
                
                if (DialogueManager.Instance.IsDialogueActive)
                    yield return new WaitUntil(() => !DialogueManager.Instance.IsDialogueActive);
            }

            bool hasCutscene = !string.IsNullOrEmpty(deathCutscene);

            if (hasCutscene)
                yield return PlayCutscene(deathCutscene, player, bossEnemy);
            
            deathSequenceRunning = false;
            if(bossEnemy != null)
                bossEnemy.Context.Health.FinalizeDeath();

            if (endCreditsPending)
            {
                StartEndCredits(hasCutscene ? 0f : endCreditsDelay);
                yield break;
            }
            
            player.Controller.FreezeMovement(false);
        }

        private void StartEndCredits(float delay)
        {
            if (endCreditsStarted) return;
            endCreditsStarted = true;
            StartCoroutine(EndCreditsRoutine(delay));
        }

        private IEnumerator EndCreditsRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            GameManager.Instance?.PlayEndCredits();
        }

        private IEnumerator PlayCutscene(string filename, Player player, Enemy bossEnemy = null)
        {
            if (string.IsNullOrEmpty(filename))
                yield break;

            if (CutscenePlayer.Instance == null)
            {
                Debug.LogWarning("[BossTrigger] Cutscene requested but not CutscenePlayer is loaded");
                yield break;
            }
            
            player.Controller.FreezeMovement(true);
            CutscenePlayer.Instance.SetCutsceneUIHidden(true);
            AudioManager.ToggleSFXMute();

            Coroutine fadeOut = FadeCanvasController.Instance != null
                ? FadeCanvasController.Instance.FadeOut(cutsceneFadeOutDuration)
                : null;
            
            if (MusicManager.Instance != null)
            {
                MusicManager.Instance.StopMusicForCutscene();
                yield return new WaitForSecondsRealtime(MusicManager.Instance.StopFadeDuration);
            }

            if (fadeOut != null) yield return fadeOut;
            
            deathSequenceRunning = false;
            if(bossEnemy != null)
                bossEnemy.Context.Health.FinalizeDeath();
            
            yield return CutscenePlayer.Instance.Play(filename);
            
            
            MusicManager.Instance?.RestartMusic();
            AudioManager.ToggleSFXMute();
            CutscenePlayer.Instance.SetCutsceneUIHidden(false);

            if (endCreditsPending) yield break;

            if (FadeCanvasController.Instance != null)
                yield return FadeCanvasController.Instance.FadeIn(cutsceneFadeInDuration);

            player.Controller.FreezeMovement(false);
        }
    }
}