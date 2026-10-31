using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Troca o fundo da batalha a cada fase (onda ou chefe), como se a equipe viajasse para outro lugar:
// a fase acaba -> escurece a tela -> troca o fundo e mostra o nome do local -> clareia.
// Os fundos ficam no GameManager (Wave Backgrounds / Boss Backgrounds). Fase sem fundo = mantem o atual.
// Adicionado automaticamente pelo BattleUI.
public class StageBackdrop : MonoBehaviour
{
    public Image background;
    public float fadeOutSeconds = 0.5f;
    public float holdSeconds = 0.6f;
    public float fadeInSeconds = 0.6f;
    [Tooltip("Mostra o nome do local na tela preta da transicao.")]
    public bool showLocationName = false;

    // Ligado pelos teasers: troca o fundo na hora, sem cortina (eles tem cortes proprios).
    public static bool InstantTransitions;

    GameManager gm;
    CanvasGroup curtain;
    TMP_Text locationText;
    TMP_FontAsset font;
    Coroutine transition;
    bool pendingSpawn;

    public void Init(GameManager manager, Image target, TMP_FontAsset textFont)
    {
        gm = manager;
        background = target;
        font = textFont;
        if (background == null) Debug.LogWarning("StageBackdrop: imagem de fundo (BackGround) nao encontrada; o fundo nao vai mudar.", this);

        BuildCurtain();
        gm.OnStageCleared += OnStageCleared;
        gm.OnStageSpawned += OnStageSpawned;
        ApplyCurrent(); // fundo da primeira onda
    }

    void OnDestroy()
    {
        if (gm == null) return;
        gm.OnStageCleared -= OnStageCleared;
        gm.OnStageSpawned -= OnStageSpawned;
    }

    static string LocationName(GameManager.StageLocation loc)
    {
        if (loc == null) return null;
        if (!string.IsNullOrWhiteSpace(loc.name)) return loc.name;
        if (loc.image == null) return null;
        string file = loc.image.name;
        int cut = file.IndexOf('_');
        if (cut > 0 && cut < file.Length - 1 && char.IsDigit(file[0])) file = file.Substring(cut + 1);
        return file.Replace('_', ' ');
    }

    bool ApplyCurrent()
    {
        var loc = gm.CurrentLocation;
        if (background == null || loc == null || loc.image == null) return false;
        background.sprite = loc.image;
        return true;
    }

    // ---------- Transicao ----------

    void OnStageCleared()
    {
        if (InstantTransitions) return;
        if (transition != null) StopCoroutine(transition);
        pendingSpawn = true;
        transition = StartCoroutine(FadeOutBeforeSpawn());
    }

    void OnStageSpawned()
    {
        if (InstantTransitions) { ApplyCurrent(); return; }
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(SwapAndFadeIn());
    }

    IEnumerator FadeOutBeforeSpawn()
    {
        // Deixa o efeito de morte do ultimo inimigo aparecer e termina de escurecer bem na hora da troca.
        // O tempo e relido a cada quadro: o prologo/teaser podem segurar a troca (bossTransitionSeconds alto).
        float t = 0f;
        while (pendingSpawn && t < gm.bossTransitionSeconds - fadeOutSeconds)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (!pendingSpawn) yield break;
        yield return Fade(curtain.alpha, 1f, fadeOutSeconds);
    }

    IEnumerator SwapAndFadeIn()
    {
        pendingSpawn = false;
        if (curtain.alpha < 0.99f) yield return Fade(curtain.alpha, 1f, Mathf.Min(0.25f, fadeOutSeconds));

        ApplyCurrent();
        string locationName = LocationName(gm.CurrentLocation);
        if (showLocationName && !string.IsNullOrEmpty(locationName))
        {
            locationText.text = locationName;
            locationText.gameObject.SetActive(true);
            UiJuice.On(locationText).Pop(0.35f);
        }

        yield return new WaitForSeconds(holdSeconds);
        locationText.gameObject.SetActive(false);
        yield return Fade(1f, 0f, fadeInSeconds);
        transition = null;
    }

    IEnumerator Fade(float from, float to, float seconds)
    {
        curtain.blocksRaycasts = true;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            curtain.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        curtain.alpha = to;
        curtain.blocksRaycasts = to > 0.01f;
    }

    // ---------- Cortina ----------

    void BuildCurtain()
    {
        var go = new GameObject("StageCurtain", typeof(RectTransform), typeof(CanvasGroup));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // acima da batalha, abaixo das camadas do teaser/prologo (1000)
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();

        curtain = go.GetComponent<CanvasGroup>();
        curtain.alpha = 0f;
        curtain.blocksRaycasts = false;
        curtain.interactable = false;

        var black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
        black.transform.SetParent(go.transform, false);
        black.color = Color.black;
        Stretch(black.rectTransform);

        locationText = new GameObject("Location", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        locationText.transform.SetParent(go.transform, false);
        if (font != null) locationText.font = font;
        locationText.fontSize = 48;
        locationText.enableAutoSizing = true;
        locationText.fontSizeMin = 20;
        locationText.fontSizeMax = 48;
        locationText.color = new Color(1f, 0.84f, 0.3f);
        locationText.alignment = TextAlignmentOptions.Center;
        locationText.textWrappingMode = TextWrappingModes.NoWrap;
        locationText.raycastTarget = false;
        locationText.rectTransform.sizeDelta = new Vector2(1600, 120);
        locationText.gameObject.SetActive(false);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
