using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Diretor do teaser de 30 s. Usa o jogo de verdade (GameManager, BattleUI, efeitos) e "joga sozinho":
// monta as maos, fixa vida e ataques dos inimigos, move um cursor falso e clica nas cartas.
// Por cima, desenha abertura, apresentacao das classes, legendas e o encerramento.
//
// Roteiro (2 herois: Paladino de Espadas + Barbaro de Paus):
//   Abertura e classes -> Onda (3 inimigos): Paladino mata o Escudeiro com 7 de Espadas, Barbaro
//   protege com 8 de Paus e cura o aliado com 5 de Copas; Soldado e Mercador atacam e o escudo absorve.
//   Corte -> Dragao: 6 de Espadas, Barbaro protege o aliado, Cuspir Fogo; combo final 10+9 (Paladino)
//   e 8 (Barbaro) de Espadas derruba o chefe -> Encerramento.
// As vidas abaixo batem exatamente com esse roteiro; mudar as cartas exige recalcular.
public class TeaserDirector : MonoBehaviour
{
    public const string RecordPrefKey = "Teaser.Record";

    [Header("Dados (preenchidos por Game > Teaser > Criar cena do Teaser)")]
    public ClassData[] showcaseClasses = new ClassData[4];
    public ClassData leftHero;   // Paladino (Espadas)
    public ClassData rightHero;  // Barbaro (Paus)
    [Tooltip("Ordem fixa: Soldado de Espadas, Mercador de Ouros, Escudeiro de Copas.")]
    public BossData[] waveEnemies = new BossData[3];
    public BossData boss;        // Dragao
    public TMP_FontAsset font;

    [Header("Musica")]
    public AudioClip music;
    public float musicStartTime;
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    [Header("Tempo")]
    public float totalSeconds = 30f;
    public float leadInSeconds = 0.25f;
    public float titleSeconds = 2.6f;
    public float classesSeconds = 3.2f;
    public float minEndCardSeconds = 3f;
    public float cursorMoveSeconds = 0.22f;
    public float afterClickPause = 0.1f;

    [Header("Textos")]
    public string gameTitle = "Suits of Destiny";
    public string studio = "Affogato Game Studio";
    public string classesCaption = "Cada herói domina um naipe";
    public string targetCaption = "Espadas: escolha seu alvo";
    public string protectCaption = "Paus protege. Copas cura.";
    public string bossCaption = "Derrote os chefes";
    public string comboCaption = "Ataquem juntos!";
    public string playersLine = "De 1 a 4 jogadores";
    public string ctaLine = "Jogue agora no itch.io";
    [Range(0f, 1f)] public float captionHeight = 0.9f;

    [Header("Gravacao")]
    [Tooltip("Com a gravacao ligada (Game > Teaser > Gravar video ao dar Play), sai do Play no fim para fechar o video.")]
    public bool exitPlayModeWhenRecording = true;

    // Vidas fixas do roteiro.
    const int SoldierHealth = 14, MerchantHealth = 12, SquireHealth = 8, BossHealth = 39;

    static readonly Color TitleGold = new(1f, 0.84f, 0.3f);

    GameManager gm;
    BattleUI ui;
    RectTransform overlay;
    Image blackout, flash;
    RectTransform cursor;
    RectTransform captionBox;
    TMP_Text captionText;
    AudioSource audioSource;
    float startTime;
    bool bossDown;
    readonly List<GameObject> fallingCards = new();

    float Elapsed => Time.unscaledTime - startTime;

    void Start()
    {
        gm = FindFirstObjectByType<GameManager>();
        ui = FindFirstObjectByType<BattleUI>();
        if (gm == null || ui == null)
        {
            Debug.LogError("TeaserDirector: GameManager ou BattleUI ativo nao encontrado na cena.");
            enabled = false;
            return;
        }
        if (font == null && ui.bossMoveText != null) font = ui.bossMoveText.font;

        ConfigureGame();
        BuildOverlay();
        StopSceneMusic();
        PlayMusic();

        gm.OnEnemyDefeated += e =>
        {
            if (!e.isBoss) return;
            bossDown = true;
            // Segura o proximo chefe fora de cena: o teaser termina aqui.
            gm.bossTransitionSeconds = 999f;
        };
        gm.OnPhaseChanged += phase =>
        {
            // O chefe entra crescendo (os inimigos das ondas ja fazem isso no BattleUI).
            if (phase == GamePhase.BossRoll && gm.IsBossStage && ui.bossSprite != null && gm.BossEnemy?.health == gm.BossEnemy?.maxHealth)
                UiJuice.On(ui.bossSprite).Pop(0.45f);
        };

        StartCoroutine(Run());
    }

