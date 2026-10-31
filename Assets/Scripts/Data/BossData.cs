using System;
using System.Collections.Generic;
using UnityEngine;

// Estilo visual do ataque. Auto escolhe pelo nome do ataque (fogo, moeda, estocada, mordida...).
public enum AttackFx { Auto, Slash, Pierce, Claw, Fire, Coins, Magic }

[Serializable]
public class BossAttack
{
    public string attackName = "Ataque";
    public Sprite icon;
    public AttackFx fx = AttackFx.Auto;

    public AttackFx ResolveFx()
    {
        if (fx != AttackFx.Auto) return fx;

        string n = Simplify(attackName);
        if (Has(n, "fogo", "chama", "queim", "fire", "brasa")) return AttackFx.Fire;
        if (Has(n, "moed", "ouro", "cofre", "coin")) return AttackFx.Coins;
        if (Has(n, "mord", "garra", "arranh", "bite", "claw")) return AttackFx.Claw;
        if (Has(n, "magia", "feiti", "raio", "maldi", "rejuven", "encant", "magic")) return AttackFx.Magic;
        if (Has(n, "estoc", "apunhal", "perfur", "flecha", "dupla", "pierce")) return AttackFx.Pierce;
        return hits > 1 ? AttackFx.Pierce : AttackFx.Slash;
    }

    static bool Has(string s, params string[] keys)
    {
        foreach (var k in keys) if (s.Contains(k)) return true;
        return false;
    }

    static string Simplify(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (char c in s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD))
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    [Header("Dano (em todos os jogadores)")]
    public int damage;
    [Min(1)] public int hits = 1;

    [Header("Efeitos no proprio chefe")]
    public int gainBlock;
    public int gainThorns;
    public int gainArmor;


    public string Describe()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (damage > 0) parts.Add(hits > 1 ? $"{damage}x{hits} dano" : $"{damage} dano");
        if (gainBlock > 0) parts.Add($"+{gainBlock} bloqueio");
        if (gainThorns > 0) parts.Add($"+{gainThorns} espinhos");
        if (gainArmor > 0) parts.Add($"+{gainArmor} armadura");
        return parts.Count == 0 ? attackName : $"{attackName} - {string.Join(", ", parts)}";
    }
}

[CreateAssetMenu(menuName = "Game/Boss", fileName = "Boss")]
public class BossData : ScriptableObject
{
    public string displayName;
    public Sprite sprite;

    public int[] healthByPlayerCount = { 60, 80, 120, 160 };

    public int startingArmor;

    [Header("Ataques")]
    public List<BossAttack> attacks = new();
    public bool fixedOrder;
}
