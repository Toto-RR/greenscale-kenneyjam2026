using UnityEngine;
using UnityEngine.InputSystem;

public class BetterJumping : MonoBehaviour
{
    private Rigidbody2D rb;

    [Header("Multipliers")]
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    public float ascendMultiplier = 1.4f;

    [Header("Input")]
    [SerializeField] private Key jumpKey = Key.Space;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += (fallMultiplier - 1) * Physics2D.gravity.y * rb.gravityScale * Time.deltaTime * Vector2.up;
        }
        else if (rb.linearVelocity.y > 0 && !Keyboard.current[jumpKey].isPressed)
        {
            rb.linearVelocity += (lowJumpMultiplier - 1) * Physics2D.gravity.y * rb.gravityScale * Time.deltaTime * Vector2.up;
        }
        else if (rb.linearVelocity.y > 0 && Keyboard.current[jumpKey].isPressed)
        {
            rb.linearVelocity += (ascendMultiplier - 1) * Physics2D.gravity.y * Time.deltaTime * Vector2.up;
        }
    }
}