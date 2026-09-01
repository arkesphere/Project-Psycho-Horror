using System;
using System.Collections;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// A container that is opened and emptied inside the examine view, the way
    /// Resident Evil handles a case or a toolbox: you turn it over in your hands,
    /// click the lid, it opens there, and you take each item out of it. The box is
    /// only put down once there is nothing left in it.
    ///
    /// This lives on the EXAMINE PREFAB, not on the scene object, so it is spawned
    /// and destroyed with the model the player is holding.
    /// </summary>
    public class ExamineContainer : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            public ItemData item;
            [Tooltip("The model inside the container. Hidden until the lid opens.")]
            public GameObject model;
            [Tooltip("The ExamineHotspot id on that model.")]
            public string hotspotId;
        }

        [Header("Lid")]
        [SerializeField] private Animator lidAnimator;
        [SerializeField] private string openTrigger = "Open";
        [Tooltip("The ExamineHotspot id on the lid.")]
        [SerializeField] private string lidHotspotId = "Lid";
        [Tooltip("Seconds into the lid animation before the contents appear.")]
        [SerializeField] private float revealDelay = 0.9f;

        [Header("Contents")]
        [SerializeField] private Slot[] contents;

        [Header("Finish")]
        [Tooltip("Seconds after the last item is taken before the box is put down.")]
        [SerializeField] private float closeDelay = 0.5f;

        [Header("Messages")]
        [SerializeField] private string fullMessage = "No more room.";

        private bool lidOpen;
        private Inventory inventory;
        private ItemExaminer examiner;
        private ItemData examinedItem;

        private void Awake()
        {
            if (lidAnimator == null) lidAnimator = GetComponent<Animator>();
            SetContentsActive(false);
        }

        private void OnEnable()
        {
            // Only one item is ever in the examine view, so every hotspot event that
            // arrives while this instance is alive belongs to this container.
            EventBus.Subscribe<ExamineHotspotActivatedEvent>(OnHotspot);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ExamineHotspotActivatedEvent>(OnHotspot);
        }

        private void SetContentsActive(bool on)
        {
            if (contents == null) return;
            foreach (var slot in contents)
                if (slot != null && slot.model != null) slot.model.SetActive(on);
        }

        private void OnHotspot(ExamineHotspotActivatedEvent evt)
        {
            examinedItem = evt.Item;

            if (!lidOpen && evt.HotspotId == lidHotspotId)
            {
                OpenLid();
                return;
            }

            if (!lidOpen) return;
            TakeItem(evt.HotspotId);
        }

        private void OpenLid()
        {
            lidOpen = true;
            if (lidAnimator != null) lidAnimator.SetTrigger(openTrigger);
            StartCoroutine(RevealAfterDelay());
        }

        private IEnumerator RevealAfterDelay()
        {
            // Unscaled: the examine view runs while the rest of the game is frozen.
            float t = 0f;
            while (t < revealDelay) { t += Time.unscaledDeltaTime; yield return null; }
            SetContentsActive(true);
        }

        private void TakeItem(string hotspotId)
        {
            if (contents == null) return;

            foreach (var slot in contents)
            {
                if (slot == null || slot.model == null) continue;
                if (slot.hotspotId != hotspotId || !slot.model.activeSelf) continue;

                if (inventory == null) inventory = FindAnyObjectByType<Inventory>();
                if (inventory == null || slot.item == null) return;

                if (!inventory.TryAdd(slot.item, 1))
                {
                    EventBus.Publish(new InteractionMessageRequestedEvent(fullMessage, 1.6f));
                    return;
                }

                slot.model.SetActive(false);
                EventBus.Publish(new InteractionMessageRequestedEvent(
                    "Took " + (string.IsNullOrEmpty(slot.item.displayName) ? slot.item.name : slot.item.displayName), 1.6f));

                if (IsEmpty()) StartCoroutine(FinishWhenEmpty());
                return;
            }
        }

        private bool IsEmpty()
        {
            foreach (var slot in contents)
                if (slot != null && slot.model != null && slot.model.activeSelf) return false;
            return true;
        }

        private IEnumerator FinishWhenEmpty()
        {
            float t = 0f;
            while (t < closeDelay) { t += Time.unscaledDeltaTime; yield return null; }

            // Tell the world object it has been cleared out before the view tears
            // this instance down, or the message would never be sent.
            EventBus.Publish(new ExamineContainerEmptiedEvent(examinedItem));

            if (examiner == null) examiner = FindAnyObjectByType<ItemExaminer>();
            if (examiner != null) examiner.EndExamine();
        }
    }
}
