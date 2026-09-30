using System;
using System.Collections;
using UnityEngine;

public class PlayerHeals : MonoBehaviour
{
    private int curr;
    [SerializeField] private int max = 3;
    [SerializeField] private float healAmount = 70f;

    private PlayerStats pStats;
    public event Action<int> OnUpdateHeals; 

    private Coroutine FindStatsRoutine;

    private void Start()
    {
        ResetHeals();
        FindStatsRoutine = StartCoroutine(FindStats());
    }

    private IEnumerator FindStats()
    {
        while (pStats == null)
        {
            pStats = GetComponent<PlayerStats>();
            yield return null;
        }
        FindStatsRoutine = null;
    }

    public int GetCurrentHealCount()
    {
        return curr;
    }

    public void ResetHeals()
    {
        curr = max;
        OnUpdateHeals?.Invoke(curr);
    }

    public void SetAmountOfHeals(int amount)
    {
        curr = amount;
        OnUpdateHeals?.Invoke(curr);
    }

    public void TryUseHeal()
    {
        if (curr <= 0) return;
        curr -= 1;
        OnUpdateHeals?.Invoke(curr);
        pStats.Heal(healAmount);
    }

    public void SetHealAmount(float amount)
    {
        healAmount = amount;
    }
}
