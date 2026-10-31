using System;
using System.Collections.Generic;
using UnityEngine;

// Sequencias de quadros (flipbooks) usadas pelos efeitos de batalha.
// O asset fica em Assets/Resources/FxLibrary.asset e e criado/atualizado automaticamente no editor.
[CreateAssetMenu(menuName = "Game/FX Library", fileName = "FxLibrary")]
public class FxLibrary : ScriptableObject
{
    [Serializable]
    public class Sequence
    {
        public string id;
        public Sprite[] frames;
        public float fps = 14f;
    }

    public const string AssetPath = "Assets/Resources/FxLibrary.asset";
    const string RubberDuck = "Assets/Temp/Effects - Author rubberduck";
    const string Fire = "Assets/Temp/Pixel fire asset pack v1.2/Pixel Fire Asset Pack Colored";

    // id, pasta com os quadros (nome_01.png, nome_1.png...), quadros por segundo.
    public static readonly (string id, string folder, float fps)[] Sources =
    {
        ("sparks",      $"{RubberDuck}/sparks_effect - - Author rubberduck", 22),
        ("glow",        $"{RubberDuck}/light_glow_effect - Author rubberduck", 18),
        ("magic",       $"{RubberDuck}/magic_effect - Author rubberduck", 18),
        ("magic2",      $"{RubberDuck}/magic_effect_2 - - Author rubberduck", 18),
        ("ring",        $"{RubberDuck}/green_effect - - Author rubberduck", 18),
        ("fire_ball",   $"{Fire}/fire asset red/Group 4 - 5", 16),
        ("fire_burst",  $"{Fire}/fire asset red/Group 6 - 5", 16),
        ("fire_dark",   $"{Fire}/fire asset red/Group 7 - 5", 16),
        ("fire_tall",   $"{Fire}/fire asset red/Group 4 - 3", 16),
        ("fire_purple", $"{Fire}/fire asset purple/Group 4 - 5", 16),
        ("fire_yellow", $"{Fire}/fire asset yellow/Group 6 - 5", 16),
        ("smoke_dark",  $"{Fire}/fire asset smoke/Smoke Dark - 1", 14),
        ("smoke_light", $"{Fire}/fire asset smoke/Smoke Light - 3", 14),
    };

    public List<Sequence> sequences = new();

    public Sequence Get(string id)
    {
        var s = sequences.Find(x => x.id == id);
        return s != null && s.frames != null && s.frames.Length > 0 ? s : null;
    }

    static FxLibrary cached;

    public static FxLibrary Load()
    {
        if (cached != null) return cached;
        cached = Resources.Load<FxLibrary>("FxLibrary");
#if UNITY_EDITOR
        // Rede de seguranca no editor: monta na memoria direto das pastas se o asset nao existir.
        if (cached == null || cached.sequences.Count == 0) cached = BuildInEditor(null);
#endif
        if (cached == null) Debug.LogError("FxLibrary nao encontrada em Resources: os efeitos de batalha nao vao aparecer.");
        return cached;
    }

#if UNITY_EDITOR
    // Le os quadros das pastas de Sources. Se "target" for nulo, cria uma instancia so na memoria.
    public static FxLibrary BuildInEditor(FxLibrary target)
    {
        var lib = target != null ? target : CreateInstance<FxLibrary>();
        lib.sequences = new List<Sequence>();

        foreach (var (id, folder, fps) in Sources)
        {
            if (!System.IO.Directory.Exists(folder)) { Debug.LogWarning($"Pasta de efeito nao encontrada: {folder}"); continue; }

            var files = new List<(int order, string path)>();
            foreach (var f in System.IO.Directory.GetFiles(folder, "*.png"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(System.IO.Path.GetFileNameWithoutExtension(f), @"_(\d+)$");
                if (!m.Success) continue; // a tira com todos os quadros (sem sufixo) e ignorada
                files.Add((int.Parse(m.Groups[1].Value), f.Replace('\\', '/')));
            }
            files.Sort((a, b) => a.order.CompareTo(b.order));

            var frames = new List<Sprite>();
            foreach (var (_, file) in files)
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(file))
                    if (asset is Sprite s) { frames.Add(s); break; }

            if (frames.Count > 0) lib.sequences.Add(new Sequence { id = id, frames = frames.ToArray(), fps = fps });
            else Debug.LogWarning($"Nenhum quadro de efeito em {folder}");
        }
        return lib;
    }
#endif
}
