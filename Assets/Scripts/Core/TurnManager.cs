using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TurnManager : MonoBehaviour
{
    // ─────────────────────────────────────────
    // Inspector References
    // ─────────────────────────────────────────

    [Header("Camera")]
    public CameraFollow cameraFollow;       // Camera that follows the current player

    [Header("Board")]
    public TileManager tileManager;         // Reference to the tile board manager

    [Header("Players in turn order")]
    public List<PlayerState> players = new List<PlayerState>();

    [Header("Dice")]
    public int minDice = 1;
    public int maxDice = 6;

    [Header("UI")]
    public TMP_Text diceText;               // Displays dice roll result
    public TMP_Text turnText;               // Displays whose turn it is
    public TMP_Text moneyText;              // Displays all players money
    public EventUI eventUI;                 // Reference to the event popup UI

    [Header("Game Over UI")]
    public GameObject gameOverPanel;        // Panel shown when game ends
    public TMP_Text winnerText;             // Displays winner name and money

    [Header("Pass Start Rule")]
    public int passStartReward = 200;       // Money earned when passing Start

    [Header("Ownership Colors")]
    public Color humanOwnedColor = new Color(0.25f, 0.75f, 1f, 1f);  // Blue for human player
    public Color aiOwnedColor    = new Color(1f, 0.85f, 0.2f, 1f);   // Yellow for AI

    [Header("AI Settings")]
    public float aiRollDelay     = 0.7f;    // Delay before AI rolls dice
    public float aiDecisionDelay = 0.45f;   // Delay before AI makes a decision

    // ─────────────────────────────────────────
    // Private State
    // ─────────────────────────────────────────

    private int currentPlayerIndex  = 0;
    private bool waitingForDecision = false;
    private bool gameOver           = false;
    private Tile pendingPropertyTile;
    private Coroutine aiRoutine;

    // ─────────────────────────────────────────
    // Unity Lifecycle
    // ─────────────────────────────────────────

    private void Start()
    {
        // Hide game over panel at start
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        UpdateUI();

        // Camera starts following the first player
        if (cameraFollow != null && players.Count > 0)
            cameraFollow.SetTarget(players[0].transform);

        StartTurnAutoIfAI();
    }

    private void Update()
    {
        // Human player rolls dice by pressing Space
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!IsCurrentPlayerAI() && !waitingForDecision && !gameOver)
                TryRollDice();
        }
    }

    // ─────────────────────────────────────────
    // AI Turn Control
    // ─────────────────────────────────────────

    private bool IsCurrentPlayerAI()
    {
        if (players == null || players.Count == 0) return false;
        var p = players[currentPlayerIndex];
        return p != null && p.isAI && !p.isBankrupt;
    }

    private bool IsHuman(PlayerState p) => p != null && !p.isAI;

    // Automatically start AI turn after a short delay
    private void StartTurnAutoIfAI()
    {
        if (!IsCurrentPlayerAI()) return;
        if (aiRoutine != null) StopCoroutine(aiRoutine);
        aiRoutine = StartCoroutine(AI_RollAfterDelay());
    }

    private IEnumerator AI_RollAfterDelay()
    {
        yield return new WaitForSeconds(aiRollDelay);
        if (!waitingForDecision && !gameOver) TryRollDice();
    }

    // ─────────────────────────────────────────
    // Dice Roll and Movement
    // ─────────────────────────────────────────

    public void TryRollDice()
    {
        if (waitingForDecision || gameOver) return;

        if (tileManager == null || tileManager.tiles == null || tileManager.tiles.Count == 0)
        {
            Debug.LogError("TurnManager: tileManager.tiles is empty.");
            return;
        }

        PlayerState p = players[currentPlayerIndex];
        if (p == null || p.isBankrupt) { EndTurn(); return; }

        PlayerMover mover = p.GetComponent<PlayerMover>();
        if (mover == null) { Debug.LogError("PlayerMover missing!"); return; }
        if (mover.IsMoving) return;
        if (mover.tileManager == null) mover.tileManager = tileManager;

        // Roll random dice value between min and max
        int dice = Random.Range(minDice, maxDice + 1);
        if (diceText) diceText.text = $"Dice: {dice}";

        waitingForDecision = true;

        // Move the player the rolled number of steps
        mover.MoveSteps(
            steps: dice,
            getCurrentIndex: () => p.currentTileIndex,
            setCurrentIndex: (idx) => p.currentTileIndex = idx,
            onPassStart: () =>
            {
                // Award money for passing Start
                p.money += passStartReward;
                UpdateUI();
            },
            onFinish: () =>
            {
                // Handle the tile the player landed on
                HandleLanding(p, p.currentTileIndex);
                UpdateUI();
            }
        );
    }

    // ─────────────────────────────────────────
    // Tile Landing Logic
    // ─────────────────────────────────────────

    private void HandleLanding(PlayerState player, int tileIndex)
    {
        pendingPropertyTile = null;

        Transform tileTf = tileManager.tiles[tileIndex];
        Tile tile = tileTf.GetComponent<Tile>();
        if (tile == null)
        {
            waitingForDecision = false;
            StartTurnAutoIfAI();
            return;
        }

        // Handle different tile types
        switch (tile.tileType)
        {
            case TileType.Start:
                // Bonus for landing directly on Start
                player.money += tile.value;
                ShowOK($"START!\n+${tile.value} bonus!");
                break;

            case TileType.Tax:
                // Deduct tax amount from player
                player.money -= tile.value;
                ShowOK($"TAX!\nYou paid ${tile.value}");
                break;

            case TileType.Property:
                HandleProperty(player, tile);
                break;

            default:
                ShowOK("Nothing happens here.");
                break;
        }

        CheckBankrupt(player);

        // AI makes decisions automatically
        if (IsCurrentPlayerAI())
        {
            if (aiRoutine != null) StopCoroutine(aiRoutine);
            aiRoutine = StartCoroutine(AI_DecideAndAutoClick());
        }
    }

    // ─────────────────────────────────────────
    // Property Tile Logic
    // ─────────────────────────────────────────

    private void HandleProperty(PlayerState player, Tile tile)
    {
        if (!tile.isOwned)
        {
            // Offer to buy unowned property
            pendingPropertyTile = tile;
            ShowBuy($"Property\nPrice: ${tile.price}  Rent: ${tile.rent}\n\nDo you want to buy?");
            return;
        }

        if (tile.ownerPlayerIndex == currentPlayerIndex)
        {
            // Player owns this tile already
            ShowOK("This is your property!");
            return;
        }

        // Pay rent to the owner
        int rent = tile.rent;
        player.money -= rent;

        if (tile.ownerPlayerIndex >= 0 && tile.ownerPlayerIndex < players.Count)
        {
            var owner = players[tile.ownerPlayerIndex];
            if (owner != null && !owner.isBankrupt) owner.money += rent;
        }

        ShowOK($"Pay rent: ${rent}\nto {players[tile.ownerPlayerIndex].playerName}");
    }

    // ─────────────────────────────────────────
    // AI Decision Making
    // Rule-based AI: buys property if it can keep at least $200 reserve
    // ─────────────────────────────────────────

    private IEnumerator AI_DecideAndAutoClick()
    {
        yield return new WaitForSeconds(aiDecisionDelay);

        var ai = players[currentPlayerIndex];
        if (ai == null || ai.isBankrupt) { ForceClosePanel(); EndTurn(); yield break; }

        if (pendingPropertyTile != null && !pendingPropertyTile.isOwned)
        {
            if (AI_ShouldBuy(ai, pendingPropertyTile))
            {
                if (eventUI != null) eventUI.OnClickBuy(); else OnEventBuy();
            }
            else
            {
                if (eventUI != null) eventUI.OnClickSkip(); else OnEventSkip();
            }
            yield break;
        }

        if (eventUI != null) eventUI.OnClickOK(); else OnEventOK();
    }

    // AI buys property only if it keeps at least $200 after buying
    private bool AI_ShouldBuy(PlayerState ai, Tile t)
    {
        int afterBuy = ai.money - t.price;
        return afterBuy >= 200;
    }

    // ─────────────────────────────────────────
    // UI Event Callbacks
    // ─────────────────────────────────────────

    private void OnEventOK()   => EndTurn();
    private void OnEventSkip() => EndTurn();

    private void OnEventBuy()
    {
        PlayerState p = players[currentPlayerIndex];

        if (pendingPropertyTile != null && !pendingPropertyTile.isOwned && p.money >= pendingPropertyTile.price)
        {
            // Deduct purchase price from player
            p.money -= pendingPropertyTile.price;

            // Mark tile as owned
            pendingPropertyTile.SetOwner(currentPlayerIndex);

            // Change tile color to show ownership
            PaintOwnedTile(pendingPropertyTile, p.isAI);

            // Update the floating label on the tile to show owner name
            tileManager.UpdateTileLabel(p.currentTileIndex, p.playerName, p.isAI);
        }

        EndTurn();
    }

    // ─────────────────────────────────────────
    // Turn Management
    // ─────────────────────────────────────────

    private void EndTurn()
    {
        pendingPropertyTile  = null;
        waitingForDecision   = false;

        // Move to next alive player
        currentPlayerIndex = GetNextAlivePlayerIndex(currentPlayerIndex);

        // Switch camera to follow next player
        if (cameraFollow != null && players.Count > 0)
            cameraFollow.SetTarget(players[currentPlayerIndex].transform);

        UpdateUI();

        // Check if game should end
        if (CheckWinCondition()) return;

        // Auto trigger AI if it is their turn
        StartTurnAutoIfAI();
    }

    private int GetNextAlivePlayerIndex(int from)
    {
        int n = players.Count;
        for (int k = 1; k <= n; k++)
        {
            int idx = (from + k) % n;
            if (players[idx] != null && !players[idx].isBankrupt)
                return idx;
        }
        return from;
    }

    // ─────────────────────────────────────────
    // UI Update
    // ─────────────────────────────────────────

    private void UpdateUI()
    {
        if (players == null || players.Count == 0) return;

        PlayerState p = players[currentPlayerIndex];
        if (p == null) return;

        // Show whose turn it is
        if (turnText != null)
            turnText.text = $"{(p.isAI ? "AI" : "Player")} Turn: {p.playerName}";

        // Show all players money
        if (moneyText != null)
        {
            string allMoney = "";
            foreach (var player in players)
            {
                if (player == null) continue;
                string status = player.isBankrupt ? " (Bankrupt)" : "";
                allMoney += $"{player.playerName}: ${player.money}{status}\n";
            }
            moneyText.text = allMoney.TrimEnd();
        }
    }

    // ─────────────────────────────────────────
    // Win Condition
    // Game ends when only one player is not bankrupt
    // ─────────────────────────────────────────

    private bool CheckWinCondition()
    {
        List<PlayerState> alive = new List<PlayerState>();
        foreach (var p in players)
            if (p != null && !p.isBankrupt) alive.Add(p);

        if (alive.Count <= 1)
        {
            gameOver = true;
            PlayerState winner = alive.Count == 1 ? alive[0] : players[0];

            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (winnerText != null)
                winnerText.text = $"Winner!\n{winner.playerName}\n${winner.money}";

            Debug.Log($"GAME OVER! Winner: {winner.playerName}");
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────
    // Helper Methods
    // ─────────────────────────────────────────

    private void ShowOK(string msg)
    {
        if (eventUI == null) { OnEventOK(); return; }
        eventUI.ShowOK(msg, OnEventOK);
    }

    private void ShowBuy(string msg)
    {
        if (eventUI == null) { OnEventSkip(); return; }
        eventUI.ShowBuy(msg, OnEventBuy, OnEventSkip);
    }

    // Change tile color to show who owns it
    private void PaintOwnedTile(Tile tile, bool ownerIsAI)
    {
        if (tile == null) return;
        Renderer r = tile.GetComponent<Renderer>();
        if (r == null) return;
        r.material.color = ownerIsAI ? aiOwnedColor : humanOwnedColor;
    }

    private void ForceClosePanel()
    {
        if (eventUI != null && eventUI.panel != null)
            eventUI.panel.SetActive(false);
    }

    // Check if player has gone bankrupt
    private void CheckBankrupt(PlayerState p)
    {
        if (p == null || p.isBankrupt) return;
        if (p.money < 0)
        {
            p.money      = 0;
            p.isBankrupt = true;
            Debug.Log($"{p.playerName} is BANKRUPT!");
        }
    }
}