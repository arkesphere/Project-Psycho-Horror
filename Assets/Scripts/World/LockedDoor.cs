using System.Collections;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Keeps a physics door shut until the player is carrying the right key.
    ///
    /// Locking is done by making the door kinematic, not by clamping the hinge.
    /// DoorLatch's latched limits are only ±0.2°, and against a 10 kg player a 1 kg
    /// door simply gets shoved through them by the solver — the door swung open
    /// while still reporting itself locked. A kinematic body cannot be pushed at all.
    /// DoorLatch is disabled alongside it so its collision release can't fire either.
    ///
    /// Put this on the same object as the DoorLatch.
    /// </summary>
    [RequireComponent(typeof(DoorLatch))]
    public class LockedDoor : MonoBehaviour
    {
        [Header("Lock")]
        [SerializeField] private DoorLatch latch;
        [SerializeField] private Rigidbody body;
        [Tooltip("Item that unlocks this door. Held in the inventory; not consumed.")]
        [SerializeField] private ItemData keyItem;
        [SerializeField] private bool lockedAtStart = true;

        [Header("Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip lockedSound;
        [SerializeField] private AudioClip unlockSound;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.9f;
        [Tooltip("Seconds before the rattle can play again, so repeated bumps do not " +
                 "machine-gun the sound.")]
        [SerializeField] private float retriggerDelay = 1.2f;
        [SerializeField] private string lockedMessage = "It's locked.";
        [SerializeField] private string unlockedMessage = "Unlocked with the key.";

        [Header("Detection")]
        [Tooltip("Who counts as the player when they push on the door.")]
        [SerializeField] private string playerTag = "Player";

        private bool locked;
        private float nextSoundTime;
        private PlayerInteractor cachedInteractor;

        public bool IsLocked => locked;

        private void Reset()
        {
            latch = GetComponent<DoorLatch>();
            body = GetComponent<Rigidbody>();
            audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (latch == null) latch = GetComponent<DoorLatch>();
            if (body == null) body = GetComponent<Rigidbody>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            locked = lockedAtStart;
        }

        private IEnumerator Start()
        {
            if (latch == null) yield break;

            // DoorLatch squares the door up against its frame in its own Start, one
            // FixedUpdate in. Freezing it before that would lock it at whatever angle
            // it happened to be resting at, so wait for the latch to engage first.
            int guard = 0;
            while (!latch.IsLatched && guard++ < 300) yield return new WaitForFixedUpdate();

            if (locked) ApplyLock(true);
        }

        private void ApplyLock(bool on)
        {
            if (latch != null) latch.enabled = !on;

            if (body != null)
            {
                if (on)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = on;
            }
        }

        // Enter only. OnCollisionStay fires every physics step, so leaning on the door
        // kept re-arming the rattle; a locked door should answer a push, then go quiet.
        private void OnCollisionEnter(Collision collision)
        {
            if (!locked) return;
            if (collision.rigidbody == null || !collision.rigidbody.CompareTag(playerTag)) return;

            if (cachedInteractor == null)
                cachedInteractor = collision.rigidbody.transform.root
                    .GetComponentInChildren<PlayerInteractor>(true);

            // Pushing on the door is what turns the key. If the player is carrying it,
            // the door simply opens instead of rattling — no separate unlock input.
            if (cachedInteractor != null && TryUnlock(cachedInteractor.Inventory))
            {
                if (!string.IsNullOrEmpty(unlockedMessage))
                    cachedInteractor.ShowMessage(unlockedMessage);
                return;
            }

            if (Time.time < nextSoundTime) return;
            nextSoundTime = Time.time + retriggerDelay;

            if (audioSource != null && lockedSound != null)
                audioSource.PlayOneShot(lockedSound, volume);

            if (cachedInteractor != null && !string.IsNullOrEmpty(lockedMessage))
                cachedInteractor.ShowMessage(lockedMessage);
        }

        /// <summary>
        /// Unlocks if the player holds the key. Safe to call repeatedly; it does
        /// nothing once already unlocked.
        /// </summary>
        public bool TryUnlock(Inventory inventory)
        {
            if (!locked) return true;
            if (keyItem == null || inventory == null || !inventory.Has(keyItem)) return false;

            Unlock();
            return true;
        }

        /// <summary>Unlocks unconditionally.</summary>
        public void Unlock()
        {
            if (!locked) return;
            locked = false;
            ApplyLock(false);

            if (audioSource != null && unlockSound != null)
                audioSource.PlayOneShot(unlockSound, volume);
        }
    }
}
