using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public abstract class PlayerProjectileBase : MonoBehaviour
{
    [SerializeField] private float lifetime = float.MaxValue;
    protected Rigidbody2D Rb { get; private set; }
    protected int Damage { get; private set; }
    public PlayerProjectileBase SourcePrefab { get; set; }
    private float _lifeTimer;
    protected AttackContext Context;

    protected void Awake()
    {
        Rb = GetComponent<Rigidbody2D>();
    }

    public void Launch(Vector2 origin, Vector2 initialVelocity, int damage, AttackContext context)
    {
        origin = transform.position;
        Damage = damage;
        _lifeTimer = lifetime;
        Context = context;
        OnLaunch(initialVelocity);
    }

    protected abstract void OnLaunch(Vector2 initialVelocity);

    protected void Update()
        {
            _lifeTimer -= Time.deltaTime;
            if (_lifeTimer <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }
    protected virtual bool OnHitTrigger(Collider2D other) => true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (OnHitTrigger(other))
        {
            Destroy(gameObject);
        }   
    }
}