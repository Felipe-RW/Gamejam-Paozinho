using System.Collections.Generic;
using UnityEngine;

public class Deck
{
    readonly List<CardData> drawPile = new();
    readonly List<CardData> discardPile = new();

    public int Count => drawPile.Count;

    public Deck(IEnumerable<CardData> cards)
    {
        drawPile.AddRange(cards);
        Shuffle();
    }

    public void Shuffle()
    {
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
        }
    }

    public CardData Draw()
    {
        if (drawPile.Count == 0)
        {
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle();
        }
        if (drawPile.Count == 0) return null;

        var card = drawPile[drawPile.Count - 1];
        drawPile.RemoveAt(drawPile.Count - 1);
        return card;
    }

    public CardData TakeMatching(System.Predicate<CardData> match)
    {
        int i = drawPile.FindIndex(match);
        if (i >= 0)
        {
            var card = drawPile[i];
            drawPile.RemoveAt(i);
            return card;
        }

        i = discardPile.FindIndex(match);
        if (i >= 0)
        {
            var card = discardPile[i];
            discardPile.RemoveAt(i);
            return card;
        }

        return null;
    }

    public void Discard(CardData card) => discardPile.Add(card);
}
