using System.Collections.Generic;

public class Hand
{
    readonly List<CardData> cards = new();

    public int Limit { get; }
    public IReadOnlyList<CardData> Cards => cards;
    public bool IsFull => cards.Count >= Limit;

    public Hand(int limit) => Limit = limit;

    public void Add(CardData card) => cards.Add(card);

    public bool Remove(CardData card) => cards.Remove(card);

    public bool DrawOne(Deck deck)
    {
        if (IsFull) return false;
        var card = deck.Draw();
        if (card == null) return false;
        cards.Add(card);
        return true;
    }

    public void FillFrom(Deck deck)
    {
        while (DrawOne(deck)) { }
    }
}
