using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Estilo "3D" para os textos criados por codigo: degrade vertical (claro em cima, escuro embaixo),
// contorno escuro e uma sombra solida deslocada para baixo/direita, que da volume a fonte em pixel.
// Os materiais sao compartilhados por fonte e espessura (nao cria um material por texto).
public static class TextStyle
{
    static readonly Dictionary<(TMP_FontAsset font, int depth), Material> cache = new();
    static readonly Color Outline = new(0.08f, 0.04f, 0.03f, 1f);
    static readonly Color Shadow = new(0.05f, 0.02f, 0.02f, 0.9f);

    // depth: tamanho da sombra (0 = so contorno e degrade; 1 = padrao; 2 = titulos grandes).
    public static void Apply3D(TMP_Text text, float depth = 1f)
    {
        if (text == null) return;

        // Degrade a partir da cor atual do texto; a cor do componente vira branco (o alfa continua valendo).
        Color baseColor = text.color;
        Color top = Color.Lerp(baseColor, Color.white, 0.45f);
        Color bottom = Color.Lerp(baseColor, Color.black, 0.35f);
        top.a = bottom.a = 1f;
        text.enableVertexGradient = true;
        text.colorGradient = new VertexGradient(top, top, bottom, bottom);
        text.color = new Color(1f, 1f, 1f, baseColor.a);

        var font = text.font;
        if (font == null || font.material == null) return;

        int key = Mathf.RoundToInt(depth * 10f);
        if (!cache.TryGetValue((font, key), out var mat) || mat == null)
        {
            mat = new Material(font.material) { name = $"{font.name} 3D {depth}" };
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Outline);
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);

            if (depth > 0f)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                mat.SetColor(ShaderUtilities.ID_UnderlayColor, Shadow);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.55f * depth);
                mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.65f * depth);
                mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.35f);
                mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            }
            cache[(font, key)] = mat;
        }
        text.fontSharedMaterial = mat;
    }
}
