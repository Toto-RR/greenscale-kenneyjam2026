using UnityEngine;

public class BetterJumping : MonoBehaviour
{
    private Rigidbody2D rb;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Multipliers")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    public float ascendMultiplier = 1.4f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        bool jumpHeld = playerMovement.IsJumpHeld;

        if (rb.linearVelocity.y < 0)
            rb.linearVelocity += (fallMultiplier - 1) * Physics2D.gravity.y * rb.gravityScale * Time.deltaTime * Vector2.up;
        else if (rb.linearVelocity.y > 0 && !jumpHeld)
            rb.linearVelocity += (lowJumpMultiplier - 1) * Physics2D.gravity.y * rb.gravityScale * Time.deltaTime * Vector2.up;
        else if (rb.linearVelocity.y > 0 && jumpHeld)
            rb.linearVelocity += (ascendMultiplier - 1) * Physics2D.gravity.y * Time.deltaTime * Vector2.up;
    }
}