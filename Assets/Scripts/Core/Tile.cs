using UnityEngine;

public enum TileType
{
    Empty,
    Start,
    Tax,
    Property,
    Card
}

public class Tile : MonoBehaviour
{
    [Header("Type")]
    public TileType tileType = TileType.Empty;

    [Header("Value")]
    // Start / Tax / Card can use this value if needed
    public int value = 0;

    [Header("Property")]
    public int price = 200;
    public int rent = 50;

    [Header("Ownership")]
    public bool isOwned = false;
    public int ownerPlayerIndex = -1;

    public void SetOwner(int playerIndex)
    {
        isOwned = true;
        ownerPlayerIndex = playerIndex;
    }

    public void ClearOwner()
    {
        isOwned = false;
        ownerPlayerIndex = -1;
    }
}