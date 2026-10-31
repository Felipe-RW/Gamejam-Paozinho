using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Coreografia dos efeitos de batalha: cada tipo de ataque dos inimigos tem visual proprio,
// montado com os flipbooks da FxLibrary (Assets/Resources/FxLibrary.asset).
[RequireComponent(typeof(BattleUI))]
public class BattleEffects : MonoBehaviour
{
    public RectTransform effectsRoot;
    public int sortingOrder = 100;

    [Header("Turno dos inimigos")]
    public Image darkOverlay;
    [Range(0f, 1f)] public float darkAlpha = 0.7f;
    public float fadeSeconds = 0.3f;

    [Header("Impacto")]
    [Tooltip("Acessibilidade: 0 desliga tremores e congelamentos.")]
    [Range(0f, 1f)] public float shakeIntensity = 1f;
    [Tooltip("Dano a partir do qual o impacto congela o jogo por um instante.")]
    public int bigHitDamage = 8;

    // Paleta dos efeitos.
    static readonly Color Red = new(1f, 0.28f, 0.22f);
    static readonly Color Gold = new(1f, 0.84f, 0.3f);
    static readonly Color Shield = new(0.45f, 0.75f, 1f);
    static readonly Color Steel = new(0.85f, 0.92f, 1f);
    static readonly Color Thorn = new(0.5f, 1f, 0.45f);
    static readonly Color Arcane = new(0.8f, 0.55f, 1f);
    static readonly Color SwordHit = new(1f, 0.95f, 0.7f);
    static readonly Color Heal = new(0.55f, 1f, 0.6f);

    const float WindUp = 0.12f;

    BattleUI battleUI;
    GameManager gm;
    FxPlayer fx;
    Coroutine fading;
    int lastHitStopFrame = -1;

    void Awake()
    {
        battleUI = GetComponent<BattleUI>();
        fx = gameObject.AddComponent<FxPlayer>();
        fx.root = effectsRoot;
        fx.sortingOrder = sortingOrder;
        if (battleUI.bossMoveText != null) fx.font = battleUI.bossMoveText.font;
    }

    void OnEnable()
    {
        gm = battleUI.gameManager;
        gm.OnCardDealStart += OnCardDealStart;
        gm.OnEnemyAttackStart += OnEnemyAttackStart;
        gm.OnEnemyAttackEnd += OnEnemyAttackEnd;
        gm.OnEnemyDefense += OnEnemyDefense;
        gm.OnEnemyHit += OnEnemyHit;
        gm.OnEnemyDefeated += OnEnemyDefeated;
        gm.OnPlayerDamaged += OnPlayerDamaged;
        gm.OnPlayerRevived += OnPlayerRevived;
        gm.OnEffectApplied += OnEffectApplied;
        SetOverlayAlpha(0f);
    }

    void OnDisable()
    {
        if (gm == null) return;
        gm.OnCardDealStart -= OnCardDealStart;
        gm.OnEnemyAttackStart -= OnEnemyAttackStart;
        gm.OnEnemyAttackEnd -= OnEnemyAttackEnd;
        gm.OnEnemyDefense -= OnEnemyDefense;
        gm.OnEnemyHit -= OnEnemyHit;
        gm.OnEnemyDefeated -= OnEnemyDefeated;
        gm.OnPlayerDamaged -= OnPlayerDamaged;
        gm.OnPlayerRevived -= OnPlayerRevived;
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

    // ---------- Inimigo ataca ----------

    void OnEnemyAttackStart(EnemyState enemy, BossAttack move)
    {
        FadeOverlay(darkAlpha);
        var from = battleUI.GetEnemyAnchor(enemy);
        if (from == null) return;

        var kind = move.ResolveFx();

        // Antecipacao: estica para cima, depois avanca na direcao dos jogadores.
        var juice = UiJuice.On(from);
        juice.Squash(new Vector2(-0.12f, 0.2f), 0.3f);
        StartCoroutine(After(WindUp, () => { if (juice != null) juice.Punch(LungeOffset(from, kind), 0.4f); }));

        // Carga no proprio inimigo.
        switch (kind)
        {
            case AttackFx.Fire: fx.Flipbook("fire_tall", from, 0.55f, Color.white, 0f, new Vector2(0f, 0.1f)); break;
            case AttackFx.Coins: fx.Flipbook("sparks", from, 0.7f, Gold); break;
            case AttackFx.Magic: fx.Flipbook("magic2", from, 0.8f, Arcane); break;
            case AttackFx.Pierce: fx.Flipbook("glow", from, 0.45f, Steel); break;
        }

        float travel = Mathf.Max(0.1f, gm.bossBulletSeconds - WindUp);
        foreach (var player in gm.Players)
        {
            if (!player.IsAlive) continue;
            var target = battleUI.GetPlayerAnchor(player);
            if (target == null) continue;

            switch (kind)
            {
                case AttackFx.Fire:
                    fx.Projectile("fire_ball", from, target, travel, 0.45f, Color.white, 0.12f, true, 0f, WindUp);
                    break;
                case AttackFx.Coins:
                    fx.Projectile("sparks", from, target, travel, 0.4f, Gold, 0.35f, false, 0f, WindUp);
                    break;
                case AttackFx.Magic:
                    fx.Projectile("magic2", from, target, travel, 0.5f, Arcane, 0.08f, false, 0.12f, WindUp);
                    break;
                case AttackFx.Pierce:
                    // Rapido: sai mais tarde e chega junto com o golpe.
                    fx.Projectile("glow", from, target, travel * 0.5f, 0.28f, Steel, 0f, false, 0f, WindUp + travel * 0.5f);
                    break;
                // Slash/Claw sao corpo a corpo: o avanco ja vende o golpe.
            }
        }
    }

    Vector3 LungeOffset(RectTransform from, AttackFx kind)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var p in gm.Players)
        {
            if (!p.IsAlive) continue;
            var a = battleUI.GetPlayerAnchor(p);
            if (a == null) continue;
            sum += a.TransformPoint(a.rect.center);
            n++;
        }
        if (n == 0 || from.parent == null) return Vector3.zero;

