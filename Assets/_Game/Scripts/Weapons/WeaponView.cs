using UnityEngine;

namespace ProjectElimination.Weapons
{
    /// <summary>Visual references only. Shot direction and damage belong to the weapon logic.</summary>
    [DisallowMultipleComponent]
    public sealed class WeaponView : MonoBehaviour
    {
        [SerializeField] private Transform weaponHolder;
        [SerializeField] private Transform weaponModel;
        [Tooltip("Visual effects origin only; hitscan shots still originate from the camera.")]
        [SerializeField] private Transform muzzlePoint;

        public Transform WeaponHolder => weaponHolder;
        public Transform WeaponModel => weaponModel;
        public Transform MuzzlePoint => muzzlePoint;
    }
}
