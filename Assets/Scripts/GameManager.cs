using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum GamePhase { Setup, PlayerTurn, Resolving, BossTurn, Won, Lost }

public class GameManager : MonoBehaviour
{
    [Header("Chefe")]
    public BossData boss;
    [Range(0, 2)] public int difficulty;

    [Header("Regras")]
    public int suitBonus = 2;
    public int slotsPerPlayer = 3;

    [Header("Cartas")]
    public List<CardData> allCards = new();

    public GamePhase Phase { get; private set; } = GamePhase.Setup;
    public int BossHealth { get; private set; }
    public BossData Boss => boss;
    public int BossMaxHealth => boss.healthByDifficulty[Mathf.Clamp(difficulty, 0, boss.healthByDifficulty.Length - 1)];
    public int BossBlock { get; private set; }
    public int BossThorns { get; private set; }
    public int BossArmor { get; private set; }
    public IReadOnlyList<PlayerState> Players => players;
    public PlayerState CurrentPlayer => players[currentIndex];
    public int CardsPlayedThisTurn { get; private set; }

    public event Action OnStateChanged;
    public event Action<PlayerState, CardData, int> OnCardPlaced;
    public event Action<PlayerState, Suit, int> OnEffectApplied;
    public event Action<BossAttack> OnBossAttack;
    public event Action<GamePhase> OnPhaseChanged;

    readonly List<PlayerState> players = new();
    int currentIndex;
    int lastBossAttack;

    public void StartGame(List<ClassData> chosenClasses)
    {
        if (chosenClasses.Count < 1 || chosenClasses.Count > 4)
        {
            Debug.LogError("O jogo precisa de 1 a 4 jogadores.");
            return;
        }

        players.Clear();
        int handLimit = HandLimitFor(chosenClasses.Count);
        foreach (var c in chosenClasses)
            players.Add(new PlayerState(c, slotsPerPlayer, handLimit, new Deck(allCards)));

        BossHealth = BossMaxHealth;
        BossArmor = boss.startingArmor;
        BossBlock = BossArmor;
        BossThorns = 0;
        lastBossAttack = -1;
        foreach (var p in players) p.hand.FillFrom(p.deck);

        currentIndex = 0;
        BeginTurn();
    }

    public static int HandLimitFor(int playerCount) => 9 - playerCount;

    void SetPhase(GamePhase phase)
    {
        Phase = phase;
        OnPhaseChanged?.Invoke(phase);
        OnStateChanged?.Invoke();
    }

    void BeginTurn()
    {
        CardsPlayedThisTurn = 0;
        CurrentPlayer.hand.FillFrom(CurrentPlayer.deck);
        SetPhase(GamePhase.PlayerTurn);
    }

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
        target.slotPlayedBy[slotIndex] = currentIndex;
        CardsPlayedThisTurn++;

        OnCardPlaced?.Invoke(target, card, slotIndex);
        OnStateChanged?.Invoke();
        return true;
    }

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
        return -1;
    }

    void ResolveRound()
    {
        SetPhase(GamePhase.Resolving);

        for (int o = 0; o < players.Count; o++)
        {
            var owner = players[o];
            for (int i = 0; i < owner.slots.Length; i++)
            {
                var card = owner.slots[i];
                if (card == null) continue;

                var player = players[owner.slotPlayedBy[i]];
                int amount = card.value + (card.suit == player.classData.suit ? suitBonus : 0);
                ApplySuitEffect(o, owner.slotPlayedBy[i], card.suit, amount);

                player.deck.Discard(card);
                owner.slots[i] = null;
            }
        }

        if (BossHealth <= 0) { SetPhase(GamePhase.Won); return; }
        BossAttackPhase();
    }

    void ApplySuitEffect(int ownerIndex, int playedBy, Suit suit, int amount)
    {
        var owner = players[ownerIndex];
        switch (suit)
        {
            case Suit.Clubs:
                owner.block += amount;
                break;
            case Suit.Spades:
                DamageBoss(amount, playedBy);
                break;
            case Suit.Hearts:
                owner.Heal(amount);
                break;
            case Suit.Diamonds:
                DrawClockwise(ownerIndex, amount);
                break;
        }
        OnEffectApplied?.Invoke(owner, suit, amount);
    }

    void DamageBoss(int amount, int attackerIndex)
    {
        int absorbed = Mathf.Min(BossBlock, amount);
        BossBlock -= absorbed;
        BossHealth = Mathf.Max(0, BossHealth - (amount - absorbed));

        if (BossThorns > 0) players[attackerIndex].TakeDamage(BossThorns);
    }

    void DrawClockwise(int startIndex, int count)
    {
        while (count > 0)
        {
            bool drewAny = false;
            for (int k = 0; k < players.Count && count > 0; k++)
            {
                var p = players[(startIndex + k) % players.Count];
                if (!p.IsAlive || !p.hand.DrawOne(p.deck)) continue;
                count--;
                drewAny = true;
            }
            if (!drewAny) return;
        }
    }

    void BossAttackPhase()
    {
        SetPhase(GamePhase.BossTurn);

        BossBlock = BossArmor;

        var attack = PickBossAttack();
        if (attack != null)
        {
            for (int h = 0; h < attack.hits && attack.damage > 0; h++)
                foreach (var p in players)
                    if (p.IsAlive) p.TakeDamage(attack.damage);

            BossBlock += attack.gainBlock;
            BossThorns += attack.gainThorns;
            BossArmor += attack.gainArmor;
            OnBossAttack?.Invoke(attack);
        }

        foreach (var p in players) p.block = 0;

        if (!players.Exists(p => p.IsAlive)) { SetPhase(GamePhase.Lost); return; }

        currentIndex = NextAliveFromStart();
        BeginTurn();
    }

    BossAttack PickBossAttack()
    {
        var list = boss.attacks;
        if (list.Count == 0) return null;

        int next;
        if (boss.fixedOrder)
        {
            next = (lastBossAttack + 1) % list.Count;
        }
        else
        {
            do next = UnityEngine.Random.Range(0, list.Count);
            while (list.Count > 1 && next == lastBossAttack);
        }
        lastBossAttack = next;
        return list[next];
    }

    int NextAliveFromStart()
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsAlive) return i;
        return 0;
    }
}
