using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Visual de um inimigo da onda. Criado em tempo de execucao pelo BattleUI.
public class EnemyView : MonoBehaviour, IPointerClickHandler
{
    public event Action<EnemyView> Clicked;
    public int Index { get; set; }
    public EnemyState Enemy { get; private set; }
    public RectTransform Anchor => sprite.rectTransform;
    public Image SpriteImage => sprite;

    Image frame;
    Image sprite;
    Image moveIcon;
    TMP_Text moveText;
    RectTransform healthFill;
    TMP_Text healthText;
    TMP_Text statusText;
    TMP_Text incomingText;
    Coroutine spinning;

    static readonly Color FrameTargetable = new(1f, 0.9f, 0.2f, 0.18f);
    static readonly Color FrameFocused = new(1f, 0.9f, 0.2f, 0.55f);

    public static EnemyView Create(Transform parent, Vector2 size, TMP_FontAsset font)
    {
        var root = NewRect("Enemy", parent);
        root.sizeDelta = size;
        var view = root.gameObject.AddComponent<EnemyView>();
        float k = Mathf.Max(0.5f, size.y / 260f); // textos e icone crescem junto com o inimigo

        // Fundo transparente recebe os cliques do mouse.
        var hit = root.gameObject.AddComponent<Image>();
        hit.color = new Color(0, 0, 0, 0);

        view.frame = NewImage("Frame", root, new Color(0, 0, 0, 0));
        Stretch(view.frame.rectTransform, 0, 0, 1, 1);
        view.frame.raycastTarget = false;

        // Topo: icone e descricao do ataque escolhido.
        view.moveIcon = NewImage("MoveIcon", root, Color.white);
        var mi = view.moveIcon.rectTransform;
        mi.anchorMin = mi.anchorMax = new Vector2(0.5f, 1f);
        mi.pivot = new Vector2(0.5f, 1f);
        mi.sizeDelta = new Vector2(48, 48) * k;
        mi.anchoredPosition = new Vector2(0, -4 * k);
        view.moveIcon.preserveAspect = true;

        view.moveText = NewText("MoveText", root, font, 12 * k, TextAlignmentOptions.Top);
        Stretch(view.moveText.rectTransform, 0, 0.68f, 1, 1);
        view.moveText.rectTransform.offsetMax = new Vector2(0, -54 * k);
        view.moveText.enableAutoSizing = true;
        view.moveText.fontSizeMin = 7 * k;
        view.moveText.fontSizeMax = 12 * k;

        // Meio: sprite do inimigo.
        view.sprite = NewImage("Sprite", root, Color.white);
        Stretch(view.sprite.rectTransform, 0.08f, 0.24f, 0.92f, 0.68f);
        view.sprite.preserveAspect = true;

        // Texto de dano previsto das espadas mirando neste inimigo.
        view.incomingText = NewText("Incoming", root, font, 16 * k, TextAlignmentOptions.Center);
        Stretch(view.incomingText.rectTransform, 0, 0.42f, 1, 0.56f);
        view.incomingText.color = new Color(1f, 0.85f, 0.2f);
        view.incomingText.fontStyle = FontStyles.Bold;

        // Base: barra de vida, texto de vida e status.
        var barBg = NewImage("HealthBar", root, new Color(0.15f, 0.05f, 0.05f, 0.9f));
        Stretch(barBg.rectTransform, 0.06f, 0.13f, 0.94f, 0.21f);
        var fill = NewImage("Fill", barBg.rectTransform, new Color(0.8f, 0.15f, 0.15f, 1f));
        view.healthFill = fill.rectTransform;
        Stretch(view.healthFill, 0, 0, 1, 1);

        view.healthText = NewText("HealthText", barBg.rectTransform, font, 11 * k, TextAlignmentOptions.Center);
        Stretch(view.healthText.rectTransform, 0, 0, 1, 1);

        view.statusText = NewText("Status", root, font, 11 * k, TextAlignmentOptions.Center);
        Stretch(view.statusText.rectTransform, 0, 0f, 1, 0.12f);

        return view;
    }

    public void Bind(EnemyState enemy, int index)
    {
        Enemy = enemy;
        Index = index;
        sprite.sprite = enemy.data.sprite;
        Refresh(0, false, false);
    }

    public void Refresh(int incoming, bool targetable, bool focused)
    {
        var e = Enemy;
        if (e == null) return;

        bool alive = e.IsAlive;
        sprite.color = alive ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.35f);

        float ratio = e.maxHealth > 0 ? Mathf.Clamp01((float)e.health / e.maxHealth) : 0f;
        healthFill.anchorMax = new Vector2(ratio, 1f);
        healthText.text = $"{e.health}/{e.maxHealth}";

        var parts = new List<string>();
        if (e.block > 0) parts.Add($"BLQ {e.block}");
        if (e.thorns > 0) parts.Add($"ESP {e.thorns}");
        statusText.text = string.Join("  ", parts);

        if (spinning == null)
        {
            var move = alive ? e.currentAttack : null;
            moveIcon.enabled = move != null && move.icon != null;
            if (move != null) moveIcon.sprite = move.icon;
            moveText.text = move != null ? move.Describe() : "";
        }

        incomingText.text = alive && incoming > 0 ? $"-{incoming}" : "";

        frame.color = !alive ? new Color(0, 0, 0, 0)
                    : focused ? FrameFocused
                    : targetable ? FrameTargetable
                    : new Color(0, 0, 0, 0);
    }

    public void StartSpin(float duration)
    {
        StopSpin();
        if (!isActiveAndEnabled || Enemy == null || !Enemy.IsAlive) return;
        spinning = StartCoroutine(Spin(duration));
    }

    public void StopSpin()
    {
        if (spinning != null) StopCoroutine(spinning);
        spinning = null;
    }

    IEnumerator Spin(float duration)
    {
        var attacks = Enemy.data.attacks;
        moveText.text = "...";
        if (attacks == null || attacks.Count == 0) { spinning = null; yield break; }

        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        int i = UnityEngine.Random.Range(0, attacks.Count);
        while (true)
        {
            var icon = attacks[i++ % attacks.Count].icon;
            moveIcon.enabled = icon != null;
            moveIcon.sprite = icon;
            float interval = Mathf.Lerp(0.06f, 0.25f, elapsed / duration);
            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = Color.white;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static void Stretch(RectTransform rt, float minX, float minY, float maxX, float maxY)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
