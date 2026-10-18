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
        Make(PlayerClass.Barbarian, "Bárbaro",    Suit.Clubs);
        Make(PlayerClass.Paladin,   "Paladino",   Suit.Spades);
        Make(PlayerClass.Healer,    "Curandeiro", Suit.Hearts);
        Make(PlayerClass.Sorcerer,  "Feiticeiro", Suit.Diamonds);
        AssetDatabase.SaveAssets();
        Debug.Log("ClassData gerados em " + ClassesPath);
    }

    [MenuItem("Game/Generate Valete de Paus")]
    public static void GenerateValeteDePaus()
    {
        Directory.CreateDirectory("Assets/Data/Bosses");
        const string path = "Assets/Data/Bosses/ValeteDePaus.asset";
        var boss = AssetDatabase.LoadAssetAtPath<BossData>(path);
        if (boss == null)
        {
            boss = ScriptableObject.CreateInstance<BossData>();
            AssetDatabase.CreateAsset(boss, path);
        }
        boss.displayName = "Valete de Paus";
        boss.healthByDifficulty = new[] { 80, 120, 160 };
        boss.startingArmor = 4;
        boss.fixedOrder = false;
        boss.attacks = new System.Collections.Generic.List<BossAttack>
        {
            new BossAttack { attackName = "Ataque Forte",    damage = 14 },
            new BossAttack { attackName = "Golpe de Escudo", damage = 8, gainBlock = 10 },
            new BossAttack { attackName = "Levantar Escudo", gainThorns = 2 },
            new BossAttack { attackName = "Apunhalar",       damage = 7, hits = 2 },
        };
        EditorUtility.SetDirty(boss);
        AssetDatabase.SaveAssets();
        Debug.Log("Valete de Paus gerado em " + path + " (falta colocar o sprite)");
    }

    static void Make(PlayerClass pc, string name, Suit suit)
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
        EditorUtility.SetDirty(data);
    }
}
