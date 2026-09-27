using System;
using System.Collections;
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

        private bool hasTriggered;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasTriggered) return;
            if (!other.CompareTag(playerTag)) return;

            Player player = other.GetComponentInParent<Player>();
            if (player == null) return;

            if (DialogueManager.Instance == null)
            {
                Debug.LogWarning("[BossTrigger] DialogueManager not found");
                return;
            }

            hasTriggered = true;

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

            Enemy bossEnemy = bossSpawner != null ? bossSpawner.SpawnedEnemy?.GetComponent<Enemy>() : null;

            if (bossEnemy != null)
            {
                bossEnemy.SetBossBounds(arenaBounds);
                bossEnemy.TriggerLockOn(player.transform);
            }
            else
                Debug.LogWarning("[BossTrigger] No spawned boss Enemy found to trigger lock-on");
        }
    }
}
