using UnityEngine;

public class PushableBox : MonoBehaviour
{
    [Header("Obstacle References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private new Collider2D collider;

    [Header("Layer Required")]
    [SerializeField] private LayerChanger layerChanger;

    [Header("Physics")]
    [SerializeField] private float mass = 5f;
    [SerializeField] private float linearDrag = 4f;

    private Rigidbody2D rb;
    private int requiredSortingLayer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.mass = mass;
        rb.linearDamping = linearDrag;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();
        if (!collider) collider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        requiredSortingLayer = layerChanger.GetLayerIndexFromSortingLayerId(spriteRenderer.sortingLayerID);
        gameObject.layer = layerChanger.GetPushableLayerId(requiredSortingLayer);
    }

    private void OnEnable()
    {
        layerChanger.OnLayerChanged += ApplyLayer;
    }

    private void OnDisable()
    {
        layerChanger.OnLayerChanged -= ApplyLayer;
    }

    private void ApplyLayer(int layer)
    {
        layerChanger.SetSpriteOnLayer(spriteRenderer);
    }
}