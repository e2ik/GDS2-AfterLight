using System.Collections;
using UnityEngine;

public class CamControls : MonoBehaviour
{
    public static CamControls Instance { get; private set; }
    private Transform targetCameraTransform;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public static void Shake(float duration, float magnitude, float frequency = 25f)
    {
        if (Instance != null && Instance.targetCameraTransform == null)
        {
            Instance.GetActiveCamera();
        }
        
        if (Instance != null && Instance.targetCameraTransform != null)
        {
            if (Instance.shakeCoroutine != null)
            {
                Instance.StopCoroutine(Instance.shakeCoroutine);
            }
            Instance.shakeCoroutine = Instance.StartCoroutine(Instance.ShakeRoutine(duration, magnitude, frequency));
        }
        else
        {
            Debug.LogWarning("CamControls: No target camera transform assigned to shake!");
        }
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude, float frequency)
    {
        float elapsed = 0f;
        float seedX = Random.value * 1000f;
        float seedY = Random.value * 1000f + 500f;

        while (elapsed < duration)
        {
            float normalizedProgress = elapsed / duration;
            float currentMagnitude = magnitude * (1f - normalizedProgress);
            
            float sampleX = seedX + (Time.unscaledTime * frequency);
            float sampleY = seedY + (Time.unscaledTime * frequency);

            float x = (Mathf.PerlinNoise(sampleX, 0f) - 0.5f) * 2f * currentMagnitude;
            float y = (Mathf.PerlinNoise(0f, sampleY) - 0.5f) * 2f * currentMagnitude;

            Vector3 basePosition = targetCameraTransform.parent != null 
                ? targetCameraTransform.parent.position 
                : targetCameraTransform.position;

            targetCameraTransform.position = new Vector3(basePosition.x + x, basePosition.y + y, targetCameraTransform.position.z);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (targetCameraTransform.parent != null)
        {
            targetCameraTransform.localPosition = new Vector3(0f, 0f, targetCameraTransform.localPosition.z);
        }

        shakeCoroutine = null;
    }

    private void GetActiveCamera()
    {
        if (targetCameraTransform == null && Camera.main != null)
        {
            targetCameraTransform = Camera.main.transform;
        }
    }
}