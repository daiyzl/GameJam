using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class WorldCharacter : MonoBehaviour
{
    public float moveSpeed = 4f;
    public SpriteRenderer bodyView;
    private Rigidbody2D body;
    private Vector2 movement;
    private bool testOverride;
    public Vector2 Facing { get; private set; } = Vector2.up;
    public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
    private void Awake() { body = GetComponent<Rigidbody2D>(); }
    private void Update()
    {
        if (!testOverride) movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        if (movement.sqrMagnitude > 0.001f) Facing = movement.normalized;
        if (bodyView != null)
        {
            bodyView.sortingOrder = 1000 - Mathf.RoundToInt(transform.position.y * 10);
            bodyView.flipX = Facing.x < -0.1f;
            bodyView.transform.localPosition = new Vector3(0, 0.22f + (movement.sqrMagnitude > 0 ? Mathf.Sin(Time.time * 14) * 0.035f : 0), 0);
        }
    }
    private void FixedUpdate() { body.velocity = movement * moveSpeed; }
    public void SetVerificationMovement(Vector2 direction)
    { testOverride = true; movement = direction.normalized; }
    public void ReleaseVerificationMovement() { movement = Vector2.zero; testOverride = false; }
    public void Teleport(Vector2 position)
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        body.position = position; body.velocity = Vector2.zero; transform.position = position;
    }
}
