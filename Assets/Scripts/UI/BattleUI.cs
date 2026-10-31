using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BattleUI : MonoBehaviour
{
    [Header("Jogo")]
    public GameManager gameManager;
    public LayoutPresets layoutPresets;

    [Header("Jogadores (na ordem P0..P3)")]
    public PlayerPanelView[] panels;

    [Header("Chefe")]
    [Tooltip("Objeto que agrupa toda a UI do chefe. Fica escondido durante as ondas. Vazio = esconde so os campos abaixo.")]
    public GameObject bossRoot;
    public Slider bossHealthBar;
    public TMP_Text bossHealthText;
    public Image bossSprite;
    public Image bossMoveImage;
    public TMP_Text bossMoveText;
    public TMP_Text bossArmorText;
    public TMP_Text bossBlockText;
    public TMP_Text bossThornsText;

    [Header("Inimigos das ondas")]
    [Tooltip("Area onde os inimigos ficam lado a lado. Vazio = criada no lugar do sprite do chefe.")]
    public RectTransform enemyRow;
    [Range(0.2f, 1f)] public float enemyRowWidth = 0.6f;
    public Vector2 enemyViewSize = new(180, 260);
    public float enemySpacing = 24f;
    [Tooltip("Opcional: mostra 'Onda 1/2' ou 'Chefe'.")]
    public TMP_Text waveText;

    [Header("HUD")]
    public Button endTurnButton;

    [Header("Teste: classes dos jogadores (vazio = ignorado)")]
    public bool startOnPlay = true;
    public ClassData[] seatClasses = new ClassData[4];

    CardData selected;
    readonly List<int> seatOfPlayer = new();
    readonly List<int> deviceOfPlayer = new();
    readonly List<EnemyView> enemyViews = new();
    readonly List<EnemyState> viewsBuiltFor = new();
    Coroutine spinning;
    int pendingPlayer = -1, pendingSlot = -1;
    bool bossVisible = true;

    public CardData Selected => selected;
    public IReadOnlyList<int> PlayerDevices => deviceOfPlayer;
    public Button EndTurnButton => endTurnButton;
    public IReadOnlyList<EnemyView> EnemyViews => enemyViews;

    // Carta de Espadas ja posicionada num slot, esperando o jogador escolher o inimigo.
    public bool IsChoosingTarget => selected != null && pendingSlot >= 0;
    // Espada selecionada que precisa de alvo (pode clicar direto no inimigo).
    public bool WantsTarget => selected != null && gameManager.NeedsTarget(selected);
    // Inimigo em foco pelo controle/teclado (-1 = nenhum).
    public int FocusedEnemy { get; set; } = -1;

    // Dispositivo dono do jogador da vez (None = sem restricao, ex.: teste no editor).
    public int CurrentDevice
    {
        get
        {
            var players = gameManager.Players;
            if (players.Count == 0) return PlayerInputs.None;
            for (int i = 0; i < players.Count && i < deviceOfPlayer.Count; i++)
                if (players[i] == gameManager.CurrentPlayer) return deviceOfPlayer[i];
            return PlayerInputs.None;
        }
    }

    bool MouseAllowed()
    {
        int dev = CurrentDevice;
        return dev == PlayerInputs.None || dev == PlayerInputs.KeyboardMouse;
    }

    void OnEnable()
    {
        gameManager.OnStateChanged += Refresh;
        gameManager.OnPhaseChanged += OnPhaseChanged;
        gameManager.OnEnemyMovesChosen += ShowMoves;
    }

    void OnDisable()
    {
        gameManager.OnStateChanged -= Refresh;
        gameManager.OnPhaseChanged -= OnPhaseChanged;
        gameManager.OnEnemyMovesChosen -= ShowMoves;
        spinning = null;
    }

    void Start()
    {
        for (int i = 0; i < panels.Length; i++)
        {
            int seat = i;
            panels[i].HandCardClicked += view => { if (MouseAllowed()) ActivateHandCard(view.Data); };
            panels[i].SlotClicked += slot => { if (MouseAllowed()) ActivateSlot(seatOfPlayer.IndexOf(seat), slot); };
        }

        endTurnButton.onClick.AddListener(() => { if (MouseAllowed()) EndTurnPressed(); });

        // Um componente de texto desmarcado no Inspector deixa o valor invisivel: garante que estejam ligados.
        foreach (var text in new[] { bossHealthText, bossMoveText, bossArmorText, bossBlockText, bossThornsText })
            if (text != null) text.enabled = true;

        // Navegacao do EventSystem faria o controle "clicar" no ultimo botao do mouse.
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
        gameObject.AddComponent<BattleFocus>().Init(this, gameManager);

        if (!startOnPlay) return;

        bool fromMenu = GameSession.HasSelection;
        var source = fromMenu ? GameSession.SeatClasses : seatClasses;
        GameSession.HasSelection = false;
        StartBattle(source, fromMenu);
    }

    // Comeca a partida com as classes de cada assento (vazio = assento livre).
    // Chamado no Start (startOnPlay) ou por quem controla a cena, como o teaser.
    public void StartBattle(ClassData[] source, bool fromMenu = false)
    {
        var chosen = new List<ClassData>();
        seatOfPlayer.Clear();
        deviceOfPlayer.Clear();
        for (int seat = 0; seat < source.Length && chosen.Count < panels.Length; seat++)
        {
            if (source[seat] == null) continue;
            chosen.Add(source[seat]);
            seatOfPlayer.Add(chosen.Count - 1);
            deviceOfPlayer.Add(fromMenu ? GameSession.SeatDevices[seat] : PlayerInputs.None);
        }
        if (layoutPresets != null) layoutPresets.Apply(chosen.Count);
        gameManager.StartGame(chosen);
    }

    public PlayerPanelView GetPanel(PlayerState player)
    {
        for (int i = 0; i < gameManager.Players.Count; i++)
        {
            if (gameManager.Players[i] != player) continue;
            if (i >= seatOfPlayer.Count || seatOfPlayer[i] >= panels.Length) return null;
            return panels[seatOfPlayer[i]];
        }
        return null;
    }

    public RectTransform BossAnchor => bossSprite != null ? bossSprite.rectTransform : null;

    public RectTransform GetEnemyAnchor(EnemyState enemy)
    {
        if (enemy == null || enemy.isBoss) return BossAnchor;
        foreach (var v in enemyViews)
            if (v != null && v.Enemy == enemy) return v.Anchor;
        return BossAnchor;
    }

    public Image GetEnemyImage(EnemyState enemy)
    {
        if (enemy == null || enemy.isBoss) return bossSprite;
        foreach (var v in enemyViews)
            if (v != null && v.Enemy == enemy) return v.SpriteImage;
        return null;
    }

    public Image GetPlayerPortrait(PlayerState player)
    {
        var panel = GetPanel(player);
        return panel != null ? panel.portrait : null;
    }

    public RectTransform GetPlayerAnchor(PlayerState player)
    {
        int playerIndex = -1;
        for (int i = 0; i < gameManager.Players.Count; i++)
            if (gameManager.Players[i] == player) playerIndex = i;

        if (playerIndex < 0 || playerIndex >= seatOfPlayer.Count) return null;
        int seat = seatOfPlayer[playerIndex];
        return seat < panels.Length ? panels[seat].EffectAnchor : null;
    }

    void OnPhaseChanged(GamePhase phase)
    {
        if (phase != GamePhase.PlayerTurn) ClearPending();
        if (phase != GamePhase.BossRoll) return;

        SyncEnemyViews();
        if (gameManager.IsBossStage)
        {
            if (spinning != null) StopCoroutine(spinning);
            spinning = StartCoroutine(SpinBossMove());
        }
        else
        {
            foreach (var v in enemyViews) v.StartSpin(gameManager.bossRollSeconds);
        }
    }

    IEnumerator SpinBossMove()
    {
        var attacks = gameManager.Boss.attacks;
        if (bossMoveText != null) bossMoveText.text = "...";
        if (attacks.Count == 0) yield break;

        float duration = Mathf.Max(0.01f, gameManager.bossRollSeconds);
        float elapsed = 0f;
        int i = 0;
        while (true)
        {
            if (bossMoveImage != null) bossMoveImage.sprite = attacks[i++ % attacks.Count].icon;
            float interval = Mathf.Lerp(0.06f, 0.25f, elapsed / duration);
            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }
    }

    void ShowMoves()
    {
        if (gameManager.IsBossStage)
        {
            ShowBossMove(gameManager.BossEnemy?.currentAttack);
            return;
        }
        foreach (var v in enemyViews) v.StopSpin();
        Refresh();
    }

    void ShowBossMove(BossAttack move)
    {
        if (spinning != null) StopCoroutine(spinning);
        spinning = null;
        if (move == null) return;

        if (bossMoveImage != null) bossMoveImage.sprite = move.icon;
        if (bossMoveText != null) bossMoveText.text = move.Describe();
    }

    // Filtro opcional de jogadas (tutorial do prologo). Nulo = tudo liberado.
    // (acao, indice do jogador dono do slot ou -1, indice do slot/inimigo ou -1, carta envolvida)
    public enum GuideAction { HandCard, Slot, Enemy, EndTurn }
    public System.Func<GuideAction, int, int, CardData, bool> actionFilter;

    bool Allowed(GuideAction action, int player, int index, CardData card) =>
        actionFilter == null || actionFilter(action, player, index, card);

    public void ActivateHandCard(CardData card)
    {
        if (!Allowed(GuideAction.HandCard, -1, -1, card)) return;
        ClearPending();
        selected = selected == card ? null : card;
        Refresh();
    }

    public void ClearSelection()
    {
        ClearPending();
        selected = null;
        Refresh();
    }

    // Volta da escolha de alvo para a escolha de slot (a carta continua selecionada).
    public void CancelTarget()
    {
        ClearPending();
        Refresh();
    }

    void ClearPending()
    {
        pendingPlayer = pendingSlot = -1;
        FocusedEnemy = -1;
    }

    public void EndTurnPressed()
    {
        if (!Allowed(GuideAction.EndTurn, -1, -1, null)) return;
        ClearPending();
        selected = null;
        gameManager.EndTurn();
    }

    public void ActivateSlot(int playerIndex, int slotIndex)
    {
        if (playerIndex < 0) return;

        var slotCard = gameManager.Players[playerIndex].slots[slotIndex];
        if (!Allowed(GuideAction.Slot, playerIndex, slotIndex, slotCard != null ? slotCard : selected)) return;

        if (slotCard != null)
        {
            if (gameManager.ReturnCard(playerIndex, slotIndex)) { ClearPending(); selected = null; }
            return;
        }

        if (selected == null) return;

        if (gameManager.NeedsTarget(selected))
        {
            pendingPlayer = playerIndex;
            pendingSlot = slotIndex;
            Refresh();
            return;
        }

        if (gameManager.PlayCard(selected, playerIndex, slotIndex)) selected = null;
    }

    // Clique/confirmacao num inimigo: define o alvo da carta de Espadas selecionada.
    // Sem slot escolhido antes, a carta vai para o primeiro slot livre do jogador da vez.
    public void ActivateEnemy(int enemyIndex)
    {
        if (selected == null || selected.suit != Suit.Spades) return;
        if (!Allowed(GuideAction.Enemy, -1, enemyIndex, selected)) return;
        var enemies = gameManager.Enemies;
        if (enemyIndex < 0 || enemyIndex >= enemies.Count || !enemies[enemyIndex].IsAlive) return;

        int playerIndex = pendingSlot >= 0 ? pendingPlayer : CurrentPlayerIndex();
        int slotIndex = pendingSlot >= 0 ? pendingSlot : -1;

        if (gameManager.PlayCard(selected, playerIndex, slotIndex, enemyIndex)) selected = null;
        ClearPending();
        Refresh();
    }

    int CurrentPlayerIndex()
    {
        var players = gameManager.Players;
        for (int i = 0; i < players.Count; i++)
            if (players[i] == gameManager.CurrentPlayer) return i;
        return -1;
    }

    // ---------- Inimigos das ondas ----------

    void SyncEnemyViews()
    {
        var enemies = gameManager.Enemies;
        bool same = viewsBuiltFor.Count == enemies.Count;
        for (int i = 0; same && i < enemies.Count; i++) same = viewsBuiltFor[i] == enemies[i];
        if (same) return;

        foreach (var v in enemyViews) if (v != null) Destroy(v.gameObject);
        enemyViews.Clear();
        viewsBuiltFor.Clear();
        viewsBuiltFor.AddRange(enemies);
        FocusedEnemy = -1;

        bool boss = gameManager.IsBossStage;
        SetBossVisible(boss);
        if (boss) return;

        var row = EnsureEnemyRow();
        if (row == null) return;
        row.gameObject.SetActive(true);

        var font = bossMoveText != null ? bossMoveText.font : TMP_Settings.defaultFontAsset;
        int n = enemies.Count;
        float rowWidth = row.rect.width > 1f ? row.rect.width : (enemyViewSize.x + enemySpacing) * n;
        float wanted = enemyViewSize.x + enemySpacing;
        float step = n > 0 ? Mathf.Min(wanted, rowWidth / n) : wanted;
        float scale = Mathf.Min(1f, step / wanted);

        for (int i = 0; i < n; i++)
        {
            var view = EnemyView.Create(row, enemyViewSize, font);
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * step, 0f);
            rt.localScale = Vector3.one * scale;
            view.Bind(enemies[i], i);
            view.Clicked += v => { if (MouseAllowed()) ActivateEnemy(v.Index); };
            enemyViews.Add(view);
            UiJuice.On(view).Pop(0.35f, i * 0.09f); // entram um de cada vez
        }
    }

    RectTransform EnsureEnemyRow()
    {
        if (enemyRow != null) return enemyRow;

        Canvas canvas = bossSprite != null ? bossSprite.canvas : endTurnButton.GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        var canvasRect = (RectTransform)canvas.transform;

        var go = new GameObject("EnemyRow", typeof(RectTransform));
        enemyRow = (RectTransform)go.transform;
        enemyRow.SetParent(canvasRect, false);
        enemyRow.anchorMin = enemyRow.anchorMax = enemyRow.pivot = new Vector2(0.5f, 0.5f);
        enemyRow.sizeDelta = new Vector2(canvasRect.rect.width * enemyRowWidth, enemyViewSize.y);

        if (bossSprite != null)
        {
            var b = bossSprite.rectTransform;
            enemyRow.position = b.TransformPoint(b.rect.center);

            // Desenha logo acima da UI do chefe, abaixo do resto (paineis, overlay, menus).
            Transform top = b;
            while (top.parent != null && top.parent != canvasRect) top = top.parent;
            if (top.parent == canvasRect) enemyRow.SetSiblingIndex(top.GetSiblingIndex() + 1);
        }
        return enemyRow;
    }

    void SetBossVisible(bool visible)
    {
        bossVisible = visible;
        if (enemyRow != null) enemyRow.gameObject.SetActive(!visible);

        var root = ResolveBossRoot();
        if (root != null) root.SetActive(visible);

        // Campos fora do root (ex.: icone/texto do movimento) sao escondidos um a um.
        foreach (var c in new Component[] { bossSprite, bossHealthBar, bossHealthText, bossMoveImage, bossMoveText, bossArmorText, bossBlockText, bossThornsText })
            if (c != null && (root == null || !c.transform.IsChildOf(root.transform))) c.gameObject.SetActive(visible);
    }

    GameObject resolvedBossRoot;
    bool bossRootResolved;

    // Usa o Boss Root do Inspector se ele for seguro de esconder; senao o pai do sprite do chefe (ex.: BossArea).
    GameObject ResolveBossRoot()
    {
        if (bossRootResolved) return resolvedBossRoot;
        bossRootResolved = true;

        if (bossRoot != null)
        {
            if (SafeToHide(bossRoot.transform)) return resolvedBossRoot = bossRoot;
            Debug.LogWarning($"Boss Root '{bossRoot.name}' contem o BattleUI, o GameManager ou paineis dos jogadores e nao pode ser escondido. Use o objeto que agrupa so a UI do chefe (ex.: BossArea).", this);
        }

        if (bossSprite != null)
        {
            var parent = bossSprite.transform.parent;
            if (parent != null && parent.GetComponent<Canvas>() == null && SafeToHide(parent))
                return resolvedBossRoot = parent.gameObject;
        }
        return resolvedBossRoot = null;
    }

    bool SafeToHide(Transform t)
    {
        if (transform.IsChildOf(t)) return false;
        if (gameManager != null && gameManager.transform.IsChildOf(t)) return false;
        if (endTurnButton != null && endTurnButton.transform.IsChildOf(t)) return false;
        foreach (var p in panels)
            if (p != null && p.transform.IsChildOf(t)) return false;
        return true;
    }

    // Dano previsto das cartas de Espadas ja colocadas, por inimigo alvo.
    int[] IncomingDamage()
    {
        var result = new int[gameManager.Enemies.Count];
        foreach (var owner in gameManager.Players)
            for (int i = 0; i < owner.slots.Length; i++)
            {
                var card = owner.slots[i];
                if (card == null || card.suit != Suit.Spades) continue;
                int t = owner.slotTarget[i];
                if (t >= 0 && t < result.Length) result[t] += gameManager.SlotAmount(owner, i);
            }
        return result;
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

        if (gameManager.Enemies.Count == 0) return;
        SyncEnemyViews();

        if (waveText != null)
            waveText.text = gameManager.IsBossStage ? "Chefe" : $"Onda {gameManager.WaveIndex + 1}/{gameManager.WaveCount}";

        var boss = gameManager.BossEnemy;
        if (boss != null && bossVisible)
        {
            if (bossSprite != null) bossSprite.sprite = boss.data.sprite;

            if (bossHealthBar != null)
            {
                bossHealthBar.maxValue = boss.maxHealth;
                bossHealthBar.value = boss.health;
            }
            if (bossHealthText != null) bossHealthText.text = $"{boss.health}/{boss.maxHealth}";

            if (bossArmorText != null) bossArmorText.text = boss.armor.ToString();
            if (bossBlockText != null) bossBlockText.text = boss.block.ToString();
            if (bossThornsText != null) bossThornsText.text = boss.thorns.ToString();
        }

        bool targeting = gameManager.Phase == GamePhase.PlayerTurn && WantsTarget;
        var incoming = IncomingDamage();
        for (int i = 0; i < enemyViews.Count; i++)
            enemyViews[i].Refresh(incoming[i], targeting, targeting && FocusedEnemy == i);

        endTurnButton.interactable = gameManager.Phase == GamePhase.PlayerTurn;

        if (gameManager.Phase == GamePhase.Lost) Debug.Log("Derrota");
    }

    // Chamado pelo BattleFocus quando o foco do controle muda de inimigo.
    public void RefreshEnemyFocus()
    {
        bool targeting = gameManager.Phase == GamePhase.PlayerTurn && WantsTarget;
        var incoming = IncomingDamage();
        for (int i = 0; i < enemyViews.Count; i++)
            enemyViews[i].Refresh(incoming[i], targeting, targeting && FocusedEnemy == i);
    }
}
