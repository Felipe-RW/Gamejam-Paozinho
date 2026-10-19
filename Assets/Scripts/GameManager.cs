using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GamePhase { Setup, Dealing, BossRoll, PlayerTurn, Resolving, BossTurn, Won, Lost }

public class GameManager : MonoBehaviour
{
    [Header("Chefe")]
    public BossData boss;
    public float bossRollSeconds = 2f;
    public float dealSeconds = 0.25f;
    public float bossBulletSeconds = 0.8f;
    public float bossHitPause = 0.6f;

    [Header("Menu")]
    public GameObject menuPanel;
    public string menuScene = "Menu";

    [Header("Regras")]
    public int suitBonus = 2;
    public int slotsPerPlayer = 3;

    [Header("Cartas")]
    public List<CardData> allCards = new();

    public GamePhase Phase { get; private set; } = GamePhase.Setup;
    public int BossHealth { get; private set; }
    public BossData Boss => boss;
    public int BossMaxHealth => boss.healthByPlayerCount[Mathf.Clamp(players.Count - 1, 0, boss.healthByPlayerCount.Length - 1)];
    public int BossBlock { get; private set; }
    public int BossThorns { get; private set; }
    public int BossArmor { get; private set; }
    public BossAttack CurrentBossAttack { get; private set; }
    public IReadOnlyList<PlayerState> Players => players;
    public PlayerState CurrentPlayer => players[currentIndex];
    public int CardsPlayedThisTurn { get; private set; }

    public event Action OnStateChanged;
    public event Action<PlayerState, CardData, int> OnCardPlaced;
    public event Action<PlayerState, Suit, int> OnEffectApplied;
    public event Action<BossAttack> OnBossMoveChosen;
    public event Action<BossAttack> OnBossAttack;
    public event Action<PlayerState, CardData> OnCardDealStart;
    public event Action<BossAttack> OnBossAttackStart;
    public event Action OnBossAttackEnd;
    public event Action<BossAttack> OnBossDefense;
    public event Action<int, int> OnBossHit;
    public event Action<PlayerState, int> OnPlayerDamaged;
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
        BossBlock = 0;
        BossThorns = 0;
        CurrentBossAttack = null;
        lastBossAttack = -1;

