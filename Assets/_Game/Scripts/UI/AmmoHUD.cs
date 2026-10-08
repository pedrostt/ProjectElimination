using ProjectElimination.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectElimination.UI
{
    [DisallowMultipleComponent]
    public sealed class AmmoHUD : MonoBehaviour
    {
        [SerializeField] private WeaponAmmo weaponAmmo;

        private GameObject canvasObject;
        private Text ammoText;

        private void Awake()
        {
            if (weaponAmmo == null)
            {
                Debug.LogError("AmmoHUD requires a WeaponAmmo reference.", this);
                enabled = false;
                return;
            }

            canvasObject = new GameObject("Ammo HUD Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject label = new GameObject("Ammo", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-32f, 32f);
            rect.sizeDelta = new Vector2(260f, 60f);

            ammoText = label.GetComponent<Text>();
            ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            ammoText.fontSize = 32;
            ammoText.color = Color.white;
            ammoText.alignment = TextAnchor.MiddleRight;
            ammoText.raycastTarget = false;
            ammoText.supportRichText = false;
            label.GetComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            canvasObject.SetActive(enabled);
        }

        private void OnEnable()
        {
            if (weaponAmmo == null || canvasObject == null) return;
            weaponAmmo.AmmoChanged += RefreshAmmo;
            canvasObject.SetActive(true);
            RefreshAmmo(weaponAmmo.CurrentAmmo, weaponAmmo.ReserveAmmo);
        }

        private void OnDisable()
        {
            if (weaponAmmo != null) weaponAmmo.AmmoChanged -= RefreshAmmo;
            if (canvasObject != null) canvasObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
        }

        private void RefreshAmmo(int current, int reserve)
        {
            if (ammoText != null) ammoText.text = $"{current} / {reserve}";
        }
    }
}
