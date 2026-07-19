using DG.Tweening;
using System;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    public enum ControlScheme { Keyboard, MouseOnly }

    [Header("Input")]
    [SerializeField] private ControlScheme controlScheme = ControlScheme.Keyboard;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float mouseDeadzone = 0.3f;

    [Header("References")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Detectors")]
    [SerializeField] private Transform leftWallDetector;
    [SerializeField] private Transform rightWallDetector;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private Vector2 wallCheckSize = Vector2.one;

    [Header("Physics Layers")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private LayerMask[] groundMaskPerLayer;
    [SerializeField] private LayerMask[] pushableMaskPerLayer;

    [Header("Movement settings")]
    [SerializeField] private float baseMoveSpeed = 5f;
    public int direction = 0;

    [Header("Jump Settings")]
    [SerializeField] private float baseJumpForce = 9f;
    [SerializeField] private float coyoteTime = 0.2f;
    [SerializeField] private float jumpBufferTime = 0.2f;
    [SerializeField] private float slidingTime = 0.2f;
    [SerializeField] private float slidingGravityScale = 0.6f;

    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private float slidingTimeCounter;
    private int lastWallJumpSide = 0; // 0 = any, -1 = left, 1 = right
    private int coyoteSourceSide = 0;

    [Header("Layer Changer")]
    [SerializeField] private LayerChanger layerChanger;

    [Header("Layer Multipliers")]
    [SerializeField] private float[] speedMultiplierPerLayer = { 1.3f, 1f, 0.75f };
    [SerializeField] private float[] gravityScalePerLayer = { 0.85f, 1f, 1.3f };
    [SerializeField] private float[] groundCheckSize = { 0.15f, 0.25f, 0.5f };
    [SerializeField] private Vector2[] wallCheckPerLayer = { new(0,0), new(0, 0), new(0, 0) };

    [Header("States")]
    public bool canMove;
    public bool wallLeft;
    public bool wallRight;
    public bool isGrounded;
    public bool isJumping;
    public bool dash;
    public bool isPushingWall;
    public bool isSliding;
    public bool IsJumpHeld { get; private set; }

    public float speed;

    private InputSystem_Actions controls;
    public InputSystem_Actions Controls => controls;

    // --- EVENTS ---
    public event Action<int> OnMoving;
    public event Action<bool> OnGround;
    public event Action OnJump;
    public event Action<int> OnWallJump;
    public event Action<int> OnSlidingWall;
    public event Action OnEndSlide;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!mainCamera) mainCamera = Camera.main;
        controls = new InputSystem_Actions();
    }

    void OnEnable()
    {
        controls.Player.Enable();
    }

    void OnDisable()
    {
        controls.Player.Disable();
    }

    void Update()
    {
        ColisionChecker();
        OnGround?.Invoke(isGrounded);

        direction = GetMoveDirection();
        IsJumpHeld = controls.Player.Jump.IsPressed();

        ApplyLayer(layerChanger.layerIndex);

        OnWall();
        Move();
        Jump();

        if (Keyboard.current.tabKey.wasPressedThisFrame)
            SetControlScheme(controlScheme == ControlScheme.Keyboard ? ControlScheme.MouseOnly : ControlScheme.Keyboard);
    }

    private int GetMoveDirection()
    {
        if (controlScheme == ControlScheme.Keyboard)
        {
            Vector2 moveInput = controls.Player.Move.ReadValue<Vector2>();
            return Mathf.RoundToInt(moveInput.x);
        }

        if (!Mouse.current.leftButton.isPressed) return 0;

        Vector2 mouseScreenPos = controls.Player.MousePosition.ReadValue<Vector2>();
        Vector3 screenPoint = mainCamera.WorldToScreenPoint(transform.position);
        screenPoint.x = mouseScreenPos.x;
        screenPoint.y = mouseScreenPos.y;
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(screenPoint);

        float dx = mouseWorldPos.x - transform.position.x;
        if (Mathf.Abs(dx) < mouseDeadzone) return 0;
        return dx > 0f ? 1 : -1;
    }

    public void SetControlScheme(ControlScheme scheme)
    {
        controlScheme = scheme;
    }

    private void ApplyLayer(int layer)
    {
        rb.gravityScale = gravityScalePerLayer[layer];
        groundCheckRadius = groundCheckSize[layer];
        wallCheckSize = wallCheckPerLayer[layer];
        speed = baseMoveSpeed * speedMultiplierPerLayer[layer];
    }

    private void Jump()
    {
        bool touchingWallLeft = !isGrounded && direction < 0 && wallLeft && lastWallJumpSide != -1;
        bool touchingWallRight = !isGrounded && direction > 0 && wallRight && lastWallJumpSide != 1;
        bool canWallJumpLeft = touchingWallLeft;
        bool canWallJumpRight = touchingWallRight;

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            coyoteSourceSide = 0;
            lastWallJumpSide = 0;
        }
        else if (canWallJumpLeft)
        {
            coyoteTimeCounter = coyoteTime;
            coyoteSourceSide = -1;
        }
        else if (canWallJumpRight)
        {
            coyoteTimeCounter = coyoteTime;
            coyoteSourceSide = 1;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (controls.Player.Jump.WasPressedThisFrame())
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        if (coyoteTimeCounter > 0f && jumpBufferCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, baseJumpForce);
            jumpBufferCounter = 0f;
            coyoteTimeCounter = 0f;

            if (coyoteSourceSide != 0)
            {
                lastWallJumpSide = coyoteSourceSide;
                OnWallJump?.Invoke(coyoteSourceSide);
            }

            OnJump?.Invoke();
            coyoteSourceSide = 0;
        }
    }

    private void OnWall()
    {
        float savedSpeed = speed;
        int sidePushing = 0; // 0 any, -1 left, 1 right
        float initialGravityScale = rb.gravityScale;

        if (direction < 0 && wallLeft)
        {
            isPushingWall = true;
            sidePushing = -1;
            slidingTimeCounter -= Time.deltaTime;
        }
        else if (direction > 0 && wallRight)
        {
            isPushingWall = true;
            sidePushing = 1;
            slidingTimeCounter -= Time.deltaTime;
        }
        else
        {
            slidingTimeCounter = slidingTime;
            isPushingWall = false;
        }

        speed = isPushingWall ? 0 : savedSpeed;

        bool isFalling = rb.linearVelocity.y <= 0f;

        if (isPushingWall && !isGrounded && slidingTimeCounter > 0f && isFalling)
        {
            rb.gravityScale *= slidingGravityScale;
            isSliding = true;
            OnSlidingWall?.Invoke(sidePushing);
        }
        else
        {
            isSliding = false;
            rb.gravityScale = initialGravityScale;
            OnEndSlide?.Invoke();
        }
    }

    private void Move()
    {
        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
        OnMoving?.Invoke(direction);
    }

    private void ColisionChecker()
    {
        LayerMask terrainMask = groundMask | groundMaskPerLayer[layerChanger.layerIndex];
        LayerMask floorMask = terrainMask | pushableMaskPerLayer[layerChanger.layerIndex];

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, floorMask);

        wallLeft = Physics2D.OverlapBox(leftWallDetector.position, wallCheckSize, 0f, terrainMask);
        wallRight = Physics2D.OverlapBox(rightWallDetector.position, wallCheckSize, 0f, terrainMask);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        Gizmos.DrawWireCube(leftWallDetector.position, wallCheckSize);
        Gizmos.DrawWireCube(rightWallDetector.position, wallCheckSize);
    }
}