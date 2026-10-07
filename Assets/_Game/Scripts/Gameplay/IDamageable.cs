namespace ProjectElimination.Gameplay
{
    public interface IDamageable
    {
        /// <summary>Applies a positive, finite amount of damage.</summary>
        void TakeDamage(float amount);
    }
}
