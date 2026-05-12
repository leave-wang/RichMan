using System.Collections.Generic;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    [Header("Prefab & Layout")]
    public GameObject tilePrefab;
    public int tileCount = 20;
    public float radius = 8f;
    public Vector3 tileOffset = Vector3.zero;

    [Header("Generated tiles")]
    public List<Transform> tiles = new List<Transform>();

    [Header("Type Rules")]
    public int startReward = 200;
    public int taxAmount = 100;
    public int propertyPrice = 200;
    public int propertyRent = 50;
    public int taxEveryN = 5;

    [Header("Card Tile Positions")]
    public int[] cardTileIndexes = new int[] { 3, 8, 13, 18 };

    [Header("Colors")]
    public Color startColor    = new Color(0.2f, 0.9f, 0.2f, 1f);
    public Color taxColor      = new Color(0.95f, 0.25f, 0.25f, 1f);
    public Color propertyColor = new Color(0.2f, 0.6f, 1f, 1f);
    public Color cardColor     = new Color(0.7f, 0.3f, 1f, 1f);
    public Color emptyColor    = new Color(0.85f, 0.85f, 0.85f, 1f);

    // ─────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────

    private void Start()
    {
        ClearGeneratedTiles();
        GenerateTiles();
    }

#if UNITY_EDITOR
    [ContextMenu("Generate (Clear + Create)")]
    private void EditorGenerate()
    {
        ClearGeneratedTiles();
        GenerateTiles();
    }

    [ContextMenu("Clear Generated Tiles")]
    private void EditorClear()
    {
        ClearGeneratedTiles();
    }
#endif

    // ─────────────────────────────────────────
    // Procedural Board Generation
    // Places tiles in a circle using cos/sin math
    // ─────────────────────────────────────────

    private void GenerateTiles()
    {
        if (tilePrefab == null)
        {
            Debug.LogError("TileManager: tilePrefab is not assigned!");
            return;
        }

        tiles.Clear();

        for (int i = 0; i < tileCount; i++)
        {
            // Calculate position on circle
            float angle = i * Mathf.PI * 2f / tileCount;
            Vector3 pos = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius + tileOffset;

            GameObject tileObj = Instantiate(tilePrefab, pos, Quaternion.identity, transform);
            tileObj.name = $"Tile_{i:00}";

            Tile tile = tileObj.GetComponent<Tile>();
            if (tile == null)
            {
                Debug.LogError("TileManager: tilePrefab is missing Tile component!");
                Destroy(tileObj);
                continue;
            }

            SetTileType(tile, i);
            ApplyColor(tile);
            CreateTileLabel(tileObj, tile, i);

            tiles.Add(tileObj.transform);
        }
    }

    // ─────────────────────────────────────────
    // Assign tile type based on index
    // ─────────────────────────────────────────

    private void SetTileType(Tile tile, int index)
    {
        tile.ClearOwner();

        if (index == 0)
        {
            tile.tileType = TileType.Start;
            tile.value = startReward;
        }
        else if (IsCardTile(index))
        {
            tile.tileType = TileType.Card;
            tile.value = 0;
        }
        else if (taxEveryN > 0 && index % taxEveryN == 0)
        {
            tile.tileType = TileType.Tax;
            tile.value = taxAmount;
        }
        else
        {
            tile.tileType = TileType.Property;
            tile.value = 0;
            tile.price = propertyPrice;
            tile.rent = propertyRent;
        }
    }

    private bool IsCardTile(int index)
    {
        if (cardTileIndexes == null) return false;
        foreach (int cardIndex in cardTileIndexes)
        {
            if (index == cardIndex) return true;
        }
        return false;
    }

    // ─────────────────────────────────────────
    // Apply color based on tile type
    // ─────────────────────────────────────────

    public void ApplyColor(Tile tile)
    {
        if (tile == null) return;
        Renderer r = tile.GetComponent<Renderer>();
        if (r == null) return;

        switch (tile.tileType)
        {
            case TileType.Start:    r.material.color = startColor;    break;
            case TileType.Tax:      r.material.color = taxColor;      break;
            case TileType.Property: r.material.color = propertyColor; break;
            case TileType.Card:     r.material.color = cardColor;     break;
            default:                r.material.color = emptyColor;    break;
        }
    }

    // ─────────────────────────────────────────
    // Create floating 3D text label above each tile
    // Shows tile type, price, and index number
    // ─────────────────────────────────────────

    private void CreateTileLabel(GameObject tileObj, Tile tile, int index)
    {
        // Create empty child object for the label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(tileObj.transform);

        // Position above tile surface
        labelObj.transform.localPosition = new Vector3(0, 0.6f, 0);

        // Rotate so text faces upward and is readable from above
        labelObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

        labelObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        // Add TextMesh component for 3D text
        TextMesh tm = labelObj.AddComponent<TextMesh>();
        tm.fontSize = 14;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = Color.white;

        // Set label text based on tile type
        switch (tile.tileType)
        {
            case TileType.Start:
                tm.text = "START\n+$" + startReward;
                break;
            case TileType.Tax:
                tm.text = "TAX\n-$" + taxAmount;
                break;
            case TileType.Property:
                tm.text = "P" + index + "\n$" + propertyPrice;
                break;
            case TileType.Card:
                tm.text = "EVENT\nCARD";
                break;
            default:
                tm.text = "?";
                break;
        }
    }

    // ─────────────────────────────────────────
    // Update tile label after property is purchased
    // Called by TurnManager when a player buys a property
    // ─────────────────────────────────────────

    public void UpdateTileLabel(int tileIndex, string ownerName, bool isAI)
    {
        if (tileIndex < 0 || tileIndex >= tiles.Count) return;

        Transform labelTransform = tiles[tileIndex].Find("Label");
        if (labelTransform == null) return;

        TextMesh tm = labelTransform.GetComponent<TextMesh>();
        if (tm == null) return;

        // Show owner name on the tile label
        tm.text = "P" + tileIndex + "\n[" + ownerName + "]";

        // Yellow for human player, orange for AI
        tm.color = isAI ? new Color(1f, 0.6f, 0f) : Color.yellow;
    }

    // ─────────────────────────────────────────
    // Clear all generated tiles from the scene
    // ─────────────────────────────────────────

    private void ClearGeneratedTiles()
    {
        tiles.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }
}