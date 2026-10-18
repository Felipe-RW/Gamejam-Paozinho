using UnityEngine;

[CreateAssetMenu(menuName = "Game/Card", fileName = "Card")]
public class CardData : ScriptableObject
{
    public Suit suit;
    [Range(1, 10)] public int value = 1;
    public Sprite sprite;

    public override string ToString() => $"{value} of {suit}";
}
