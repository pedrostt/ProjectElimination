using UnityEngine;

namespace ProjectElimination.Weapons
{
    // Run after HitscanWeapon.LateUpdate so a shot starts its visual response this frame.
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class WeaponRecoil : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HitscanWeapon weapon;
        [SerializeField] private Transform recoilTransform;

        [Header("Impulse")]
        [SerializeField, Min(0f)] private float kickback = 0.04f;
        [SerializeField, Min(0f)] private float verticalKick = 0.012f;
        [Tooltip("Upward pitch impulse, in degrees.")]
        [SerializeField, Min(0f)] private float rotationKick = 2f;
        [Tooltip("Yaw impulse, in degrees. Zero disables horizontal rotation.")]
        [SerializeField] private float horizontalRotation = 0.2f;

        [Header("Response")]
        [Tooltip("How quickly the visual transform follows the impulse, in inverse seconds.")]
        [SerializeField, Min(0.01f)] private float recoilSpeed = 35f;
        [Tooltip("How quickly the impulse returns to rest, in inverse seconds.")]
        [SerializeField, Min(0.01f)] private float returnSpeed = 12f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 targetPosition;
        private Vector3 targetRotation;
        private Vector3 positionOffset;
        private Vector3 rotationOffset;
        private bool initialized;

        private void Awake()
        {
            if (weapon == null || recoilTransform == null ||
                recoilTransform.GetComponentInChildren<Camera>(true) != null)
            {
                Debug.LogError("WeaponRecoil requires a weapon and a visual transform that does not contain a camera.", this);
                enabled = false;
                return;
            }

            initialized = true;
        }

        private void OnEnable()
        {
            if (!initialized) return;
            restPosition = recoilTransform.localPosition;
            restRotation = recoilTransform.localRotation;
            weapon.Fired += OnFired;
        }

        private void OnDisable()
        {
            if (weapon != null) weapon.Fired -= OnFired;
            if (initialized && recoilTransform != null)
            {
                recoilTransform.localPosition = restPosition;
                recoilTransform.localRotation = restRotation;
            }

            targetPosition = targetRotation = positionOffset = rotationOffset = Vector3.zero;
        }

        private void OnFired()
        {
            targetPosition += new Vector3(0f, verticalKick, -kickback);
            targetRotation += new Vector3(-rotationKick, horizontalRotation, 0f);
        }

        private void LateUpdate()
        {
            if (recoilTransform == null) return;

            float dt = Time.deltaTime;
            float followDecay = Mathf.Exp(-recoilSpeed * dt);
            float returnDecay = Mathf.Exp(-returnSpeed * dt);
            float difference = recoilSpeed - returnSpeed;
            // Exact integration of a decaying impulse followed by exponential smoothing.
            // Unlike two sequential Lerps, this gives the same response across time step sizes.
            float impulseWeight = Mathf.Abs(difference) < 0.001f
                ? recoilSpeed * dt * followDecay
                : recoilSpeed * (returnDecay - followDecay) / difference;

            positionOffset = positionOffset * followDecay + targetPosition * impulseWeight;
            rotationOffset = rotationOffset * followDecay + targetRotation * impulseWeight;
            targetPosition *= returnDecay;
            targetRotation *= returnDecay;

            recoilTransform.localPosition = restPosition + restRotation * positionOffset;
            recoilTransform.localRotation = restRotation * Quaternion.Euler(rotationOffset);
        }

        private void OnValidate()
        {
            kickback = Sanitize(kickback, 0.04f, 0f);
            verticalKick = Sanitize(verticalKick, 0.012f, 0f);
            rotationKick = Sanitize(rotationKick, 2f, 0f);
            if (float.IsNaN(horizontalRotation) || float.IsInfinity(horizontalRotation)) horizontalRotation = 0f;
            recoilSpeed = Sanitize(recoilSpeed, 35f, 0.01f);
            returnSpeed = Sanitize(returnSpeed, 12f, 0.01f);
        }

        private static float Sanitize(float value, float fallback, float minimum)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(minimum, value);
        }
    }
}
