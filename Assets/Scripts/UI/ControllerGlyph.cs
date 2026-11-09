using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

// Icones de botao conforme o controle em uso (PlayStation ou Xbox). Sem controle conectado nao ha icone.
// Sprites em Resources/Botoes/{xbox,play}. O Play nao tem icone de Start.
public enum GlyphRole { Confirm, Cancel, Start, Dpad }

public static class ControllerGlyphs
{
    public static bool IsPlay(Gamepad pad) => pad is DualShockGamepad;

    // Ultimo dispositivo apertado foi um controle (e nao teclado/mouse)? Comeca true se ja houver controle.
    public static bool UsingPad { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void TrackLastDevice()
    {
        UsingPad = Gamepad.current != null;
        InputSystem.onAnyButtonPress.Call(ctrl => UsingPad = ctrl.device is Gamepad);
    }

    public static Sprite Get(GlyphRole role) => UsingPad ? Get(role, Gamepad.current) : null;

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

[RequireComponent(typeof(Image))]
public class ControllerGlyph : MonoBehaviour
{
    public GlyphRole role;

    Image image;
    Gamepad lastPad;
    bool lastUsingPad;

    void Awake() => image = GetComponent<Image>();

    void OnEnable() => Refresh();

    void Update()
    {
        if (Gamepad.current != lastPad || ControllerGlyphs.UsingPad != lastUsingPad) Refresh();
    }

    void Refresh()
    {
        lastPad = Gamepad.current;
        lastUsingPad = ControllerGlyphs.UsingPad;
        var sprite = ControllerGlyphs.Get(role);
        image.sprite = sprite;
        image.enabled = sprite != null;
    }
}
