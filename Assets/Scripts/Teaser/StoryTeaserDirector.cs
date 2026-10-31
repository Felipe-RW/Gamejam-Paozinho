using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Teaser 2 (30 s): historia + jogo, com os 4 herois.
//
//   0-6 s   A traicao: "O Rei os enviou para morrer." sobre o fundo do menu (Rei em vermelho).
//   6-9 s   Os quatro herois e seus naipes.
//   9-17 s  Onda: cada heroi joga um naipe (Espadas no alvo, Paus, Copas no aliado, Ouros compra);
//           os inimigos atacam e o escudo absorve.
//   17-25 s Corte para o Valete de Paus: golpe forte em todos; combo de 4 Espadas derruba o chefe.
//   25-30 s Encerramento: os 4 Valetes, 4 Damas e 4 Reis de Naiporia, chamada e titulo.
//
// Como no teaser 1, as maos, vidas e ataques sao fixos para o roteiro sair sempre igual.
// As vidas abaixo batem exatamente com as cartas do roteiro; mudar uma carta exige recalcular.
public class StoryTeaserDirector : MonoBehaviour
{
    [Header("Dados (preenchidos por Game > Teaser > Criar cena do Teaser)")]
    [Tooltip("Ordem dos assentos: Paladino, Barbaro, Curandeiro, Bardo.")]
    public ClassData[] heroes = new ClassData[4];
    [Tooltip("Arte de corpo inteiro na mesma ordem dos herois.")]
    public Sprite[] heroArt = new Sprite[4];
    [Tooltip("Ordem fixa: Soldado de Espadas, Mercador de Ouros, Escudeiro de Copas.")]
    public BossData[] waveEnemies = new BossData[3];
    public BossData boss;          // Valete de Paus
    public Sprite storyBackdrop;   // fundo do menu
    public Sprite[] jacks = new Sprite[4];
    public Sprite[] queens = new Sprite[4];
    public Sprite[] kings = new Sprite[4];
    public TMP_FontAsset font;

    [Header("Musica")]
    public AudioClip music;
    public float musicStartTime;
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    [Header("Tempo")]
    public float totalSeconds = 30f;
    public float minEndCardSeconds = 3.5f;
    public float cursorMoveSeconds = 0.16f;
    public float afterClickPause = 0.07f;
    [Range(0.3f, 1f)] public float startingHealth = 0.8f;

    [Header("Textos")]
    public string kingdom = "Naipória";
    public string[] story =
    {
        "O Rei os enviou para morrer.",
        "Eles sobreviveram.",
        "Agora voltam a {reino} para cobrar a traição.",
    };
    public string heroesCaption = "Quatro heróis. Quatro naipes.";
    public string waveCaption = "Joguem juntos: cada herói, um naipe.";
    public string bossCaption = "Os Valetes do Rei esperam.";
    public string comboCaption = "Ataquem juntos!";
    public string courtLine = "Enfrentem os 4 Valetes, as 4 Damas e os 4 Reis";
    public string gameTitle = "Suits of Destiny";
    public string playersLine = "De 1 a 4 jogadores";
    public string ctaLine = "Jogue agora no itch.io";
    public string studio = "Affogato Game Studio";
    [Range(0f, 1f)] public float captionHeight = 0.9f;

    [Header("Gravacao")]
    public bool exitPlayModeWhenRecording = true;

    // Vidas do roteiro. Escudeiro: 7 Espadas + 2 do Paladino - 1 de armadura = 8.
    // Valete (armadura 4): rodada 1 = 6+2-4 = 4; rodada 2 = (10+2)+8+9+7-4 = 32. Total 36.
    const int SoldierHealth = 20, MerchantHealth = 16, SquireHealth = 8, BossHealth = 36;

    static readonly Color Gold = new(1f, 0.84f, 0.3f);
    static readonly Color Blood = new(0.8f, 0.1f, 0.1f);
    static readonly string[] SuitNames = { "Paus", "Espadas", "Copas", "Ouros" };

    GameManager gm;
    BattleUI ui;
    RectTransform overlay;
    Image blackout, flash;
    RectTransform cursor, captionBox;
    TMP_Text captionText;
    AudioSource audioSource;
    float startTime;
    bool bossDown;

