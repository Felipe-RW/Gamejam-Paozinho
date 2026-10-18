using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GamePhase { Setup, PlayerTurn, Resolving, BossTurn, Won, Lost }

public class GameManager : MonoBehaviour
{
    [Header("Chefe")]
    public int bossMaxHealth = 24;
    public int bossAttack = 16;

    [Header("Regras")]
    public int suitBonus = 2;          // somado quando o naipe da carta combina com a classe do slot
    public int slotsPerPlayer = 3;

    [Header("Cartas")]
    public List<CardData> allCards = new();   // as 40 cartas (A-10 x 4 naipes)

    public GamePhase Phase { get; private set; } = GamePhase.Setup;
    public int BossHealth { get; private set; }
    public IReadOnlyList<PlayerState> Players => players;
    public PlayerState CurrentPlayer => players[currentIndex];
    public int CardsPlayedThisTurn { get; private set; }

    // Ganchos para a UI.
    public event Action OnStateChanged;
    public event Action<PlayerState, CardData, int> OnCardPlaced;   // dono do slot, carta, indice do slot
    public event Action<PlayerState, EffectType, int> OnEffectApplied;
    public event Action<int> OnBossAttack;
    public event Action<GamePhase> OnPhaseChanged;

    readonly List<PlayerState> players = new();
    Deck deck;
    int currentIndex;

    // Chamado pelo menu depois que cada jogador escolheu a classe (2 a 4).
    public void StartGame(List<ClassData> chosenClasses)
    {
        if (chosenClasses.Count < 2 || chosenClasses.Count > 4)
        {
            Debug.LogError("O jogo precisa de 2 a 4 jogadores.");
            return;
        }

        players.Clear();
        foreach (var c in chosenClasses) players.Add(new PlayerState(c, slotsPerPlayer));

        BossHealth = bossMaxHealth;
        deck = new Deck(allCards);
        foreach (var p in players) p.hand.FillFrom(deck);

        currentIndex = 0;
        BeginTurn();
    }

    void SetPhase(GamePhase phase)
    {
        Phase = phase;
        OnPhaseChanged?.Invoke(phase);
        OnStateChanged?.Invoke();
    }

    void BeginTurn()
    {
        CardsPlayedThisTurn = 0;
        CurrentPlayer.hand.FillFrom(deck);
        SetPhase(GamePhase.PlayerTurn);
    }

    // Coloca uma carta da mao do jogador atual no slot de qualquer jogador (inclusive o proprio).
    public bool PlayCard(CardData card, int targetPlayerIndex, int slotIndex = -1)
    {
        if (Phase != GamePhase.PlayerTurn) return false;
        if (targetPlayerIndex < 0 || targetPlayerIndex >= players.Count) return false;
        if (!CurrentPlayer.hand.Cards.Contains(card)) return false;

        var target = players[targetPlayerIndex];
        if (!target.IsAlive) return false;

        if (slotIndex < 0) slotIndex = target.FreeSlotIndex();
        if (slotIndex < 0 || slotIndex >= target.slots.Length || target.slots[slotIndex] != null) return false;

        CurrentPlayer.hand.Remove(card);
        target.slots[slotIndex] = card;
        CardsPlayedThisTurn++;

        OnCardPlaced?.Invoke(target, card, slotIndex);
        OnStateChanged?.Invoke();
        return true;
    }

    // Termina a vez do jogador atual. Depois do ultimo jogador, resolve a rodada.
    public void EndTurn()
    {
        if (Phase != GamePhase.PlayerTurn) return;

        int next = NextAliveIndex(currentIndex);
        if (next >= 0)
        {
            currentIndex = next;
            BeginTurn();
        }
        else
        {
            ResolveRound();
        }
    }

    int NextAliveIndex(int from)
    {
        for (int i = from + 1; i < players.Count; i++)
            if (players[i].IsAlive) return i;
        return -1; // sem proximo: fim da rodada
    }

    void ResolveRound()
    {
        SetPhase(GamePhase.Resolving);

        foreach (var owner in players)
        {
            for (int i = 0; i < owner.slots.Length; i++)
            {
                var card = owner.slots[i];
                if (card == null) continue;

                int amount = card.value + (card.suit == owner.classData.suit ? suitBonus : 0);
                ApplyEffect(owner, amount);
                deck.Discard(card);
                owner.slots[i] = null;
            }
        }

        if (BossHealth <= 0) { SetPhase(GamePhase.Won); return; }
        BossAttackPhase();
    }

    // Ataque: tira vida do chefe. Cura e escudo: aplicados em todos os jogadores vivos.
    void ApplyEffect(PlayerState owner, int amount)
    {
        switch (owner.classData.effect)
        {
            case EffectType.Attack:
                BossHealth = Mathf.Max(0, BossHealth - amount);
                break;
            case EffectType.Heal:
                foreach (var p in players) p.Heal(amount);
                break;
            case EffectType.Shield:
                foreach (var p in players) if (p.IsAlive) p.shield += amount;
                break;
        }
        OnEffectApplied?.Invoke(owner, owner.classData.effect, amount);
    }

    void BossAttackPhase()
    {
        SetPhase(GamePhase.BossTurn);

        foreach (var p in players)
        {
            if (!p.IsAlive) continue;
            p.TakeDamage(bossAttack);
        }
        OnBossAttack?.Invoke(bossAttack);

        // Escudo dura so uma rodada.
        foreach (var p in players) p.shield = 0;

        if (!players.Exists(p => p.IsAlive)) { SetPhase(GamePhase.Lost); return; }

        currentIndex = NextAliveFromStart();
        BeginTurn();
    }

    int NextAliveFromStart()
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsAlive) return i;
        return 0;
    }
}
