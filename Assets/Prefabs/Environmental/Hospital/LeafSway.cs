using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LeafSway : MonoBehaviour
{
    [Header("Idle Sway (randomised per leaf)")]
    [SerializeField] private Vector2 swayAmplitudeRange = new(2f, 5f);
    [SerializeField] private Vector2 swayFrequencyRange = new(0.1f, 0.25f);

    [Header("Jolt")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private Vector2 joltStrengthRange = new(100f, 180f);
    [SerializeField] private Vector2 stiffnessRange = new(80f, 140f);
    [SerializeField] private Vector2 dampingRange = new(4f, 8f);
    [SerializeField] private float maxJoltAngle = 25f;
    [SerializeField] private float joltCooldown = 0.4f;
    [SerializeField] private bool invertJoltDirection = false;

    private Quaternion baseRotation;
    private float swayAmplitude, swayFrequency, swayPhase, swayPhase2;
    private float stiffness, damping;

    private float joltAngle;
    private float joltVelocity;
    private float nextJoltTime;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;

        baseRotation = transform.localRotation;

        swayAmplitude = Random.Range(swayAmplitudeRange.x, swayAmplitudeRange.y);
        swayFrequency = Random.Range(swayFrequencyRange.x, swayFrequencyRange.y);
        swayPhase = Random.Range(0f, Mathf.PI * 2f);
        swayPhase2 = Random.Range(0f, Mathf.PI * 2f);

        stiffness = Random.Range(stiffnessRange.x, stiffnessRange.y);
        damping = Random.Range(dampingRange.x, dampingRange.y);
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.033f);

        float w = Time.time * swayFrequency * Mathf.PI * 2f;
        float sway = (Mathf.Sin(w + swayPhase) + 0.4f * Mathf.Sin(w * 1.7f + swayPhase2)) / 1.4f;
        sway *= swayAmplitude;

        float accel = -stiffness * joltAngle - damping * joltVelocity;
        joltVelocity += accel * dt;
        joltAngle += joltVelocity * dt;
        joltAngle = Mathf.Clamp(joltAngle, -maxJoltAngle, maxJoltAngle);

        transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, sway + joltAngle);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag)) return;

        float dx = transform.position.x - other.transform.position.x;
        float direction = Mathf.Abs(dx) < 0.01f
            ? (Random.value < 0.5f ? -1f : 1f)
            : Mathf.Sign(dx);

        Jolt(direction);
    }


    public void Jolt(float direction)
    {
        if (Time.time < nextJoltTime) return;
        nextJoltTime = Time.time + joltCooldown;

        float sign = invertJoltDirection ? direction : -direction;
        joltVelocity += sign * Random.Range(joltStrengthRange.x, joltStrengthRange.y);
    }
}