    float Elapsed => Time.unscaledTime - startTime;

    void Start()
    {
        gm = FindFirstObjectByType<GameManager>();
        ui = FindFirstObjectByType<BattleUI>();
        if (gm == null || ui == null)
        {
            Debug.LogError("Teaser: GameManager ou BattleUI ativo nao encontrado na cena.");
            enabled = false;
            return;
        }
        if (font == null && ui.bossMoveText != null) font = ui.bossMoveText.font;

        gm.menuPanel = null;
        ConfigureGame();
        BuildOverlay();
        PlayMusic();

        gm.OnEnemyDefeated += e =>
        {
            if (!e.isBoss) return;
            bossDown = true;
            gm.bossTransitionSeconds = 999f; // o teaser termina aqui
        };
        gm.OnPhaseChanged += phase =>
        {
            if (phase == GamePhase.BossRoll && gm.IsBossStage && ui.bossSprite != null && gm.BossEnemy?.health == gm.BossEnemy?.maxHealth)
                UiJuice.On(ui.bossSprite).Pop(0.45f);
        };

        StartCoroutine(Run());
    }

    // ---------- Jogo configurado para o roteiro ----------

    void ConfigureGame()
    {
        gm.dealSeconds = 0.04f;
        gm.bossRollSeconds = 0.4f;
        gm.bossBulletSeconds = 0.42f;
        gm.bossHitPause = 0.25f;
        gm.bossTransitionSeconds = 0.7f;

        gm.waveSizes = new List<int> { 3 };
        gm.extraEnemiesPerLap = 0;
        gm.enemyTypes = new List<BossData>(waveEnemies);
        gm.randomWaveEnemies = false;
        gm.bosses = new List<BossData> { boss };

        gm.healthOverride = d =>
            d == boss ? BossHealth :
            d == waveEnemies[0] ? SoldierHealth :
            d == waveEnemies[1] ? MerchantHealth :
            d == waveEnemies[2] ? SquireHealth : 0;

        gm.attackOverride = e =>
            FindAttack(e.data, e.isBoss ? "Ataque Forte" :
                               e.data == waveEnemies[0] ? "Corte" :
                               e.data == waveEnemies[1] ? "Moedada" : "Escudada");
    }

    static BossAttack FindAttack(BossData data, string name)
    {
        var a = data.attacks.Find(x => x.attackName == name);
        if (a == null) Debug.LogWarning($"Teaser: ataque '{name}' nao encontrado em {data.displayName}.");
        return a;
    }

    void StartBattleNow()
    {
        ui.StartBattle(heroes);
        if (gm.Players.Count < 4) { Debug.LogWarning("Teaser: o roteiro precisa de 4 herois."); return; }

        // Paladino, Barbaro, Curandeiro, Bardo (5 cartas cada com 4 jogadores).
        Stack(0, (Suit.Spades, 7), (Suit.Spades, 6), (Suit.Spades, 10), (Suit.Hearts, 3), (Suit.Diamonds, 2));
        Stack(1, (Suit.Clubs, 8), (Suit.Clubs, 9), (Suit.Spades, 8), (Suit.Hearts, 4), (Suit.Diamonds, 3));
        Stack(2, (Suit.Hearts, 6), (Suit.Spades, 9), (Suit.Clubs, 2), (Suit.Diamonds, 4), (Suit.Spades, 3));
        Stack(3, (Suit.Diamonds, 5), (Suit.Spades, 7), (Suit.Clubs, 4), (Suit.Hearts, 2), (Suit.Diamonds, 6));

        // Voltam feridos da emboscada: a cura aparece de verdade.
        foreach (var p in gm.Players)
            p.health = Mathf.Max(1, Mathf.RoundToInt(p.classData.maxHealth * startingHealth));
    }

    void Stack(int player, params (Suit suit, int value)[] cards)
    {
        for (int i = cards.Length - 1; i >= 0; i--)
        {
            var card = gm.allCards.Find(c => c.suit == cards[i].suit && c.value == cards[i].value);
            if (card != null) gm.Players[player].deck.PutOnTop(card);
        }
    }

