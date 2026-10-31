using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Game > Prologo > Criar cena do Prologo: copia a CenadeJogo para Assets/Scenes/Prologo.unity,
// prepara o PrologueDirector e coloca a cena no Build Settings entre o Menu e a CenadeJogo.
public static class PrologueSceneBuilder
{
    const string SourceScene = "Assets/Scenes/CenadeJogo.unity";
    const string PrologueScene = "Assets/Scenes/Prologo.unity";

    [MenuItem("Game/Prologo/Criar cena do Prologo", priority = 0)]
    public static void CreatePrologueScene()
    {
        if (!File.Exists(SourceScene))
        {
            EditorUtility.DisplayDialog("Prologo", $"Cena base nao encontrada: {SourceScene}", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(PrologueScene) &&
            !EditorUtility.DisplayDialog("Prologo", "A cena Prologo ja existe. Recriar a partir da CenadeJogo? As mudancas feitas nela serao perdidas.", "Recriar", "Cancelar"))
            return;

        AssetDatabase.DeleteAsset(PrologueScene);
        if (!AssetDatabase.CopyAsset(SourceScene, PrologueScene))
        {
            Debug.LogError("Prologo: nao foi possivel copiar a cena.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(PrologueScene, OpenSceneMode.Single);

        foreach (var selector in Object.FindObjectsByType<LayoutSelector>(FindObjectsInactive.Include))
        {
            selector.testPlayerCount = 2; // igual as classes de teste do diretor
            EditorUtility.SetDirty(selector);
        }
        foreach (var battle in Object.FindObjectsByType<BattleUI>(FindObjectsInactive.Include))
        {
            battle.startOnPlay = false; // quem comeca o treino e o diretor
            EditorUtility.SetDirty(battle);
        }

        var director = new GameObject("PrologueDirector").AddComponent<PrologueDirector>();
        Fill(director);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddToBuildSettings();
        Selection.activeGameObject = director.gameObject;
        Debug.Log($"Prologo criado em {PrologueScene} e adicionado ao Build Settings. Ele toca sempre depois da selecao de herois, com opcao de pular.");
    }

    static void Fill(PrologueDirector d)
    {
        d.fallbackClasses = new[]
        {
            Load<ClassData>("Assets/Data/Classes/Paladin.asset"),
            Load<ClassData>("Assets/Data/Classes/Barbarian.asset"),
        };
        d.scoutA = Load<BossData>("Assets/Data/Enemies/SoldadoDeEspadas.asset");
        d.scoutB = Load<BossData>("Assets/Data/Enemies/MercadorDeOuros.asset");

        d.kingSprite = LoadSprite("king_spade");
        d.jacks = new[] { LoadSprite("jack_club"), LoadSprite("jack_spade"), LoadSprite("jack_heart"), LoadSprite("jack_diamond") };
        d.queens = new[] { LoadSprite("queen_club"), LoadSprite("queen_spade"), LoadSprite("queen_heart"), LoadSprite("queen_diamond") };
        d.kings = new[] { LoadSprite("king_club"), LoadSprite("king_spade"), LoadSprite("king_heart"), LoadSprite("king_diamond") };

        d.font = Load<TMPro.TMP_FontAsset>("Assets/Font/PressStart2P-Regular SDF.asset");
        foreach (var guid in AssetDatabase.FindAssets("kaazoom t:AudioClip"))
        {
            d.music = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (d.music != null) break;
        }
        EditorUtility.SetDirty(d);
    }

    // Menu -> Prologo -> CenadeJogo (as outras cenas ficam como estavam).
    static void AddToBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == PrologueScene)) return;

        int at = scenes.FindIndex(s => s.path == SourceScene);
        var entry = new EditorBuildSettingsScene(PrologueScene, true);
        if (at < 0) scenes.Add(entry);
        else scenes.Insert(at, entry);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    [MenuItem("Game/Prologo/Abrir cena do Prologo", priority = 1)]
    static void OpenPrologueScene()
    {
        if (!File.Exists(PrologueScene)) { CreatePrologueScene(); return; }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(PrologueScene, OpenSceneMode.Single);
    }

    static Sprite LoadSprite(string file)
    {
        string path = $"Assets/Sprites/{file}.png";
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite s) return s;
        Debug.LogWarning($"Prologo: sprite nao encontrado: {path}");
        return null;
    }

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogWarning($"Prologo: asset nao encontrado: {path}");
        return asset;
    }
}
