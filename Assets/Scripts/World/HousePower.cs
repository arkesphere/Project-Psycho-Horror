using System.Collections;
using System.Linq;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// The house mains. Everything is dark until the fuse goes in.
    ///
    /// Lights are switched off rather than destroyed, and the player's own lights are
    /// deliberately excluded — killing the flashlight along with the house would leave
    /// the player with no way to find the fuse in the first place.
    ///
    /// Put this anywhere in the scene; it collects the lights itself.
    /// </summary>
    public class HousePower : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool poweredAtStart = false;

        [Header("Lights")]
        [Tooltip("Leave empty to collect every scene light automatically at startup.")]
        [SerializeField] private Light[] lights;
        [Tooltip("Lights under these roots are never touched. The player's flashlight " +
                 "must stay independent of the mains.")]
        [SerializeField] private Transform[] excludeRoots;
        [Tooltip("Individual lights the mains never controls. The examine lights must " +
                 "go here: ItemExaminer reparents them onto its own rig at startup, " +
                 "which moves them out from under the player and into this sweep. " +
                 "Listing them by reference is immune to that reparenting.")]
        [SerializeField] private Light[] excludeLights;
        [Tooltip("Directional lights are usually moonlight from outside, not house " +
                 "wiring, so they stay on by default.")]
        [SerializeField] private bool includeDirectional = false;

        [Header("Feel")]
        [Tooltip("Seconds for the lights to come up. A slow rise reads as current " +
                 "reaching the filaments rather than a switch flipping.")]
        [SerializeField] private float warmUpTime = 1.2f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip powerOnSound;

        private float[] fullIntensity;
        private bool powered;

        public bool IsPowered => powered;

        private void Awake()
        {
            if (lights == null || lights.Length == 0) CollectLights();

            // Remember what each light was authored at; that is its "on" value.
            fullIntensity = lights.Select(l => l != null ? l.intensity : 0f).ToArray();

            powered = poweredAtStart;
            ApplyImmediate(powered ? 1f : 0f);
        }

        private void CollectLights()
        {
            lights = FindObjectsByType<Light>(FindObjectsInactive.Include)
                .Where(l => includeDirectional || l.type != LightType.Directional)
                .Where(l => !IsExcluded(l))
                .ToArray();
        }

        private bool IsExcluded(Light light)
        {
            if (excludeLights != null)
                foreach (var l in excludeLights)
                    if (l == light) return true;

            if (excludeRoots != null)
                foreach (var root in excludeRoots)
                    if (root != null && light.transform.IsChildOf(root)) return true;

            return false;
        }

        private void ApplyImmediate(float scale)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].intensity = fullIntensity[i] * scale;
                lights[i].enabled = scale > 0.001f;
            }
        }

        /// <summary>Restores mains power. Called when the fuse is seated.</summary>
        public void TurnOn()
        {
            if (powered) return;
            powered = true;

            if (audioSource != null && powerOnSound != null)
                audioSource.PlayOneShot(powerOnSound);

            StartCoroutine(WarmUp());
        }

        public void TurnOff()
        {
            powered = false;
            StopAllCoroutines();
            ApplyImmediate(0f);
        }

        private IEnumerator WarmUp()
        {
            // Enable first so the rise is visible from zero rather than popping in
            // at whatever intensity the fade happens to reach on its first frame.
            foreach (var l in lights) if (l != null) l.enabled = true;

            float t = 0f;
            while (t < warmUpTime)
            {
                t += Time.deltaTime;
                ApplyScaleOnly(warmUpTime <= 0f ? 1f : Mathf.Clamp01(t / warmUpTime));
                yield return null;
            }
            ApplyScaleOnly(1f);
        }

        private void ApplyScaleOnly(float scale)
        {
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].intensity = fullIntensity[i] * scale;
        }

        /// <summary>Count of lights under mains control, for sanity checks.</summary>
        public int ControlledLightCount => lights != null ? lights.Length : 0;
    }
}
