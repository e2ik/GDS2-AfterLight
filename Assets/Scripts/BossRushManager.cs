using System;
using System.Collections;
using System.Collections.Generic;
using Enemies;
using Tutorial;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class BossRushManager : MonoBehaviour
{
      [Serializable]
      public class Stage
      {
            public string bossID;
            public FastTravelNodeSO arrivalNode;
            public ERarity rewardRarity = ERarity.Rare;
            [TextArea(1, 3)] public string rewardMessage = "New gear received!";
      }
      
      public static BossRushManager Instance { get; private set; }
      public static bool IsActive { get; private set; }

      [SerializeField] private Stage[] stages;
      [SerializeField] private bool requireRematchInteract = true;
      [SerializeField, Min(0f)] private float autoStartDelay = 1f;
      [SerializeField, Min(0f)] private float betweenStagesDelay = 2f;
      [SerializeField, Min(0f)] private float winReturnDelay = 4f;
      [SerializeField, Min(1f)] private float triggerTimeout = 5f;
      [SerializeField] private bool playCreditsOnWin = true;

      [Header("Reward Notice")] 
      [SerializeField, Min(0.5f)] private float noticeDuration = 4f;
      [SerializeField] private DialogueEffect noticeEffect;
      
      public int StageIndex { get; private set; }
      public int StageCount => stages != null ? stages.Length : 0;
      public event Action<int> OnStageStarted;
      public event Action<bool> OnRushFinished;

      private bool stageCleared;
      private BossTrigger currentTrigger;

      [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
      private static void ResetStatics()
      {
            IsActive = false; 
            Instance = null;
      }
      
      private void Awake()
      {
            if (Instance != null && Instance != this)
            {
                  Destroy(gameObject); 
                  return;
            }
            
            Instance = this;
      }

      private void OnDestroy()
      {
            if (Instance == this)
            {
                  Instance = null; 
                  IsActive = false;
            }
      }

      public void StartRush()
      {
            Debug.Log($"[BossRush] StartRush. IsActive={IsActive}, stages={StageCount}");
            
            if (IsActive || StageCount == 0)
                  return;

            StageIndex = 0;
            stageCleared = false;
            currentTrigger = null;
            IsActive = true;
            StartCoroutine(RushRoutine());
      }

      public void Fail() => Finish(false);

      public void HandleBossDefeated(string bossID)
      {
            if (IsActive && StageIndex < StageCount && stages[StageIndex].bossID == bossID)
                  stageCleared = true;
      }

      private IEnumerator RushRoutine()
      {
            while (GameManager.Instance == null || GameManager.Instance.Player == null)
                  yield return null;

            for (StageIndex = 0; StageIndex < stages.Length; StageIndex++)
            {
                  Stage stage = stages[StageIndex];
                  stageCleared = false;
                  currentTrigger = null;
                  
                  FastTravelManager.Instance.TravelTo(stage.arrivalNode, recordProgress: false);
                  Debug.Log($"[BossRush] Stage {StageIndex}: travelling={FastTravelManager.Instance.IsTravelling}");

                  while (FastTravelManager.Instance.IsTravelling)
                        yield return null;

                  if (!IsActive)
                        yield break;

                  float waited = 0f;

                  while (!BossTrigger.TryGet(stage.bossID, out currentTrigger) && waited < triggerTimeout)
                  {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                  }

                  if (currentTrigger == null)
                  {
                        Debug.LogError($"[BossRush] No BossTrigger with id '{stage.bossID}' found after travelling)");
                        Finish(false);
                        yield break;
                  }

                  Player player = GameManager.Instance.Player;
                  currentTrigger.PrepareRushArena();
                  GameManager.Instance.SpawnAllOfRarity(stage.rewardRarity);
                  ShowRewardNotice(stage, player);
                  OnStageStarted?.Invoke(StageIndex);

                  if (requireRematchInteract && currentTrigger.CanOfferRush)
                  {
                        currentTrigger.OfferRushFight();
                  }
                  else
                  {
                        yield return new WaitForSeconds(autoStartDelay);
                        if (!IsActive) 
                              yield break;

                        if (!currentTrigger.StartRushFight(player))
                        {
                              Finish(false);
                              yield break;
                        }
                  }
                  
                  while (IsActive && !stageCleared)
                        yield return null;
                        
                  if(!IsActive)
                        yield break;

                  currentTrigger.EndRush();

                  if (StageIndex < stages.Length - 1)
                        yield return new WaitForSeconds(betweenStagesDelay);
            }

            Finish(true);
      }

      private void Finish(bool won)
      {
            if (!IsActive)
                  return;
            
            IsActive = false;

            if (currentTrigger != null)
                  currentTrigger.EndRush();
            
            OnRushFinished?.Invoke(won);
            StartCoroutine(ReturnToTitleAfter(won, won ? winReturnDelay : 0f));
      }

      private IEnumerator ReturnToTitleAfter(bool won, float delay)
      {
            if (delay > 0)
                  yield return new WaitForSeconds(delay);
            
            if(won && playCreditsOnWin)
                  GameManager.Instance?.PlayEndCredits(standardCredits: true);
            else
                  GameManager.Instance?.ReturnToTitle();
      }

      private void ShowRewardNotice(Stage stage, Player player)
      {
            if (string.IsNullOrEmpty(stage.rewardMessage) || player == null)
                  return;

            if (TutorialSpeechBubblePool.Instance == null)
                  return;

            TutorialSpeechBubblePool.Instance.Show(stage.rewardMessage, player.transform, noticeDuration, noticeEffect);
      }

      public void Abort()
      {
            StopAllCoroutines();
            
            if(currentTrigger != null)
                  currentTrigger.EndRush();

            currentTrigger = null;
            stageCleared = false;
            IsActive = false;
      }
}