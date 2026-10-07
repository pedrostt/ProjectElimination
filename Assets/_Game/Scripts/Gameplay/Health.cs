using System;
using UnityEngine;

namespace ProjectElimination.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsDead { get; private set; }

        /// <summary>
        /// Raised after damage changes health, with current and maximum health.
        /// Subscribers should read the properties for their initial state after Awake.
        /// </summary>
        public event Action<float, float> HealthChanged;

        /// <summary>Raised once on death, after HealthChanged.</summary>
        public event Action Died;

        private void Awake()
        {
            ValidateMaxHealth();
            CurrentHealth = maxHealth;
        }

        private void OnValidate()
        {
            ValidateMaxHealth();
        }

        private void ValidateMaxHealth()
        {
            if (float.IsNaN(maxHealth) || float.IsInfinity(maxHealth))
                maxHealth = 100f;
            else
                maxHealth = Mathf.Max(1f, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
                return;

            float nextHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (nextHealth == CurrentHealth) return;

            CurrentHealth = nextHealth;
            // Capture this transition before notifying listeners, which may apply more damage.
            bool diedNow = CurrentHealth <= 0f;
            IsDead = diedNow;
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (diedNow) Died?.Invoke();
        }
    }
}
