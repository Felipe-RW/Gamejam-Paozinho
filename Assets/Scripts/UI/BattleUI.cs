using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUI : MonoBehaviour
{
    [Header("Jogo")]
    public GameManager gameManager;

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

    [Header("Teste (remover quando existir a selecao de classe)")]
    public bool startOnPlay = true;
    public List<ClassData> debugClasses = new();

    CardData selected;

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
            int playerIndex = i;
            panels[i].HandCardClicked += OnHandCardClicked;
            panels[i].SlotClicked += slot => OnSlotClicked(playerIndex, slot);
        }

        endTurnButton.onClick.AddListener(() =>
        {
            selected = null;
            gameManager.EndTurn();
        });

        if (startOnPlay) gameManager.StartGame(debugClasses);
    }

    void OnHandCardClicked(CardView view)
    {
        selected = selected == view.Data ? null : view.Data;
        Refresh();
    }

    void OnSlotClicked(int playerIndex, int slotIndex)
    {
        if (selected == null) return;
        if (gameManager.PlayCard(selected, playerIndex, slotIndex)) selected = null;
    }

    void Refresh()
    {
        var players = gameManager.Players;

        for (int i = 0; i < panels.Length; i++)
        {
            bool exists = i < players.Count;
            panels[i].gameObject.SetActive(exists);
            if (exists) panels[i].Refresh(players[i], players[i] == gameManager.CurrentPlayer, selected);
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
