using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TurnManager : MonoBehaviour
{
    [Header("Camera")]
    public CameraFollow cameraFollow;

    [Header("Board")]
    public TileManager tileManager;

    [Header("Players in turn order")]
    public List<PlayerState> players = new List<PlayerState>();

    [Header("Dice")]
    public int minDice = 1;
    public int maxDice = 6;

    [Header("UI")]
    public TMP_Text diceText;
    public TMP_Text turnText;
    public TMP_Text moneyText;
    public EventUI eventUI;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TMP_Text winnerText;

    [Header("Pass Start Rule")]
    public int passStartReward = 200;

    [Header("Ownership Colors")]
    public Color humanOwnedColor = new Color(0.25f, 0.75f, 1f, 1f);
    public Color aiOwnedColor    = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("AI Settings")]
    public float aiRollDelay      = 0.7f;
    public float aiDecisionDelay  = 0.45f;

    private int currentPlayerIndex = 0;
    private bool waitingForDecision = false;
    private bool gameOver = false;
    private Tile pendingPropertyTile;
    private Coroutine aiRoutine;


    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        UpdateUI();

        if (cameraFollow != null && players.Count > 0)
            cameraFollow.SetTarget(players[0].transform);

        StartTurnAutoIfAI();
    }

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!IsCurrentPlayerAI() && !waitingForDecision && !gameOver)
                TryRollDice();
        }
    }

    private bool IsCurrentPlayerAI()
    {
        if (players == null || players.Count == 0) return false;
        var p = players[currentPlayerIndex];
        return p != null && p.isAI && !p.isBankrupt;
    }

    private bool IsHuman(PlayerState p) => p != null && !p.isAI;

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

        int dice = Random.Range(minDice, maxDice + 1);
        if (diceText) diceText.text = $"Dice: {dice}";

        waitingForDecision = true;

        mover.MoveSteps(
            steps: dice,
            getCurrentIndex: () => p.currentTileIndex,
            setCurrentIndex: (idx) => p.currentTileIndex = idx,
            onPassStart: () =>
            {
                p.money += passStartReward;
                UpdateUI();
            },
            onFinish: () =>
            {
                HandleLanding(p, p.currentTileIndex);
                UpdateUI();
            }
        );
    }


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

        switch (tile.tileType)
        {
            case TileType.Start:
                player.money += tile.value;
                ShowOK($"START!\n+${tile.value} bonus!");
                break;

            case TileType.Tax:
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

        if (IsCurrentPlayerAI())
        {
            if (aiRoutine != null) StopCoroutine(aiRoutine);
            aiRoutine = StartCoroutine(AI_DecideAndAutoClick());
        }
    }

    private void HandleProperty(PlayerState player, Tile tile)
    {
        if (!tile.isOwned)
        {
            pendingPropertyTile = tile;
            ShowBuy($"Property\nPrice: ${tile.price}  Rent: ${tile.rent}\n\nDo you want to buy?");
            return;
        }

        if (tile.ownerPlayerIndex == currentPlayerIndex)
        {
            ShowOK("This is your property!");
            return;
        }

        int rent = tile.rent;
        player.money -= rent;
        if (tile.ownerPlayerIndex >= 0 && tile.ownerPlayerIndex < players.Count)
        {
            var owner = players[tile.ownerPlayerIndex];
            if (owner != null && !owner.isBankrupt) owner.money += rent;
        }
        ShowOK($"Pay rent: ${rent}\nto {players[tile.ownerPlayerIndex].playerName}");
    }


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

    private bool AI_ShouldBuy(PlayerState ai, Tile t)
    {
        int afterBuy = ai.money - t.price;
        if (afterBuy < 200) return false;
        return true;
    }

    private int GetHumanMoney()
    {
        int best = 0;
        foreach (var p in players)
            if (p != null && IsHuman(p) && !p.isBankrupt)
                best = Mathf.Max(best, p.money);
        return best;
    }


    private void OnEventOK()   => EndTurn();
    private void OnEventSkip() => EndTurn();

    private void OnEventBuy()
    {
        PlayerState p = players[currentPlayerIndex];
        if (pendingPropertyTile != null && !pendingPropertyTile.isOwned && p.money >= pendingPropertyTile.price)
        {
            p.money -= pendingPropertyTile.price;
            pendingPropertyTile.SetOwner(currentPlayerIndex);
            PaintOwnedTile(pendingPropertyTile, p.isAI);
        }
        EndTurn();
    }

    private void EndTurn()
    {
        pendingPropertyTile  = null;
        waitingForDecision   = false;

        currentPlayerIndex = GetNextAlivePlayerIndex(currentPlayerIndex);

        if (cameraFollow != null && players.Count > 0)
            cameraFollow.SetTarget(players[currentPlayerIndex].transform);

        UpdateUI();

        if (CheckWinCondition()) return;

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

    private void UpdateUI()
    {
        if (players == null || players.Count == 0) return;

        PlayerState p = players[currentPlayerIndex];
        if (p == null) return;

        if (turnText != null)
            turnText.text = $"{(p.isAI ? "AI" : "Player")} Turn: {p.playerName}";

        if (moneyText != null)
        {
            string allMoney = "";
            foreach (var player in players)
            {
                if (player == null) continue;
                string tag = player.isBankrupt ? " (Bankrupt)" : "";
                allMoney += $"{player.playerName}: ${player.money}{tag}\n";
            }
            moneyText.text = allMoney.TrimEnd();
        }
    }

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

    private void CheckBankrupt(PlayerState p)
    {
        if (p == null || p.isBankrupt) return;
        if (p.money < 0)
        {
            p.money    = 0;
            p.isBankrupt = true;
            Debug.Log($"{p.playerName} is BANKRUPT!");
        }
    }
}