    void OnDestroy() => StageBackdrop.InstantTransitions = false;

    // ---------- Configuracao do jogo para o roteiro ----------

    void ConfigureGame()
    {
        gm.dealSeconds = 0.06f;
        gm.bossRollSeconds = 0.5f;
        gm.bossBulletSeconds = 0.45f;
        gm.bossHitPause = 0.25f;
        gm.bossTransitionSeconds = 0.9f;
        gm.resolveStepSeconds = 0.05f;
        gm.stageArrivalSeconds = 0f;
        StageBackdrop.InstantTransitions = true; // o teaser tem cortes proprios

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
            FindAttack(e.data, e.isBoss ? "Cuspir Fogo" :
                               e.data == waveEnemies[0] ? "Estocada Dupla" :
                               e.data == waveEnemies[1] ? "Moedada" : "Pancada");
    }

    static BossAttack FindAttack(BossData data, string name)
    {
        var a = data.attacks.Find(x => x.attackName == name);
        if (a == null) Debug.LogWarning($"Teaser: ataque '{name}' nao encontrado em {data.displayName}.");
        return a;
    }

    CardData Card(Suit suit, int value) => gm.allCards.Find(c => c.suit == suit && c.value == value);

    void StackHand(PlayerState player, params (Suit suit, int value)[] cards)
    {
        // A ultima carta posta no topo e a primeira comprada: empilha de tras para frente.
        for (int i = cards.Length - 1; i >= 0; i--)
        {
            var card = Card(cards[i].suit, cards[i].value);
            if (card != null) player.deck.PutOnTop(card);
            else Debug.LogWarning($"Teaser: carta {cards[i].value} de {cards[i].suit} nao existe em All Cards.");
        }
    }

    void StartBattleNow()
    {
        ui.StartBattle(new[] { leftHero, rightHero });
        if (gm.Players.Count < 2) return;

        // O jogo compra as cartas a partir do proximo quadro, entao da tempo de montar as maos.
        StackHand(gm.Players[0], (Suit.Spades, 7), (Suit.Spades, 10), (Suit.Hearts, 4), (Suit.Spades, 6),
                                 (Suit.Spades, 9), (Suit.Diamonds, 8), (Suit.Clubs, 2));
        StackHand(gm.Players[1], (Suit.Clubs, 8), (Suit.Hearts, 5), (Suit.Clubs, 9), (Suit.Spades, 8),
                                 (Suit.Diamonds, 3), (Suit.Hearts, 6), (Suit.Clubs, 4));
    }

    // ---------- Roteiro ----------

