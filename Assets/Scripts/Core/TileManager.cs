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
    public Color startColor = new Color(0.2f, 0.9f, 0.2f, 1f);
    public Color taxColor = new Color(0.95f, 0.25f, 0.25f, 1f);
    public Color propertyColor = new Color(0.2f, 0.6f, 1f, 1f);
    public Color cardColor = new Color(0.7f, 0.3f, 1f, 1f);
    public Color emptyColor = new Color(0.85f, 0.85f, 0.85f, 1f);

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

            tiles.Add(tileObj.transform);
        }
    }

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
            if (index == cardIndex)
            {
                return true;
            }
        }

        return false;
    }

    public void ApplyColor(Tile tile)
    {
        if (tile == null) return;

        Renderer r = tile.GetComponent<Renderer>();
        if (r == null) return;

        switch (tile.tileType)
        {
            case TileType.Start:
                r.material.color = startColor;
                break;

            case TileType.Tax:
                r.material.color = taxColor;
                break;

            case TileType.Property:
                r.material.color = propertyColor;
                break;

            case TileType.Card:
                r.material.color = cardColor;
                break;

            default:
                r.material.color = emptyColor;
                break;
        }
    }

    private void ClearGeneratedTiles()
    {
        tiles.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(child.gameObject);
            }
            else
            {
                Destroy(child.gameObject);
            }
#else
            Destroy(child.gameObject);
#endif
        }
    }
}