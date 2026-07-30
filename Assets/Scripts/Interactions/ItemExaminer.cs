using System;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// The "hold it up and turn it over" view. Spawns the item's model on a pivot a
    /// short distance in front of the camera, auto-fits it to a target size, and lets
    /// the player spin and zoom it while movement is locked.
    ///
    /// Put this on the player camera (or a child of it). It parents its own pivot to
    /// the camera, so the item tracks the view without any per-frame follow code.
    /// </summary>
    public class ItemExaminer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;

        [Tooltip("Large unlit dark quad that dims the room. It is reparented onto the examine rig at " +
                 "startup, so you can author it anywhere. A Screen Space Overlay canvas cannot be used " +
                 "here because it would draw over the 3D item as well.")]
        [SerializeField] private GameObject backdrop;

        [Tooltip("Metres in front of the camera the backdrop sits. Must be greater than the maximum " +
                 "zoom distance so the item never sinks behind it.")]
        [SerializeField, Min(0.1f)] private float backdropDistance = 0.9f;

        [Tooltip("Optional GameObject holding a Volume with depth of field / vignette overrides. " +
                 "Enabled for the duration of the examine.")]
        [SerializeField] private GameObject volumeToEnable;

        [Tooltip("Optional UI panel showing the item name and description.")]
        [SerializeField] private ExamineInfoBinding infoUI;

        [Header("Placement")]
        [Tooltip("Metres in front of the camera. Keep this comfortably beyond the camera's near clip plane.")]
        [SerializeField, Min(0.05f)] private float holdDistance = 0.45f;
        [SerializeField] private Vector3 holdOffset = new Vector3(0f, -0.02f, 0f);
        [Tooltip("The item is scaled so its largest dimension matches this, in metres.")]
        [SerializeField, Min(0.01f)] private float targetViewSize = 0.22f;

        [Header("Rotation")]
        [SerializeField] private float rotateSpeed = 220f;
        [Tooltip("Spin only while the rotate button is held, rather than on any mouse movement.")]
        [SerializeField] private bool requireHoldToRotate = false;
        [SerializeField] private bool invertY = false;
        [Tooltip("Slow drift when the player is not dragging, so the item always reads as 3D.")]
        [SerializeField] private float idleSpinSpeed = 8f;

        [Header("Zoom")]
        [SerializeField] private bool allowZoom = true;
        [SerializeField] private float zoomSpeed = 0.06f;
        [SerializeField] private Vector2 zoomRange = new Vector2(0.28f, 0.7f);

        [Header("Transition")]
        [Tooltip("Seconds the item takes to scale up on open and down on close.")]
        [SerializeField, Min(0f)] private float transitionDuration = 0.18f;
        [Tooltip("Grace period after opening during which close input is ignored. Without this, the " +
                 "same key press that picked the item up would immediately dismiss the view.")]
        [SerializeField, Min(0f)] private float inputGracePeriod = 0.2f;

        [Header("HDRP Lighting")]
        [Tooltip("Rendering layer index (0-31) assigned to the examined model. Give your examine " +
                 "light the matching Light Layer so scene lights cannot touch the item and vice versa. " +
                 "Set to -1 to leave rendering layers untouched.")]
        [SerializeField, Range(-1, 31)] private int examineRenderingLayer = -1;
        [Tooltip("Light(s) enabled only while examining. Parent them to this component so they follow the view.")]
        [SerializeField] private Light[] examineLights;

        [Header("Culling")]
        [Tooltip("Unity layer index the spawned model is moved to. -1 leaves prefab layers as-is. " +
                 "Useful if you want reflection probes or a custom pass to ignore held items.")]
        [SerializeField, Range(-1, 31)] private int examineUnityLayer = -1;

        public event Action<ItemData> OnExamineBegin;
        public event Action<ItemData> OnExamineEnd;

        public bool IsExamining { get; private set; }
        public ItemData CurrentItem { get; private set; }

        private Transform _rig;
        private Transform _pivot;
        private GameObject _instance;
        private float _currentDistance;
        private float _fittedScale = 1f;
        private float _transitionTimer;
        private float _inputAllowedAt;
        private bool _closing;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInParent<Camera>();
            if (playerCamera == null) playerCamera = Camera.main;

            // The rig lives at the scene ROOT and copies the camera transform every
            // LateUpdate. It is deliberately not parented to the camera: controllers
            // that crouch by squashing the player's localScale would otherwise apply
            // a non-uniform scale to a rotated child, which shears the held model.
            var rigGo = new GameObject("ExamineRig");
            _rig = rigGo.transform;
            _rig.localScale = Vector3.one;

            var pivotGo = new GameObject("ExaminePivot");
            _pivot = pivotGo.transform;
            _pivot.SetParent(_rig, false);
            _pivot.localRotation = Quaternion.identity;

            // Move authored props onto the rig so they inherit the same clean scale.
            if (backdrop != null)
            {
                backdrop.transform.SetParent(_rig, false);
                backdrop.transform.localPosition = new Vector3(0f, 0f, backdropDistance);
            }
            if (examineLights != null)
            {
                for (int i = 0; i < examineLights.Length; i++)
                    if (examineLights[i] != null) examineLights[i].transform.SetParent(_rig, false);
            }

            SyncRigToCamera();
            SetVisualsActive(false);
        }

        private void LateUpdate()
        {
            // Runs after the controller has written its camera rotation.
            SyncRigToCamera();
        }

        private void SyncRigToCamera()
        {
            if (_rig == null || playerCamera == null) return;
            Transform cam = playerCamera.transform;
            _rig.SetPositionAndRotation(cam.position, cam.rotation);
        }

        private void OnDestroy()
        {
            // Never leave the player frozen if this object dies mid-examine.
            if (IsExamining) PlayerControlGate.Pop();
            if (_rig != null) Destroy(_rig.gameObject);
        }

        /// <summary>Opens the examine view for an item. Safe to call while another item is shown.</summary>
        public void BeginExamine(ItemData item)
        {
            if (item == null) return;

            var model = item.ExamineModel;
            if (model == null)
            {
                Debug.LogWarning($"ItemExaminer: '{item.displayName}' has no examinePrefab or worldPrefab, nothing to show.", this);
                return;
            }

            if (IsExamining) TeardownInstance();
            else
            {
                PlayerControlGate.Push();
                IsExamining = true;
            }

            CurrentItem = item;
            _closing = false;
            _transitionTimer = 0f;
            _inputAllowedAt = Time.unscaledTime + inputGracePeriod;
            _currentDistance = holdDistance;

            _pivot.localPosition = Vector3.forward * _currentDistance + holdOffset;
            _pivot.localRotation = Quaternion.identity;

            SpawnAndFit(item, model);
            SetVisualsActive(true);

            infoUI?.Bind(item);
            OnExamineBegin?.Invoke(item);
        }

        /// <summary>Closes the examine view. No-op if nothing is being shown.</summary>
        public void EndExamine()
        {
            if (!IsExamining || _closing) return;
            _closing = true;
            _transitionTimer = 0f;

            if (transitionDuration <= 0f) FinishClose();
        }

        private void Update()
        {
            if (!IsExamining) return;

            if (_closing)
            {
                TickTransition(false);
                return;
            }

            TickTransition(true);

            if (Time.unscaledTime < _inputAllowedAt) return;

            if (InputCompat.CancelPressed || InputCompat.InteractPressed)
            {
                EndExamine();
                return;
            }

            HandleRotation();
            HandleZoom();
        }

        private void HandleRotation()
        {
            if (_instance == null || playerCamera == null) return;

            bool dragging = !requireHoldToRotate || InputCompat.RotateHeld;
            Vector2 delta = dragging ? InputCompat.PointerDelta : Vector2.zero;

            if (delta.sqrMagnitude > 0.0000001f)
            {
                float yaw = -delta.x * rotateSpeed * Time.unscaledDeltaTime;
                float pitch = (invertY ? -delta.y : delta.y) * rotateSpeed * Time.unscaledDeltaTime;

                // Rotate around the CAMERA's axes so dragging always feels screen-relative,
                // no matter how far the object has already been turned.
                Transform cam = playerCamera.transform;
                _instance.transform.Rotate(cam.up, yaw, Space.World);
                _instance.transform.Rotate(cam.right, pitch, Space.World);
            }
            else if (idleSpinSpeed != 0f)
            {
                _instance.transform.Rotate(playerCamera.transform.up, idleSpinSpeed * Time.unscaledDeltaTime, Space.World);
            }
        }

        private void HandleZoom()
        {
            if (!allowZoom) return;

            float scroll = InputCompat.ScrollDelta;
            if (Mathf.Abs(scroll) < 0.0001f) return;

            _currentDistance = Mathf.Clamp(_currentDistance - scroll * zoomSpeed, zoomRange.x, zoomRange.y);
            _pivot.localPosition = Vector3.forward * _currentDistance + holdOffset;
        }

        private void TickTransition(bool opening)
        {
            if (_instance == null) return;

            if (transitionDuration <= 0f)
            {
                _instance.transform.localScale = Vector3.one * _fittedScale;
                if (!opening) FinishClose();
                return;
            }

            _transitionTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_transitionTimer / transitionDuration);
            float eased = opening ? 1f - Mathf.Pow(1f - t, 3f) : 1f - t;

            _instance.transform.localScale = Vector3.one * (_fittedScale * Mathf.Max(eased, 0.001f));

            if (!opening && t >= 1f) FinishClose();
        }

        private void FinishClose()
        {
            var finished = CurrentItem;

            TeardownInstance();
            SetVisualsActive(false);
            infoUI?.Clear();

            CurrentItem = null;
            IsExamining = false;
            _closing = false;

            PlayerControlGate.Pop();
            OnExamineEnd?.Invoke(finished);
        }

        private void SpawnAndFit(ItemData item, GameObject model)
        {
            _instance = Instantiate(model, _pivot);
            _instance.transform.localPosition = Vector3.zero;
            _instance.transform.localRotation = Quaternion.Euler(item.examineRotationOffset);
            _instance.transform.localScale = Vector3.one;
            _instance.name = $"Examine_{item.name}";

            StripGameplayComponents(_instance);

            if (examineUnityLayer >= 0) SetLayerRecursive(_instance.transform, examineUnityLayer);

            var renderers = _instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                _fittedScale = item.examineScaleMultiplier;
                _instance.transform.localScale = Vector3.one * _fittedScale;
                return;
            }

            if (examineRenderingLayer >= 0)
            {
                uint mask = 1u << examineRenderingLayer;
                for (int i = 0; i < renderers.Length; i++) renderers[i].renderingLayerMask = mask;
            }

            // Fit: scale so the largest bounds dimension matches targetViewSize.
            Bounds bounds = GetWorldBounds(renderers);
            float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            _fittedScale = (targetViewSize / Mathf.Max(largest, 0.0001f)) * Mathf.Max(0.01f, item.examineScaleMultiplier);
            _instance.transform.localScale = Vector3.one * _fittedScale;

            // Re-centre: bounds have moved with the scale, so recompute before offsetting.
            bounds = GetWorldBounds(renderers);
            Vector3 centreInPivotSpace = _pivot.InverseTransformPoint(bounds.center);
            _instance.transform.localPosition -= centreInPivotSpace;
        }

        private static Bounds GetWorldBounds(Renderer[] renderers)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>Prefabs meant for the world carry physics and interaction scripts. Strip them.</summary>
        private static void StripGameplayComponents(GameObject go)
        {
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            foreach (var wi in go.GetComponentsInChildren<WorldItem>(true)) wi.enabled = false;
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursive(root.GetChild(i), layer);
        }

        private void TeardownInstance()
        {
            if (_instance != null) Destroy(_instance);
            _instance = null;
        }

        private void SetVisualsActive(bool active)
        {
            if (backdrop != null) backdrop.SetActive(active);
            if (volumeToEnable != null) volumeToEnable.SetActive(active);
            if (examineLights != null)
                for (int i = 0; i < examineLights.Length; i++)
                    if (examineLights[i] != null) examineLights[i].enabled = active;
        }
    }
}