    IEnumerator Run()
    {
        yield return null;
        startTime = Time.unscaledTime;
        SetAlpha(blackout, 1f);
        yield return Wait(leadInSeconds);

        yield return TitleCard(titleSeconds);
        yield return ClassesCard(classesSeconds);

        StartBattleNow();
        yield return Fade(blackout, 1f, 0f, 0.35f);

        // Onda: escolher alvo, proteger e curar.
        yield return WaitTurn(0);
        ShowCaption(targetCaption);
        yield return PlayCard(0, Suit.Spades, 7, 0, 2);
        yield return EndTurn();

        yield return WaitTurn(1);
        ShowCaption(protectCaption);
        yield return PlayCard(1, Suit.Clubs, 8, 1);
        yield return PlayCard(1, Suit.Hearts, 5, 0);
        yield return EndTurn();

        // Inimigos atacam; na proxima vez do Paladino, corta para o chefe.
        yield return WaitTurn(0);
        yield return Fade(flash, 0f, 1f, 0.1f);
        gm.ForceClearStage();
        HideCaption();
        StartCoroutine(Fade(flash, 1f, 0f, 0.45f));

        // Chefe.
        yield return WaitTurn(0);
        ShowCaption(bossCaption);
        yield return PlayCard(0, Suit.Spades, 6, 0);
        yield return EndTurn();

        yield return WaitTurn(1);
        yield return PlayCard(1, Suit.Clubs, 9, 0);
        yield return EndTurn();

        yield return WaitTurn(0);
        ShowCaption(comboCaption);
        yield return PlayCard(0, Suit.Spades, 10, 0);
        yield return PlayCard(0, Suit.Spades, 9, 0);
        yield return EndTurn();

        yield return WaitTurn(1);
        yield return PlayCard(1, Suit.Spades, 8, 1);
        yield return EndTurn();

        yield return WaitUntilOrTimeout(() => bossDown, 12f, "chefe derrotado");
        HideCaption();
        HideCursor();
        yield return Wait(1.2f);

        yield return EndCard();

#if UNITY_EDITOR
        if (exitPlayModeWhenRecording && UnityEditor.EditorPrefs.GetBool(RecordPrefKey, false))
            UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // Espera em tempo nao escalado por quadro: ignora o hit-stop e segue o relogio do Recorder.
    static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator WaitTurn(int playerIndex)
    {
        yield return WaitUntilOrTimeout(() =>
            gm.Phase == GamePhase.PlayerTurn && gm.Players.Count > playerIndex && gm.CurrentPlayer == gm.Players[playerIndex],
            20f, $"vez do jogador {playerIndex + 1}");
        yield return new WaitForSeconds(0.1f);
    }

    IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeout, string what)
    {
        float t = 0f;
        while (!condition())
        {
            t += Time.unscaledDeltaTime;
            if (t > timeout) { Debug.LogWarning($"Teaser: tempo esgotado esperando {what}."); yield break; }
            yield return null;
        }
    }

    // ---------- Acoes com o cursor ----------

    IEnumerator PlayCard(int playerIndex, Suit suit, int value, int ownerIndex, int enemyIndex = -1)
    {
        var player = gm.Players[playerIndex];
        CardData card = null;
        foreach (var c in player.hand.Cards)
            if (c.suit == suit && c.value == value) { card = c; break; }
        if (card == null) { Debug.LogWarning($"Teaser: {value} de {suit} nao esta na mao do jogador {playerIndex + 1}."); yield break; }

        // 1) Carta da mao.
        yield return MoveCursor(HandCardRect(player, card));
        yield return Click();
        ui.ActivateHandCard(card);
        yield return new WaitForSeconds(afterClickPause);

        // 2) Slot livre do dono.
        var owner = gm.Players[ownerIndex];
        int slot = owner.FreeSlotIndex();
        var ownerPanel = ui.GetPanel(owner);
        yield return MoveCursor(ownerPanel != null ? ownerPanel.GetSlotRect(slot) : null);
        yield return Click();
        ui.ActivateSlot(ownerIndex, slot);
        yield return new WaitForSeconds(afterClickPause);

        // 3) Alvo (so quando o jogo pede: Espadas com mais de um inimigo vivo).
        if (ui.IsChoosingTarget)
        {
            int target = enemyIndex >= 0 ? enemyIndex : gm.FirstAliveEnemy();
            RectTransform rect = ui.EnemyTargetRect(target);
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
        if (Vector3.Distance(from, to) < 1f) yield break;

        float t = 0f;
        float duration = Mathf.Max(0.01f, cursorMoveSeconds);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float e = p < 0.5f ? 4f * p * p * p : 1f - Mathf.Pow(-2f * p + 2f, 3f) / 2f; // ease in-out cubic
            // O alvo pode se mexer (mao reorganizando): recalcula o destino.
            to = target != null ? target.TransformPoint(target.rect.center) : to;
            cursor.position = Vector3.Lerp(from, to, e);
            yield return null;
        }
        cursor.position = to;
    }

    IEnumerator Click()
    {
        if (!cursor.gameObject.activeSelf) yield break;
        UiJuice.On(cursor).Squash(new Vector2(-0.2f, -0.2f), 0.2f);
        StartCoroutine(ClickGlow(cursor.position));
        yield return new WaitForSeconds(0.06f);
    }

