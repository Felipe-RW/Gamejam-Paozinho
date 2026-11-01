using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Painel do fim da demo: logo do estudio, "Obrigado por jogar! Em breve na Steam." e volta ao menu
// (botao, confirmar/Start em qualquer dispositivo, ou sozinho depois de alguns segundos).
public class EndScreen : MonoBehaviour
{
    GameManager gm;
    CanvasGroup group;
    float autoReturn;
    bool leaving;
    float shownAt;

    public static void Show(GameManager manager, Sprite logo, string title, string subtitle, float autoReturnSeconds)
    {
        var go = new GameObject("EndScreen", typeof(RectTransform), typeof(CanvasGroup));
        var screen = go.AddComponent<EndScreen>();
        screen.gm = manager;
        screen.autoReturn = autoReturnSeconds;
        screen.Build(logo, title, subtitle);
    }

    void Build(Sprite logo, string title, string subtitle)
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 950; // acima da batalha e da cortina de troca de local
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;

        var font = FindFont();
        var root = (RectTransform)transform;

        var bg = NewImage("Background", root, null, new Color(0.03f, 0.02f, 0.03f, 0.92f));
        Stretch(bg.rectTransform);
        bg.raycastTarget = true; // bloqueia cliques na batalha por tras

        // Moldura clara para o logo (o fundo do logo e creme).
        var frame = NewImage("LogoFrame", root, null, new Color(1f, 0.96f, 0.88f, 1f));
        frame.rectTransform.sizeDelta = new Vector2(440, 440);
        frame.rectTransform.anchoredPosition = new Vector2(0, 170);
        var frameOutline = frame.gameObject.AddComponent<Outline>();
        frameOutline.effectColor = new Color(1f, 0.84f, 0.3f, 1f);
        frameOutline.effectDistance = new Vector2(6, 6);

        if (logo != null)
        {
            var img = NewImage("Logo", frame.rectTransform, logo, Color.white);
            img.preserveAspect = true;
            img.rectTransform.anchorMin = Vector2.zero;
            img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.offsetMin = new Vector2(12, 12);
            img.rectTransform.offsetMax = new Vector2(-12, -12);
        }
        else Debug.LogWarning("EndScreen: Studio Logo vazio no GameManager.");

        var titleText = NewText("Title", root, font, title, 56, new Color(1f, 0.84f, 0.3f));
        titleText.rectTransform.anchoredPosition = new Vector2(0, -130);
        TextStyle.Apply3D(titleText, 2f);

        var subText = NewText("Subtitle", root, font, subtitle, 36, Color.white);
        subText.rectTransform.anchoredPosition = new Vector2(0, -215);
        TextStyle.Apply3D(subText);

        // Botao voltar ao menu.
        var buttonImg = NewImage("MenuButton", root, null, new Color(0.25f, 0.12f, 0.08f, 0.95f));
        buttonImg.raycastTarget = true;
        buttonImg.rectTransform.sizeDelta = new Vector2(460, 80);
        buttonImg.rectTransform.anchoredPosition = new Vector2(0, -330);
        var bo = buttonImg.gameObject.AddComponent<Outline>();
        bo.effectColor = new Color(1f, 0.84f, 0.3f, 1f);
        bo.effectDistance = new Vector2(4, 4);
        var label = NewText("Label", buttonImg.rectTransform, font, "Voltar ao Menu", 28, Color.white);
        Stretch(label.rectTransform);
        TextStyle.Apply3D(label, 0.8f);
        var button = buttonImg.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImg;
        button.onClick.AddListener(BackToMenu);

        UiJuice.On(frame).Pop(0.5f, 0.2f);
        UiJuice.On(titleText).Pop(0.4f, 0.6f);
        UiJuice.On(subText).Pop(0.4f, 0.85f);
        UiJuice.On(buttonImg).Pop(0.35f, 1.1f);

        shownAt = Time.unscaledTime;
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / 0.6f;
            yield return null;
        }
        group.alpha = 1f;
    }

    void Update()
    {
        if (leaving || Time.unscaledTime - shownAt < 1.2f) return; // evita pular sem querer

        foreach (int dev in PlayerInputs.Devices())
            if (PlayerInputs.Submit(dev) || PlayerInputs.Start(dev) || PlayerInputs.Cancel(dev)) { BackToMenu(); return; }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { BackToMenu(); return; }

        if (autoReturn > 0f && Time.unscaledTime - shownAt > autoReturn) BackToMenu();
    }

    void BackToMenu()
    {
        if (leaving) return;
        leaving = true;
        Time.timeScale = 1f;
        if (gm != null) gm.BackToMenu();
    }

    static TMP_FontAsset FindFont()
    {
        var ui = FindAnyObjectByType<BattleUI>();
        if (ui != null && ui.bossMoveText != null) return ui.bossMoveText.font;
        return TMP_Settings.defaultFontAsset;
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = size;
        tmp.fontSizeMin = size * 0.4f;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        ((RectTransform)go.transform).sizeDelta = new Vector2(1600, size * 2f);
        return tmp;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
