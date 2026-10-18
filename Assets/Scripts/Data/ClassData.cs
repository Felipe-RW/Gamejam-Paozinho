using UnityEngine;

[CreateAssetMenu(menuName = "Game/Class", fileName = "Class")]
public class ClassData : ScriptableObject
{
    public PlayerClass playerClass;
    public string displayName;
    public Suit suit;
    public EffectType effect;
    public int maxHealth = 20;
    public Sprite portrait;
}
