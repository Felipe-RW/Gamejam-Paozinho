using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UI;

// Icones de botao conforme o controle em uso (PlayStation ou Xbox). Sem controle conectado nao ha icone.
// Sprites em Resources/Botoes/{xbox,play}. O Play nao tem icone de Start.
public enum GlyphRole { Confirm, Cancel, Start, Dpad }

public static class ControllerGlyphs
{
    public static bool IsPlay(Gamepad pad) => pad is DualShockGamepad;

    // Controle mais recente (Gamepad.current), ou null se nenhum estiver conectado.
    public static Sprite Get(GlyphRole role) => Get(role, Gamepad.current);

    // Icone de um controle especifico (null = sem icone).
    public static Sprite Get(GlyphRole role, Gamepad pad)
    {
        if (pad == null) return null;
        bool play = IsPlay(pad);
        string name = (role, play) switch
        {
            (GlyphRole.Confirm, true) => "play/botao-X-Play",
            (GlyphRole.Cancel, true) => "play/botao-O-Play",
            (GlyphRole.Dpad, true) => "play/setas-Play",
            (GlyphRole.Confirm, false) => "xbox/botao-A-xbox",
            (GlyphRole.Cancel, false) => "xbox/botao-B-xbox",
            (GlyphRole.Start, false) => "xbox/botao-start-xbox",
            (GlyphRole.Dpad, false) => "xbox/setas-xbox",
            _ => null,
        };
        return name == null ? null : Resources.Load<Sprite>("Botoes/" + name);
    }
}

// Coloque num objeto com Image: troca o sprite sozinho quando o controle muda e esconde sem controle.
[RequireComponent(typeof(Image))]
public class ControllerGlyph : MonoBehaviour
{
    public GlyphRole role;

    Image image;
    Gamepad lastPad;

    void Awake() => image = GetComponent<Image>();

    void OnEnable() => Refresh();

    void Update()
    {
        if (Gamepad.current != lastPad) Refresh();
    }

    void Refresh()
    {
        lastPad = Gamepad.current;
        var sprite = ControllerGlyphs.Get(role);
        image.sprite = sprite;
        image.enabled = sprite != null;
    }
}
