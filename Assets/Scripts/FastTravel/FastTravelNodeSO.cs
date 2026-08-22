using UnityEngine;

[CreateAssetMenu(fileName = "NewFastTravelNode", menuName = "World/Fast Travel Node")]
public class FastTravelNodeSO : ScriptableObject
{
    [Header("Node Identity")]
    public string nodeID;
    public string displayName;
    
    [Header("Map Positioning")]
    public Vector2 mapUIPosition;

    [Header("Scene Destination")]
    public string targetSceneName;
    public Vector3 targetWorldPosition;
}