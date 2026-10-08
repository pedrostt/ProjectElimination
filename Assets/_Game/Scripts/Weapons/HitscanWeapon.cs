using System;
using ProjectElimination.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectElimination.Weapons
{
    [DisallowMultipleComponent]
    public sealed class HitscanWeapon : MonoBehaviour
    {
        /// <summary>Raised once after a shot is cast, whether it hits or misses.</summary>
        public event Action Fired;
        public event Action<ShotResult> ShotFired;

        public readonly struct ShotResult
        {
            public Vector3 Origin { get; }
            public Vector3 Direction { get; }
            public Vector3 EndPoint { get; }
            public bool HasHit { get; }
            public Vector3 ImpactPosition { get; }
            public Vector3 ImpactNormal { get; }

            public ShotResult(Ray ray, float range, bool hasHit, RaycastHit hit)
            {
                Origin = ray.origin;
                Direction = ray.direction;
                HasHit = hasHit;
                EndPoint = hasHit ? hit.point : ray.GetPoint(range);
                ImpactPosition = hasHit ? hit.point : Vector3.zero;
                ImpactNormal = hasHit ? hit.normal : Vector3.zero;
            }
        }

        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Camera firingCamera;
        [SerializeField] private WeaponAmmo weaponAmmo;

        [Header("Shot")]
        [SerializeField, Min(0.01f)] private float range = 100f;
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

        [Header("Development")]
        [SerializeField] private bool debugLogs;

        private InputActionAsset runtimeActions;
        private InputAction attackAction;
        private bool readyToFire;

        private void Awake()
        {
            if (inputActions == null || firingCamera == null || weaponAmmo == null)
            {
                Debug.LogError("HitscanWeapon requires an Input Actions asset, a firing camera and WeaponAmmo.", this);
                enabled = false;
                return;
            }

            // Own the action lifecycle without affecting FPSController or the shared asset.
            runtimeActions = Instantiate(inputActions);
            attackAction = runtimeActions.FindActionMap("Player")?.FindAction("Attack");
            if (attackAction == null)
            {
                Debug.LogError("HitscanWeapon requires the Player/Attack action.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            readyToFire = false;
            attackAction?.Enable();
        }

        private void OnDisable()
        {
            readyToFire = false;
            attackAction?.Disable();
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) readyToFire = false;
        }

        private void LateUpdate()
        {
            // FPSController updates the cursor and camera in Update. Observe their final state.
            bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.Locked || escapePressed)
            {
                readyToFire = false;
                return;
            }

            if (!readyToFire)
            {
                // A recapture click (even pressed and released in one frame) cannot fire.
                readyToFire = !attackAction.IsPressed() && !attackAction.WasPressedThisFrame();
                return;
            }

            if (attackAction.WasPressedThisFrame()) Fire();
        }

        private void Fire()
        {
            if (firingCamera == null || weaponAmmo == null || !weaponAmmo.TryConsumeAmmo()) return;

            Ray ray = firingCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore);
            Debug.DrawRay(ray.origin, ray.direction * (hasHit ? hit.distance : range),
                hasHit ? Color.red : Color.yellow, 1f);
            Fired?.Invoke();
            ShotFired?.Invoke(new ShotResult(ray, range, hasHit, hit));

            if (!hasHit)
            {
                if (debugLogs) Debug.Log("HitscanWeapon: nenhum objeto atingido.", this);
                return;
            }

            float multiplier = 1f;
            float finalDamage = damage;
            bool damageSent;
            if (hit.collider.TryGetComponent(out DamageHitbox hitbox))
            {
                multiplier = hitbox.DamageMultiplier;
                damageSent = hitbox.TryApplyDamage(damage, out finalDamage);
            }
            else
            {
                // Simple targets still work without a hitbox, including colliders on children.
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                damageSent = target != null;
                if (damageSent) target.TakeDamage(damage);
            }

            if (debugLogs)
            {
                string result = damageSent
                    ? "Dano enviado ao IDamageable."
                    : "Dano nao enviado: sem receptor valido ou dano nulo/invalido.";
                Debug.Log($"HitscanWeapon: atingiu '{hit.collider.gameObject.name}'. " +
                    $"Dano final: {finalDamage}. Multiplicador: {multiplier}. {result}", hit.collider);
            }
        }

        private void OnValidate()
        {
            range = float.IsNaN(range) || float.IsInfinity(range) ? 100f : Mathf.Max(0.01f, range);
            damage = float.IsNaN(damage) || float.IsInfinity(damage) ? 25f : Mathf.Max(0f, damage);
        }
    }
}
