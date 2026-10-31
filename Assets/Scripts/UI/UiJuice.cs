using UnityEngine;

// Movimento "juice" temporario num elemento de UI: tremor, avanco (lunge), squash & stretch e pop de entrada.
// Guarda a posicao/escala de repouso quando ocioso e sempre volta para ela no fim.
// So escreve escala quando um efeito de escala esta ativo (nao briga com quem controla a escala).
[DisallowMultipleComponent]
public class UiJuice : MonoBehaviour
{
    Vector3 basePos, baseScale;
    bool posActive, scaleActive;

    float shakeT, shakeDur, shakeAmp;
    float punchT, punchDur; Vector2 punchOffset;
    float squashT, squashDur; Vector2 squashAmount;
    float popT, popDur; bool popping;

    public static UiJuice On(Component target)
    {
        if (target == null) return null;
        var j = target.GetComponent<UiJuice>();
        return j != null ? j : target.gameObject.AddComponent<UiJuice>();
    }

    void BeginPos()
    {
        if (!posActive) { basePos = transform.localPosition; posActive = true; }
        enabled = true;
    }

    void BeginScale()
    {
        if (!scaleActive) { baseScale = transform.localScale; scaleActive = true; }
        enabled = true;
    }

    // Tremor suave e que decai (amplitude em unidades locais).
    public void Shake(float amplitude, float duration)
    {
        BeginPos();
        shakeAmp = Mathf.Max(shakeAmp * (1f - Progress(shakeT, shakeDur)), amplitude);
        shakeDur = Mathf.Max(0.01f, duration);
        shakeT = 0f;
    }

    // Vai rapido ate o offset e volta com suavidade (avanco de ataque, recuo ao levar dano).
    public void Punch(Vector2 offset, float duration)
    {
        BeginPos();
        punchOffset = offset;
        punchDur = Mathf.Max(0.01f, duration);
        punchT = 0f;
    }

    // Deforma a escala e volta como mola (ex.: (0.25, -0.2) estica na largura e achata na altura).
    public void Squash(Vector2 amount, float duration)
    {
        BeginScale();
        squashAmount = amount;
        squashDur = Mathf.Max(0.01f, duration);
        squashT = 0f;
    }

    // Entrada: cresce do zero com overshoot.
    public void Pop(float duration, float delay = 0f)
    {
        BeginScale();
        popping = true;
        popDur = Mathf.Max(0.01f, duration);
        popT = -delay;
        transform.localScale = Vector3.zero;
    }

    static float Progress(float t, float d) => d <= 0f ? 1f : Mathf.Clamp01(t / d);

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        Vector3 offset = Vector3.zero;
        Vector3 scaleMul = Vector3.one;

        if (shakeT < shakeDur)
        {
            shakeT += dt;
            float k = 1f - Progress(shakeT, shakeDur);
            float a = shakeAmp * k * k;
            float time = Time.time * 45f;
            offset += new Vector3(Mathf.Sin(time * 1.7f) * a, Mathf.Sin(time * 2.3f + 1f) * a * 0.7f, 0f);
        }

        if (punchT < punchDur)
        {
            punchT += dt;
            float p = Progress(punchT, punchDur);
            // 25% do tempo indo (ease-out), 75% voltando (ease-in-out).
            float f = p < 0.25f ? 1f - Mathf.Pow(1f - p / 0.25f, 3f)
                                : 1f - Mathf.SmoothStep(0f, 1f, (p - 0.25f) / 0.75f);
            offset += (Vector3)(punchOffset * f);
        }

        if (squashT < squashDur)
        {
            squashT += dt;
            float p = Progress(squashT, squashDur);
            float spring = Mathf.Exp(-5f * p) * Mathf.Cos(p * 14f) * (1f - p);
            scaleMul = Vector3.Scale(scaleMul, new Vector3(1f + squashAmount.x * spring, 1f + squashAmount.y * spring, 1f));
        }

        if (popping)
        {
            popT += dt;
            float p = Mathf.Clamp01(popT / popDur);
            // Ease-out-back: passa um pouco de 1 e assenta.
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float e = p <= 0f ? 0f : 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
            scaleMul *= e;
            if (p >= 1f) popping = false;
        }

        bool posBusy = shakeT < shakeDur || punchT < punchDur;
        bool scaleBusy = squashT < squashDur || popping;

        if (posActive) transform.localPosition = posBusy ? basePos + offset : basePos;
        if (scaleActive) transform.localScale = scaleBusy ? Vector3.Scale(baseScale, scaleMul) : baseScale;

        if (!posBusy) posActive = false;
        if (!scaleBusy) scaleActive = false;
        if (!posActive && !scaleActive) enabled = false;
    }

    void OnDisable()
    {
        // Se o objeto for desligado no meio de um efeito, volta ao repouso.
        if (posActive) transform.localPosition = basePos;
        if (scaleActive) transform.localScale = baseScale;
        posActive = scaleActive = popping = false;
        shakeT = shakeDur = punchT = punchDur = squashT = squashDur = 0f;
    }
}
