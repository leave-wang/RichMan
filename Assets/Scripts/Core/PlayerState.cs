using UnityEngine;

public class PlayerState : MonoBehaviour
{
    [Header("Player Info")]
    public string playerName = "Player";
    public bool isAI = false;

    [Header("Game State")]
    public int money = 1000;
    public int currentTileIndex = 0;
    public bool isBankrupt = false;

    public void AddMoney(int amount)
    {
        money += amount;
    }

    public void SpendMoney(int amount)
    {
        money -= amount;
    }
}