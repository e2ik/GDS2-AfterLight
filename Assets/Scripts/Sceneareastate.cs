using UnityEngine;

public class SceneAreaState : MonoBehaviour
{
    [SerializeField] private GameObject exteriorRoot;
    [SerializeField] private GameObject interiorRoot;

    public AreaSide CurrentSide { get; private set; } = AreaSide.Exterior;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            SetSide(GameManager.Instance.CurrentAreaSide);
        }
    }

    public void SetSide(AreaSide side)
    {
        CurrentSide = side; // Store current side

        if (exteriorRoot != null) exteriorRoot.SetActive(side == AreaSide.Exterior);
        if (interiorRoot != null) interiorRoot.SetActive(side == AreaSide.Interior);
    }
}