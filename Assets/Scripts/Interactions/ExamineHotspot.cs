using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// A clickable part of a model shown in the examine view — the toolbox lid, a
    /// clasp, a hidden drawer. Put this on the sub-object, together with a collider
    /// covering the part you want the player to be able to pick out.
    ///
    /// The examiner aims from the centre of the screen (there is no free cursor
    /// while examining — the mouse turns the object) and publishes
    /// ExamineHotspotActivatedEvent when the player presses interact on it. Whatever
    /// placed the item in the view decides what that means; the hotspot itself has
    /// no behaviour of its own.
    ///
    /// Deliberately does not touch materials. Writing _EmissiveColor through a
    /// property block to "highlight" a part overwrites whatever the material was
    /// authored with and cannot be undone cleanly, which shows up as the model
    /// looking wrong in the examine view. The on-screen hint carries the cue instead.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExamineHotspot : MonoBehaviour
    {
        [Tooltip("Identifies this part in the activation event, e.g. \"Lid\".")]
        [SerializeField] private string hotspotId = "Lid";

        [Tooltip("Colliders that can be aimed at. Left empty, every collider on this " +
                 "object and its children is used.")]
        [SerializeField] private Collider[] targets;

        public string HotspotId => hotspotId;

        private void Reset()
        {
            targets = GetComponentsInChildren<Collider>(true);
        }

        /// <summary>Colliders the examiner may aim at. Never null.</summary>
        public Collider[] Targets
        {
            get
            {
                if (targets == null || targets.Length == 0)
                    targets = GetComponentsInChildren<Collider>(true);
                return targets;
            }
        }
    }
}
