using UnityEngine;

public class SceneAreaState : MonoBehaviour
{
    [SerializeField] private GameObject exteriorRoot;
    [SerializeField] private GameObject interiorRoot;
    [SerializeField] private AreaSide startingSide = AreaSide.Exterior;

    public AreaSide CurrentSide { get; private set; }

    private void Awake()
    {
        CurrentSide = startingSide;
        ApplyState();
        GameManager.Instance?.SetAreaSide(CurrentSide);
    }

    public void SetSide(AreaSide side)
    {
        CurrentSide = side;
        ApplyState();
        GameManager.Instance?.SetAreaSide(CurrentSide);
    }

    private void ApplyState()
    {
        bool isInterior = CurrentSide == AreaSide.Interior;
        if (exteriorRoot != null) exteriorRoot.SetActive(!isInterior);
        if (interiorRoot != null) interiorRoot.SetActive(isInterior);
    }
}