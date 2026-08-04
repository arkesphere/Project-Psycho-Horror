using UnityEngine;
namespace SurvivalHorror
{
    /// <summary>
    /// Reference-counted gate for suspending player movement/look.
    /// Anything that takes over the screen (examine view, inventory menu, cutscene)
    /// calls Push() on open and Pop() on close. Nested locks are handled correctly.
    ///
    /// Hook into your existing character controller with one line at the top of Update():
    ///     if (PlayerControlGate.Locked) return;
    /// </summary>
    public static class PlayerControlGate
    {
        private static int _lockCount;

        /// <summary>True while at least one system is holding the player still.</summary>
        public static bool Locked => _lockCount > 0;

public static void Push()
        {
            _lockCount++;
            if (_lockCount == 1)
            {
                ApplyCursor(true);
                EventBus.Publish(new PlayerControlLockChangedEvent(true));
            }
        }

public static void Pop()
        {
            if (_lockCount == 0) return;
            _lockCount--;
            if (_lockCount == 0)
            {
                ApplyCursor(false);
                EventBus.Publish(new PlayerControlLockChangedEvent(false));
            }
        }

        /// <summary>Call this on scene load to avoid a stale lock softlocking the player.</summary>
public static void ForceClear()
        {
            bool wasLocked = Locked;
            _lockCount = 0;
            if (wasLocked)
            {
                ApplyCursor(false);
                EventBus.Publish(new PlayerControlLockChangedEvent(false));
            }
        }

        /// <summary>
        /// Set to false if your own controller already owns cursor state and you
        /// don't want this class fighting it.
        /// </summary>
        public static bool ManageCursor = true;

        private static void ApplyCursor(bool freed)
        {
            if (!ManageCursor) return;
            Cursor.lockState = freed ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = freed;
        }
    }
}
