using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Prologo + tutorial. Conta a traicao do Rei, apresenta os herois escolhidos, ensina a jogar numa
// batalha guiada contra os Batedores do Rei, mostra o que vem pela frente e carrega a batalha.
//
// Ritmo (ensinar -> praticar -> testar):
//   Lenda (calmo) -> Herois (calmo) -> Treino guiado: Espadas+alvo, Paus+intencao, Copas, Ouros, passar a vez
//   -> Inimigos atacam (tensao baixa, sem risco) -> Rodada livre: derrotar os batedores (teste)
//   -> Ameacas da jornada (calmo) -> Batalha de verdade.
// Cada mecanica do treino vai para a vez de um jogador diferente (rodizio); sozinho, faz todas.
// O treino e travado: so a jogada destacada e aceita, entao nao ha como travar o tutorial.
public class PrologueDirector : MonoBehaviour
{
    [Header("Cenas")]
    public string gameScene = "CenadeJogo";

    [Header("Dados (preenchidos por Game > Prologo > Criar cena do Prologo)")]
    [Tooltip("Classes usadas quando a cena e aberta direto no editor, sem passar pela selecao.")]
    public ClassData[] fallbackClasses = new ClassData[2];
    public BossData scoutA;   // Soldado de Espadas
    public BossData scoutB;   // Mercador de Ouros
    public Sprite kingSprite;
    public Sprite[] jacks = new Sprite[4];
    public Sprite[] queens = new Sprite[4];
    public Sprite[] kings = new Sprite[4];
    public TMP_FontAsset font;
    public AudioClip music;
    [Range(0f, 1f)] public float musicVolume = 0.7f;

    [Header("Texto")]
    public string kingdom = "Naipória";
    [TextArea(2, 4)]
    public string[] legend =
    {
        "No Reino de {reino}, quatro heróis juraram proteger a Coroa.",
        "Um dia, o Rei os mandou salvar o castelo de um cerco distante.",
        "Era uma armadilha. O Rei queria os quatro mortos.",
        "Mas eles sobreviveram. Feridos, voltam para casa...",
        "...e cada estrada até o trono está tomada pelos servos do Rei.",
    };
    public string heroesTitle = "Os heróis desta jornada";
    public string scoutsLine = "Batedores do Rei bloqueiam a estrada. Relembrem como lutar!";
    public string freePlayLine = "Agora é com vocês! Derrotem os batedores.";
    public string threatsTitle = "Para chegar ao Rei traidor, vocês vão enfrentar:";
    public string wavesLine = "...e ondas de soldados a cada passo.";
    public string farewell = "Que o destino esteja nas suas cartas.";

    [Header("Tempo")]
    public float lineMinSeconds = 2.6f;
    public float charsPerSecond = 38f;
    [Range(0.2f, 1f)] public float startingHealth = 0.7f;

    static readonly Color Gold = new(1f, 0.84f, 0.3f);
    static readonly Color Blood = new(0.75f, 0.08f, 0.08f);
    static readonly string[] SuitNames = { "Paus", "Espadas", "Copas", "Ouros" }; // ordem do enum Suit

    // ---------- Licoes do treino ----------

    enum StepKind { PlayCard, EndTurn }

    class Step
    {
        public StepKind kind;
        public CardData card;
        public int owner;          // jogador dono do slot
        public int enemy = -1;     // alvo (Espadas)
    }

    struct Lesson
    {
        public Suit suit;
        public int value;
        public string text;        // {nome} = classe do jogador da vez
    }

    static readonly Lesson[] Lessons =
    {
        new() { suit = Suit.Spades,   value = 7, text = "{nome}: Espadas atacam! Escolha a carta de Espadas, um slot seu e o inimigo." },
        new() { suit = Suit.Clubs,    value = 6, text = "{nome}: o ícone sobre o inimigo mostra o próximo golpe. Proteja-se com Paus!" },
        new() { suit = Suit.Hearts,   value = 5, text = "{nome}: Copas curam. Coloque a carta {onde}." },
        new() { suit = Suit.Diamonds, value = 4, text = "{nome}: Ouros compram cartas novas para a equipe." },
    };

