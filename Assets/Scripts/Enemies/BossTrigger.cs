using System;
using System.Collections;
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
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private string bossID = "boss_intro_unique_id";

        [Header("Camera")]
        [SerializeField] private bool lockCameraToArena = true;

        [Header("Music")] 
        [SerializeField] private EventReference bossMusicEvent;
        [SerializeField] private EventReference postBossMusicEvent;
        
        private bool hasTriggered;
        private bool bossDefeated;

        private Enemy activeBossEnemy;
        private Transform engagedPlayer;
        private bool cameraLocked;
        private bool doorsLockedByThis;
        private bool deathSequenceRunning;

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
            if (activeBossEnemy == null || deathSequenceRunning) return;

            bool bossGone = !activeBossEnemy.isActiveAndEnabled;
            if (bossGone || engagedPlayer == null) Disengage();
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
            if (activeBossEnemy != null) return;
            if (hasTriggered) return;
            if (bossDefeated) return;
            if (!other.CompareTag(playerTag)) return;

            Player player = other.GetComponentInParent<Player>();
            if (player == null) return;
            
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

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
            
            DialogueManager.Instance.StartDialogue(introDialogue, player);

            if (DialogueManager.Instance.IsDialogueActive)
                StartCoroutine(WaitForDialogueEnd(player));
            else
                hasTriggered = false;
        }

        private IEnumerator WaitForDialogueEnd(Player player)
        {
            yield return new WaitUntil(() => !DialogueManager.Instance.IsDialogueActive);

            TriggerBossFight(player);
        }

        private void TriggerBossFight(Player player)
        {
            Enemy bossEnemy = bossSpawner != null ? bossSpawner.SpawnedEnemy?.GetComponent<Enemy>() : null;

            if (bossEnemy != null)
            {
                bossEnemy.SetBossBounds(arenaBounds);
                bossEnemy.TriggerLockOn(player.transform);
                
                MusicManager.Instance?.SwitchMusic(bossMusicEvent, isBossMusic: true);
                BossHealthBarUI.Instance?.Initialize(bossEnemy.Context.Health.CurrentHealth, bossEnemy.Context.Health.MaxHealth);

                activeBossEnemy = bossEnemy;
                engagedPlayer = player.transform;
                engagedStats = player.Stats;
                if (engagedStats != null) engagedStats.OnDied += HandlePlayerDied;

                LockCamera();
                SetDoorsLocked(true);

                bossEnemy.Context.Health.DeferDeath = true;
                onHealthDepleted = () => StartCoroutine(PlayDeathDialogueThenFinalize(bossEnemy, player));
                bossEnemy.Context.Health.OnHealthDepleted += onHealthDepleted;

                onBossDamaged = (amount, currentHealth, isCrit) =>
                {
                    MusicManager.Instance?.SetBossIntensity((float)currentHealth / bossEnemy.Context.Health.MaxHealth);
                    BossHealthBarUI.Instance?.SetHealth(currentHealth, bossEnemy.Context.Health.MaxHealth);
                };
                
                onBossDeath = () => 
                {
                    bossDefeated = true;
                    ReleaseCamera();
                    SetDoorsLocked(false);
                    MusicManager.Instance?.SwitchMusic(postBossMusicEvent, isBossMusic: false);
                    BossHealthBarUI.Instance?.Hide();
                    CleanupFightSubscriptions();
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
        

        private void HandlePlayerDied()
        {
            ReleaseCamera();
            SetDoorsLocked(false);
        }

        private void SetDoorsLocked(bool locked)
        {
            if (locked == doorsLockedByThis) return;
            doorsLockedByThis = locked;

            bool wasEngaged = AnyBossEngaged;
            engagedTriggerCount = Mathf.Max(0, engagedTriggerCount + (locked ? 1 : -1));

            if (AnyBossEngaged != wasEngaged) OnBossEngagementChanged?.Invoke(AnyBossEngaged);
        }

        private void LockCamera()
        {
            if (!lockCameraToArena || arenaBounds == null) return;

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

        private void CleanupFightSubscriptions()
        {
            if (engagedStats != null) engagedStats.OnDied -= HandlePlayerDied;
            engagedStats = null;

            if (activeBossEnemy != null)
            {
                if (!deathSequenceRunning)
                    activeBossEnemy.Context.Health.DeferDeath = false;

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

        private IEnumerator PlayDeathDialogueThenFinalize(Enemy bossyEnemy, Player player)
        {
            deathSequenceRunning = true;
            bossyEnemy.FreezeForDeathSequence();
            
            player.Controller.FreezeMovement(true);

            if (deathDialogue != null && DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue(deathDialogue, player);
                

                if (DialogueManager.Instance.IsDialogueActive)
                    yield return new WaitUntil(() => !DialogueManager.Instance.IsDialogueActive);
            }
            
            player.Controller.FreezeMovement(false);

            deathSequenceRunning = false;
            bossyEnemy.Context.Health.FinalizeDeath();
        }
        
    }
}