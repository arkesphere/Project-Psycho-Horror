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
    public static class InputCombat
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

        /// <summary>Fire/attack with the currently held weapon. Default: left mouse button.</summary>
        public static bool FirePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        /// <summary>Reload the current weapon. Default: R.</summary>
        public static bool ReloadPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.R);
#endif
            }
        }

        /// <summary>Play the weapon inspect animation. Default: I.</summary>
        public static bool InspectPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.I);
#endif
            }
        }

        /// <summary>Generic hand-touch gesture (e.g. checking a door). Default: T.</summary>
        public static bool TouchPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.T);
#endif
            }
        }

        /// <summary>Switch to the knife. Default: 2.</summary>
        public static bool EquipKnifePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Alpha2);
#endif
            }
        }

        /// <summary>Switch to the gun. Default: 1.</summary>
        public static bool EquipGunPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.digit1Key.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Alpha1);
#endif
            }
        }
        
    }
}