    IEnumerator ClickGlow(Vector3 position)
    {
        var seq = FxLibrary.Load()?.Get("glow");
        if (seq == null) yield break;

        var img = NewImage("ClickGlow", overlay, seq.frames[0], new Color(1f, 0.9f, 0.3f, 0.9f));
        img.rectTransform.sizeDelta = new Vector2(90, 90);
        img.rectTransform.position = position;
        img.transform.SetSiblingIndex(cursor.GetSiblingIndex()); // por baixo do cursor
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

    // ---------- Cartelas ----------

    IEnumerator TitleCard(float seconds)
    {
        var rain = StartCoroutine(CardRain(seconds + 0.5f, 16));
        var group = NewGroup("Title");
        var title = NewText("TitleText", group.transform, gameTitle, 84, TitleGold);
        title.rectTransform.anchoredPosition = new Vector2(0, 30);
        var sub = NewText("Studio", group.transform, studio, 26, Color.white);
        sub.rectTransform.anchoredPosition = new Vector2(0, -70);

        UiJuice.On(title).Pop(0.55f, 0.15f);
        UiJuice.On(sub).Pop(0.4f, 0.55f);

        yield return Wait(Mathf.Max(0.3f, seconds - 0.3f));
        yield return FadeGroup(group, 1f, 0f, 0.3f);
        Destroy(group.gameObject);
        StopCoroutine(rain);
        ClearFallingCards();
    }

    IEnumerator ClassesCard(float seconds)
    {
        var group = NewGroup("Classes");
        var heading = NewText("Heading", group.transform, classesCaption, 34, TitleGold);
        heading.rectTransform.anchoredPosition = new Vector2(0, 300);
        UiJuice.On(heading).Pop(0.35f);

        int n = showcaseClasses.Length;
        for (int i = 0; i < n; i++)
        {
            var c = showcaseClasses[i];
            if (c == null) continue;

            var col = new GameObject(c.displayName, typeof(RectTransform));
            var rt = (RectTransform)col.transform;
            rt.SetParent(group.transform, false);
            rt.sizeDelta = new Vector2(320, 480);
            rt.anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * 400f, -30f);

            var portrait = NewImage("Portrait", rt, c.portrait, Color.white);
            portrait.preserveAspect = true;
            portrait.rectTransform.sizeDelta = new Vector2(300, 300);
            portrait.rectTransform.anchoredPosition = new Vector2(0, 70);

            var suit = NewImage("Suit", rt, c.suitSprite, Color.white);
            suit.preserveAspect = true;
            suit.rectTransform.sizeDelta = new Vector2(72, 72);
            suit.rectTransform.anchoredPosition = new Vector2(0, -130);

            var name = NewText("Name", rt, c.displayName, 26, Color.white);
            name.rectTransform.anchoredPosition = new Vector2(0, -200);

            UiJuice.On(rt).Pop(0.4f, 0.25f + i * 0.3f);
        }

        yield return Wait(Mathf.Max(0.4f, seconds - 0.35f));
        yield return FadeGroup(group, 1f, 0f, 0.35f);
        Destroy(group.gameObject);
    }

    IEnumerator EndCard()
    {
        float duration = Mathf.Max(minEndCardSeconds, totalSeconds - Elapsed);

        yield return Fade(blackout, blackout.color.a, 1f, 0.4f);
        var rain = StartCoroutine(CardRain(duration, 14));

        var group = NewGroup("End");
        var title = NewText("TitleText", group.transform, gameTitle, 84, TitleGold);
        title.rectTransform.anchoredPosition = new Vector2(0, 80);
        var players = NewText("Players", group.transform, playersLine, 32, Color.white);
        players.rectTransform.anchoredPosition = new Vector2(0, -30);
        var cta = NewText("Cta", group.transform, ctaLine, 32, TitleGold);
        cta.rectTransform.anchoredPosition = new Vector2(0, -100);
        var by = NewText("Studio", group.transform, studio, 22, new Color(1f, 1f, 1f, 0.75f));
        by.rectTransform.anchoredPosition = new Vector2(0, -200);

        UiJuice.On(title).Pop(0.55f);
        UiJuice.On(players).Pop(0.35f, 0.4f);
        UiJuice.On(cta).Pop(0.35f, 0.65f);
        UiJuice.On(by).Pop(0.35f, 0.9f);

        float hold = Mathf.Max(0f, duration - 0.4f - 1.5f);
        yield return Wait(hold);
        yield return FadeMusic(1.5f);
        StopCoroutine(rain);
    }

