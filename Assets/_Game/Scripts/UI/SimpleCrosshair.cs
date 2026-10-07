using UnityEngine;
using UnityEngine.UI;

namespace ProjectElimination.UI
{
    [DisallowMultipleComponent]
    public sealed class SimpleCrosshair : MonoBehaviour
    {
        [SerializeField] private Color color = Color.white;
        [Tooltip("Bar length in UI units at the 1920 x 1080 reference resolution.")]
        [SerializeField, Min(1f)] private float size = 16f;
        [SerializeField, Min(1f)] private float thickness = 2f;

        private GameObject canvasObject;

        private void Awake()
        {
            canvasObject = new GameObject("Crosshair Canvas", typeof(RectTransform),
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

            // Images without sprites render solid rectangles; no texture or font assets needed.
            CreateBar("Horizontal", new Vector2(size, thickness));
            CreateBar("Vertical", new Vector2(thickness, size));
            canvasObject.SetActive(enabled);
        }

        private void CreateBar(string barName, Vector2 dimensions)
        {
            GameObject bar = new GameObject(barName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = dimensions;

            Image image = bar.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private void OnEnable()
        {
            if (canvasObject != null) canvasObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (canvasObject != null) canvasObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (canvasObject != null) Destroy(canvasObject);
        }

        private void OnValidate()
        {
            size = float.IsNaN(size) || float.IsInfinity(size) ? 16f : Mathf.Max(1f, size);
            thickness = float.IsNaN(thickness) || float.IsInfinity(thickness)
                ? 2f
                : Mathf.Clamp(thickness, 1f, size);
        }
    }
}
