using UnityEngine;

public class HDripSprite : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite leftSprite;
    [SerializeField] private Sprite rightSprite;
    [SerializeField] private Sprite idleSprite;
    [SerializeField, Min(0f)] private float minSpeed = 0.05f;

    private float lastX;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        lastX = transform.position.x;
    }

    private void LateUpdate()
    {
        if (spriteRenderer == null || Time.deltaTime <= 0f) return;

        float x = transform.position.x;
        float speed = (x - lastX) / Time.deltaTime;
        lastX = x;

        if (speed > minSpeed) SetSprite(rightSprite);
        else if (speed < -minSpeed) SetSprite(leftSprite);
        else SetSprite(idleSprite);
    }

    private void SetSprite(Sprite sprite)
    {
        if (sprite != null && spriteRenderer.sprite != sprite)
            spriteRenderer.sprite = sprite;
    }
}