using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BossAttack
{
    public string attackName = "Ataque";

    [Header("Dano (em todos os jogadores)")]
    public int damage;
    [Min(1)] public int hits = 1;

    [Header("Efeitos no proprio chefe")]
    public int gainBlock;
    public int gainThorns;
    public int gainArmor;
}

[CreateAssetMenu(menuName = "Game/Boss", fileName = "Boss")]
public class BossData : ScriptableObject
{
    public string displayName;
    public Sprite sprite;

    public int[] healthByDifficulty = { 80, 120, 160 };

    public int startingArmor;

    [Header("Ataques")]
    public List<BossAttack> attacks = new();
    public bool fixedOrder;
}