        Vector3 worldDir = sum / n - from.TransformPoint(from.rect.center);
        Vector3 local = from.parent.InverseTransformVector(worldDir);
        if (local.sqrMagnitude < 0.01f) return Vector3.zero;

        bool melee = kind == AttackFx.Slash || kind == AttackFx.Claw || kind == AttackFx.Pierce;
        float dist = Mathf.Max(30f, from.rect.height * (melee ? 0.3f : 0.12f));
        return local.normalized * dist;
    }

    void OnEnemyAttackEnd()
    {
        FadeOverlay(0f);
    }

    // ---------- Jogador leva dano ----------

    void OnPlayerDamaged(PlayerState player, int dealt, int absorbed)
    {
        var anchor = battleUI.GetPlayerAnchor(player);
        var enemy = gm.AttackingEnemy;
        var move = enemy?.currentAttack;
        bool enemyTurn = gm.Phase == GamePhase.BossTurn && move != null;
        if (anchor == null) return;

        if (enemyTurn) AttackImpact(move.ResolveFx(), anchor, enemy);
        else ThornsImpact(anchor);

        var portrait = battleUI.GetPlayerPortrait(player);
        if (dealt > 0)
        {
            fx.Number(anchor, $"-{dealt}", enemyTurn ? Red : Thorn, dealt >= bigHitDamage ? 1.25f : 1f);
            fx.Flash(portrait, new Color(1f, 0.15f, 0.1f, 0.85f));
            var j = UiJuice.On(portrait != null ? (Component)portrait : anchor);
            j.Shake(10f * shakeIntensity, 0.3f);
            j.Squash(new Vector2(0.14f, -0.14f), 0.3f);
            if (dealt >= bigHitDamage || (enemy != null && enemy.isBoss)) HitStop(0.06f);
        }
        if (absorbed > 0)
        {
            fx.Flipbook("ring", anchor, 0.95f, Shield);
            fx.Number(anchor, $"-{absorbed}", Shield, 0.8f, 0.08f);
            if (dealt == 0) fx.Flash(portrait, new Color(Shield.r, Shield.g, Shield.b, 0.6f));
        }

        if (!player.IsAlive)
        {
            fx.Flipbook("smoke_dark", anchor, 1.2f, Color.white, 0f, new Vector2(0f, 0.15f), 0.1f);
            fx.Flash(portrait, Color.white, 0.6f);
        }
    }

    void AttackImpact(AttackFx kind, RectTransform anchor, EnemyState enemy)
    {
        switch (kind)
        {
            case AttackFx.Slash:
                fx.Streak(anchor, 35f, 1.15f, 0.05f, Red);
                fx.Streak(anchor, -35f, 1.15f, 0.05f, Red, 0.22f, default, 0.05f);
                fx.Flipbook("sparks", anchor, 0.8f, Color.white);
                break;
            case AttackFx.Claw:
                for (int i = 0; i < 3; i++)
                    fx.Streak(anchor, -62f, 1.0f, 0.04f, Red, 0.24f, new Vector2((i - 1) * 0.16f, 0f), i * 0.04f);
                fx.Flipbook("sparks", anchor, 0.7f, Red, 0f, default, 0.05f);
                break;
            case AttackFx.Pierce:
            {
                float angle = 90f;
                var from = battleUI.GetEnemyAnchor(enemy);
                if (from != null)
                {
                    Vector3 d = anchor.TransformPoint(anchor.rect.center) - from.TransformPoint(from.rect.center);
                    angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                }
                fx.Streak(anchor, angle, 1.4f, 0.03f, Steel, 0.18f);
                fx.Flipbook("sparks", anchor, 0.6f, Steel);
                break;
            }
            case AttackFx.Fire:
                fx.Flipbook("fire_burst", anchor, 1.0f, Color.white);
                fx.Flipbook("smoke_dark", anchor, 0.9f, Color.white, 0f, new Vector2(0f, 0.25f), 0.25f);
                break;
            case AttackFx.Coins:
                fx.Flipbook("sparks", anchor, 0.95f, Gold);
                fx.Flipbook("glow", anchor, 0.6f, Gold);
                break;
            case AttackFx.Magic:
                fx.Flipbook("magic", anchor, 1.1f, Arcane);
                fx.Flipbook("glow", anchor, 0.7f, Arcane, 0f, default, 0.05f);
                break;
        }
    }

    // Espinhos do inimigo devolvem dano ao atacante.
    void ThornsImpact(RectTransform anchor)
    {
        fx.Flipbook("magic2", anchor, 0.75f, Thorn);
        for (int i = 0; i < 3; i++)
            fx.Streak(anchor, 70f + i * 20f, 0.6f, 0.035f, Thorn, 0.2f, new Vector2((i - 1) * 0.15f, -0.1f), i * 0.03f);
    }

    // ---------- Inimigo se defende / se fortalece ----------

    void OnEnemyDefense(EnemyState enemy)
    {
        var anchor = battleUI.GetEnemyAnchor(enemy);
        if (anchor == null) return;

        var move = enemy.currentAttack;
        if (move == null) return;
        float delay = 0f;

        UiJuice.On(anchor).Squash(new Vector2(0.12f, -0.1f), 0.35f);

        if (move.gainBlock > 0)
        {
            fx.Flipbook("ring", anchor, 1.15f, Shield);
            fx.Flipbook("glow", anchor, 0.8f, Shield, 0f, default, 0.05f);
            fx.Number(anchor, $"+{move.gainBlock}", Shield);
            delay += 0.18f;
        }
        if (move.gainArmor > 0)
        {
            fx.Flipbook("glow", anchor, 1.0f, Steel, 0f, default, delay);
            fx.Flipbook("sparks", anchor, 0.8f, Steel, 0f, default, delay + 0.05f);
            fx.Number(anchor, $"+{move.gainArmor} ARM", Steel, 0.8f, delay + 0.05f);
            delay += 0.18f;
        }
        if (move.gainThorns > 0)
        {
            fx.Flipbook("magic2", anchor, 1.0f, Thorn, 0f, default, delay);
            for (int i = 0; i < 6; i++)
                fx.Streak(anchor, i * 60f + 15f, 0.55f, 0.03f, Thorn, 0.25f, default, delay + 0.05f);
            fx.Number(anchor, $"+{move.gainThorns} ESP", Thorn, 0.8f, delay + 0.08f);
        }
    }

    // ---------- Espada do jogador acerta o inimigo ----------

    void OnEnemyHit(EnemyState enemy, int dealt, int absorbed)
    {
        var anchor = battleUI.GetEnemyAnchor(enemy);
        if (anchor == null) return;

        var image = battleUI.GetEnemyImage(enemy);
        var juice = UiJuice.On(anchor);

        if (dealt > 0)
        {
            float angle = UnityEngine.Random.Range(25f, 55f) * (UnityEngine.Random.value < 0.5f ? 1f : -1f);
            fx.Streak(anchor, angle, 1.25f, 0.05f, SwordHit);
            fx.Flipbook("sparks", anchor, 0.85f, Color.white);
            fx.Flash(image, Color.white, 0.16f);
            fx.Number(anchor, $"-{dealt}", Gold, dealt >= 10 ? 1.3f : 1f);
            juice.Shake(12f * shakeIntensity, 0.3f);
            juice.Squash(new Vector2(0.18f, -0.15f), 0.3f);
            if (dealt >= 10) HitStop(0.07f);
        }
        if (absorbed > 0)
        {
            fx.Flipbook("ring", anchor, 1.0f, Shield);
            fx.Number(anchor, $"-{absorbed}", Shield, 0.8f, 0.08f);
            if (dealt == 0)
            {
                fx.Flash(image, new Color(Shield.r, Shield.g, Shield.b, 0.6f), 0.15f);
                juice.Shake(4f * shakeIntensity, 0.15f);
            }
        }
    }

    void OnEnemyDefeated(EnemyState enemy)
    {
        var anchor = battleUI.GetEnemyAnchor(enemy);
        if (anchor == null) return;

        fx.Flash(battleUI.GetEnemyImage(enemy), Color.white, 0.5f);
        fx.Flipbook("magic", anchor, 1.3f, Gold);
        fx.Flipbook("smoke_light", anchor, 1.4f, Color.white, 0f, new Vector2(0f, 0.1f), 0.12f);

        var juice = UiJuice.On(anchor);
        juice.Squash(new Vector2(0.35f, -0.4f), 0.55f);
        juice.Shake(16f * shakeIntensity, 0.4f);

        if (enemy.isBoss)
        {
            HitStop(0.18f);
            for (int i = 0; i < 4; i++)
                fx.Flipbook("fire_yellow", anchor, 0.7f, Color.white, 0f,
                    new Vector2(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(-0.3f, 0.3f)), 0.1f + i * 0.12f);
        }
        else
        {
            HitStop(0.08f);
        }
    }

    // ---------- Jogador caido volta na nova onda/chefe ----------

    void OnPlayerRevived(PlayerState player, int health)
    {
        var anchor = battleUI.GetPlayerAnchor(player);
        if (anchor == null) return;

        var portrait = battleUI.GetPlayerPortrait(player);
        fx.Flipbook("glow", anchor, 1.3f, Gold);
        fx.Flipbook("magic", anchor, 1.1f, Gold, 0f, default, 0.1f);
        fx.Flipbook("sparks", anchor, 0.9f, Gold, 0f, default, 0.2f);
        fx.Flash(portrait, new Color(Gold.r, Gold.g, Gold.b, 0.9f), 0.6f);
        fx.Number(anchor, $"+{health}", Heal, 1.2f, 0.15f);
        if (portrait != null) UiJuice.On(portrait).Pop(0.45f);
    }

    // ---------- Cartas dos jogadores (paus = bloqueio, copas = cura) ----------

    void OnEffectApplied(PlayerState owner, Suit suit, int amount)
    {
        var anchor = battleUI.GetPlayerAnchor(owner);
        if (anchor == null) return;

        var portrait = battleUI.GetPlayerPortrait(owner);
        switch (suit)
        {
            case Suit.Clubs:
                fx.Flipbook("ring", anchor, 1.05f, Shield);
                fx.Flipbook("glow", anchor, 0.7f, Shield, 0f, default, 0.05f);
                fx.Flash(portrait, new Color(Shield.r, Shield.g, Shield.b, 0.5f), 0.25f);
                fx.Number(anchor, $"+{amount}", Shield, 0.85f);
                break;
            case Suit.Hearts:
                fx.Flipbook("magic", anchor, 1.0f, Heal);
                fx.Flipbook("glow", anchor, 0.8f, Heal, 0f, default, 0.08f);
                fx.Flash(portrait, new Color(Heal.r, Heal.g, Heal.b, 0.5f), 0.3f);
                fx.Number(anchor, $"+{amount}", Heal, 0.85f);
                break;
            case Suit.Diamonds:
                fx.Flipbook("sparks", anchor, 0.8f, Gold);
                break;
        }
    }

    // ---------- Utilidades ----------

    void HitStop(float seconds)
    {
        if (shakeIntensity <= 0f || Time.frameCount == lastHitStopFrame) return;
        lastHitStopFrame = Time.frameCount;
        fx.HitStop(seconds * shakeIntensity);
    }

    IEnumerator After(float seconds, Action action)
    {
        yield return new WaitForSeconds(seconds);
        action();
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

    void PutOnTop(GameObject instance)
    {
        instance.transform.SetAsLastSibling();
        var canvas = instance.GetComponent<Canvas>();
        if (canvas == null) canvas = instance.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
    }
}
