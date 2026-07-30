using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SurvivalHorror
{
    /// <summary>
    /// Thin input wrapper so the pickup/examine system compiles whether the project
    /// uses the new Input System, the legacy Input Manager, or both.
    /// If you already have a PlayerInput / InputActions asset, replace the bodies
    /// of these properties with reads from your own actions.
    /// </summary>
    public static class InputCompat
    {
#if ENABLE_INPUT_SYSTEM
        // New Input System reports mouse delta in pixels and scroll in 120-per-notch.
        // These factors bring it roughly in line with legacy axis magnitudes.
        private const float DeltaScale = 0.1f;
        private const float ScrollScale = 1f / 120f;
#endif

        /// <summary>Primary "use / take" button. Default: E.</summary>
        public static bool InteractPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.E);
#endif
            }
        }

        /// <summary>Close the examine view. Default: Escape or right mouse button.</summary>
        public static bool CancelPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                bool rmb = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
                return esc || rmb;
#else
                return Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1);
#endif
            }
        }

        /// <summary>Held while dragging to spin the examined object. Default: left mouse button.</summary>
        public static bool RotateHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
                return Input.GetMouseButton(0);
#endif
            }
        }

        /// <summary>Frame mouse movement, normalised to legacy-ish magnitudes.</summary>
        public static Vector2 PointerDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current == null) return Vector2.zero;
                return Mouse.current.delta.ReadValue() * DeltaScale;
#else
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
            }
        }

        /// <summary>Scroll wheel, roughly +/-1.0 per physical notch on both backends.</summary>
        public static float ScrollDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current == null) return 0f;
                return Mouse.current.scroll.ReadValue().y * ScrollScale;
#else
                return Input.GetAxis("Mouse ScrollWheel") * 10f;
#endif
            }
        }
    }
}
