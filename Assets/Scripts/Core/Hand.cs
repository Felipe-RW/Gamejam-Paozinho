using System.Collections.Generic;

public class Hand
{
    public const int Size = 5;

    readonly List<CardData> cards = new();

    public IReadOnlyList<CardData> Cards => cards;

    public void Add(CardData card) => cards.Add(card);

    public bool Remove(CardData card) => cards.Remove(card);

    public void FillFrom(Deck deck)
    {
        while (cards.Count < Size)
        {
            var card = deck.Draw();
            if (card == null) break;
            cards.Add(card);
        }
    }
}
