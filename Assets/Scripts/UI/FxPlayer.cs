using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Primitivas visuais da batalha (UI): flipbooks, projeteis, rastros de corte, numeros flutuantes,
// flash de cor e hit-stop. A coreografia (o que tocar em cada evento) fica no BattleEffects.
public class FxPlayer : MonoBehaviour
{
    public RectTransform root;
    public int sortingOrder = 100;
    public TMP_FontAsset font;

    FxLibrary lib;
    int hitStopId;

    public bool HasLibrary => lib != null;

    void Awake()
    {
        lib = FxLibrary.Load();
    }

    Transform Parent(RectTransform anchor)
    {
        if (root != null) return root;
        return anchor.GetComponentInParent<Canvas>().rootCanvas.transform;
    }

    static Vector3 Center(RectTransform rt) => rt.TransformPoint(rt.rect.center);

    // Tamanho do anchor convertido para unidades locais do pai dos efeitos.
    static Vector2 SizeIn(RectTransform anchor, Transform parent)
    {
        float ps = Mathf.Max(0.0001f, parent.lossyScale.x);
        return new Vector2(anchor.rect.width * anchor.lossyScale.x, anchor.rect.height * anchor.lossyScale.y) / ps;
    }

    RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.SetAsLastSibling();
        var canvas = go.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        return rt;
    }

    Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.preserveAspect = sprite != null;
        img.raycastTarget = false;
        return img;
    }

    // ---------- Flipbook parado sobre um alvo ----------

    // size = fracao do menor lado do alvo. offset = fracao do tamanho do alvo.
    public void Flipbook(string id, RectTransform anchor, float size, Color tint, float rotation = 0f,
                         Vector2 offset = default, float delay = 0f, float speed = 1f)
    {
        var seq = lib != null ? lib.Get(id) : null;
        if (seq == null || anchor == null) return;
        StartCoroutine(FlipbookRoutine(seq, anchor, size, tint, rotation, offset, delay, speed));
    }

    IEnumerator FlipbookRoutine(FxLibrary.Sequence seq, RectTransform anchor, float size, Color tint,
                                float rotation, Vector2 offset, float delay, float speed)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (anchor == null) yield break;

        var parent = Parent(anchor);
        var a = SizeIn(anchor, parent);
        float side = Mathf.Min(a.x, a.y) * size;

        var img = NewImage("Fx_" + seq.id, parent, seq.frames[0], tint);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(side, side);
        rt.position = Center(anchor);
        rt.localPosition += new Vector3(offset.x * a.x, offset.y * a.y, 0f);
        rt.localRotation = Quaternion.Euler(0, 0, rotation);

        float frameTime = 1f / Mathf.Max(1f, seq.fps * speed);
        for (int i = 0; i < seq.frames.Length; i++)
        {
            img.sprite = seq.frames[i];
            yield return new WaitForSeconds(frameTime);
        }
        Destroy(rt.gameObject);
    }

    // ---------- Projetil animado ----------

    // arc = altura da curva em fracao da distancia. rotateAlong: gira o "topo" do sprite contra a direcao (cauda para tras).
    public void Projectile(string id, RectTransform from, RectTransform to, float duration, float size,
                           Color tint, float arc = 0f, bool rotateAlong = true, float wobble = 0f, float delay = 0f)
    {
        var seq = lib != null ? lib.Get(id) : null;
        if (seq == null || from == null || to == null) return;
        StartCoroutine(ProjectileRoutine(seq, from, to, duration, size, tint, arc, rotateAlong, wobble, delay));
    }

    IEnumerator ProjectileRoutine(FxLibrary.Sequence seq, RectTransform from, RectTransform to, float duration,
                                  float size, Color tint, float arc, bool rotateAlong, float wobble, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (from == null || to == null) yield break;

        var parent = Parent(from);
        var fa = SizeIn(from, parent);
        float side = Mathf.Min(fa.x, fa.y) * size;

        var img = NewImage("Proj_" + seq.id, parent, seq.frames[0], tint);
        var rt = img.rectTransform;
        rt.sizeDelta = new Vector2(side, side);

        Vector3 a = parent.InverseTransformPoint(Center(from));
        Vector3 b = parent.InverseTransformPoint(Center(to));
        Vector3 dir = b - a;
        Vector3 normal = new Vector3(-dir.y, dir.x, 0f).normalized;
        if (normal.y < 0f) normal = -normal; // arco sempre "para cima"

        float frameTime = 1f / Mathf.Max(1f, seq.fps);
        duration = Mathf.Max(0.01f, duration);
        float t = 0f, ft = 0f;
        int frame = 0;
        Vector3 prev = a;
        while (t < duration)
        {
            t += Time.deltaTime;
            ft += Time.deltaTime;
            if (ft >= frameTime) { ft = 0f; frame = (frame + 1) % seq.frames.Length; img.sprite = seq.frames[frame]; }

            float p = Mathf.Clamp01(t / duration);
            float e = p * p * (3f - 2f * p) * 0.35f + p * 0.65f; // leve aceleracao no meio
            Vector3 pos = Vector3.Lerp(a, b, e) + normal * (4f * arc * dir.magnitude * p * (1f - p));
            if (wobble > 0f) pos += normal * Mathf.Sin(p * Mathf.PI * 4f) * wobble * side;
            rt.localPosition = pos;

            Vector3 v = pos - prev;
            if (rotateAlong && v.sqrMagnitude > 0.01f)
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + 90f);
            prev = pos;
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ---------- Rastro de corte (retangulo que estica e some) ----------

    public void Streak(RectTransform anchor, float angle, float length, float thickness, Color color,
                       float duration = 0.22f, Vector2 offset = default, float delay = 0f)
    {
        if (anchor == null) return;
        StartCoroutine(StreakRoutine(anchor, angle, length, thickness, color, duration, offset, delay));
    }

    IEnumerator StreakRoutine(RectTransform anchor, float angle, float length, float thickness, Color color,
                              float duration, Vector2 offset, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (anchor == null) yield break;

        var parent = Parent(anchor);
        var a = SizeIn(anchor, parent);
        float len = Mathf.Min(a.x, a.y) * length;
        float thick = Mathf.Max(2f, Mathf.Min(a.x, a.y) * thickness);

        // Nucleo claro + brilho colorido por baixo.
        var glow = NewImage("StreakGlow", parent, null, new Color(color.r, color.g, color.b, color.a * 0.45f));
        var core = NewImage("Streak", parent, null, Color.Lerp(color, Color.white, 0.7f));
        var rts = new[] { glow.rectTransform, core.rectTransform };
        foreach (var r in rts)
        {
            r.position = Center(anchor);
            r.localPosition += new Vector3(offset.x * a.x, offset.y * a.y, 0f);
            r.localRotation = Quaternion.Euler(0, 0, angle);
        }

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(p / 0.35f), 3f);   // estica rapido
            float fade = p < 0.4f ? 1f : 1f - (p - 0.4f) / 0.6f;              // depois some
            float w = Mathf.Lerp(1f, 0.15f, p);                               // e afina
            rts[0].sizeDelta = new Vector2(len * grow * 1.08f, thick * 2.4f * w);
            rts[1].sizeDelta = new Vector2(len * grow, thick * w);
            glow.color = new Color(color.r, color.g, color.b, color.a * 0.45f * fade);
            var c = core.color; c.a = fade; core.color = c;
            yield return null;
        }
        Destroy(glow.gameObject);
        Destroy(core.gameObject);
    }

    // ---------- Numero flutuante ----------

    public void Number(RectTransform anchor, string text, Color color, float size = 1f, float delay = 0f)
    {
        if (anchor == null) return;
        StartCoroutine(NumberRoutine(anchor, text, color, size, delay));
    }

    IEnumerator NumberRoutine(RectTransform anchor, string text, Color color, float size, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (anchor == null) yield break;

        var parent = Parent(anchor);
        var a = SizeIn(anchor, parent);
        var rt = NewRect("Number", parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = Mathf.Clamp(Mathf.Min(a.x, a.y) * 0.22f, 18f, 44f) * size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = color;
        tmp.raycastTarget = false;
        TextStyle.Apply3D(tmp, 1.5f);
        rt.sizeDelta = new Vector2(300, 80);

        rt.position = Center(anchor);
        Vector3 start = rt.localPosition + new Vector3(Random.Range(-0.18f, 0.18f) * a.x, a.y * 0.1f, 0f);
        Vector3 end = start + new Vector3(0f, Mathf.Max(50f, a.y * 0.45f), 0f);

        const float duration = 0.9f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            rt.localPosition = Vector3.Lerp(start, end, 1f - Mathf.Pow(1f - p, 3f));
            float pop = p < 0.15f ? Mathf.Lerp(0.4f, 1.35f, p / 0.15f) : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((p - 0.15f) / 0.2f));
            rt.localScale = Vector3.one * pop;
            var c = tmp.color; c.a = p < 0.6f ? 1f : 1f - (p - 0.6f) / 0.4f; tmp.color = c;
            yield return null;
        }
        Destroy(rt.gameObject);
    }

    // ---------- Flash de cor sobre uma imagem ----------

    // Copia a silhueta da imagem (mesmo sprite) por cima dela e some. Nao mexe na cor original.
    public void Flash(Image target, Color color, float duration = 0.18f)
    {
        if (target == null || !target.isActiveAndEnabled) return;
        StartCoroutine(FlashRoutine(target, color, duration));
    }

    static Material solid;

    IEnumerator FlashRoutine(Image target, Color color, float duration)
    {
        var go = new GameObject("Flash", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(target.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = target.sprite;
        img.preserveAspect = target.preserveAspect;
        img.type = target.type;
        img.raycastTarget = false;

        if (solid == null)
        {
            var shader = Shader.Find("UI/SolidFlash");
            if (shader != null) solid = new Material(shader);
        }
        if (solid != null) img.material = solid;

        float t = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (t < duration && img != null)
        {
            t += Time.unscaledDeltaTime; // continua mesmo durante o hit-stop
            float p = Mathf.Clamp01(t / duration);
            img.color = new Color(color.r, color.g, color.b, color.a * (1f - p) * (1f - p));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ---------- Hit-stop ----------

    // Congela o jogo por um instante para vender o impacto. Usa tempo real para voltar.
    public void HitStop(float seconds, float slow = 0.05f)
    {
        if (seconds <= 0f) return;
        StartCoroutine(HitStopRoutine(seconds, slow));
    }

    IEnumerator HitStopRoutine(float seconds, float slow)
    {
        int id = ++hitStopId;
        Time.timeScale = slow;
        yield return new WaitForSecondsRealtime(seconds);
        if (id == hitStopId) Time.timeScale = 1f;
    }

    void OnDisable()
    {
        if (hitStopId > 0) Time.timeScale = 1f;
    }
}
