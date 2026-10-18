public class PlayerState
{
    public readonly ClassData classData;
    public readonly Hand hand;
    public readonly Deck deck;
    public readonly CardData[] slots;
    public readonly int[] slotPlayedBy;

    public int health;
    public int block;

    public bool IsAlive => health > 0;

    public PlayerState(ClassData classData, int slotCount, int handLimit, Deck deck)
    {
        this.classData = classData;
        this.deck = deck;
        hand = new Hand(handLimit);
        health = classData.maxHealth;
        slots = new CardData[slotCount];
        slotPlayedBy = new int[slotCount];
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

    public void TakeDamage(int amount)
    {
        int absorbed = System.Math.Min(block, amount);
        block -= absorbed;
        health = System.Math.Max(0, health - (amount - absorbed));
    }
}
