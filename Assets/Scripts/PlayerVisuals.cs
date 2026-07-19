using DG.Tweening;
using System.Security.Cryptography;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class PlayerVisuals : MonoBehaviour
{
    [Header("Player Movement")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("References")]
    [SerializeField] private LayerChanger layerChanger;
    [SerializeField] private Animator animator;
    [SerializeField] SpriteRenderer spriteRenderer;

    [Header("Wall Jump Spin")]
    [SerializeField] private float spinDuration = 0.4f;
    [SerializeField] private TrailRenderer spinTrail;

    [Header("Sliding")]
    [SerializeField] private float slidingTiltAngle = 25f;
    [SerializeField] private float slidingTiltDuration = 0.15f;
    public bool isSliding = false;

    [Header("Blocked Feedback")]
    [SerializeField] private Color blockedFlashColor = Color.red;
    [SerializeField] private float blockedFlashDuration = 0.15f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem dustFX;
    [SerializeField] private ParticleSystem slideFX;

    [Header("Layer Change FX")]
    [SerializeField] private float flipDuration = 0.45f;
    [SerializeField] private ParticleSystem rippleFX;

    private bool isSpinning = false;
    private bool facingLeft = false;
    private bool grounded = false;
    private Color playerColor;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int JumpHash = Animator.StringToHash("jump");
    private static readonly int IsGroundedHash = Animator.StringToHash("onGround");

    private void OnEnable()
    {
        playerMovement.OnMoving += HandleMoving;
        playerMovement.OnJump += HandleJump;
        playerMovement.OnGround += HandleGround;
        playerMovement.OnWallJump += HandleWallJump;
        playerMovement.OnSlidingWall += HandleSlidingWall;
        playerMovement.OnEndSlide += ResetSlidingTilt;

        layerChanger.OnLayerChanged += ApplyLayer;
        layerChanger.OnLayerChangeBlocked += HandleLayerChangeBlocked;
    }

    private void OnDisable()
    {
        playerMovement.OnMoving -= HandleMoving;
        playerMovement.OnJump -= HandleJump;
        playerMovement.OnGround -= HandleGround;
        playerMovement.OnWallJump -= HandleWallJump;
        playerMovement.OnSlidingWall -= HandleSlidingWall;
        playerMovement.OnEndSlide -= ResetSlidingTilt;

        layerChanger.OnLayerChanged -= ApplyLayer;
        layerChanger.OnLayerChangeBlocked -= HandleLayerChangeBlocked;
    }

    private void Start()
    {
        facingLeft = spriteRenderer.flipX; // If flipX is false -> player is facing right
        spinTrail.emitting = false;
    }

    private void HandleMoving(int direction)
    {
        Flip(direction);
        animator.SetInteger(SpeedHash, Mathf.RoundToInt(direction));
    }

    private void HandleJump()
    {
        dustFX.Play();
        animator.SetTrigger(JumpHash);
    }

    private void HandleGround(bool isGrounded)
    {
        animator.SetBool(IsGroundedHash, isGrounded);
        grounded = isGrounded;
    }

    private void ApplyLayer(int layer)
    {
        playerColor = layerChanger.GetActiveColors[layer];
        spriteRenderer.DOColor(new Color(playerColor.r, playerColor.g, playerColor.b, 1f), layerChanger.TransitionDuration);

        transform.DOKill(true);
        transform.DOPunchRotation(new Vector3(0, 360, 0), flipDuration);

        var rippleMain = rippleFX.main;
        rippleMain.startColor = new Color(playerColor.r, playerColor.g, playerColor.b, 0.5f);
        rippleFX.Play();

        var main = dustFX.main;
        main.startColor = playerColor;
    }

    private void Flip(int direction)
    {
        if (direction > 0 && facingLeft || direction < 0 && !facingLeft)
        {
            facingLeft = !facingLeft;

            spriteRenderer.flipX = facingLeft;
            if (grounded)
            {
                var vel = dustFX.velocityOverLifetime;
                vel.enabled = true;
                vel.x = new ParticleSystem.MinMaxCurve(-direction * 0.2f);
                dustFX.Play();
            }
        }
    }

    private void HandleWallJump(int wallSide)
    {
        isSpinning = true; // NUEVO

        float spinAngle = wallSide < 0 ? -360f : 360f;

        spinTrail.startColor = playerColor;
        spinTrail.emitting = true;

        transform.DOKill(); 
        transform.DORotate(new Vector3(0f, 0f, spinAngle), spinDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.OutQuad)
            .OnKill(() =>
            {
                transform.rotation = Quaternion.identity;
                spinTrail.emitting = false;
                isSpinning = false;
            });
    }

    private void HandleSlidingWall(int wallSide)
    {
        if (isSliding) return;

        isSliding = true;
        float targetAngle = wallSide < 0 ? -slidingTiltAngle : slidingTiltAngle;

        slideFX.Play();

        transform.DOKill();
        transform.DOLocalRotate(new Vector3(0f, 0f, targetAngle), slidingTiltDuration);
    }

    private void ResetSlidingTilt()
    {
        if (!isSliding) return;

        isSliding = false;
        slideFX.Stop();

        if (isSpinning) return;

        transform.DOKill();
        transform.DOLocalRotate(Vector3.zero, slidingTiltDuration);
    }

    private void HandleLayerChangeBlocked()
    {
        spriteRenderer.DOKill();

        Sequence flash = DOTween.Sequence();
        flash.Append(spriteRenderer.DOColor(blockedFlashColor, blockedFlashDuration * 0.5f));
        flash.Append(spriteRenderer.DOColor(playerColor, blockedFlashDuration * 0.5f)); 

        transform.DOShakePosition(blockedFlashDuration * 2f, strength: 0.08f, vibrato: 20);
    }
}
