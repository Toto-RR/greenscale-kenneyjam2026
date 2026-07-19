using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using DG.Tweening;
using Unity.Cinemachine;
using System;
using System.Collections.Generic;

public class LayerChanger : MonoBehaviour
{
    public static LayerChanger Instance { get; private set; }

    [Header("Player Reference")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private CapsuleCollider2D playerCollider;
    [SerializeField] private float[] playerScales = { 0.5f, 1f, 2f };

    [Header("Cam Reference")]
    [SerializeField] private CinemachineCamera vCam;
    [SerializeField] private float[] zoomSizes = { 5f, 8f, 10f };

    [Header("Layer Parents")]
    [SerializeField] private Transform[] layerGroups; // 0 -> big layer / 1 -> medium layer / 2 -> little layer
    [SerializeField] private string[] layerSortingLayerNames;

    [Header("Physics Layers")]
    [SerializeField] private string[] dimensionLayerNames;
    [SerializeField] private string[] pushableLayerNames;
    [SerializeField] private string playerLayerName = "Player";

    [Header("Fit Check")]
    [SerializeField] private LayerMask floorMask;
    [SerializeField, Range(0.5f, 1f)] private float fitCheckShrink = 0.9f;

    [Header("Colors")]
    [SerializeField]
    private Color[] activeColors =
    {
        new Color(0.10f, 0.20f, 0.55f),
        new Color(0.25f, 0.45f, 0.95f),
        new Color(0.65f, 0.80f, 1.00f)
    };
    [Range(0f, 1f)]
    [SerializeField] private float ghostAlpha = 0.2f;

    public float GhostAlpha => ghostAlpha;

    [Header("Game feel")]
    [SerializeField] private Vector3 punchVector = new(0, 0, 1);
    [SerializeField] private float punchDuration = 0.2f;
    [SerializeField] private float transitionDuration = 0.25f;

    public int layerIndex = -1;

    public float TransitionDuration => transitionDuration;

    public bool debug = true;

    private TilemapRenderer[][] renderersPerLayer;
    private TilemapCollider2D[][] collidersPerLayer;

    private Dictionary<int, int> sortingLayerIndexById;
    private int[] dimensionLayerIds;
    private int[] pushableLayerIds;
    private int playerLayerId;

    private InputSystem_Actions controls;
    private float lastScrollValue = 0f;

    // --- EVENTS ---
    public event Action<int> OnLayerChanged;
    public event Action OnLayerChangeBlocked;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Ya existe un LayerChanger en la escena, destruyendo duplicado.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        renderersPerLayer = new TilemapRenderer[layerGroups.Length][];
        collidersPerLayer = new TilemapCollider2D[layerGroups.Length][];

        for (int i = 0; i < layerGroups.Length; i++)
        {
            renderersPerLayer[i] = layerGroups[i].GetComponentsInChildren<TilemapRenderer>(true);
            collidersPerLayer[i] = layerGroups[i].GetComponentsInChildren<TilemapCollider2D>(true);
        }


        sortingLayerIndexById = new Dictionary<int, int>(layerSortingLayerNames.Length);
        for (int i = 0; i < layerSortingLayerNames.Length; i++)
        {
            string layerName = layerSortingLayerNames[i];

            if (!SortingLayerExists(layerName))
            {
                Debug.LogError($"That Sorting Layer doesnt exist!");
                continue;
            }

            int id = SortingLayer.NameToID(layerName);
            sortingLayerIndexById[id] = i;
        }


        dimensionLayerIds = new int[dimensionLayerNames.Length];
        for (int i = 0; i < dimensionLayerNames.Length; i++)
        {
            int id = LayerMask.NameToLayer(dimensionLayerNames[i]);
            if (id == -1)
                Debug.LogError($"LayerChanger: Physics Layer '{dimensionLayerNames[i]}' doesnt exitsz");
            dimensionLayerIds[i] = id;
        }

        playerLayerId = LayerMask.NameToLayer(playerLayerName);

        dimensionLayerIds = new int[dimensionLayerNames.Length];
        for (int i = 0; i < dimensionLayerNames.Length; i++)
            dimensionLayerIds[i] = LayerMask.NameToLayer(dimensionLayerNames[i]);

        pushableLayerIds = new int[pushableLayerNames.Length]; // NUEVO
        for (int i = 0; i < pushableLayerNames.Length; i++)
        {
            int id = LayerMask.NameToLayer(pushableLayerNames[i]);
            if (id == -1)
                Debug.LogError($"LayerChanger: Physics Layer '{pushableLayerNames[i]}' doesnt exist.");
            pushableLayerIds[i] = id;
        }

        playerLayerId = LayerMask.NameToLayer(playerLayerName);
    }

    private bool SortingLayerExists(string layerName)
    {
        foreach (var layer in SortingLayer.layers)
        {
            if (layer.name == layerName) return true;
        }
        return false;
    }

