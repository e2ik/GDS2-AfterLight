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
        [SerializeField] private PrefabSpawner bossSpawner;
        [SerializeField] private BossBounds arenaBounds;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private string bossID = "boss_intro_unique_id";

        [Header("Music")] 
        [SerializeField] private EventReference bossMusicEvent;
        [SerializeField] private EventReference postBossMusicEvent;
        
        private bool hasTriggered;
        private bool bossDefeated;

        private Enemy activeBossEnemy;
        private Action<int, int, bool> onBossDamaged;
        private Action onBossDeath;

        private void OnTriggerEnter2D(Collider2D other)
        {
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

                onBossDamaged = (amount, currentHealth, isCrit) =>
                {
                    MusicManager.Instance?.SetBossIntensity((float)currentHealth / bossEnemy.Context.Health.MaxHealth);
                    BossHealthBarUI.Instance?.SetHealth(currentHealth, bossEnemy.Context.Health.MaxHealth);
                };
                
                onBossDeath = () => 
                {
                    bossDefeated = true;
                    MusicManager.Instance?.SwitchMusic(postBossMusicEvent, isBossMusic: false);
                    BossHealthBarUI.Instance?.Hide();
                    CleanupFightSubscriptions();
                };

                bossEnemy.Context.Health.OnDamaged += onBossDamaged;
                bossEnemy.Context.Health.OnDeath += onBossDeath;
            }
            else
                Debug.LogWarning("[BossTrigger] No spawned boss Enemy found to trigger lock-on");
        }
        

        private void CleanupFightSubscriptions()
        {
            if (activeBossEnemy != null)
            {
                if (onBossDamaged != null)
                    activeBossEnemy.Context.Health.OnDamaged -= onBossDamaged;

                if (onBossDeath != null)
                    activeBossEnemy.Context.Health.OnDeath -= onBossDeath;
            }

            activeBossEnemy = null;
            onBossDamaged = null;
            onBossDeath = null;
        }
        
    }
}
