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
    public List<BossData> bosses = new();
    public int healthPerLap = 10;
    public int armorPerLap = 2;
    public float bossTransitionSeconds = 1.5f;
    public float bossRollSeconds = 2f;
    public float dealSeconds = 0.25f;
    public float bossBulletSeconds = 0.8f;
    public float bossHitPause = 0.6f;

    [Header("Ondas de inimigos (antes de cada chefe)")]
    [Tooltip("Tipos de inimigo sorteados nas ondas. Vazio = sem ondas, vai direto ao chefe.")]
    public List<BossData> enemyTypes = new();
    [Tooltip("Quantidade de inimigos em cada onda, em ordem. Depois da ultima onda vem o chefe.")]
    public List<int> waveSizes = new() { 2, 3 };
    [Tooltip("Inimigos a mais por onda a cada volta completa pelos chefes.")]
    public int extraEnemiesPerLap = 1;
    public int maxEnemiesPerWave = 5;
    public int enemyHealthPerLap = 5;
    public int enemyArmorPerLap = 1;
    [Tooltip("Desligado = os inimigos entram na ordem da lista Enemy Types (usado pelo teaser).")]
    public bool randomWaveEnemies = true;

    // Ganchos opcionais (usados pelo teaser). Nulos = comportamento normal do jogo.
    // healthOverride: vida fixa para o inimigo/chefe (<= 0 ignora). attackOverride: ataque fixo da rodada (null ignora).
    public Func<BossData, int> healthOverride;
    public Func<EnemyState, BossAttack> attackOverride;

    [Header("Jogadores caidos")]
    [Tooltip("Vida com que o jogador caido volta na proxima onda/chefe (1 = vida cheia, 0.5 = metade).")]
    [Range(0.05f, 1f)] public float reviveHealthFraction = 1f;
    [Tooltip("Pausa para o efeito de volta antes da nova rodada.")]
    public float reviveSeconds = 0.8f;

    [Header("Menu")]
    public GameObject menuPanel;
    public GameObject defeatPanel;
    public string menuScene = "Menu";

    [Header("Regras")]
    public int suitBonus = 2;
    public int slotsPerPlayer = 3;

    [Header("Cartas")]
    public List<CardData> allCards = new();

    public GamePhase Phase { get; private set; } = GamePhase.Setup;
    public int BossIndex { get; private set; }
    public int Lap { get; private set; }
    // 0..waveSizes.Count-1 = ondas; waveSizes.Count = chefe.
    public int WaveIndex { get; private set; }
    public int WaveCount => waveSizes.Count;
    public bool IsBossStage => WaveIndex >= waveSizes.Count;
    public BossData Boss => bosses[BossIndex];
    public EnemyState BossEnemy => IsBossStage && enemies.Count > 0 ? enemies[0] : null;
    public IReadOnlyList<EnemyState> Enemies => enemies;
    public int AliveEnemyCount => enemies.Count(e => e.IsAlive);
    // Inimigo atacando agora (durante o turno dos inimigos).
    public EnemyState AttackingEnemy { get; private set; }
    public IReadOnlyList<PlayerState> Players => players;
    public PlayerState CurrentPlayer => players[currentIndex];
    public int CardsPlayedThisTurn { get; private set; }

    public event Action OnStateChanged;
    public event Action<PlayerState, CardData, int> OnCardPlaced;
    public event Action<PlayerState, Suit, int> OnEffectApplied;
    public event Action<PlayerState, CardData> OnCardDealStart;
    // (jogador, dano que passou, dano absorvido pelo bloqueio)
    public event Action<PlayerState, int, int> OnPlayerDamaged;
    public event Action<GamePhase> OnPhaseChanged;
    // (jogador, vida com que voltou)
    public event Action<PlayerState, int> OnPlayerRevived;

    public event Action OnEnemyMovesChosen;
    public event Action OnStageCleared;
    public event Action OnStageSpawned;
    public event Action<EnemyState, BossAttack> OnEnemyAttackStart;
    public event Action OnEnemyAttackEnd;
    public event Action<EnemyState> OnEnemyDefense;
    public event Action<EnemyState, int, int> OnEnemyHit;
    public event Action<EnemyState> OnEnemyDefeated;

    readonly List<PlayerState> players = new();
    readonly List<EnemyState> enemies = new();
    int currentIndex;

    public void StartGame(List<ClassData> chosenClasses)
    {
        if (chosenClasses.Count < 1 || chosenClasses.Count > 4)
        {
            Debug.LogError("O jogo precisa de 1 a 4 jogadores.");
            return;
        }

        if (bosses.Count == 0)
        {
            Debug.LogError("Adicione pelo menos um chefe na lista Bosses do GameManager.");
            return;
        }

        if (enemyTypes.Count == 0)
            Debug.LogWarning("Lista Enemy Types do GameManager vazia: as ondas serao puladas. Use Game > Generate Enemies.");

        players.Clear();
        int handLimit = HandLimitFor(chosenClasses.Count);
        foreach (var c in chosenClasses)
            players.Add(new PlayerState(c, slotsPerPlayer, handLimit, new Deck(allCards)));

        BossIndex = 0;
        Lap = 0;
        WaveIndex = 0;
        SpawnStage();

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
        if (defeatPanel != null) defeatPanel.SetActive(phase == GamePhase.Lost);
        OnPhaseChanged?.Invoke(phase);
        OnStateChanged?.Invoke();
    }

    int HealthFor(BossData data)
    {
        var table = data.healthByPlayerCount;
        if (table == null || table.Length == 0) return 1;
        return table[Mathf.Clamp(players.Count - 1, 0, table.Length - 1)];
    }

    int WaveSize(int wave) =>
        Mathf.Clamp(waveSizes[wave] + Lap * extraEnemiesPerLap, 0, Mathf.Max(1, maxEnemiesPerWave));

    void SpawnStage()
    {
        // Pula ondas vazias ou sem tipos de inimigo configurados.
        while (!IsBossStage && (enemyTypes.Count == 0 || WaveSize(WaveIndex) <= 0))
            WaveIndex++;

        enemies.Clear();
        AttackingEnemy = null;

        if (IsBossStage)
        {
            var boss = Boss;
            enemies.Add(new EnemyState(boss, true,
                OverrideHealth(boss, HealthFor(boss) + Lap * healthPerLap),
                boss.startingArmor + Lap * armorPerLap));
            return;
        }

        int count = WaveSize(WaveIndex);
        for (int i = 0; i < count; i++)
        {
            var data = randomWaveEnemies
                ? enemyTypes[UnityEngine.Random.Range(0, enemyTypes.Count)]
                : enemyTypes[i % enemyTypes.Count];
            enemies.Add(new EnemyState(data, false,
                OverrideHealth(data, HealthFor(data) + Lap * enemyHealthPerLap),
                data.startingArmor + Lap * enemyArmorPerLap));
        }
    }

    int OverrideHealth(BossData data, int health)
    {
        if (healthOverride == null) return health;
        int forced = healthOverride(data);
        return forced > 0 ? forced : health;
    }

    // Encerra a fase atual como se todos os inimigos tivessem caido (corte de cena do teaser).
    // So funciona na vez de um jogador, quando nenhuma rotina do jogo esta rodando.
    public void ForceClearStage()
    {
        if (Phase != GamePhase.PlayerTurn) return;
        foreach (var e in enemies) e.health = 0;
        StartCoroutine(NextStageRoutine());
    }

    IEnumerator NextStageRoutine()
    {
        SetPhase(GamePhase.BossTurn);
        OnStageCleared?.Invoke();
        yield return new WaitForSeconds(bossTransitionSeconds);

        if (IsBossStage)
        {
            WaveIndex = 0;
            BossIndex++;
            if (BossIndex >= bosses.Count)
            {
                BossIndex = 0;
                Lap++;
            }
        }
        else
        {
            WaveIndex++;
        }

        SpawnStage();
        foreach (var p in players) p.block = 0;
        OnStageSpawned?.Invoke();

        // Quem caiu na fase anterior volta para a nova onda/chefe, mantendo a mao.
        if (RevivePlayers()) yield return new WaitForSeconds(reviveSeconds);
        yield return EnsureHands(); // quem voltou sem cartas recebe uma mao nova

        StartCoroutine(BeginRound());
    }

    bool RevivePlayers()
    {
        bool any = false;
        foreach (var p in players)
        {
            if (p.IsAlive) continue;
            int amount = Mathf.Max(1, Mathf.CeilToInt(p.classData.maxHealth * reviveHealthFraction));
            p.health = Mathf.Min(p.classData.maxHealth, amount);
            p.block = 0;
            any = true;
            OnPlayerRevived?.Invoke(p, p.health);
        }
        if (any) OnStateChanged?.Invoke();
        return any;
    }

    IEnumerator EnsureHands()
    {
        bool any = false;
        foreach (var p in players)
        {
            if (!p.IsAlive || p.hand.Cards.Count > 0) continue;

            if (!any)
            {
                any = true;
                SetPhase(GamePhase.Dealing);
            }

            var gold = p.deck.TakeMatching(c => c.suit == Suit.Diamonds && c.value > 4);
            if (gold == null)
            {
                var options = allCards.FindAll(c => c.suit == Suit.Diamonds && c.value > 4);
                if (options.Count > 0) gold = options[UnityEngine.Random.Range(0, options.Count)];
            }
            if (gold != null) yield return DealCard(p, gold);

            for (int i = 0; i < 2; i++)
            {
                var card = p.deck.Draw();
                if (card == null) break;
                yield return DealCard(p, card);
            }
        }
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

        yield return EnsureHands();
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

        foreach (var e in enemies)
        {
            if (!e.IsAlive) continue;
            var forced = attackOverride?.Invoke(e);
            if (forced != null) e.currentAttack = forced;
            else e.PickAttack();
            e.block = e.armor;
        }
        OnEnemyMovesChosen?.Invoke();

        currentIndex = NextAliveFromStart();
        BeginTurn();
    }

    void BeginTurn()
    {
        CardsPlayedThisTurn = 0;
        SetPhase(GamePhase.PlayerTurn);
    }

    // Espadas precisam de um inimigo alvo quando ha mais de um inimigo vivo.
    public bool NeedsTarget(CardData card) => card != null && card.suit == Suit.Spades && AliveEnemyCount > 1;

    public int FirstAliveEnemy()
    {
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i].IsAlive) return i;
        return -1;
    }

    // Alvo escolhido se ainda vivo; senao o proximo inimigo vivo da fileira.
    int ResolveEnemyTarget(int index)
    {
        if (enemies.Count == 0) return -1;
        index = Mathf.Clamp(index, 0, enemies.Count - 1);
        for (int k = 0; k < enemies.Count; k++)
        {
            int i = (index + k) % enemies.Count;
            if (enemies[i].IsAlive) return i;
        }
        return -1;
    }

    public bool PlayCard(CardData card, int targetPlayerIndex, int slotIndex = -1, int enemyTarget = -1)
    {
        if (Phase != GamePhase.PlayerTurn) return false;
        if (targetPlayerIndex < 0 || targetPlayerIndex >= players.Count) return false;
        if (!CurrentPlayer.hand.Cards.Contains(card)) return false;

        var target = players[targetPlayerIndex];
        if (!target.IsAlive) return false;

        if (card.suit == Suit.Spades)
        {
            bool valid = enemyTarget >= 0 && enemyTarget < enemies.Count && enemies[enemyTarget].IsAlive;
            if (!valid)
            {
                if (NeedsTarget(card)) return false;
                enemyTarget = FirstAliveEnemy();
            }
        }

        if (slotIndex < 0) slotIndex = target.FreeSlotIndex();
        if (slotIndex < 0 || slotIndex >= target.slots.Length || target.slots[slotIndex] != null) return false;

        CurrentPlayer.hand.Remove(card);
        target.slots[slotIndex] = card;
        target.slotPlayedBy[slotIndex] = currentIndex;
        target.slotTarget[slotIndex] = enemyTarget;
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

    // Valor que a carta do slot vai aplicar (valor + bonus de naipe de quem jogou).
    public int SlotAmount(PlayerState owner, int slotIndex)
    {
        var card = owner.slots[slotIndex];
        if (card == null) return 0;
        var player = players[owner.slotPlayedBy[slotIndex]];
        return card.value + (card.suit == player.classData.suit ? suitBonus : 0);
    }

    IEnumerator ResolveRoundRoutine()
    {
        SetPhase(GamePhase.Resolving);
        foreach (var e in enemies)
            if (e.IsAlive && e.ApplyDefense()) OnEnemyDefense?.Invoke(e);

        for (int o = 0; o < players.Count; o++)
        {
            var owner = players[o];
            for (int i = 0; i < owner.slots.Length; i++)
            {
                var card = owner.slots[i];
                if (card == null) continue;

                int playedBy = owner.slotPlayedBy[i];
                int amount = SlotAmount(owner, i);
                players[playedBy].deck.Discard(card);
                owner.slots[i] = null;
                OnStateChanged?.Invoke();

                ApplySuitEffect(o, playedBy, card.suit, amount, owner.slotTarget[i]);
                if (card.suit == Suit.Diamonds) yield return DrawRoutine(o, amount);
            }
        }

        // Espinhos podem derrubar todos durante a resolucao: derrota, mesmo que a fase tenha acabado.
        if (!players.Exists(p => p.IsAlive)) { SetPhase(GamePhase.Lost); yield break; }

        if (AliveEnemyCount == 0)
        {
            StartCoroutine(NextStageRoutine());
            yield break;
        }
        StartCoroutine(EnemiesAttackRoutine());
    }

    void ApplySuitEffect(int ownerIndex, int playedBy, Suit suit, int amount, int enemyTarget)
    {
        var owner = players[ownerIndex];
        switch (suit)
        {
            case Suit.Clubs:
                owner.block += amount;
                break;
            case Suit.Spades:
                int target = ResolveEnemyTarget(enemyTarget);
                if (target >= 0) DamageEnemy(enemies[target], amount, playedBy);
                break;
            case Suit.Hearts:
                owner.Heal(amount);
                break;
        }
        OnEffectApplied?.Invoke(owner, suit, amount);
    }

    void DamageEnemy(EnemyState enemy, int amount, int attackerIndex)
    {
        int dealt = enemy.TakeDamage(amount, out int absorbed);
        OnEnemyHit?.Invoke(enemy, dealt, absorbed);

        if (enemy.thorns > 0)
        {
            int absorbedThorns = players[attackerIndex].TakeDamage(enemy.thorns);
            OnPlayerDamaged?.Invoke(players[attackerIndex], enemy.thorns - absorbedThorns, absorbedThorns);
        }

        if (!enemy.IsAlive) OnEnemyDefeated?.Invoke(enemy);
        OnStateChanged?.Invoke();
    }

    IEnumerator EnemiesAttackRoutine()
    {
        SetPhase(GamePhase.BossTurn);

        bool attacked = false;
        foreach (var enemy in enemies)
        {
            var attack = enemy.currentAttack;
            if (!enemy.IsAlive || attack == null || attack.damage <= 0) continue;
            if (!players.Exists(p => p.IsAlive)) break;

            attacked = true;
            AttackingEnemy = enemy;
            for (int h = 0; h < attack.hits; h++)
            {
                OnEnemyAttackStart?.Invoke(enemy, attack);
                yield return new WaitForSeconds(bossBulletSeconds);

                foreach (var p in players)
                {
                    if (!p.IsAlive) continue;
                    int absorbed = p.TakeDamage(attack.damage);
                    OnPlayerDamaged?.Invoke(p, attack.damage - absorbed, absorbed);
                }
                OnStateChanged?.Invoke();
                yield return new WaitForSeconds(bossHitPause);
            }
        }
        AttackingEnemy = null;
        if (attacked) OnEnemyAttackEnd?.Invoke();

        foreach (var p in players) p.block = 0;

        if (!players.Exists(p => p.IsAlive)) { SetPhase(GamePhase.Lost); yield break; }

        yield return EnsureHands();
        StartCoroutine(BeginRound());
    }

    int NextAliveFromStart()
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].IsAlive) return i;
        return 0;
    }
}