    GameManager gm;
    BattleUI ui;
    Step step;
    bool guided;
    bool leaving;
    bool stageCleared;
    bool hasSelection;

    // Camada de apresentacao.
    RectTransform overlay;
    Image blackout, flash;
    RectTransform captionBox;
    TMP_Text captionText;
    RectTransform highlight, arrow;
    Image highlightImage;
    AudioSource audioSource;
    readonly Vector3[] corners = new Vector3[4];

    // ---------- Inicio ----------

    void Start()
    {
        gm = FindFirstObjectByType<GameManager>();
        ui = FindFirstObjectByType<BattleUI>();
        if (gm == null || ui == null)
        {
            Debug.LogError("Prologo: GameManager ou BattleUI ativo nao encontrado.");
            enabled = false;
            return;
        }
        if (font == null && ui.bossMoveText != null) font = ui.bossMoveText.font;
        hasSelection = false;
        if (GameSession.HasSelection)
            foreach (var c in GameSession.SeatClasses) if (c != null) hasSelection = true;

        gm.menuPanel = null; // Esc / Start pulam o prologo em vez de abrir a pausa
        ConfigureGame();
        BuildOverlay();
        PlayMusic();

        ui.actionFilter = Filter;
        gm.OnStageCleared += () =>
        {
            stageCleared = true;
            gm.bossTransitionSeconds = 999f; // nada de chefe aqui: o prologo assume
        };

        StartCoroutine(Run());
    }

    void ConfigureGame()
    {
        gm.waveSizes = new List<int> { 2 };
        gm.extraEnemiesPerLap = 0;
        gm.enemyTypes = new List<BossData> { scoutA, scoutB };
        gm.randomWaveEnemies = false;
        gm.dealSeconds = 0.12f;

        gm.healthOverride = d => d == scoutA ? 12 : d == scoutB ? 8 : 0;

        // Golpes fracos: ninguem morre no treino.
        var hitA = new BossAttack { attackName = "Golpe", damage = 3, icon = FirstIcon(scoutA), fx = AttackFx.Slash };
        var hitB = new BossAttack { attackName = "Moedada", damage = 2, icon = FirstIcon(scoutB), fx = AttackFx.Coins };
        gm.attackOverride = e => e.data == scoutA ? hitA : e.data == scoutB ? hitB : null;
    }

    static Sprite FirstIcon(BossData d) => d != null && d.attacks.Count > 0 ? d.attacks[0].icon : null;

    ClassData[] SeatClasses() => hasSelection ? GameSession.SeatClasses : fallbackClasses;

    // ---------- Roteiro ----------

    IEnumerator Run()
    {
        SetAlpha(blackout, 1f);
        yield return null;

        yield return Legend();
        yield return Heroes();

        // Batalha de treino.
        StartTraining();
        yield return Fade(blackout, 1f, 0f, 0.5f);
        yield return Caption(scoutsLine, 2.5f);

        guided = true;
        int n = gm.Players.Count;
        var plan = new List<Lesson>[n];
        var slotsUsed = new int[n];
        var deferred = new List<Lesson>(); // nao coube nos slots da 1a rodada (jogando sozinho)
        for (int p = 0; p < n; p++) plan[p] = new List<Lesson>();
        for (int i = 0; i < Lessons.Length; i++)
        {
            int p = i % n;
            int owner = LessonOwner(Lessons[i], p);
            if (slotsUsed[owner] >= gm.slotsPerPlayer) { deferred.Add(Lessons[i]); continue; }
            slotsUsed[owner]++;
            plan[p].Add(Lessons[i]);
        }

        bool firstEndTurn = true;
        for (int p = 0; p < n; p++)
        {
            yield return WaitTurn(p);
            foreach (var lesson in plan[p])
                yield return Teach(p, lesson);

            ShowCaption(firstEndTurn
                ? "Quando terminar, passe a vez. As cartas agem no fim da rodada."
                : $"{ClassName(p)}: passe a vez.");
            firstEndTurn = false;
            step = new Step { kind = StepKind.EndTurn };
            yield return WaitUntil(() => gm.Phase != GamePhase.PlayerTurn || gm.CurrentPlayer != gm.Players[p]);
            step = null;
        }
        guided = false;

        // Resolucao e ataque dos inimigos, explicados enquanto acontecem.
        yield return WaitUntil(() => gm.Phase == GamePhase.BossTurn || gm.Phase == GamePhase.PlayerTurn || stageCleared);
        if (gm.Phase == GamePhase.BossTurn && !stageCleared)
            ShowCaption("Depois de vocês, cada inimigo ataca todos os heróis.");

        // Rodada livre ate os batedores cairem.
        yield return WaitUntil(() => gm.Phase == GamePhase.PlayerTurn || stageCleared);
        if (!stageCleared && deferred.Count > 0)
        {
            guided = true;
            yield return WaitTurn(0);
            foreach (var lesson in deferred) yield return Teach(0, lesson);
            guided = false;
        }
        if (!stageCleared) ShowCaption(freePlayLine);
        yield return WaitUntil(() => stageCleared);
        HideCaption();
        yield return Wait(1.2f);

        yield return Fade(blackout, 0f, 1f, 0.6f);
        yield return Threats();
        yield return Line(farewell, Gold, 64);
        LoadGame();
    }

