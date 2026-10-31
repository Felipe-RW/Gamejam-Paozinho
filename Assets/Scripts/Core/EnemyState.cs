// Estado em batalha de um inimigo (de uma onda) ou do chefe.
public class EnemyState
{
    public readonly BossData data;
    public readonly bool isBoss;
    public readonly int maxHealth;

    public int health;
    public int armor;
    public int block;
    public int thorns;
    public BossAttack currentAttack;

    int lastAttack = -1;

    public bool IsAlive => health > 0;

    public EnemyState(BossData data, bool isBoss, int maxHealth, int armor)
    {
        this.data = data;
        this.isBoss = isBoss;
        this.maxHealth = System.Math.Max(1, maxHealth);
        health = this.maxHealth;
        this.armor = System.Math.Max(0, armor);
        block = this.armor;
    }

    public BossAttack PickAttack()
    {
        var list = data.attacks;
        if (list == null || list.Count == 0) return currentAttack = null;

        int next;
        if (data.fixedOrder)
        {
            next = (lastAttack + 1) % list.Count;
        }
        else
        {
            do next = UnityEngine.Random.Range(0, list.Count);
            while (list.Count > 1 && next == lastAttack);
        }
        lastAttack = next;
        return currentAttack = list[next];
    }

    // Aplica os efeitos defensivos do ataque escolhido. Retorna true se ganhou bloqueio/armadura/espinhos.
    public bool ApplyDefense()
    {
        var move = currentAttack;
        if (move == null) return false;

        armor += move.gainArmor;
        block = armor + move.gainBlock;
        thorns += move.gainThorns;
        return move.gainBlock > 0 || move.gainArmor > 0 || move.gainThorns > 0;
    }

    // Retorna o dano que passou do bloqueio; "absorbed" recebe o que foi bloqueado.
    public int TakeDamage(int amount, out int absorbed)
    {
        absorbed = System.Math.Min(block, amount);
        block -= absorbed;
        int dealt = amount - absorbed;
        health = System.Math.Max(0, health - dealt);
        return dealt;
    }
}
