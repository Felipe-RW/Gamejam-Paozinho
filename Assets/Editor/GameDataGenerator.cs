using System.IO;
using UnityEditor;
using UnityEngine;

public static class GameDataGenerator
{
    const string SpritesPath = "Assets/Sprites";
    const string CardsPath = "Assets/Data/Cards";
    const string ClassesPath = "Assets/Data/Classes";

    [MenuItem("Game/Generate Card Data")]
    public static void GenerateCards()
    {
        Directory.CreateDirectory(CardsPath);

        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            for (int value = 1; value <= 10; value++)
            {
                string prefix = value == 1 ? "ace" : value.ToString();
                string spriteFile = $"{SpritesPath}/{prefix}_{suit.ToString().ToLower()}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteFile);
                if (sprite == null) { Debug.LogWarning($"Sprite não encontrado: {spriteFile}"); continue; }

                string assetPath = $"{CardsPath}/{value}_{suit}.asset";
                var card = AssetDatabase.LoadAssetAtPath<CardData>(assetPath);
                if (card == null)
                {
                    card = ScriptableObject.CreateInstance<CardData>();
                    AssetDatabase.CreateAsset(card, assetPath);
                }
                card.suit = suit;
                card.value = value;
                card.sprite = sprite;
                EditorUtility.SetDirty(card);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("CardData gerados em " + CardsPath);
    }

    [MenuItem("Game/Generate Class Data")]
    public static void GenerateClasses()
    {
        Directory.CreateDirectory(ClassesPath);
        Make(PlayerClass.Barbarian, "Bárbaro",    Suit.Clubs,    EffectType.Attack);
        Make(PlayerClass.Paladin,   "Paladino",   Suit.Spades,   EffectType.Attack);
        Make(PlayerClass.Healer,    "Curandeiro", Suit.Hearts,   EffectType.Heal);
        Make(PlayerClass.Sorcerer,  "Feiticeiro", Suit.Diamonds, EffectType.Shield);
        AssetDatabase.SaveAssets();
        Debug.Log("ClassData gerados em " + ClassesPath);
    }

    static void Make(PlayerClass pc, string name, Suit suit, EffectType effect)
    {
        string assetPath = $"{ClassesPath}/{pc}.asset";
        var data = AssetDatabase.LoadAssetAtPath<ClassData>(assetPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<ClassData>();
            AssetDatabase.CreateAsset(data, assetPath);
        }
        data.playerClass = pc;
        data.displayName = name;
        data.suit = suit;
        data.effect = effect;
        EditorUtility.SetDirty(data);
    }
}
