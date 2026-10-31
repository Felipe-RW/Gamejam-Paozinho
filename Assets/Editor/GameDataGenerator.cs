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
        boss.healthByPlayerCount = new[] { 60, 80, 120, 160 };
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

    // Monta Assets/Resources/FxLibrary.asset com os flipbooks usados pelos efeitos de batalha.
    // Roda sozinho ao abrir/recompilar o projeto se o asset nao existir (ver EnsureFxLibrary).
    [MenuItem("Game/Generate FX Library")]
    public static void GenerateFxLibrary()
    {
        Directory.CreateDirectory("Assets/Resources");
        foreach (var (_, folder, _) in FxLibrary.Sources)
            if (Directory.Exists(folder))
                foreach (var f in Directory.GetFiles(folder, "*.png")) MakePixelPerfect(f.Replace('\\', '/'));

        var lib = AssetDatabase.LoadAssetAtPath<FxLibrary>(FxLibrary.AssetPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<FxLibrary>();
            AssetDatabase.CreateAsset(lib, FxLibrary.AssetPath);
        }
        FxLibrary.BuildInEditor(lib);
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log($"FxLibrary gerada em {FxLibrary.AssetPath} com {lib.sequences.Count} sequencias.");
    }

    [InitializeOnLoadMethod]
    static void EnsureFxLibrary()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var lib = AssetDatabase.LoadAssetAtPath<FxLibrary>(FxLibrary.AssetPath);
            if (lib == null || lib.sequences.Count == 0) GenerateFxLibrary();
        };
    }

    // Pixel art: filtro Point e sem compressao para nao borrar ao ampliar.
    static void MakePixelPerfect(string file)
    {
        if (AssetImporter.GetAtPath(file) is not TextureImporter ti) return;
        if (ti.filterMode == FilterMode.Point && ti.textureCompression == TextureImporterCompression.Uncompressed) return;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
    }

    const string EnemiesPath = "Assets/Data/Enemies";

    // Cria inimigos basicos das ondas e adiciona ao GameManager da cena aberta.
    [MenuItem("Game/Generate Enemies")]
    public static void GenerateEnemies()
    {
        Directory.CreateDirectory(EnemiesPath);
        var atk = LoadSprite($"{SpritesPath}/boss_attack.png");
        var blk = LoadSprite($"{SpritesPath}/boss_block.png");
        var buf = LoadSprite($"{SpritesPath}/boss_buff.png");

        var created = new System.Collections.Generic.List<BossData>
        {
            MakeEnemy("SoldadoDeEspadas", "Soldado de Espadas", "jack_spade", new[] { 14, 18, 24, 30 }, 0,
                new BossAttack { attackName = "Corte", icon = atk, damage = 5 },
                new BossAttack { attackName = "Estocada Dupla", icon = atk, damage = 3, hits = 2 },
                new BossAttack { attackName = "Guarda", icon = blk, damage = 2, gainBlock = 4 }),
            MakeEnemy("EscudeiroDeCopas", "Escudeiro de Copas", "jack_heart", new[] { 18, 24, 32, 40 }, 1,
                new BossAttack { attackName = "Escudada", icon = blk, damage = 3, gainBlock = 4 },
                new BossAttack { attackName = "Pancada", icon = atk, damage = 5 },
                new BossAttack { attackName = "Espinhos", icon = buf, damage = 2, gainThorns = 1 }),
            MakeEnemy("MercadorDeOuros", "Mercador de Ouros", "jack_diamond", new[] { 12, 16, 22, 28 }, 0,
                new BossAttack { attackName = "Moedada", icon = atk, damage = 4 },
                new BossAttack { attackName = "Chuva de Moedas", icon = atk, damage = 2, hits = 3 },
                new BossAttack { attackName = "Cofre", icon = buf, damage = 1, gainArmor = 1 }),
        };
        AssetDatabase.SaveAssets();

        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            Undo.RecordObject(gm, "Add enemy types");
            foreach (var e in created)
                if (!gm.enemyTypes.Contains(e)) gm.enemyTypes.Add(e);
            EditorUtility.SetDirty(gm);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            Debug.Log("Inimigos gerados em " + EnemiesPath + " e adicionados ao GameManager. Salve a cena.");
        }
        else
        {
            Debug.Log("Inimigos gerados em " + EnemiesPath + ". Abra a cena do jogo e arraste-os para Enemy Types do GameManager.");
        }
    }

    static BossData MakeEnemy(string file, string name, string sprite, int[] health, int armor, params BossAttack[] attacks)
    {
        string path = $"{EnemiesPath}/{file}.asset";
        var data = AssetDatabase.LoadAssetAtPath<BossData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BossData>();
            AssetDatabase.CreateAsset(data, path);
        }
        data.displayName = name;
        data.sprite = LoadSprite($"{SpritesPath}/{sprite}.png");
        data.healthByPlayerCount = health;
        data.startingArmor = armor;
        data.fixedOrder = false;
        data.attacks = new System.Collections.Generic.List<BossAttack>(attacks);
        EditorUtility.SetDirty(data);
        return data;
    }

    static Sprite LoadSprite(string path)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite s) return s;
        Debug.LogWarning($"Sprite não encontrado: {path}");
        return null;
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
