using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Voyager.UI
{
    /// <summary>
    /// Lectura de teclas que funciona con el Input System nuevo (el que usa este proyecto)
    /// y también con el viejo, por si algún día se cambia en Project Settings → Player.
    /// </summary>
    public static class InputHelper
    {
        public static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        /// <summary>Cualquier tecla, clic o botón de joystick.</summary>
        public static bool AnyKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)) return true;
            if (Gamepad.current != null)
                foreach (var control in Gamepad.current.allControls)
                    if (control is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame) return true;
            return false;
#else
            return Input.anyKeyDown;
#endif
        }
    }
}
