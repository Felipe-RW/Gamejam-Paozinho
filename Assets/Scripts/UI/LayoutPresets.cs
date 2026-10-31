using System;
using UnityEngine;

public class LayoutPresets : MonoBehaviour
{
    [Serializable]
    public class RectState
    {
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 pivot;
        public Vector3 localScale;
    }

    [Serializable]
    public class Preset
    {
        public RectState[] states;
    }

    public RectTransform[] targets;
    public Preset[] presets = new Preset[4];

    public void Apply(int playerCount)
    {
        int index = playerCount - 1;
        if (index < 0 || index >= presets.Length) return;

        var preset = presets[index];
        if (preset == null || preset.states == null || preset.states.Length != targets.Length) return;

        for (int i = 0; i < targets.Length; i++)
        {
            var rt = targets[i];
            var s = preset.states[i];
            if (rt == null) continue;

            rt.anchorMin = s.anchorMin;
            rt.anchorMax = s.anchorMax;
            rt.pivot = s.pivot;
            rt.anchoredPosition = s.anchoredPosition;
            rt.sizeDelta = s.sizeDelta;
            rt.localScale = s.localScale;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(rt);
#endif
        }
    }

    public void Capture(int playerCount)
    {
        int index = playerCount - 1;
        if (index < 0 || index >= presets.Length) return;

        var states = new RectState[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            var rt = targets[i];
            states[i] = rt == null ? new RectState() : new RectState
            {
                anchorMin = rt.anchorMin,
                anchorMax = rt.anchorMax,
                pivot = rt.pivot,
                anchoredPosition = rt.anchoredPosition,
                sizeDelta = rt.sizeDelta,
                localScale = rt.localScale
            };
        }
        presets[index] = new Preset { states = states };
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"Layout de {playerCount} jogador(es) salvo ({targets.Length} objetos).");
    }

    [ContextMenu("Coletar todos os objetos filhos")]
    void CollectTargets()
    {
        var all = GetComponentsInChildren<RectTransform>(true);
        targets = Array.FindAll(all, r => r != transform);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"{targets.Length} objetos coletados.");
    }

    [ContextMenu("Salvar layout: 1 jogador")] void Capture1() => Capture(1);
    [ContextMenu("Salvar layout: 2 jogadores")] void Capture2() => Capture(2);
    [ContextMenu("Salvar layout: 3 jogadores")] void Capture3() => Capture(3);
    [ContextMenu("Salvar layout: 4 jogadores")] void Capture4() => Capture(4);

    [ContextMenu("Aplicar layout: 1 jogador")] void Apply1() => Apply(1);
    [ContextMenu("Aplicar layout: 2 jogadores")] void Apply2() => Apply(2);
    [ContextMenu("Aplicar layout: 3 jogadores")] void Apply3() => Apply(3);
    [ContextMenu("Aplicar layout: 4 jogadores")] void Apply4() => Apply(4);
}