    // Cartas do baralho caindo girando (abertura e encerramento).
    IEnumerator CardRain(float seconds, int count)
    {
        if (gm.allCards.Count == 0) yield break;
        float interval = seconds / Mathf.Max(1, count);
        for (int i = 0; i < count; i++)
        {
            StartCoroutine(FallingCard(gm.allCards[UnityEngine.Random.Range(0, gm.allCards.Count)].sprite));
            yield return Wait(interval);
        }
    }

    IEnumerator FallingCard(Sprite sprite)
    {
        var img = NewImage("FallingCard", overlay, sprite, new Color(1f, 1f, 1f, 0.55f));
        img.transform.SetSiblingIndex(blackout.transform.GetSiblingIndex() + 1); // atras dos textos
        fallingCards.Add(img.gameObject);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(110, 154);

        float x = UnityEngine.Random.Range(-880f, 880f);
        float spin = UnityEngine.Random.Range(-120f, 120f);
        float speed = UnityEngine.Random.Range(380f, 620f);
        float y = 640f;
        while (y > -660f && img != null)
        {
            y -= speed * Time.unscaledDeltaTime;
            rt.anchoredPosition = new Vector2(x, y);
            rt.localRotation = Quaternion.Euler(0, 0, rt.localEulerAngles.z + spin * Time.unscaledDeltaTime);
            yield return null;
        }
        if (img != null) { fallingCards.Remove(img.gameObject); Destroy(img.gameObject); }
    }

    void ClearFallingCards()
    {
        foreach (var go in fallingCards) if (go != null) Destroy(go);
        fallingCards.Clear();
    }

    // ---------- Legenda ----------

    void ShowCaption(string text)
    {
        captionText.text = text;
        captionBox.gameObject.SetActive(true);
        captionBox.sizeDelta = new Vector2(Mathf.Max(600f, captionText.GetPreferredValues(text).x + 80f), 86f);
        UiJuice.On(captionBox).Pop(0.3f);
    }

    void HideCaption()
    {
        if (captionBox != null) captionBox.gameObject.SetActive(false);
    }

    // ---------- Musica ----------

    // A cena copiada do jogo ja toca a musica da batalha sozinha (Play On Awake): para ela, senao tocam duas.
    void StopSceneMusic()
    {
        foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            if (src != audioSource && src.loop) { src.Stop(); src.playOnAwake = false; }
    }

    void PlayMusic()
    {
        if (music == null) return;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = music;
        audioSource.volume = musicVolume;
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        audioSource.time = Mathf.Clamp(musicStartTime, 0f, Mathf.Max(0f, music.length - 0.1f));
        audioSource.Play();
    }

    IEnumerator FadeMusic(float seconds)
    {
        if (audioSource == null) { yield return Wait(seconds); yield break; }
        float start = audioSource.volume, t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            audioSource.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        audioSource.volume = 0f;
    }

    // ---------- Construcao da camada do teaser ----------

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

        captionBox = NewImage("Caption", overlay, null, new Color(0f, 0f, 0f, 0.7f)).rectTransform;
        captionBox.anchorMin = captionBox.anchorMax = new Vector2(0.5f, captionHeight);
        captionBox.pivot = new Vector2(0.5f, 0.5f);
        captionText = NewText("Text", captionBox, "", 30, Color.white);
        Stretch(captionText.rectTransform);
        captionBox.gameObject.SetActive(false);

        flash = NewImage("Flash", overlay, null, new Color(1f, 1f, 1f, 0f));
        Stretch(flash.rectTransform);

        var cursorImg = NewImage("Cursor", overlay, MakeCursorSprite(), Color.white);
        cursor = cursorImg.rectTransform;
        cursor.pivot = new Vector2(0f, 1f); // ponta da seta
        cursor.sizeDelta = new Vector2(36, 48);
        cursor.anchoredPosition = new Vector2(300, -250);
        cursor.gameObject.SetActive(false);
    }

    // Seta em pixel art (12x16): X = contorno preto, W = branco.
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
        var group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        return group;
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
        TextStyle.Apply3D(tmp, size >= 60f ? 2f : 1f);
        ((RectTransform)go.transform).sizeDelta = new Vector2(1600, size * 2f);
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
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(g, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        SetAlpha(g, to);
    }

    IEnumerator FadeGroup(CanvasGroup g, float from, float to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        g.alpha = to;
    }
}
