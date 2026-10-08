using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectElimination.Weapons
{
    // Process reload requests before HitscanWeapon.LateUpdate, after cursor handling in Update.
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class WeaponAmmo : MonoBehaviour
    {
        [Header("Ammo")]
        [SerializeField, Min(1)] private int magazineCapacity = 30;
        [SerializeField, Min(0)] private int currentAmmo = 30;
        [SerializeField, Min(0)] private int reserveAmmo = 90;
        [SerializeField, Min(0.01f)] private float reloadDuration = 2f;

        [Header("Input")]
        [SerializeField] private string reloadBinding = "<Keyboard>/r";
        [SerializeField] private bool requireLockedCursor = true;

        public int MagazineCapacity => magazineCapacity;
        public int CurrentAmmo => currentAmmo;
        public int ReserveAmmo => reserveAmmo;
        public float ReloadDuration => reloadDuration;
        public bool IsReloading { get; private set; }
        public float ReloadProgress => IsReloading
            ? Mathf.Clamp01(1f - (float)((reloadEndTime - Time.timeAsDouble) / reloadDuration))
            : 0f;

        public event Action<int, int> AmmoChanged;
        public event Action ReloadStarted;
        /// <summary>Raised only when a reload completes and transfers ammunition.</summary>
        public event Action ReloadFinished;
        public event Action ReloadCancelled;

        private InputAction reloadAction;
        private double reloadEndTime;

        private void Awake()
        {
            ValidateSettings();
            reloadAction = new InputAction("Reload", InputActionType.Button, reloadBinding);
        }

        private void OnEnable()
        {
            reloadAction?.Enable();
        }

        private void OnDisable()
        {
            reloadAction?.Disable();
            CancelReload();
        }

        private void OnDestroy()
        {
            reloadAction?.Dispose();
        }

        private void LateUpdate()
        {
            if (IsReloading && Time.timeAsDouble >= reloadEndTime) CompleteReload();

            bool acceptsInput = Application.isFocused &&
                (!requireLockedCursor || Cursor.lockState == CursorLockMode.Locked);
            if (acceptsInput && reloadAction.WasPressedThisFrame()) TryReload();
        }

        public bool TryConsumeAmmo()
        {
            if (!isActiveAndEnabled || IsReloading || currentAmmo <= 0) return false;

            currentAmmo--;
            AmmoChanged?.Invoke(currentAmmo, reserveAmmo);
            return true;
        }

        public bool TryReload()
        {
            if (!isActiveAndEnabled || IsReloading || currentAmmo >= magazineCapacity || reserveAmmo <= 0)
                return false;

            reloadEndTime = Time.timeAsDouble + reloadDuration;
            IsReloading = true;
            ReloadStarted?.Invoke();
            return true;
        }

        /// <summary>Void adapter suitable for a future touch button's onClick event.</summary>
        public void RequestReload()
        {
            TryReload();
        }

        public void CancelReload()
        {
            // Nothing is deducted until completion, so cancellation preserves both counts.
            bool wasReloading = IsReloading;
            IsReloading = false;
            reloadEndTime = 0d;
            if (wasReloading) ReloadCancelled?.Invoke();
        }

        private void CompleteReload()
        {
            int transferred = Mathf.Min(magazineCapacity - currentAmmo, reserveAmmo);
            currentAmmo += transferred;
            reserveAmmo -= transferred;
            IsReloading = false;
            reloadEndTime = 0d;
            AmmoChanged?.Invoke(currentAmmo, reserveAmmo);
            ReloadFinished?.Invoke();
        }

        private void OnValidate()
        {
            ValidateSettings();
        }

        private void ValidateSettings()
        {
            magazineCapacity = Mathf.Max(1, magazineCapacity);
            currentAmmo = Mathf.Clamp(currentAmmo, 0, magazineCapacity);
            reserveAmmo = Mathf.Max(0, reserveAmmo);
            reloadDuration = float.IsNaN(reloadDuration) || float.IsInfinity(reloadDuration)
                ? 2f : Mathf.Max(0.01f, reloadDuration);
            if (string.IsNullOrWhiteSpace(reloadBinding)) reloadBinding = "<Keyboard>/r";
        }
    }
}
