using UnityEngine;

namespace ProjectElimination.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class DamageHitbox : MonoBehaviour
    {
        [Tooltip("Damage scale for this region: Body = 1, Head = 4, etc.")]
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;

        public float DamageMultiplier => damageMultiplier;

        /// <summary>
        /// Forwards scaled damage to the nearest IDamageable on a parent.
        /// Success means damage was sent, not that the receiver accepted it.
        /// finalDamage is the calculated amount, not the health actually removed.
        /// </summary>
        public bool TryApplyDamage(float baseDamage, out float finalDamage)
        {
            finalDamage = 0f;
            if (baseDamage <= 0f || float.IsNaN(baseDamage) || float.IsInfinity(baseDamage))
                return false;

            float scaledDamage = baseDamage * damageMultiplier;
            if (scaledDamage < 0f || float.IsNaN(scaledDamage) || float.IsInfinity(scaledDamage))
                return false;

            finalDamage = scaledDamage;
            if (finalDamage == 0f || transform.parent == null) return false;

            // Resolve at impact so reparenting or replacing the receiver cannot leave a stale cache.
            IDamageable receiver = transform.parent.GetComponentInParent<IDamageable>();
            if (receiver == null) return false;

            receiver.TakeDamage(finalDamage);
            return true;
        }

        private void OnValidate()
        {
            damageMultiplier = float.IsNaN(damageMultiplier) || float.IsInfinity(damageMultiplier)
                ? 1f
                : Mathf.Max(0f, damageMultiplier);
        }
    }
}
