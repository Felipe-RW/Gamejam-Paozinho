using System.Collections.Generic;
using UnityEngine.InputSystem;

// Identifica cada dispositivo por um int: teclado/mouse = KeyboardMouse, controles = deviceId.
public static class PlayerInputs
{
    public const int None = int.MinValue;
    public const int KeyboardMouse = -1;

    public static IEnumerable<int> Devices()
    {
        if (Keyboard.current != null) yield return KeyboardMouse;
        foreach (var pad in Gamepad.all) yield return pad.deviceId;
    }

    public static bool Left(int id) => id == KeyboardMouse
        ? Key2(Key.LeftArrow, Key.A)
        : Pad(id, p => p.dpad.left.wasPressedThisFrame || p.leftStick.left.wasPressedThisFrame);

    public static bool Right(int id) => id == KeyboardMouse
        ? Key2(Key.RightArrow, Key.D)
        : Pad(id, p => p.dpad.right.wasPressedThisFrame || p.leftStick.right.wasPressedThisFrame);

    public static bool Up(int id) => id == KeyboardMouse
        ? Key2(Key.UpArrow, Key.W)
        : Pad(id, p => p.dpad.up.wasPressedThisFrame || p.leftStick.up.wasPressedThisFrame);

    public static bool Down(int id) => id == KeyboardMouse
        ? Key2(Key.DownArrow, Key.S)
        : Pad(id, p => p.dpad.down.wasPressedThisFrame || p.leftStick.down.wasPressedThisFrame);

    public static bool Submit(int id) => id == KeyboardMouse
        ? Key2(Key.Enter, Key.Space) || Key2(Key.NumpadEnter, Key.NumpadEnter)
        : Pad(id, p => p.buttonSouth.wasPressedThisFrame);

    public static bool Cancel(int id) => id == KeyboardMouse
        ? Key2(Key.Backspace, Key.Backspace)
        : Pad(id, p => p.buttonEast.wasPressedThisFrame);

    public static bool Start(int id) => id != KeyboardMouse && Pad(id, p => p.startButton.wasPressedThisFrame);

    public static bool Select(int id) => id != KeyboardMouse && Pad(id, p => p.selectButton.wasPressedThisFrame);

    static bool Key2(Key a, Key b)
    {
        var kb = Keyboard.current;
        return kb != null && (kb[a].wasPressedThisFrame || kb[b].wasPressedThisFrame);
    }

    static bool Pad(int id, System.Func<Gamepad, bool> check)
    {
        foreach (var pad in Gamepad.all)
            if (pad.deviceId == id) return check(pad);
        return false;
    }
}
