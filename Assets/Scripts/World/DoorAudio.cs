using UnityEngine;

/// <summary>
/// Plays open / close / locked sounds for a DoorLatch.
///
/// This watches DoorLatch's existing IsLatched state rather than requiring events on
/// it, so the physics script stays untouched. Latched -> unlatched is an open, the
/// reverse is a close.
///
/// Put this on the same GameObject as the DoorLatch.
/// </summary>
[RequireComponent(typeof(DoorLatch))]
public class DoorAudio : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DoorLatch latch;
    [SerializeField] private AudioSource source;

    [Header("Clips")]
    [SerializeField] private AudioClip[] openClips;
    [SerializeField] private AudioClip[] closeClips;
    [Tooltip("Played when the player interacts with a door that will not open.")]
    [SerializeField] private AudioClip lockedClip;

    [Header("Mix")]
    [Range(0f, 1f)] [SerializeField] private float volume = 0.9f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [Tooltip("Distance at which the door can no longer be heard.")]
    [SerializeField] private float maxDistance = 18f;

    private bool wasLatched;

    private void Reset()
    {
        latch = GetComponent<DoorLatch>();
        source = GetComponent<AudioSource>();
    }

    private void Awake()
    {
        if (latch == null) latch = GetComponent<DoorLatch>();

        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        // A door is a definite place in the room, so it must be fully positional.
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.maxDistance = maxDistance;

        wasLatched = latch.IsLatched;
    }

    private void Update()
    {
        bool now = latch.IsLatched;
        if (now == wasLatched) return;

        // Unlatching is the door coming free; latching is it settling shut.
        Play(now ? closeClips : openClips);
        wasLatched = now;
    }

    /// <summary>Call when an interaction was refused, e.g. a locked door.</summary>
    public void PlayLocked()
    {
        if (lockedClip == null) return;
        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(lockedClip, volume);
    }

    private void Play(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        source.pitch = Random.Range(pitchRange.x, pitchRange.y);
        source.PlayOneShot(clip, volume);
    }
}
