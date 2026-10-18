using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BattleUI))]
public class BattleEffects : MonoBehaviour
{
    [Serializable]
    public class Effect
    {
        public GameObject prefab;
        public float lifetime = 1f;
        public float scale = 1f;
        public bool fitToTarget = true;
    }

    public Effect attack;
    public Effect block;
    public Effect damage;
    public Effect heal;
    public RectTransform effectsRoot;
    public int sortingOrder = 100;

    [Header("Ataque do chefe")]
    public GameObject bulletPrefab;
    public float bulletAngleOffset;
    public Image darkOverlay;
    [Range(0f, 1f)] public float darkAlpha = 0.7f;
    public float fadeSeconds = 0.3f;

    BattleUI battleUI;
    GameManager gm;
    Coroutine fading;

    void Awake()
    {
        battleUI = GetComponent<BattleUI>();
    }

    void OnEnable()
    {
        gm = battleUI.gameManager;
        gm.OnCardDealStart += OnCardDealStart;
        gm.OnBossAttackStart += OnBossAttackStart;
        gm.OnBossAttackEnd += OnBossAttackEnd;
        gm.OnBossDefense += OnBossDefense;
        gm.OnBossHit += OnBossHit;
        gm.OnPlayerDamaged += OnPlayerDamaged;
        gm.OnEffectApplied += OnEffectApplied;
        SetOverlayAlpha(0f);
    }

    void OnDisable()
    {
        if (gm == null) return;
        gm.OnCardDealStart -= OnCardDealStart;
        gm.OnBossAttackStart -= OnBossAttackStart;
        gm.OnBossAttackEnd -= OnBossAttackEnd;
        gm.OnBossDefense -= OnBossDefense;
        gm.OnBossHit -= OnBossHit;
        gm.OnPlayerDamaged -= OnPlayerDamaged;
        gm.OnEffectApplied -= OnEffectApplied;
    }

    void OnCardDealStart(PlayerState player, CardData card)
    {
        var panel = battleUI.GetPanel(player);
        if (panel == null || panel.cardPrefab == null || panel.handRoot == null) return;
        StartCoroutine(FlyDealtCard(panel, panel.handRoot as RectTransform));
    }

    IEnumerator FlyDealtCard(PlayerPanelView panel, RectTransform target)
    {
        var canvasRect = (RectTransform)target.GetComponentInParent<Canvas>().rootCanvas.transform;
        var view = Instantiate(panel.cardPrefab, canvasRect);
        view.ShowBack(panel.cardBack);
        PutOnTop(view.gameObject);

        Vector3 a = canvasRect.TransformPoint(canvasRect.rect.center);
        Vector3 b = target.TransformPoint(target.rect.center);
        view.transform.position = a;

        float duration = Mathf.Max(0.01f, gm.dealSeconds);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            view.transform.position = Vector3.Lerp(a, b, t / duration);
            yield return null;
        }
        Destroy(view.gameObject);
    }

    void OnBossAttackStart(BossAttack move)
    {
        FadeOverlay(darkAlpha);
        Play(attack, battleUI.BossAnchor, 0f);

        if (bulletPrefab == null) return;
        foreach (var player in gm.Players)
        {
            if (!player.IsAlive) continue;
            var target = battleUI.GetPlayerAnchor(player);
            if (target != null && battleUI.BossAnchor != null)
                StartCoroutine(FlyBullet(battleUI.BossAnchor, target));
        }
    }

    void OnBossAttackEnd()
    {
        FadeOverlay(0f);
    }

    void OnBossDefense(BossAttack move)
    {
        Play(block, battleUI.BossAnchor, 0f);
    }

    void OnBossHit(int dealt, int absorbed)
    {
        Play(dealt > 0 ? damage : block, battleUI.BossAnchor, 0f);
    }

    void OnPlayerDamaged(PlayerState player, int amount)
    {
        Play(damage, battleUI.GetPlayerAnchor(player), 0f);
    }

    void OnEffectApplied(PlayerState owner, Suit suit, int amount)
    {
        if (suit == Suit.Clubs) Play(block, battleUI.GetPlayerAnchor(owner), 0f);
        else if (suit == Suit.Hearts) Play(heal, battleUI.GetPlayerAnchor(owner), 0f);
    }

    void FadeOverlay(float target)
    {
        if (darkOverlay == null) return;
        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(Fade(target));
    }

    IEnumerator Fade(float target)
    {
        darkOverlay.transform.SetAsLastSibling();
        float start = darkOverlay.color.a;
        float t = 0f;
        float duration = Mathf.Max(0.01f, fadeSeconds);
        while (t < duration)
        {
            t += Time.deltaTime;
            SetOverlayAlpha(Mathf.Lerp(start, target, t / duration));
            yield return null;
        }
        SetOverlayAlpha(target);
    }

    void SetOverlayAlpha(float alpha)
    {
        if (darkOverlay == null) return;
        var c = darkOverlay.color;
        c.a = alpha;
        darkOverlay.color = c;
    }

    Transform RootFor(RectTransform anchor)
    {
        return effectsRoot != null ? effectsRoot : anchor.GetComponentInParent<Canvas>().rootCanvas.transform;
    }

    void PutOnTop(GameObject instance)
    {
        instance.transform.SetAsLastSibling();
        var canvas = instance.GetComponent<Canvas>();
        if (canvas == null) canvas = instance.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }

    IEnumerator FlyBullet(RectTransform from, RectTransform to)
    {
        var bullet = Instantiate(bulletPrefab, RootFor(from));
        PutOnTop(bullet);

        Vector3 a = from.TransformPoint(from.rect.center);
        Vector3 b = to.TransformPoint(to.rect.center);
        Vector3 dir = b - a;
        bullet.transform.position = a;
        bullet.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + bulletAngleOffset);

        float duration = Mathf.Max(0.01f, gm.bossBulletSeconds);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            bullet.transform.position = Vector3.Lerp(a, b, t / duration);
            yield return null;
        }
        Destroy(bullet);
    }

    void Play(Effect effect, RectTransform anchor, float delay)
    {
        if (effect == null || effect.prefab == null || anchor == null) return;
        StartCoroutine(Spawn(effect, anchor, delay));
    }

    IEnumerator Spawn(Effect effect, RectTransform anchor, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        var parent = RootFor(anchor);
        var instance = Instantiate(effect.prefab, parent);
        instance.transform.position = anchor.TransformPoint(anchor.rect.center);
        instance.transform.localScale = Vector3.one * effect.scale;

        var rt = instance.transform as RectTransform;
        if (effect.fitToTarget && rt != null)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            float parentScale = Mathf.Max(0.0001f, parent.lossyScale.x);
            rt.sizeDelta = new Vector2(anchor.rect.width * anchor.lossyScale.x, anchor.rect.height * anchor.lossyScale.y) / parentScale;
            instance.transform.position = anchor.TransformPoint(anchor.rect.center);

            foreach (RectTransform child in rt)
            {
                child.anchorMin = Vector2.zero;
                child.anchorMax = Vector2.one;
                child.offsetMin = Vector2.zero;
                child.offsetMax = Vector2.zero;
                child.localScale = Vector3.one;
            }
        }

        PutOnTop(instance);
        Destroy(instance, effect.lifetime);
    }
}