    void StartTraining()
    {
        var classes = SeatClasses();
        ui.StartBattle(classes, hasSelection);

        int n = gm.Players.Count;
        int limit = GameManager.HandLimitFor(n);
        var fillers = new (Suit, int)[] { (Suit.Clubs, 3), (Suit.Hearts, 2), (Suit.Diamonds, 2), (Suit.Spades, 3), (Suit.Clubs, 4), (Suit.Hearts, 4) };

        for (int p = 0; p < n; p++)
        {
            var hand = new List<(Suit, int)>();
            for (int i = p; i < Lessons.Length; i += n) hand.Add((Lessons[i].suit, Lessons[i].value));
            hand.Add((Suit.Spades, 8));
            hand.Add((Suit.Spades, 6));
            for (int f = 0; hand.Count < limit && f < fillers.Length; f++) hand.Add(fillers[f]);
            if (hand.Count > limit) hand.RemoveRange(limit, hand.Count - limit);
            StackHand(gm.Players[p], hand);

            // Voltam feridos da emboscada: da sentido a cura.
            var player = gm.Players[p];
            player.health = Mathf.Max(1, Mathf.RoundToInt(player.classData.maxHealth * startingHealth));
        }
    }

    void StackHand(PlayerState player, List<(Suit suit, int value)> cards)
    {
        for (int i = cards.Count - 1; i >= 0; i--)
        {
            var card = gm.allCards.Find(c => c.suit == cards[i].suit && c.value == cards[i].value);
            if (card != null) player.deck.PutOnTop(card);
        }
    }

    // ---------- Uma licao: destacar e esperar a jogada certa ----------

    IEnumerator Teach(int p, Lesson lesson)
    {
        var player = gm.Players[p];
        CardData card = null;
        foreach (var c in player.hand.Cards)
            if (c.suit == lesson.suit && c.value == lesson.value) { card = c; break; }
        if (card == null) yield break;

        int owner = LessonOwner(lesson, p);

        string text = lesson.text.Replace("{nome}", ClassName(p))
                                 .Replace("{onde}", owner == p ? "no seu slot" : $"no slot de {ClassName(owner)}");
        if (lesson.suit == player.classData.suit) text += $" Seu naipe: +{gm.suitBonus}!";
        ShowCaption(text);

        step = new Step { kind = StepKind.PlayCard, card = card, owner = owner, enemy = lesson.suit == Suit.Spades ? 0 : -1 };
        yield return WaitUntil(() => Array.IndexOf(gm.Players[owner].slots, card) >= 0);
        step = null;
        yield return Wait(0.35f);
    }

