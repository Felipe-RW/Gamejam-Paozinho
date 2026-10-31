using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Game > Teaser > Criar cena do Teaser: copia a CenadeJogo para Assets/Scenes/Teaser.unity e prepara
// tudo para o TeaserDirector jogar sozinho (2 jogadores, sem inicio automatico, dados ligados).
public static class TeaserSceneBuilder
{
    const string SourceScene = "Assets/Scenes/CenadeJogo.unity";
    public const string TeaserScene = "Assets/Scenes/Teaser.unity";
    const string RecordMenu = "Game/Teaser/Gravar video ao dar Play";

    [MenuItem("Game/Teaser/Criar cena do Teaser", priority = 0)]
    public static void CreateTeaserScene()
    {
        if (!File.Exists(SourceScene))
        {
            EditorUtility.DisplayDialog("Teaser", $"Cena base nao encontrada: {SourceScene}", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(TeaserScene) &&
            !EditorUtility.DisplayDialog("Teaser", "A cena Teaser ja existe. Recriar a partir da CenadeJogo? As mudancas feitas nela serao perdidas.", "Recriar", "Cancelar"))
            return;

        AssetDatabase.DeleteAsset(TeaserScene);
        if (!AssetDatabase.CopyAsset(SourceScene, TeaserScene))
        {
            Debug.LogError("Teaser: nao foi possivel copiar a cena.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(TeaserScene, OpenSceneMode.Single);

        foreach (var selector in Object.FindObjectsByType<LayoutSelector>(FindObjectsInactive.Include))
        {
            selector.testPlayerCount = 4; // teaser 2: os quatro herois
            EditorUtility.SetDirty(selector);
        }
        foreach (var battle in Object.FindObjectsByType<BattleUI>(FindObjectsInactive.Include))
        {
            battle.startOnPlay = false; // quem comeca a partida e o diretor
            EditorUtility.SetDirty(battle);
        }

        var director = new GameObject("StoryTeaserDirector").AddComponent<StoryTeaserDirector>();
        FillStoryDirector(director);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = director.gameObject;
        Debug.Log($"Cena do teaser criada em {TeaserScene}. Aperte Play para assistir; para gravar, ligue {RecordMenu}.");
    }

    // Teaser 2: historia + 4 herois + Valete de Paus.
    static void FillStoryDirector(StoryTeaserDirector d)
    {
        d.heroes = new[]
        {
            Load<ClassData>("Assets/Data/Classes/Paladin.asset"),
            Load<ClassData>("Assets/Data/Classes/Barbarian.asset"),
            Load<ClassData>("Assets/Data/Classes/Healer.asset"),
            Load<ClassData>("Assets/Data/Classes/Sorcerer.asset"),
        };
        d.heroArt = new[] { LoadSprite("paladin_full"), LoadSprite("barbarian_full"), LoadSprite("healer_full"), LoadSprite("bard_full") };
        d.waveEnemies = new[]
        {
            Load<BossData>("Assets/Data/Enemies/SoldadoDeEspadas.asset"),
            Load<BossData>("Assets/Data/Enemies/MercadorDeOuros.asset"),
            Load<BossData>("Assets/Data/Enemies/EscudeiroDeCopas.asset"),
        };
        d.boss = FindBoss("Valete");
        d.storyBackdrop = LoadSprite("menu_background");
        d.jacks = new[] { LoadSprite("jack_club"), LoadSprite("jack_spade"), LoadSprite("jack_heart"), LoadSprite("jack_diamond") };
        d.queens = new[] { LoadSprite("queen_club"), LoadSprite("queen_spade"), LoadSprite("queen_heart"), LoadSprite("queen_diamond") };
        d.kings = new[] { LoadSprite("king_club"), LoadSprite("king_spade"), LoadSprite("king_heart"), LoadSprite("king_diamond") };
        d.font = Load<TMPro.TMP_FontAsset>("Assets/Font/PressStart2P-Regular SDF.asset");

        foreach (var guid in AssetDatabase.FindAssets("Batalha t:AudioClip"))
        {
            d.music = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (d.music != null) break;
        }
        EditorUtility.SetDirty(d);
    }

    static Sprite LoadSprite(string file)
    {
        string path = $"Assets/Sprites/{file}.png";
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite s) return s;
        Debug.LogWarning($"Teaser: sprite nao encontrado: {path}");
        return null;
    }

    // Teaser 1 (2 herois + Dragao). Mantido para quem quiser montar a versao antiga a mao.
    static void FillDirector(TeaserDirector d)
    {
        var barbarian = Load<ClassData>("Assets/Data/Classes/Barbarian.asset");
        var paladin = Load<ClassData>("Assets/Data/Classes/Paladin.asset");
        var healer = Load<ClassData>("Assets/Data/Classes/Healer.asset");
        var sorcerer = Load<ClassData>("Assets/Data/Classes/Sorcerer.asset");

        d.showcaseClasses = new[] { barbarian, paladin, healer, sorcerer };
        d.leftHero = paladin;
        d.rightHero = barbarian;
        d.waveEnemies = new[]
        {
            Load<BossData>("Assets/Data/Enemies/SoldadoDeEspadas.asset"),
            Load<BossData>("Assets/Data/Enemies/MercadorDeOuros.asset"),
            Load<BossData>("Assets/Data/Enemies/EscudeiroDeCopas.asset"),
        };
        d.boss = FindBoss("Drag");
        d.font = Load<TMPro.TMP_FontAsset>("Assets/Font/PressStart2P-Regular SDF.asset");

        foreach (var guid in AssetDatabase.FindAssets("Batalha t:AudioClip"))
        {
            d.music = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (d.music != null) break;
        }

        EditorUtility.SetDirty(d);
    }

    static BossData FindBoss(string namePrefix)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:BossData", new[] { "Assets/Data" }))
        {
            var b = AssetDatabase.LoadAssetAtPath<BossData>(AssetDatabase.GUIDToAssetPath(guid));
            if (b != null && b.displayName != null && b.displayName.StartsWith(namePrefix)) return b;
        }
        Debug.LogWarning($"Teaser: chefe '{namePrefix}...' nao encontrado em Assets/Data.");
        return null;
    }

    static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogWarning($"Teaser: asset nao encontrado: {path}");
        return asset;
    }

    [MenuItem("Game/Teaser/Abrir cena do Teaser", priority = 1)]
    static void OpenTeaserScene()
    {
        if (!File.Exists(TeaserScene)) { CreateTeaserScene(); return; }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(TeaserScene, OpenSceneMode.Single);
    }

    // A gravacao em si fica em Assets/Editor/TeaserRecorder (so compila com o pacote Recorder instalado).
    [MenuItem(RecordMenu, priority = 20)]
    static void ToggleRecord()
    {
        bool on = !EditorPrefs.GetBool(TeaserDirector.RecordPrefKey, false);
        EditorPrefs.SetBool(TeaserDirector.RecordPrefKey, on);
        Debug.Log(on ? "Teaser: o proximo Play na cena Teaser grava o video em <projeto>/Recordings." : "Teaser: gravacao desligada.");
    }

    [MenuItem(RecordMenu, true)]
    static bool ToggleRecordValidate()
    {
        Menu.SetChecked(RecordMenu, EditorPrefs.GetBool(TeaserDirector.RecordPrefKey, false));
        return true;
    }
}
