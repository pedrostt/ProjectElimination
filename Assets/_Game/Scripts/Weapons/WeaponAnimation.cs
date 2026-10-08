using ProjectElimination.Player;
using UnityEngine;

namespace ProjectElimination.Weapons
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class WeaponAnimation : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform animationRoot;
        [SerializeField] private FPSController player;
        [SerializeField] private HitscanWeapon weapon;
        [SerializeField] private WeaponAmmo ammo;
        [SerializeField] private WeaponADS ads;

        [Header("Idle: local units, degrees, cycles per second")]
        [SerializeField] private Vector3 idlePosition = new Vector3(0.002f, 0.003f, 0f);
        [SerializeField] private Vector3 idleRotation = new Vector3(0.15f, 0.1f, 0f);
        [SerializeField, Min(0f)] private float idleFrequency = 0.8f;

        [Header("Walk: local units, degrees, cycles per second at walk speed")]
        [SerializeField] private Vector3 walkPosition = new Vector3(0.008f, 0.012f, 0f);
        [SerializeField] private Vector3 walkRotation = new Vector3(0.6f, 0.3f, 0.8f);
        [SerializeField, Min(0f)] private float walkFrequency = 1.8f;

        [Header("Sprint pose")]
        [SerializeField] private Vector3 sprintPosition = new Vector3(0f, -0.075f, -0.025f);
        [SerializeField] private Vector3 sprintRotation = new Vector3(12f, -8f, 12f);

        [Header("Reload pose: timing follows WeaponAmmo")]
        [SerializeField] private Vector3 reloadPosition = new Vector3(0f, -0.08f, 0.03f);
        [SerializeField] private Vector3 reloadRotation = new Vector3(12f, 0f, -18f);
        [SerializeField, Range(0.01f, 0.45f)] private float reloadEnterFraction = 0.2f;
        [SerializeField, Range(0.01f, 0.45f)] private float reloadExitFraction = 0.25f;
        [SerializeField, Min(0.01f)] private float reloadPoseSpeed = 14f;

        [Header("Fire: small additive impulse")]
        [SerializeField] private Vector3 firePosition = new Vector3(0f, 0f, -0.006f);
        [SerializeField] private Vector3 fireRotation = new Vector3(-0.3f, 0f, 0.15f);
        [SerializeField, Min(0.01f)] private float fireReturnSpeed = 25f;

        [Header("Blending")]
        [SerializeField, Min(0.01f)] private float poseSpeed = 12f;
        [SerializeField, Min(0.01f)] private float adsBlendSpeed = 14f;
        [SerializeField, Range(0f, 1f)] private float adsMotionMultiplier = 0.2f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 positionOffset;
        private Vector3 rotationOffset;
        private float walkPhase;
        private float aimBlend;
        private float fireImpulse;
        private bool reloading;
        private bool initialized;

        private void Awake()
        {
            if (animationRoot == null || player == null || weapon == null || ammo == null || ads == null ||
                animationRoot.GetComponentInChildren<Camera>(true) != null)
            {
                Debug.LogError("WeaponAnimation requires a separate visual AnimationRoot, player, weapon, ammo and ADS references.", this);
                enabled = false;
                return;
            }
            initialized = true;
        }

        private void OnEnable()
        {
            if (!initialized) return;
            restPosition = animationRoot.localPosition;
            restRotation = animationRoot.localRotation;
            weapon.Fired += OnFired;
            ammo.ReloadStarted += OnReloadStarted;
            ammo.ReloadFinished += OnReloadEnded;
            ammo.ReloadCancelled += OnReloadEnded;
            reloading = ammo.IsReloading;
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.Fired -= OnFired;
            if (ammo != null)
            {
                ammo.ReloadStarted -= OnReloadStarted;
                ammo.ReloadFinished -= OnReloadEnded;
                ammo.ReloadCancelled -= OnReloadEnded;
            }
            if (initialized && animationRoot != null)
            {
                animationRoot.localPosition = restPosition;
                animationRoot.localRotation = restRotation;
            }
            positionOffset = rotationOffset = Vector3.zero;
            fireImpulse = aimBlend = walkPhase = 0f;
            reloading = false;
        }

        private void OnFired() { fireImpulse = 1f; }
        private void OnReloadStarted() { reloading = true; }
        private void OnReloadEnded() { reloading = false; }

        private void LateUpdate()
        {
            if (animationRoot == null || player == null || ammo == null || ads == null) return;
            float dt = Time.deltaTime;
            aimBlend = Mathf.Lerp(aimBlend, ads.IsAiming ? 1f : 0f, 1f - Mathf.Exp(-adsBlendSpeed * dt));
            float motionScale = Mathf.Lerp(1f, adsMotionMultiplier, aimBlend);
            float speedRatio = player.IsGrounded && player.IsMoving
                ? player.HorizontalSpeed / Mathf.Max(0.01f, player.WalkSpeed) : 0f;
            float walkWeight = Mathf.Clamp01(speedRatio);
            walkPhase = Mathf.Repeat(walkPhase + speedRatio * walkFrequency * Mathf.PI * 2f * dt, Mathf.PI * 2f);
            float idlePhase = (float)(Time.timeAsDouble * idleFrequency * Mathf.PI * 2d % (Mathf.PI * 2d));

            Vector3 idleWave = new Vector3(Mathf.Sin(idlePhase), Mathf.Sin(idlePhase), Mathf.Sin(idlePhase));
            Vector3 walkWave = new Vector3(Mathf.Sin(walkPhase), Mathf.Sin(walkPhase * 2f), Mathf.Sin(walkPhase));
            Vector3 targetPosition = (Vector3.Scale(idlePosition, idleWave) * (1f - walkWeight)
                + Vector3.Scale(walkPosition, walkWave) * walkWeight) * motionScale;
            Vector3 targetRotation = (Vector3.Scale(idleRotation, idleWave) * (1f - walkWeight)
                + Vector3.Scale(walkRotation, walkWave) * walkWeight) * motionScale;

            // ADS keeps its alignment even if the player continues to sprint.
            float sprintWeight = player.IsSprinting ? 1f - aimBlend : 0f;
            targetPosition += sprintPosition * sprintWeight;
            targetRotation += sprintRotation * sprintWeight;
            if (reloading)
            {
                float progress = ammo.ReloadProgress;
                float envelope = Mathf.SmoothStep(0f, 1f, progress / reloadEnterFraction)
                    * Mathf.SmoothStep(0f, 1f, (1f - progress) / reloadExitFraction);
                targetPosition = reloadPosition * envelope;
                targetRotation = reloadRotation * envelope;
            }

            float blend = 1f - Mathf.Exp(-(reloading ? reloadPoseSpeed : poseSpeed) * dt);
            positionOffset = Vector3.Lerp(positionOffset, targetPosition, blend);
            rotationOffset = Vector3.Lerp(rotationOffset, targetRotation, blend);
            animationRoot.localPosition = restPosition + restRotation * (positionOffset + firePosition * fireImpulse);
            animationRoot.localRotation = restRotation * Quaternion.Euler(rotationOffset + fireRotation * fireImpulse);
            fireImpulse *= Mathf.Exp(-fireReturnSpeed * dt);
        }

        private void OnValidate()
        {
            idleFrequency = Valid(idleFrequency, 0.8f, 0f);
            walkFrequency = Valid(walkFrequency, 1.8f, 0f);
            poseSpeed = Valid(poseSpeed, 12f, 0.01f);
            adsBlendSpeed = Valid(adsBlendSpeed, 14f, 0.01f);
            reloadPoseSpeed = Valid(reloadPoseSpeed, 14f, 0.01f);
            fireReturnSpeed = Valid(fireReturnSpeed, 25f, 0.01f);
            reloadEnterFraction = Mathf.Clamp(Valid(reloadEnterFraction, 0.2f, 0.01f), 0.01f, 0.45f);
            reloadExitFraction = Mathf.Clamp(Valid(reloadExitFraction, 0.25f, 0.01f), 0.01f, 0.45f);
            adsMotionMultiplier = Mathf.Clamp01(Valid(adsMotionMultiplier, 0.2f, 0f));
        }

        private static float Valid(float value, float fallback, float minimum)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(minimum, value);
        }
    }
}
