using System;
using System.Collections;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// A looping ambience bed plus its own per-clip level trim.
    /// </summary>
    [Serializable]
    public class AmbienceBed
    {
        public AudioClip clip;
        [Tooltip("Trim so every bed sits at the same perceived loudness. These were " +
                 "measured from the source files; changing clips means re-trimming.")]
        [Range(0f, 1f)] public float volume = 1f;
        public bool enabled = true;
    }

    /// <summary>
    /// Room tone and random creaks.
    ///
    /// The bed is 2D and looping: it is the room itself, not an object in it, so
    /// localising it would be wrong. Creaks are the opposite — they are placed at a
    /// random point around the player in 3D, because a creak that comes from a
    /// specific direction behind you is the entire point of the sound.
    ///
    /// Put this anywhere in the scene; it follows the listener on its own.
    /// </summary>
    public class AmbienceController : MonoBehaviour
    {
        [Header("Bed (looping room tone)")]
        [SerializeField] private AmbienceBed[] beds;
        [Tooltip("Master trim over every bed.")]
        [Range(0f, 1f)] [SerializeField] private float bedVolume = 0.55f;
        [Tooltip("Seconds to fade the beds up on start, so it does not slam in.")]
        [SerializeField] private float fadeInTime = 3f;

        [Header("Creaks (random one-shots)")]
        [SerializeField] private AudioClip[] creaks;
        [Range(0f, 1f)] [SerializeField] private float creakVolume = 0.55f;
        [Tooltip("Seconds between creaks; a value is picked at random in this range.")]
        [SerializeField] private Vector2 creakInterval = new Vector2(14f, 38f);
        [Tooltip("How far from the listener a creak is placed.")]
        [SerializeField] private Vector2 creakDistance = new Vector2(3f, 9f);
        [Tooltip("Vertical spread, so creaks can come from the floor above or below.")]
        [SerializeField] private float creakHeightSpread = 2.5f;
        [SerializeField] private Vector2 creakPitch = new Vector2(0.9f, 1.1f);

        private AudioSource[] bedSources;
        private AudioSource creakSource;
        private Transform listener;
        private int lastCreak = -1;

        private void Awake()
        {
            var al = FindFirstObjectByType<AudioListener>();
            listener = al != null ? al.transform : null;

            // One source per bed so each keeps its own trim and loop point.
            if (beds != null)
            {
                bedSources = new AudioSource[beds.Length];
                for (int i = 0; i < beds.Length; i++)
                {
                    var go = new GameObject("Bed_" + i);
                    go.transform.SetParent(transform, false);
                    var src = go.AddComponent<AudioSource>();
                    src.clip = beds[i].clip;
                    src.loop = true;
                    src.playOnAwake = false;
                    src.spatialBlend = 0f;   // the room, not a point in it
                    src.volume = 0f;
                    bedSources[i] = src;
                }
            }

            var creakGo = new GameObject("CreakSource");
            creakGo.transform.SetParent(transform, false);
            creakSource = creakGo.AddComponent<AudioSource>();
            creakSource.playOnAwake = false;
            creakSource.spatialBlend = 1f;   // positional
            creakSource.rolloffMode = AudioRolloffMode.Linear;
            creakSource.minDistance = 1f;
            creakSource.maxDistance = 25f;
        }

        private void Start()
        {
            if (bedSources != null)
            {
                for (int i = 0; i < bedSources.Length; i++)
                    if (beds[i].enabled && beds[i].clip != null) bedSources[i].Play();
                StartCoroutine(FadeBedsIn());
            }

            if (creaks != null && creaks.Length > 0) StartCoroutine(CreakLoop());
        }

        private IEnumerator FadeBedsIn()
        {
            float t = 0f;
            while (t < fadeInTime)
            {
                t += Time.deltaTime;
                float k = fadeInTime <= 0f ? 1f : Mathf.Clamp01(t / fadeInTime);
                ApplyBedVolumes(k);
                yield return null;
            }
            ApplyBedVolumes(1f);
        }

        private void ApplyBedVolumes(float scale)
        {
            if (bedSources == null) return;
            for (int i = 0; i < bedSources.Length; i++)
                bedSources[i].volume = beds[i].volume * bedVolume * scale;
        }

        private IEnumerator CreakLoop()
        {
            // Never open on a creak; let the room establish itself first.
            yield return new WaitForSeconds(UnityEngine.Random.Range(creakInterval.x, creakInterval.y));

            while (true)
            {
                PlayCreak();
                yield return new WaitForSeconds(UnityEngine.Random.Range(creakInterval.x, creakInterval.y));
            }
        }

        private void PlayCreak()
        {
            if (creaks.Length == 0) return;

            int i;
            if (creaks.Length == 1) i = 0;
            else do { i = UnityEngine.Random.Range(0, creaks.Length); } while (i == lastCreak);
            lastCreak = i;
            if (creaks[i] == null) return;

            // Somewhere on a ring around the listener, at a random height, so it is
            // never predictable which direction the house settles from.
            Vector3 origin = listener != null ? listener.position : transform.position;
            Vector2 flat = UnityEngine.Random.insideUnitCircle.normalized
                           * UnityEngine.Random.Range(creakDistance.x, creakDistance.y);
            creakSource.transform.position = origin + new Vector3(
                flat.x,
                UnityEngine.Random.Range(-creakHeightSpread, creakHeightSpread),
                flat.y);

            creakSource.pitch = UnityEngine.Random.Range(creakPitch.x, creakPitch.y);
            creakSource.PlayOneShot(creaks[i], creakVolume);
        }

        /// <summary>Fires a creak immediately. Useful for scripted scares.</summary>
        public void TriggerCreak() => PlayCreak();
    }
}