    // Copas vao para o proximo aliado (cura em equipe); o resto fica no proprio slot.
    int LessonOwner(Lesson lesson, int p)
    {
        int n = gm.Players.Count;
        return lesson.suit == Suit.Hearts && n > 1 ? (p + 1) % n : p;
    }

    bool Filter(BattleUI.GuideAction action, int player, int index, CardData card)
    {
        if (!guided) return true;
        if (step == null) return false;

        switch (action)
        {
            case BattleUI.GuideAction.HandCard:
                return step.kind == StepKind.PlayCard && card == step.card;
            case BattleUI.GuideAction.Slot:
                return step.kind == StepKind.PlayCard && player == step.owner && card == step.card
                       && gm.Players[player].slots[index] == null;
            case BattleUI.GuideAction.Enemy:
                return step.kind == StepKind.PlayCard && index == step.enemy;
            case BattleUI.GuideAction.EndTurn:
                return step.kind == StepKind.EndTurn;
        }
        return false;
    }

    // Onde o destaque deve ficar agora, pelo estado da jogada em andamento.
    RectTransform StepTarget()
    {
        if (step == null || gm.Phase != GamePhase.PlayerTurn) return null;
        if (step.kind == StepKind.EndTurn) return (RectTransform)ui.EndTurnButton.transform;

        if (ui.Selected != step.card)
        {
            var panel = ui.GetPanel(gm.CurrentPlayer);
            if (panel == null) return null;
            foreach (var v in panel.HandViews)
                if (v != null && v.Data == step.card) return (RectTransform)v.transform;
            return null;
        }

        if (ui.IsChoosingTarget)
            return step.enemy >= 0 && step.enemy < ui.EnemyViews.Count ? ui.EnemyViews[step.enemy].Anchor : null;

        var owner = gm.Players[step.owner];
        var ownerPanel = ui.GetPanel(owner);
        int slot = owner.FreeSlotIndex();
        return ownerPanel != null && slot >= 0 ? ownerPanel.GetSlotRect(slot) : null;
    }

    void LateUpdate()
    {
        if (overlay == null) return;
        UpdateHighlight();
        if (!leaving && SkipPressed()) LoadGame();
    }

    void UpdateHighlight()
    {
        var target = StepTarget();
        bool show = target != null && target.gameObject.activeInHierarchy;
        highlight.gameObject.SetActive(show);
        arrow.gameObject.SetActive(show);
        if (!show) return;

        target.GetWorldCorners(corners);
        var scale = overlay.lossyScale;
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        Vector2 size = new((corners[2].x - corners[0].x) / scale.x, (corners[2].y - corners[0].y) / scale.y);

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
        highlight.position = center;
        highlight.sizeDelta = size + Vector2.one * (12f + pulse * 10f);
        highlightImage.color = new Color(1f, 0.9f, 0.2f, 0.12f + pulse * 0.14f);

        arrow.position = new Vector3(center.x, corners[1].y, 0f);
        arrow.anchoredPosition += new Vector2(0f, 30f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 22f);
    }

    // ---------- Narracao ----------

    IEnumerator Legend()
    {
        var group = NewGroup("Legend");
        var king = NewImage("King", group.transform, kingSprite, new Color(1f, 1f, 1f, 0f));
        king.rectTransform.sizeDelta = new Vector2(380, 520);
        king.rectTransform.anchoredPosition = new Vector2(0, 120);

        for (int i = 0; i < legend.Length; i++)
        {
            string text = legend[i].Replace("{reino}", kingdom);
            if (i == 1) StartCoroutine(Fade(king, 0f, 0.85f, 0.8f));           // o Rei aparece
            if (i == 2) { StartCoroutine(FlashRed()); UiJuice.On(king).Shake(18f, 0.5f); }
            if (i == 3) StartCoroutine(Fade(king, king.color.a, 0f, 0.8f));    // e some
            yield return Line(text, i == 2 ? new Color(1f, 0.45f, 0.4f) : Color.white, 40, group.transform, -260f);
        }
        Destroy(group.gameObject);
    }

