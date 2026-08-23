using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Toggles the handheld light and gives it a short warm-up rather than an instant
    /// switch, which is what sells it as a physical torch instead of a light component
    /// being enabled. The positional lag behind the camera is handled separately by
    /// SwayDelay on the same object.
    ///
    /// Put this on the flashlight's Light GameObject.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class Flashlight : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Light lamp;

        [Header("State")]
        [SerializeField] private bool startOn = true;

        [Header("Feel")]
        [Tooltip("Seconds for the bulb to reach full brightness. 0 for an instant switch.")]
        [SerializeField] private float warmUpTime = 0.09f;
        [Tooltip("Seconds for it to fade out when switched off.")]
        [SerializeField] private float coolDownTime = 0.05f;

        [Header("Audio")]
        [SerializeField] private AudioSource clickSource;
        [SerializeField] private AudioClip clickOn;
        [SerializeField] private AudioClip clickOff;

        private float fullIntensity;
        private float current;
        private bool isOn;

        public bool IsOn => isOn;

        private void Reset()
        {
            lamp = GetComponent<Light>();
        }

        private void Awake()
        {
            if (lamp == null) lamp = GetComponent<Light>();

            // Whatever the light was authored at is treated as "full brightness", so
            // retuning the light in the Inspector does not need a code change.
            fullIntensity = lamp.intensity;

            isOn = startOn;
            current = isOn ? fullIntensity : 0f;
            Apply();
        }

        private void Update()
        {
            // Menus and the examine view own the keyboard.
            if (!PlayerControlGate.Locked && InputCombat.FlashlightPressed)
                Toggle();

            float target = isOn ? fullIntensity : 0f;
            float time = isOn ? warmUpTime : coolDownTime;

            if (time <= 0f) current = target;
            else current = Mathf.MoveTowards(current, target, (fullIntensity / time) * Time.deltaTime);

            Apply();
        }

        private void Apply()
        {
            lamp.intensity = current;
            // Switching the component off once dark saves the shadow map and the
            // volumetric cost while the torch is stowed.
            bool shouldRender = current > 0.001f;
            if (lamp.enabled != shouldRender) lamp.enabled = shouldRender;
        }

        public void Toggle() => SetOn(!isOn);

        public void SetOn(bool on)
        {
            if (isOn == on) return;
            isOn = on;

            if (clickSource != null)
            {
                var clip = on ? clickOn : clickOff;
                if (clip != null) clickSource.PlayOneShot(clip);
            }
        }
    }
}
