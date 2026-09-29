using UnityEngine;

[System.Serializable]
public class TooltipAnchorSettings
{
    [Tooltip("Point on the slot the tooltip attaches to. (0,0) is bottom-left, (1,1) is top-right.")]
    public Vector2 targetPoint = new Vector2(1f, 1f);

    [Tooltip("Point on the tooltip that sits at the target point. (0,1) is the tooltip's top-left corner.")]
    public Vector2 tooltipPivot = new Vector2(0f, 1f);

    [Tooltip("Extra offset in canvas units.")]
    public Vector2 offset = new Vector2(8f, 0f);
}