    IEnumerator Heroes()
    {
        var group = NewGroup("Heroes");
        var title = NewText("Title", group.transform, heroesTitle, 44, Gold);
        title.rectTransform.anchoredPosition = new Vector2(0, 330);
        UiJuice.On(title).Pop(0.4f);

        var classes = new List<ClassData>();
        foreach (var c in SeatClasses()) if (c != null) classes.Add(c);

        for (int i = 0; i < classes.Count; i++)
        {
            var c = classes[i];
            var col = new GameObject(c.displayName, typeof(RectTransform));
            var rt = (RectTransform)col.transform;
            rt.SetParent(group.transform, false);
            rt.sizeDelta = new Vector2(360, 520);
            rt.anchoredPosition = new Vector2((i - (classes.Count - 1) * 0.5f) * 420f, -20f);

            var portrait = NewImage("Portrait", rt, c.portrait, Color.white);
            portrait.rectTransform.sizeDelta = new Vector2(300, 300);
            portrait.rectTransform.anchoredPosition = new Vector2(0, 80);

            var suit = NewImage("Suit", rt, c.suitSprite, Color.white);
            suit.rectTransform.sizeDelta = new Vector2(64, 64);
            suit.rectTransform.anchoredPosition = new Vector2(0, -110);

            var name = NewText("Name", rt, c.displayName, 30, Color.white);
            name.rectTransform.anchoredPosition = new Vector2(0, -175);
            var bonus = NewText("Bonus", rt, $"+{gm.suitBonus} com {SuitNames[(int)c.suit]}", 20, Gold);
            bonus.rectTransform.anchoredPosition = new Vector2(0, -220);

            UiJuice.On(rt).Pop(0.4f, 0.3f + i * 0.3f);
        }

        yield return WaitOrAdvance(2.2f + classes.Count * 0.4f);
        yield return FadeGroup(group, 1f, 0f, 0.4f);
        Destroy(group.gameObject);
    }

    IEnumerator Threats()
    {
        var group = NewGroup("Threats");
        var title = NewText("Title", group.transform, threatsTitle, 36, Gold);
        title.rectTransform.anchoredPosition = new Vector2(0, 400);
        UiJuice.On(title).Pop(0.4f);

        var rows = new (string label, Sprite[] sprites)[] { ("4 Valetes", jacks), ("4 Damas", queens), ("4 Reis", kings) };
        for (int r = 0; r < rows.Length; r++)
        {
            float y = 220f - r * 230f;
            var label = NewText("Label", group.transform, rows[r].label, 34, Color.white);
            label.rectTransform.anchoredPosition = new Vector2(-640, y);
            UiJuice.On(label).Pop(0.35f, 0.4f + r * 0.6f);

            var sprites = rows[r].sprites;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                var img = NewImage("Foe", group.transform, sprites[i], Color.white);
                img.rectTransform.sizeDelta = new Vector2(150, 200);
                img.rectTransform.anchoredPosition = new Vector2(-300 + i * 220, y);
                UiJuice.On(img).Pop(0.35f, 0.5f + r * 0.6f + i * 0.08f);
            }
        }

        var waves = NewText("Waves", group.transform, wavesLine, 28, new Color(1f, 1f, 1f, 0.8f));
        waves.rectTransform.anchoredPosition = new Vector2(0, -430);
        UiJuice.On(waves).Pop(0.35f, 2.4f);

