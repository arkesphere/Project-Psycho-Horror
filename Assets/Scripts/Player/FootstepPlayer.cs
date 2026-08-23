using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// One set of footstep clips for a kind of ground, plus the material names that
    /// should use it.
    /// </summary>
    [Serializable]
    public class FootstepSurface
    {
        public string name = "Wood";
        public AudioClip[] clips;

        [Tooltip("Substrings matched against the material name under the player, " +
                 "case-insensitive. e.g. 'M_Floor_D' or just 'tile'.")]
        public string[] materialKeywords;

        [Range(0f, 1f)] public float volume = 0.8f;
        public Vector2 pitchRange = new Vector2(0.92f, 1.08f);
    }

    /// <summary>
    /// Footsteps driven by distance travelled rather than a timer.
    ///
    /// A timer has to be re-tuned every time movement speed changes, and desyncs the
    /// moment the player accelerates. Accumulating horizontal distance and firing a
    /// step every stride length paces itself: walking, sprinting and crouching all
    /// produce correctly spaced steps from one number.
    ///
    /// Put this on the Player root.
    /// </summary>
    public class FootstepPlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioSource source;
        [SerializeField] private Rigidbody playerBody;
        [SerializeField] private CapsuleCollider body;

        [Header("Surfaces")]
        [Tooltip("Checked in order; the first whose keyword matches wins. The last " +
                 "entry acts as the fallback when nothing matches.")]
        [SerializeField] private FootstepSurface[] surfaces;

        [Header("Stride")]
        [Tooltip("Metres travelled between footsteps.")]
        [SerializeField] private float strideLength = 2.1f;
        [Tooltip("Stride is shortened by this factor while sprinting, so a run sounds " +
                 "faster rather than just louder.")]
        [SerializeField] private float sprintStrideScale = 0.78f;
        [SerializeField] private float sprintSpeedThreshold = 2.75f;
        [Tooltip("Below this speed no steps play, so drift or nudges stay silent.")]
        [SerializeField] private float minSpeed = 0.35f;

        [Header("Ground Probe")]
        [SerializeField] private float groundProbeDistance = 1.2f;
        [SerializeField] private LayerMask groundMask = ~0;

        private float distanceAccumulator;
        private int lastClipIndex = -1;
        private FootstepSurface currentSurface;

        // Resolving a material to a surface walks a keyword list, so the answer is
        // cached per material rather than recomputed on every single step.
        private readonly Dictionary<string, FootstepSurface> surfaceCache =
            new Dictionary<string, FootstepSurface>();

        private void Reset()
        {
            playerBody = GetComponent<Rigidbody>();
            body = GetComponent<CapsuleCollider>();
            source = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (playerBody == null) playerBody = GetComponent<Rigidbody>();
            if (body == null) body = GetComponent<CapsuleCollider>();

            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                // The player's own feet are not a point in the world to localise.
                source.spatialBlend = 0f;
            }
        }

        private void Update()
        {
            if (playerBody == null || surfaces == null || surfaces.Length == 0) return;
            if (PlayerControlGate.Locked) return;

            Vector3 v = playerBody.linearVelocity;
            v.y = 0f;
            float speed = v.magnitude;

            if (speed < minSpeed)
            {
                // Bleed the accumulator so stopping and starting does not instantly
                // fire a step from leftover distance.
                distanceAccumulator = Mathf.MoveTowards(distanceAccumulator, 0f, Time.deltaTime);
                return;
            }

            if (!IsGrounded()) return;

            distanceAccumulator += speed * Time.deltaTime;

            float stride = speed >= sprintSpeedThreshold
                ? strideLength * sprintStrideScale
                : strideLength;

            if (distanceAccumulator >= stride)
            {
                distanceAccumulator -= stride;
                PlayStep();
            }
        }

        private bool IsGrounded()
        {
            Vector3 origin = body != null
                ? new Vector3(body.bounds.center.x, body.bounds.min.y + 0.1f, body.bounds.center.z)
                : transform.position;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                 groundProbeDistance, groundMask, QueryTriggerInteraction.Ignore))
                return false;

            if (hit.collider.transform.IsChildOf(transform)) return false;

            currentSurface = ResolveSurface(hit.collider);
            return true;
        }

        private FootstepSurface ResolveSurface(Collider col)
        {
            var rend = col.GetComponent<Renderer>();
            string matName = rend != null && rend.sharedMaterial != null
                ? rend.sharedMaterial.name
                : string.Empty;

            if (surfaceCache.TryGetValue(matName, out var cached)) return cached;

            FootstepSurface found = null;
            foreach (var s in surfaces)
            {
                if (s.materialKeywords == null) continue;
                foreach (var key in s.materialKeywords)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    if (matName.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = s;
                        break;
                    }
                }
                if (found != null) break;
            }

            // Last entry is the fallback for anything unmapped.
            if (found == null) found = surfaces[surfaces.Length - 1];

            surfaceCache[matName] = found;
            return found;
        }

        private void PlayStep()
        {
            var s = currentSurface ?? surfaces[surfaces.Length - 1];
            if (s == null || s.clips == null || s.clips.Length == 0) return;

            int index;
            if (s.clips.Length == 1) index = 0;
            else
            {
                // Never the same clip twice running; back-to-back repeats are what
                // make a footstep loop sound synthetic.
                do { index = UnityEngine.Random.Range(0, s.clips.Length); }
                while (index == lastClipIndex);
            }
            lastClipIndex = index;

            source.pitch = UnityEngine.Random.Range(s.pitchRange.x, s.pitchRange.y);
            source.PlayOneShot(s.clips[index], s.volume);
        }
    }
}
