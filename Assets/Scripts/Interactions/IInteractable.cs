using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Implement this on anything the crosshair can target: pickups, doors,
    /// drawers, note boards, lever puzzles. PlayerInteractor only knows about
    /// this interface, so new interactable types need no changes to the player.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>False hides the prompt and blocks input (locked door, empty container).</summary>
        bool CanInteract { get; }

        /// <summary>
        /// World point the floating key-hint should appear at (RE-style: anchored to
        /// the object in 3D space, not a fixed screen position). Include any desired
        /// vertical offset here — PlayerInteractor applies none of its own.
        /// </summary>
        Vector3 PromptWorldPosition { get; }

        /// <summary>Line shown in the prompt UI, e.g. "Take Green Herb".</summary>
        string GetPromptText();

        /// <summary>
        /// The crosshair is close enough AND looking at this to interact right now.
        /// This is the state that shows the actual "E" prompt.
        /// </summary>
        void OnFocusEnter();

        /// <summary>Crosshair moved off this object, or it's no longer in interact range.</summary>
        void OnFocusExit();

        /// <summary>
        /// The player has merely come within a larger proximity radius — not
        /// necessarily looking at this, not yet close enough to interact. This is
        /// the RE-style "something's here" cue: a subtle highlight with no prompt,
        /// shown before the player is close enough for OnFocusEnter to fire.
        /// </summary>
        void OnProximityEnter();

        /// <summary>Player has left the proximity radius entirely.</summary>
        void OnProximityExit();

        /// <summary>Player pressed the interact key while focused on this object.</summary>
        void Interact(PlayerInteractor interactor);
    }
}