    // ---------- Roteiro ----------

    IEnumerator Run()
    {
        yield return null;
        startTime = Time.unscaledTime;
        SetAlpha(blackout, 1f);

        yield return Story();
        yield return Heroes();

        StartBattleNow();
        yield return Fade(blackout, 1f, 0f, 0.3f);

        // Onda: um naipe por heroi.
        yield return WaitTurn(0);
        ShowCaption(waveCaption);
        yield return PlayCard(0, Suit.Spades, 7, 0, 2);
        yield return EndTurn();
        yield return WaitTurn(1);
        yield return PlayCard(1, Suit.Clubs, 8, 1);
        yield return EndTurn();
        yield return WaitTurn(2);
        yield return PlayCard(2, Suit.Hearts, 6, 0);
        yield return EndTurn();
        yield return WaitTurn(3);
        yield return PlayCard(3, Suit.Diamonds, 5, 3);
        yield return EndTurn();

        // Inimigos atacam; na proxima vez do Paladino, corta para o Valete.
        yield return WaitTurn(0);
        yield return Fade(flash, 0f, 1f, 0.1f);
        gm.ForceClearStage();
        HideCaption();
        StartCoroutine(Fade(flash, 1f, 0f, 0.4f));

        // Chefe, rodada 1: ataque e protecao; o Valete responde com Ataque Forte.
        yield return WaitTurn(0);
        ShowCaption(bossCaption);
        yield return PlayCard(0, Suit.Spades, 6, 0);
        yield return EndTurn();
        yield return WaitTurn(1);
        yield return PlayCard(1, Suit.Clubs, 9, 2); // protege o Curandeiro
        yield return EndTurn();
        yield return WaitTurn(2);
        yield return EndTurn();
        yield return WaitTurn(3);
        yield return EndTurn();

        // Rodada 2: combo dos quatro.
        yield return WaitTurn(0);
        ShowCaption(comboCaption);
        yield return PlayCard(0, Suit.Spades, 10, 0);
        yield return EndTurn();
        yield return WaitTurn(1);
        yield return PlayCard(1, Suit.Spades, 8, 1);
        yield return EndTurn();
        yield return WaitTurn(2);
        yield return PlayCard(2, Suit.Spades, 9, 2);
        yield return EndTurn();
        yield return WaitTurn(3);
        yield return PlayCard(3, Suit.Spades, 7, 3);
        yield return EndTurn();

        yield return WaitUntilOrTimeout(() => bossDown, 10f, "chefe derrotado");
        HideCaption();
        HideCursor();
        yield return Wait(0.8f);

        yield return EndCard();

#if UNITY_EDITOR
        if (exitPlayModeWhenRecording && UnityEditor.EditorPrefs.GetBool(TeaserDirector.RecordPrefKey, false))
            UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ---------- Cartelas ----------

    IEnumerator Story()
    {
        var group = NewGroup("Story");
        var backdrop = NewImage("Backdrop", group.transform, storyBackdrop, new Color(1f, 1f, 1f, 0f));
        backdrop.preserveAspect = false;
        Stretch(backdrop.rectTransform);
        StartCoroutine(Fade(backdrop, 0f, 1f, 0.6f));
        StartCoroutine(SlowZoom(backdrop.rectTransform, 1f, 1.08f, 6.5f));

        float[] holds = { 1.4f, 1.0f, 1.5f };
        for (int i = 0; i < story.Length; i++)
        {
            if (i == 0) StartCoroutine(BloodFlash(0.35f));
            if (i == 0) UiJuice.On(backdrop).Shake(14f, 0.5f);
            string text = story[i].Replace("{reino}", kingdom);
            Color color = i == 0 ? new Color(1f, 0.5f, 0.45f) : Color.white;
            yield return TypeLine(group.transform, text, color, 46, -360f, i < holds.Length ? holds[i] : 1.2f);
        }
        yield return FadeGroup(group, 1f, 0f, 0.3f);
        Destroy(group.gameObject);
    }

    IEnumerator Heroes()
    {
        var group = NewGroup("Heroes");
        var title = NewText("Title", group.transform, heroesCaption, 40, Gold);
        title.rectTransform.anchoredPosition = new Vector2(0, 430);
        UiJuice.On(title).Pop(0.3f);

        for (int i = 0; i < heroes.Length; i++)
        {
            var col = new GameObject($"Hero{i}", typeof(RectTransform));
            var rt = (RectTransform)col.transform;
            rt.SetParent(group.transform, false);
            rt.sizeDelta = new Vector2(400, 820);
            rt.anchoredPosition = new Vector2((i - 1.5f) * 440f, -40f);

            var art = NewImage("Art", rt, i < heroArt.Length ? heroArt[i] : null, Color.white);
            art.rectTransform.sizeDelta = new Vector2(300, 720);
            art.rectTransform.anchoredPosition = new Vector2(0, 40);

            if (heroes[i] != null)
            {
                var suit = NewImage("Suit", rt, heroes[i].suitSprite, Color.white);
                suit.rectTransform.sizeDelta = new Vector2(56, 56);
                suit.rectTransform.anchoredPosition = new Vector2(0, -350);
                var name = NewText("Name", rt, $"{heroes[i].displayName} · {SuitNames[(int)heroes[i].suit]}", 20, Color.white);
                name.rectTransform.anchoredPosition = new Vector2(0, -400);
            }
            UiJuice.On(rt).Pop(0.3f, 0.1f + i * 0.18f);
        }

        yield return Wait(2.3f);
        yield return FadeGroup(group, 1f, 0f, 0.25f);
        Destroy(group.gameObject);
    }

    IEnumerator EndCard()
    {
        float duration = Mathf.Max(minEndCardSeconds, totalSeconds - Elapsed);
        yield return Fade(blackout, blackout.color.a, 1f, 0.35f);

        var group = NewGroup("End");
        var title = NewText("Title", group.transform, gameTitle, 80, Gold);
        title.rectTransform.anchoredPosition = new Vector2(0, 330);
        UiJuice.On(title).Pop(0.5f);

        var court = NewText("Court", group.transform, $"{courtLine} de {kingdom}", 24, Color.white);
        court.rectTransform.anchoredPosition = new Vector2(0, 210);
        UiJuice.On(court).Pop(0.3f, 0.3f);

        // 12 cartas da corte em linha: Valetes, Damas, Reis.
        var row = new List<Sprite>();
        row.AddRange(jacks); row.AddRange(queens); row.AddRange(kings);
        for (int i = 0; i < row.Count; i++)
        {
            if (row[i] == null) continue;
            var img = NewImage("Court", group.transform, row[i], Color.white);
            img.rectTransform.sizeDelta = new Vector2(96, 128);
            img.rectTransform.anchoredPosition = new Vector2((i - 5.5f) * 112f + (i / 4 - 1) * 24f, 30f);
            UiJuice.On(img).Pop(0.25f, 0.4f + i * 0.06f);
        }

        var players = NewText("Players", group.transform, playersLine, 30, Color.white);
        players.rectTransform.anchoredPosition = new Vector2(0, -140);
        var cta = NewText("Cta", group.transform, ctaLine, 34, Gold);
        cta.rectTransform.anchoredPosition = new Vector2(0, -215);
        var by = NewText("Studio", group.transform, studio, 20, new Color(1f, 1f, 1f, 0.75f));
        by.rectTransform.anchoredPosition = new Vector2(0, -320);
        UiJuice.On(players).Pop(0.3f, 1.2f);
        UiJuice.On(cta).Pop(0.3f, 1.4f);
        UiJuice.On(by).Pop(0.3f, 1.6f);

        yield return Wait(Mathf.Max(0f, duration - 0.35f - 1.2f));
        yield return FadeMusic(1.2f);
    }

    IEnumerator TypeLine(Transform parent, string text, Color color, float size, float y, float hold)
    {
        var t = NewText("Line", parent, text, size, color);
        t.rectTransform.anchoredPosition = new Vector2(0, y);
        t.maxVisibleCharacters = 0;
        t.ForceMeshUpdate();
        int total = t.textInfo.characterCount;
        for (float shown = 0f; shown < total; shown += 48f * Time.unscaledDeltaTime)
        {
            t.maxVisibleCharacters = Mathf.FloorToInt(shown);
            yield return null;
        }
        t.maxVisibleCharacters = total;
        yield return Wait(hold);
        var g = t.gameObject.AddComponent<CanvasGroup>();
        yield return FadeGroup(g, 1f, 0f, 0.2f);
        Destroy(t.gameObject);
    }

    IEnumerator SlowZoom(RectTransform rt, float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds && rt != null; t += Time.unscaledDeltaTime)
        {
            rt.localScale = Vector3.one * Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
    }

    IEnumerator BloodFlash(float peak)
    {
        flash.color = new Color(Blood.r, Blood.g, Blood.b, 0f);
        yield return Fade(flash, 0f, peak, 0.08f);
        yield return Fade(flash, peak, 0f, 0.6f);
        flash.color = new Color(1f, 1f, 1f, 0f);
    }

    // ---------- Cursor e jogadas ----------

    IEnumerator PlayCard(int playerIndex, Suit suit, int value, int ownerIndex, int enemyIndex = -1)
    {
        if (gm.Players.Count <= Mathf.Max(playerIndex, ownerIndex)) yield break;
        var player = gm.Players[playerIndex];
        CardData card = null;
        foreach (var c in player.hand.Cards)
            if (c.suit == suit && c.value == value) { card = c; break; }
        if (card == null) { Debug.LogWarning($"Teaser: {value} de {suit} nao esta na mao do jogador {playerIndex + 1}."); yield break; }

        yield return MoveCursor(HandCardRect(player, card));
        yield return Click();
        ui.ActivateHandCard(card);
        yield return new WaitForSeconds(afterClickPause);

        var owner = gm.Players[ownerIndex];
        int slot = owner.FreeSlotIndex();
        var panel = ui.GetPanel(owner);
        yield return MoveCursor(panel != null ? panel.GetSlotRect(slot) : null);
        yield return Click();
        ui.ActivateSlot(ownerIndex, slot);
        yield return new WaitForSeconds(afterClickPause);

        if (ui.IsChoosingTarget)
        {
            int target = enemyIndex >= 0 ? enemyIndex : gm.FirstAliveEnemy();
            var rect = target >= 0 && target < ui.EnemyViews.Count ? ui.EnemyViews[target].Anchor : null;
            yield return MoveCursor(rect);
            yield return Click();
            ui.ActivateEnemy(target);
            yield return new WaitForSeconds(afterClickPause);
        }
    }

    IEnumerator EndTurn()
    {
        yield return MoveCursor((RectTransform)ui.EndTurnButton.transform);
        yield return Click();
        ui.EndTurnPressed();
        yield return new WaitForSeconds(afterClickPause);
    }

    RectTransform HandCardRect(PlayerState player, CardData card)
    {
        var panel = ui.GetPanel(player);
        if (panel == null) return null;
        foreach (var v in panel.HandViews)
            if (v != null && v.Data == card) return (RectTransform)v.transform;
        return null;
    }

    IEnumerator MoveCursor(RectTransform target)
    {
        if (target == null) yield break;
        cursor.gameObject.SetActive(true);
        Vector3 from = cursor.position;
        Vector3 to = target.TransformPoint(target.rect.center);
        float duration = Mathf.Max(0.01f, cursorMoveSeconds);
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / duration);
            float e = p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f;
            if (target != null) to = target.TransformPoint(target.rect.center);
            cursor.position = Vector3.Lerp(from, to, e);
            yield return null;
        }
        cursor.position = to;
    }

    IEnumerator Click()
    {
        if (!cursor.gameObject.activeSelf) yield break;
        UiJuice.On(cursor).Squash(new Vector2(-0.2f, -0.2f), 0.18f);
        StartCoroutine(ClickGlow(cursor.position));
        yield return new WaitForSeconds(0.05f);
    }

    IEnumerator ClickGlow(Vector3 position)
    {
        var seq = FxLibrary.Load()?.Get("glow");
        if (seq == null) yield break;
        var img = NewImage("ClickGlow", overlay, seq.frames[0], new Color(1f, 0.9f, 0.3f, 0.9f));
        img.rectTransform.sizeDelta = new Vector2(84, 84);
        img.rectTransform.position = position;
        img.transform.SetSiblingIndex(cursor.GetSiblingIndex());
        foreach (var frame in seq.frames)
        {
            img.sprite = frame;
            yield return Wait(1f / 30f);
        }
        Destroy(img.gameObject);
    }

    void HideCursor()
    {
        if (cursor != null) cursor.gameObject.SetActive(false);
    }

    // ---------- Esperas ----------

    static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    IEnumerator WaitTurn(int playerIndex)
    {
        yield return WaitUntilOrTimeout(() =>
            gm.Phase == GamePhase.PlayerTurn && gm.Players.Count > playerIndex && gm.CurrentPlayer == gm.Players[playerIndex],
            20f, $"vez do jogador {playerIndex + 1}");
        yield return new WaitForSeconds(0.06f);
    }

    IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeout, string what)
    {
        for (float t = 0f; !condition(); t += Time.unscaledDeltaTime)
        {
            if (t > timeout) { Debug.LogWarning($"Teaser: tempo esgotado esperando {what}."); yield break; }
            yield return null;
        }
    }

    // ---------- Legenda e musica ----------

    void ShowCaption(string text)
    {
        captionText.text = text;
        captionBox.gameObject.SetActive(true);
        captionBox.sizeDelta = new Vector2(Mathf.Clamp(captionText.GetPreferredValues(text).x + 80f, 600f, 1700f), 80f);
        UiJuice.On(captionBox).Pop(0.25f);
    }

    void HideCaption()
    {
        if (captionBox != null) captionBox.gameObject.SetActive(false);
    }

    void PlayMusic()
    {
        if (music == null) return;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = music;
        audioSource.volume = musicVolume;
        audioSource.loop = false;
        audioSource.time = Mathf.Clamp(musicStartTime, 0f, Mathf.Max(0f, music.length - 0.1f));
        audioSource.Play();
    }

    IEnumerator FadeMusic(float seconds)
    {
        if (audioSource == null) { yield return Wait(seconds); yield break; }
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
        var go = new GameObject("TeaserOverlay", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        overlay = (RectTransform)go.transform;

        blackout = NewImage("Blackout", overlay, null, Color.black);
        Stretch(blackout.rectTransform);

        captionBox = NewImage("Caption", overlay, null, new Color(0f, 0f, 0f, 0.72f)).rectTransform;
        captionBox.anchorMin = captionBox.anchorMax = new Vector2(0.5f, captionHeight);
        captionText = NewText("Text", captionBox, "", 28, Color.white);
        Stretch(captionText.rectTransform);
        captionBox.gameObject.SetActive(false);

        flash = NewImage("Flash", overlay, null, new Color(1f, 1f, 1f, 0f));
        Stretch(flash.rectTransform);

        cursor = NewImage("Cursor", overlay, MakeCursorSprite(), Color.white).rectTransform;
        cursor.pivot = new Vector2(0f, 1f);
        cursor.sizeDelta = new Vector2(36, 48);
        cursor.anchoredPosition = new Vector2(300, -250);
        cursor.gameObject.SetActive(false);
    }

    static Sprite MakeCursorSprite()
    {
        string[] rows =
        {
            "X...........", "XX..........", "XWX.........", "XWWX........",
            "XWWWX.......", "XWWWWX......", "XWWWWWX.....", "XWWWWWWX....",
            "XWWWWWWWX...", "XWWWWWWWWX..", "XWWWWWXXXXX.", "XWWXWWX.....",
            "XWX.XWWX....", "XX..XWWX....", "X....XWWX...", ".....XXXX...",
        };
        int w = rows[0].Length, h = rows.Length;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = rows[h - 1 - y][x];
                tex.SetPixel(x, y, c == 'X' ? Color.black : c == 'W' ? Color.white : Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 1f), 100f);
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
        // A fonte em pixel e larga: textos longos diminuem ate caber na largura, nunca saem da tela.
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = size;
        tmp.fontSizeMin = size * 0.4f;
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
