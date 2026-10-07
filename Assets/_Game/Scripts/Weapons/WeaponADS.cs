using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectElimination.Weapons
{
    // Observe the cursor after FPSController.Update and pose the weapon before recoil (order 100).
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class WeaponADS : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeaponView weaponView;
        [SerializeField] private Transform adsRoot;
        [SerializeField] private Camera playerCamera;

        [Header("Input")]
        [Tooltip("Binding for the local Aim action; does not modify the shared Input Actions asset.")]
        [SerializeField] private string aimBinding = "<Mouse>/rightButton";
        [Tooltip("Disable for a future touch setup that does not capture a desktop cursor.")]
        [SerializeField] private bool requireLockedCursor = true;

        [Header("Local poses relative to WeaponHolder")]
        [SerializeField] private Vector3 hipPosition;
        [SerializeField] private Vector3 hipRotation;
        [SerializeField] private Vector3 adsPosition;
        [SerializeField] private Vector3 adsRotation;

        [Header("Transition")]
        [SerializeField, Min(0.01f)] private float adsTransitionSpeed = 14f;
        [SerializeField, Range(1f, 179f)] private float normalFOV = 60f;
        [SerializeField, Range(1f, 179f)] private float adsFOV = 55f;
        [SerializeField, Min(0.01f)] private float fovTransitionSpeed = 12f;

        public bool IsAiming { get; private set; }

        private bool externalAimHeld;
        private bool initialized;
        private InputAction aimAction;

        private void Awake()
        {
            if (weaponView == null || weaponView.WeaponHolder == null || adsRoot == null ||
                playerCamera == null || string.IsNullOrWhiteSpace(aimBinding) ||
                adsRoot.parent != weaponView.WeaponHolder ||
                adsRoot.GetComponentInChildren<Camera>(true) != null)
            {
                Debug.LogError("WeaponADS requires a WeaponView, camera and a separate ADSRoot directly beneath WeaponHolder.", this);
                enabled = false;
                return;
            }

            aimAction = new InputAction("Aim", InputActionType.Button, aimBinding);
            initialized = true;
        }

        private void OnEnable()
        {
            if (!initialized) return;
            ResetPose();
            aimAction.Enable();
        }

        private void OnDisable()
        {
            aimAction?.Disable();
            externalAimHeld = false;
            IsAiming = false;
            if (initialized) ResetPose();
        }

        private void OnDestroy()
        {
            aimAction?.Dispose();
        }

        /// <summary>For a future touch adapter: call true on press and false on release/cancel.</summary>
        public void SetAimHeld(bool held)
        {
            externalAimHeld = held;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) externalAimHeld = false;
        }

        private void LateUpdate()
        {
            if (adsRoot == null || playerCamera == null) return;

            bool acceptsInput = Application.isFocused &&
                (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);
            if (!acceptsInput) externalAimHeld = false;
            IsAiming = acceptsInput && (aimAction.IsPressed() || externalAimHeld);

            float poseBlend = 1f - Mathf.Exp(-adsTransitionSpeed * Time.deltaTime);
            float fovBlend = 1f - Mathf.Exp(-fovTransitionSpeed * Time.deltaTime);
            adsRoot.localPosition = Vector3.Lerp(adsRoot.localPosition,
                IsAiming ? adsPosition : hipPosition, poseBlend);
            adsRoot.localRotation = Quaternion.Slerp(adsRoot.localRotation,
                Quaternion.Euler(IsAiming ? adsRotation : hipRotation), poseBlend);
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView,
                IsAiming ? adsFOV : normalFOV, fovBlend);
        }

        private void ResetPose()
        {
            if (adsRoot != null)
            {
                adsRoot.localPosition = hipPosition;
                adsRoot.localRotation = Quaternion.Euler(hipRotation);
            }
            if (playerCamera != null) playerCamera.fieldOfView = normalFOV;
        }

        private void OnValidate()
        {
            adsTransitionSpeed = ValidRange(adsTransitionSpeed, 14f, 0.01f, float.MaxValue);
            fovTransitionSpeed = ValidRange(fovTransitionSpeed, 12f, 0.01f, float.MaxValue);
            normalFOV = ValidRange(normalFOV, 60f, 1f, 179f);
            adsFOV = ValidRange(adsFOV, 55f, 1f, normalFOV);
        }

        private static float ValidRange(float value, float fallback, float min, float max)
        {
            return Mathf.Clamp(float.IsNaN(value) || float.IsInfinity(value) ? fallback : value, min, max);
        }
    }
}
