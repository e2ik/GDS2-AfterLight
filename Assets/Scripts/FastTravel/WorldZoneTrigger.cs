using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class WorldZoneStreamer : MonoBehaviour
{
    [Header("Streaming Targets")]
    [Tooltip("The scene to load as the player approaches")]
    [SerializeField] private string sceneToLoad;

    [Tooltip("An optional scene behind the player to unload once crossed")]
    [SerializeField] private string sceneToUnload;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (!string.IsNullOrEmpty(sceneToLoad) && !SceneManager.GetSceneByName(sceneToLoad).isLoaded)
        {
            SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Additive);
        }

        if (!string.IsNullOrEmpty(sceneToUnload) && SceneManager.GetSceneByName(sceneToUnload).isLoaded)
        {
            SceneManager.UnloadSceneAsync(sceneToUnload);
        }
    }
}