        yield return WaitOrAdvance(5f);
        yield return FadeGroup(group, 1f, 0f, 0.4f);
        Destroy(group.gameObject);
    }

    // Uma frase com maquina de escrever. Confirmar completa o texto; confirmar de novo avanca.
    IEnumerator Line(string text, Color color, float size, Transform parent = null, float y = 0f)
    {
        var t = NewText("Line", parent != null ? parent : overlay, text, size, color);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.rectTransform.sizeDelta = new Vector2(1500, 300);
        t.rectTransform.anchoredPosition = new Vector2(0, y);
        t.maxVisibleCharacters = 0;
        t.ForceMeshUpdate();
        int total = t.textInfo.characterCount;

        float shown = 0f;
        yield return null;
        while (shown < total)
        {
            if (AdvancePressed()) break;
            shown += charsPerSecond * Time.unscaledDeltaTime;
            t.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        t.maxVisibleCharacters = total;

        yield return WaitOrAdvance(Mathf.Max(0.8f, lineMinSeconds - total / charsPerSecond));
        var g = t.gameObject.AddComponent<CanvasGroup>();
        yield return FadeGroup(g, 1f, 0f, 0.3f);
        Destroy(t.gameObject);
    }

    IEnumerator FlashRed()
    {
        flash.color = new Color(Blood.r, Blood.g, Blood.b, 0f);
        yield return Fade(flash, 0f, 0.6f, 0.08f);
        yield return Fade(flash, 0.6f, 0f, 0.6f);
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    // ---------- Entrada (avancar / pular) ----------

    bool AdvancePressed()
    {
        foreach (int dev in PlayerInputs.Devices())
            if (PlayerInputs.Submit(dev)) return true;
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    bool SkipPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
        foreach (int dev in PlayerInputs.Devices())
            if (PlayerInputs.Start(dev)) return true;
        return false;
    }

    void LoadGame()
    {
        if (leaving) return;
        leaving = true;
        StopAllCoroutines();
        ui.actionFilter = null;
        Time.timeScale = 1f;
        if (!hasSelection)
        {
            // Aberto direto no editor: leva as classes de teste para a batalha.
            System.Array.Clear(GameSession.SeatClasses, 0, GameSession.SeatClasses.Length);
            for (int i = 0; i < GameSession.SeatDevices.Length; i++) GameSession.SeatDevices[i] = PlayerInputs.None;
            for (int i = 0; i < fallbackClasses.Length && i < GameSession.SeatClasses.Length; i++)
                GameSession.SeatClasses[i] = fallbackClasses[i];
        }
        GameSession.HasSelection = true; // a batalha usa as mesmas classes e controles
        StartCoroutine(LeaveRoutine());
    }

    IEnumerator LeaveRoutine()
    {
        HideCaption();
        StartCoroutine(FadeMusic(0.6f));
        yield return Fade(blackout, blackout.color.a, 1f, 0.6f);
        SceneManager.LoadScene(gameScene);
    }

    // ---------- Esperas ----------

    IEnumerator WaitTurn(int playerIndex) =>
        WaitUntil(() => gm.Phase == GamePhase.PlayerTurn && gm.CurrentPlayer == gm.Players[playerIndex]);

    static IEnumerator WaitUntil(Func<bool> condition)
    {
        while (!condition()) yield return null;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    IEnumerator WaitOrAdvance(float seconds)
    {
        yield return null;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            if (AdvancePressed()) yield break;
            yield return null;
        }
    }

    // ---------- Legenda ----------

    // Com mais de um jogador inclui o numero: duas pessoas podem escolher a mesma classe.
    string ClassName(int p)
    {
        string name = gm.Players[p].classData.displayName;
        return gm.Players.Count > 1 ? $"Jogador {p + 1} ({name})" : name;
    }

    IEnumerator Caption(string text, float seconds)
    {
        ShowCaption(text);
        yield return Wait(seconds);
        HideCaption();
    }

    void ShowCaption(string text)
    {
        if (captionText.text == text && captionBox.gameObject.activeSelf) return;
        captionText.text = text;
        captionBox.gameObject.SetActive(true);
        var pref = captionText.GetPreferredValues(text, 1500f, 0f);
        captionBox.sizeDelta = new Vector2(Mathf.Min(1600f, Mathf.Max(600f, pref.x + 80f)), Mathf.Max(80f, pref.y + 40f));
        UiJuice.On(captionBox).Pop(0.3f);
    }

    void HideCaption()
    {
        if (captionBox != null) captionBox.gameObject.SetActive(false);
    }

    // ---------- Musica ----------

    void PlayMusic()
    {
        if (music == null) return;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = music;
        audioSource.loop = true;
        audioSource.volume = musicVolume * PlayerPrefs.GetFloat("MasterVolume", 1f);
        audioSource.Play();
    }

    IEnumerator FadeMusic(float seconds)
    {
        if (audioSource == null) yield break;
        float start = audioSource.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            audioSource.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        audioSource.volume = 0f;
    }

    // ---------- Construcao da camada ----------

    void BuildOverlay()
    {
        var go = new GameObject("PrologueOverlay", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>(); // so o botao Pular recebe clique
        overlay = (RectTransform)go.transform;

        // Destaque do tutorial (por baixo da cortina preta e das legendas).
        highlightImage = NewImage("Highlight", overlay, null, new Color(1f, 0.9f, 0.2f, 0.2f));
        highlight = highlightImage.rectTransform;
        var outline = highlight.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.9f, 0.2f, 1f);
        outline.effectDistance = new Vector2(5f, 5f);
        highlight.gameObject.SetActive(false);

        var arrowImg = NewImage("Arrow", overlay, MakeArrowSprite(), Color.white);
        arrow = arrowImg.rectTransform;
        arrow.pivot = new Vector2(0.5f, 0f);
        arrow.sizeDelta = new Vector2(56, 64);
        arrow.gameObject.SetActive(false);

        blackout = NewImage("Blackout", overlay, null, Color.black);
        Stretch(blackout.rectTransform);

        flash = NewImage("Flash", overlay, null, new Color(1f, 1f, 1f, 0f));
        Stretch(flash.rectTransform);

        captionBox = NewImage("Caption", overlay, null, new Color(0f, 0f, 0f, 0.78f)).rectTransform;
        captionBox.anchorMin = captionBox.anchorMax = new Vector2(0.5f, 0.86f);
        captionText = NewText("Text", captionBox, "", 26, Color.white);
        captionText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(captionText.rectTransform);
        captionText.rectTransform.offsetMin = new Vector2(30, 10);
        captionText.rectTransform.offsetMax = new Vector2(-30, -10);
        captionBox.gameObject.SetActive(false);

        BuildSkipButton();
    }

    void BuildSkipButton()
    {
        var img = NewImage("Skip", overlay, null, new Color(0f, 0f, 0f, 0.6f));
        img.raycastTarget = true;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24, -24);
        rt.sizeDelta = new Vector2(330, 56);
        var label = NewText("Label", rt, "Pular (Esc / Start)", 18, Color.white);
        Stretch(label.rectTransform);
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.onClick.AddListener(LoadGame);
    }

    // Seta para baixo em pixel art (11x12): X = contorno, Y = amarelo.
    static Sprite MakeArrowSprite()
    {
        string[] rows =
        {
            "...XXXXX...", "...XYYYX...", "...XYYYX...", "...XYYYX...",
            "...XYYYX...", "XXXXYYYXXXX", "XYYYYYYYYYX", ".XYYYYYYYX.",
            "..XYYYYYX..", "...XYYYX...", "....XYX....", ".....X.....",
        };
        int w = rows[0].Length, h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var yellow = new Color(1f, 0.85f, 0.2f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = rows[h - 1 - y][x];
                tex.SetPixel(x, y, c == 'X' ? Color.black : c == 'Y' ? yellow : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f);
    }

    CanvasGroup NewGroup(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        var rt = (RectTransform)go.transform;
        rt.SetParent(overlay, false);
        Stretch(rt);
        rt.SetSiblingIndex(blackout.transform.GetSiblingIndex() + 1);
        var g = go.GetComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        g.interactable = false;
        return g;
    }

    Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        img.preserveAspect = sprite != null;
        return img;
    }

    TMP_Text NewText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.outlineWidth = 0.2f;
        tmp.outlineColor = new Color32(10, 5, 5, 255);
        ((RectTransform)go.transform).sizeDelta = new Vector2(1600, size * 2.2f);
        return tmp;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetAlpha(Graphic g, float a)
    {
        var c = g.color; c.a = a; g.color = c;
    }

    IEnumerator Fade(Graphic g, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(g, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        SetAlpha(g, to);
    }

    IEnumerator FadeGroup(CanvasGroup g, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            g.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        g.alpha = to;
    }
}
