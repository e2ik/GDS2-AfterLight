using UnityEngine;

public class PlayerVFX : MonoBehaviour
{
    public void PlayParry(Vector3 position)
    {
        PSpawner.Spawn("ParryEffect", position);
    }

    public void PlayPlayerHit(Vector3 position)
    {
        PSpawner.Spawn("PlayerHit", position);
    }

    public void PlayTurnDust(Vector3 position)
    {
        PSpawner.Spawn("TurnDust", position);
    }

    public void PlayJumpDust(Vector3 position, Quaternion rotation)
    {
        PSpawner.Spawn("JumpDust", position, rotation);
    }
}