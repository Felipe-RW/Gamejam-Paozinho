using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUI : MonoBehaviour
{
    [Header("Jogo")]
    public GameManager gameManager;
    public LayoutPresets layoutPresets;

    [Header("Jogadores (na ordem P0..P3)")]
    public PlayerPanelView[] panels;

    [Header("Chefe")]
    public Slider bossHealthBar;
    public TMP_Text bossHealthText;
    public Image bossSprite;
    public TMP_Text bossArmorText;
    public TMP_Text bossBlockText;
    public TMP_Text bossThornsText;

    [Header("HUD")]
    public Button endTurnButton;

    [Header("Teste: classes dos jogadores (vazio = ignorado)")]
    public bool startOnPlay = true;
    public ClassData[] seatClasses = new ClassData[4];

    CardData selected;
    readonly List<int> seatOfPlayer = new();

    void OnEnable()
    {
        gameManager.OnStateChanged += Refresh;
    }

    void OnDisable()
    {
        gameManager.OnStateChanged -= Refresh;
    }

    void Start()
    {
        for (int i = 0; i < panels.Length; i++)
        {
            int seat = i;
            panels[i].HandCardClicked += OnHandCardClicked;
            panels[i].SlotClicked += slot => OnSlotClicked(seat, slot);
        }

        endTurnButton.onClick.AddListener(() =>
        {
            selected = null;
            gameManager.EndTurn();
        });

        if (!startOnPlay) return;

        var source = GameSession.HasSelection ? GameSession.SeatClasses : seatClasses;
        GameSession.HasSelection = false;

        var chosen = new List<ClassData>();
        seatOfPlayer.Clear();
        for (int seat = 0; seat < source.Length && chosen.Count < panels.Length; seat++)
        {
            if (source[seat] == null) continue;
            chosen.Add(source[seat]);
            seatOfPlayer.Add(chosen.Count - 1);
        }
        if (layoutPresets != null) layoutPresets.Apply(chosen.Count);
        gameManager.StartGame(chosen);
    }

    void OnHandCardClicked(CardView view)
    {
        selected = selected == view.Data ? null : view.Data;
        Refresh();
    }

    void OnSlotClicked(int seat, int slotIndex)
    {
        if (selected == null) return;
        int playerIndex = seatOfPlayer.IndexOf(seat);
        if (playerIndex < 0) return;
        if (gameManager.PlayCard(selected, playerIndex, slotIndex)) selected = null;
    }

    void Refresh()
    {
        var players = gameManager.Players;

        for (int seat = 0; seat < panels.Length; seat++)
        {
            int playerIndex = seatOfPlayer.IndexOf(seat);
            bool exists = playerIndex >= 0 && playerIndex < players.Count;
            panels[seat].SetVisible(exists);
            if (exists) panels[seat].Refresh(players[playerIndex], players[playerIndex] == gameManager.CurrentPlayer, selected);
        }

        var boss = gameManager.Boss;
        if (bossSprite != null) bossSprite.sprite = boss.sprite;

        bossHealthBar.maxValue = gameManager.BossMaxHealth;
        bossHealthBar.value = gameManager.BossHealth;
        if (bossHealthText != null)
            bossHealthText.text = $"{gameManager.BossHealth}/{gameManager.BossMaxHealth}";

        if (bossArmorText != null) bossArmorText.text = gameManager.BossArmor.ToString();
        if (bossBlockText != null) bossBlockText.text = gameManager.BossBlock.ToString();
        if (bossThornsText != null) bossThornsText.text = gameManager.BossThorns.ToString();

        endTurnButton.interactable = gameManager.Phase == GamePhase.PlayerTurn;

        if (gameManager.Phase == GamePhase.Won) Debug.Log("Vitoria!");
        else if (gameManager.Phase == GamePhase.Lost) Debug.Log("Derrota");
    }
}