    private void Start()
    {
        controls = playerMovement.Controls;

        layerIndex = 1;
        ApplyLayerState(true);
        OnLayerChanged?.Invoke(layerIndex);
    }

    void Update()
    {
        float scroll = controls.Player.ScrollLayer.ReadValue<float>();

        if (scroll > 0f && lastScrollValue <= 0f)
            ChangeLayer(layerIndex + 1);
        else if (scroll < 0f && lastScrollValue >= 0f)
            ChangeLayer(layerIndex - 1);

        lastScrollValue = scroll;
    }

    private void ChangeLayer(int newIndex)
    {
        newIndex = Mathf.Clamp(newIndex, 0, layerGroups.Length - 1);
        if (newIndex == layerIndex) return;

        if (!CanFitInLayer(newIndex))
        {
            OnLayerChangeBlocked?.Invoke();
            return;
        }

        bool zoomIn = newIndex > layerIndex;
        layerIndex = newIndex;
        ApplyLayerState(zoomIn);
        OnLayerChanged?.Invoke(newIndex);
    }

    private bool CanFitInLayer(int newIndex)
    {
        float currentScale = playerScales[layerIndex];
        float targetScale = playerScales[newIndex];

        if (targetScale <= currentScale) return true;

        float feetY = playerCollider.bounds.min.y + 0.02f;

        Vector2 size = playerCollider.size * targetScale * fitCheckShrink;
        Vector2 point = new Vector2(player.position.x, feetY + size.y * 0.5f);

        LayerMask checkMask = floorMask
            | (1 << dimensionLayerIds[newIndex])
            | (1 << pushableLayerIds[newIndex]);

        Collider2D hit = Physics2D.OverlapCapsule(point, size, playerCollider.direction, 0f, checkMask);
        return hit == null;
    }

    private void ApplyLayerState(bool zoomIn)
    {
        DOTween.Kill(vCam, true);
        DOTween.Kill(vCam.transform, true);

        DOTween.To(
            () => vCam.Lens.OrthographicSize,
            size =>
            {
                var lens = vCam.Lens;
                lens.OrthographicSize = size;
                vCam.Lens = lens;
            },
            zoomSizes[layerIndex],
            transitionDuration
        ).SetId(vCam);

        float punchDirection = zoomIn ? 1f : -1f;
        vCam.transform.DOPunchRotation(punchVector * punchDirection, punchDuration);

        player.DOScale(playerScales[layerIndex], transitionDuration);

        for (int i = 0; i < layerGroups.Length; i++)
        {
            bool isActive = (i == layerIndex);

            Color baseColor = activeColors[i];
            Color finalColor = isActive
                ? baseColor
                : new Color(baseColor.r, baseColor.g, baseColor.b, ghostAlpha);

            foreach (var renderer in renderersPerLayer[i])
                renderer.material.DOColor(finalColor, transitionDuration);
            
            Physics2D.IgnoreLayerCollision(playerLayerId, dimensionLayerIds[i], !isActive);
            Physics2D.IgnoreLayerCollision(playerLayerId, pushableLayerIds[i], !isActive);
        }
    }

    public Color[] GetActiveColors => activeColors;

    public void SetSpriteOnLayer(SpriteRenderer renderer)
    {
        if (renderer == null) return;

        int index = GetLayerIndexFromSortingLayerId(renderer.sortingLayerID);
        if (index < 0)
        {
            Debug.Log($"SpriteRenderer '{renderer.name}' has a non-registered Sorting Layer!");
            return;
        }

        bool isActive = index == layerIndex;

        Color baseColor = activeColors[index];
        renderer.color = isActive
            ? baseColor
            : new Color(baseColor.r, baseColor.g, baseColor.b, ghostAlpha);
    }

    public int GetPhysicsLayerId(int dimensionIndex) => dimensionLayerIds[dimensionIndex];
    public int GetPushableLayerId(int dimensionIndex) => pushableLayerIds[dimensionIndex];

    public int GetLayerFromSortingLayer(SortingLayer layer)
    {
        return GetLayerIndexFromSortingLayerId(layer.id);
    }

    public int GetLayerIndexFromSortingLayerId(int sortingLayerId)
    {
        return sortingLayerIndexById.TryGetValue(sortingLayerId, out int index) ? index : -1;
    }

    private void OnDrawGizmosSelected()
    {
        if (player == null || playerCollider == null) return;

        Gizmos.color = Color.yellow;
        float targetScale = playerScales[Mathf.Clamp(layerIndex + 1, 0, playerScales.Length - 1)];
        float feetY = playerCollider.bounds.min.y + 0.02f;
        Vector2 size = playerCollider.size * targetScale * fitCheckShrink;
        Vector2 point = new Vector2(player.position.x, feetY + size.y * 0.5f);
        Gizmos.DrawWireCube(point, size);
    }
}