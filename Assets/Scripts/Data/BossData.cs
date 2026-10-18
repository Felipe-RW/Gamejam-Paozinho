using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BossAttack
{
    public string attackName = "Ataque";
    public Sprite icon;

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
