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
    [Tooltip("Multiplica o tamanho dos inimigos das ondas. Com muitos inimigos, eles encolhem para caber na fileira.")]
    [Range(0.5f, 3f)] public float enemyScale = 1.8f;
    [Tooltip("Quanto o encaixe automatico pode encolher os inimigos (1 = nunca encolhe; 0,6 = ate 60% do Enemy Scale).")]
    [Range(0.2f, 1f)] public float minEnemyFit = 0.6f;
    public float enemySpacing = 24f;
    [Tooltip("Opcional: mostra 'Onda 1/2' ou 'Chefe'.")]
    public TMP_Text waveText;

    [Header("HUD")]
    public Button endTurnButton;
    [Tooltip("Fundo que muda a cada onda/chefe. Vazio = procura o objeto 'BackGround' do Canvas.")]
    public Image backgroundImage;

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
            if (text != null) { text.enabled = true; TextStyle.Apply3D(text); }
        foreach (var panel in panels)
            if (panel != null) TextStyle.Apply3D(panel.healthText);
        if (waveText != null) TextStyle.Apply3D(waveText, 1.5f);

        // Navegacao do EventSystem faria o controle "clicar" no ultimo botao do mouse.
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
        gameObject.AddComponent<BattleFocus>().Init(this, gameManager);
        gameObject.AddComponent<StageBackdrop>().Init(gameManager, FindBackground(),
            bossMoveText != null ? bossMoveText.font : null);

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

    // Imagem de fundo da batalha: campo do Inspector ou o objeto "BackGround" do mesmo Canvas.
    Image FindBackground()
    {
        if (backgroundImage != null) return backgroundImage;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return null;
        foreach (var img in canvas.rootCanvas.GetComponentsInChildren<Image>(true))
            if (img.name == "BackGround") return img;
        return null;
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
        var viewSize = enemyViewSize * enemyScale;
        float gap = enemySpacing * enemyScale;

        // Espaco livre entre os paineis dos jogadores, na altura da fileira.
        FreeSpan(row, viewSize.y, out float left, out float right);
        float avail = Mathf.Max(viewSize.x * 0.5f, right - left);
        float centerX = (left + right) * 0.5f;

        // Lado a lado ou em zigue-zague (um acima, outro abaixo), o que deixar os inimigos maiores.
        float lineWidth = viewSize.x * n + gap * (n - 1);
        float lineScale = Mathf.Min(1f, avail / Mathf.Max(1f, lineWidth));
        float zigStep = viewSize.x * 0.62f;
        float zigWidth = zigStep * (n - 1) + viewSize.x;
        float zigScale = n >= 3 ? Mathf.Min(1f, avail / Mathf.Max(1f, zigWidth)) : 0f;
        bool zigzag = zigScale > lineScale + 0.05f;
        float scale = Mathf.Max(minEnemyFit, zigzag ? zigScale : lineScale);
        float step = (zigzag ? zigStep : viewSize.x + gap) * scale;
        float zigOffset = viewSize.y * 0.16f * scale;

        for (int i = 0; i < n; i++)
        {
            var view = EnemyView.Create(row, viewSize, font);
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            float y = zigzag ? (i % 2 == 0 ? zigOffset : -zigOffset) : 0f;
            rt.anchoredPosition = new Vector2(centerX + (i - (n - 1) * 0.5f) * step, y);
            rt.localScale = Vector3.one * scale;
            if (zigzag && i % 2 == 1) rt.SetAsLastSibling(); // os de baixo ficam na frente
            view.Bind(enemies[i], i);
            view.Clicked += v => { if (MouseAllowed()) ActivateEnemy(v.Index); };
            enemyViews.Add(view);
            UiJuice.On(view).Pop(0.35f, i * 0.09f); // entram um de cada vez
        }
    }

    // Intervalo horizontal livre (no espaco local da fileira) na faixa de altura dos inimigos:
    // tudo que e dos paineis dos jogadores (slots, retrato, vida, mao) e o botao de fim de turno limita a area.
    void FreeSpan(RectTransform row, float bandHeight, out float left, out float right)
    {
        var canvas = row.GetComponentInParent<Canvas>();
        var canvasRect = (RectTransform)(canvas != null ? canvas.rootCanvas.transform : row.parent);
        canvasRect.GetWorldCorners(corners);
        float l = row.InverseTransformPoint(corners[0]).x + 20f;
        float r = row.InverseTransformPoint(corners[2]).x - 20f;
        float half = bandHeight * 0.5f;
        const float pad = 16f;

        // Limita pela area realmente desenhada (nao pela caixa do RectTransform, que costuma ser maior).
        void BlockRect(RectTransform rt, Rect local)
        {
            Vector3 a = row.InverseTransformPoint(rt.TransformPoint(new Vector3(local.xMin, local.yMin)));
            Vector3 b = row.InverseTransformPoint(rt.TransformPoint(new Vector3(local.xMax, local.yMax)));
            float xMin = Mathf.Min(a.x, b.x), xMax = Mathf.Max(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y), yMax = Mathf.Max(a.y, b.y);
            if (yMax < -half || yMin > half) return; // fora da faixa dos inimigos
            if ((xMin + xMax) * 0.5f < 0f) l = Mathf.Max(l, xMax + pad);
            else r = Mathf.Min(r, xMin - pad);
        }

        void Block(Component c)
        {
            if (c == null || !c.gameObject.activeInHierarchy || c.transform is not RectTransform rt) return;
            var rect = rt.rect;
            // Imagem com "Preserve Aspect": so o retangulo onde o sprite aparece.
            if (c is Image img && img.preserveAspect && img.sprite != null && img.sprite.rect.height > 0f)
            {
                float aspect = img.sprite.rect.width / img.sprite.rect.height;
                Vector2 size = rect.width / rect.height > aspect
                    ? new Vector2(rect.height * aspect, rect.height)
                    : new Vector2(rect.width, rect.width / aspect);
                rect = new Rect(rect.center - size * 0.5f, size);
            }
            BlockRect(rt, rect);
        }

        // Mao: as cartas, nao o container (que costuma ocupar a largura toda).
        void BlockChildren(Transform root)
        {
            if (root == null || !root.gameObject.activeInHierarchy) return;
            foreach (Transform child in root)
                if (child.gameObject.activeInHierarchy && child is RectTransform crt) BlockRect(crt, crt.rect);
        }

        foreach (var panel in panels)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) continue;
            foreach (var slot in panel.slotImages) Block(slot);
            Block(panel.portrait);
            Block(panel.healthBar);
            BlockChildren(panel.handRoot);
        }
        Block(endTurnButton);

        if (r - l < 100f) // layout inesperado: volta para a largura configurada
        {
            float w = canvasRect.rect.width * enemyRowWidth * 0.5f;
            l = -w;
            r = w;
        }
        left = l;
        right = r;
    }
    readonly Vector3[] corners = new Vector3[4];

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
        enemyRow.sizeDelta = new Vector2(canvasRect.rect.width * enemyRowWidth, enemyViewSize.y * enemyScale);
        if (bossSprite != null)
        {
            var b = bossSprite.rectTransform;
            enemyRow.position = b.TransformPoint(b.rect.center);
        }

        // Desenha logo acima do fundo e abaixo de tudo o mais (cartas, slots, paineis, overlay, menus).
        var bg = FindBackground();
        Transform top = bg != null ? bg.transform : null;
        while (top != null && top.parent != null && top.parent != canvasRect) top = top.parent;
        enemyRow.SetSiblingIndex(top != null && top.parent == canvasRect ? top.GetSiblingIndex() + 1 : 0);
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