        StopAllCoroutines();
        StartCoroutine(StartRoutine());
    }

    public static int HandLimitFor(int playerCount) => 9 - playerCount;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) ToggleMenu();
    }

    public void ToggleMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(!menuPanel.activeSelf);
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene(menuScene);
    }

    void SetPhase(GamePhase phase)
    {
        Phase = phase;
        OnPhaseChanged?.Invoke(phase);
        OnStateChanged?.Invoke();
    }

    IEnumerator StartRoutine()
    {
        SetPhase(GamePhase.Dealing);
        yield return null;

        foreach (var p in players)
            while (!p.hand.IsFull)
            {
                var card = p.deck.Draw();
                if (card == null) break;
                yield return DealCard(p, card);
            }

        StartCoroutine(BeginRound());
    }

    IEnumerator DealCard(PlayerState player, CardData card)
    {
        OnCardDealStart?.Invoke(player, card);
        yield return new WaitForSeconds(dealSeconds);
        player.hand.Add(card);
        OnStateChanged?.Invoke();
    }

    IEnumerator DrawRoutine(int startIndex, int count)
    {
        while (count > 0)
        {
            bool drewAny = false;
            for (int k = 0; k < players.Count && count > 0; k++)
            {
                var p = players[(startIndex + k) % players.Count];
                if (!p.IsAlive || p.hand.IsFull) continue;

                var card = p.deck.Draw();
                if (card == null) continue;

                yield return DealCard(p, card);
                count--;
                drewAny = true;
            }
            if (!drewAny) yield break;
        }
    }

    IEnumerator BeginRound()
    {
        SetPhase(GamePhase.BossRoll);
        yield return new WaitForSeconds(bossRollSeconds);

        CurrentBossAttack = PickBossAttack();
        BossBlock = BossArmor;
        OnBossMoveChosen?.Invoke(CurrentBossAttack);

        currentIndex = NextAliveFromStart();
        BeginTurn();
    }

    void BeginTurn()
    {
        CardsPlayedThisTurn = 0;
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

    public bool ReturnCard(int targetPlayerIndex, int slotIndex)
    {
        if (Phase != GamePhase.PlayerTurn) return false;
        if (targetPlayerIndex < 0 || targetPlayerIndex >= players.Count) return false;

        var target = players[targetPlayerIndex];
        if (slotIndex < 0 || slotIndex >= target.slots.Length) return false;

        var card = target.slots[slotIndex];
        if (card == null || target.slotPlayedBy[slotIndex] != currentIndex) return false;

        target.slots[slotIndex] = null;
        CurrentPlayer.hand.Add(card);
        CardsPlayedThisTurn = Mathf.Max(0, CardsPlayedThisTurn - 1);
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
            StartCoroutine(ResolveRoundRoutine());
        }
    }

    int NextAliveIndex(int from)
    {
        for (int i = from + 1; i < players.Count; i++)
            if (players[i].IsAlive) return i;
        return -1;
    }

    void ApplyBossDefense()
    {
        var move = CurrentBossAttack;
        if (move == null) return;

        BossArmor += move.gainArmor;
        BossBlock = BossArmor + move.gainBlock;
        BossThorns += move.gainThorns;

        if (move.gainBlock > 0 || move.gainArmor > 0) OnBossDefense?.Invoke(move);
    }

    IEnumerator ResolveRoundRoutine()
    {
        SetPhase(GamePhase.Resolving);
        ApplyBossDefense();

        for (int o = 0; o < players.Count; o++)
        {
            var owner = players[o];
            for (int i = 0; i < owner.slots.Length; i++)
            {
                var card = owner.slots[i];
                if (card == null) continue;

                var player = players[owner.slotPlayedBy[i]];
                int amount = card.value + (card.suit == player.classData.suit ? suitBonus : 0);
                player.deck.Discard(card);
                owner.slots[i] = null;
                OnStateChanged?.Invoke();

                ApplySuitEffect(o, owner.slotPlayedBy[i], card.suit, amount);
                if (card.suit == Suit.Diamonds) yield return DrawRoutine(o, amount);
            }
        }

        if (BossHealth <= 0) { SetPhase(GamePhase.Won); yield break; }
        StartCoroutine(BossAttackRoutine());
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
        }
        OnEffectApplied?.Invoke(owner, suit, amount);
    }

    void DamageBoss(int amount, int attackerIndex)
    {
        int absorbed = Mathf.Min(BossBlock, amount);
        BossBlock -= absorbed;
        BossHealth = Mathf.Max(0, BossHealth - (amount - absorbed));
        OnBossHit?.Invoke(amount - absorbed, absorbed);

        if (BossThorns > 0)
        {
            players[attackerIndex].TakeDamage(BossThorns);
            OnPlayerDamaged?.Invoke(players[attackerIndex], BossThorns);
        }
    }

    IEnumerator BossAttackRoutine()
    {
        SetPhase(GamePhase.BossTurn);

        var attack = CurrentBossAttack;
        if (attack != null && attack.damage > 0)
        {
            for (int h = 0; h < attack.hits; h++)
            {
                OnBossAttackStart?.Invoke(attack);
                yield return new WaitForSeconds(bossBulletSeconds);

                foreach (var p in players)
                {
                    if (!p.IsAlive) continue;
                    p.TakeDamage(attack.damage);
                    OnPlayerDamaged?.Invoke(p, attack.damage);
                }
                OnStateChanged?.Invoke();
                yield return new WaitForSeconds(bossHitPause);
            }
            OnBossAttackEnd?.Invoke();
        }

        if (attack != null) OnBossAttack?.Invoke(attack);

        foreach (var p in players) p.block = 0;

        if (!players.Exists(p => p.IsAlive)) { SetPhase(GamePhase.Lost); yield break; }

        StartCoroutine(BeginRound());
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
