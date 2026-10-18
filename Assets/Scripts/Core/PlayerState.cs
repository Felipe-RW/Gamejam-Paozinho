public class PlayerState
{
    public readonly ClassData classData;
    public readonly Hand hand = new();
    public readonly CardData[] slots;

    public int health;
    public int shield;

    public bool IsAlive => health > 0;

    public PlayerState(ClassData classData, int slotCount)
    {
        this.classData = classData;
        health = classData.maxHealth;
        slots = new CardData[slotCount];
    }

    public int FreeSlotIndex()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null) return i;
        return -1;
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        health = System.Math.Min(classData.maxHealth, health + amount);
    }

    // O escudo absorve o dano primeiro.
    public void TakeDamage(int amount)
    {
        int absorbed = System.Math.Min(shield, amount);
        shield -= absorbed;
        health = System.Math.Max(0, health - (amount - absorbed));
    }
}
