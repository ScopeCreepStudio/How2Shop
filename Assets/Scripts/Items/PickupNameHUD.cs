using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace How2Shop.Items
{
    public sealed class PickupNameHUD : MonoBehaviour
    {
        [SerializeField] private float visibleSeconds = 2.5f;
        [SerializeField] private float fadeSeconds = 0.45f;

        private CanvasGroup canvasGroup;
        private TMP_Text itemNameText;
        private float remainingSeconds;

        private void Awake()
        {
            EnsureUI();
            canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (remainingSeconds <= 0f)
                return;

            remainingSeconds -= Time.unscaledDeltaTime;
            canvasGroup.alpha = remainingSeconds < fadeSeconds
                ? Mathf.Clamp01(remainingSeconds / fadeSeconds)
                : 1f;
        }

        public void Show(string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return;

            EnsureUI();
            itemNameText.text = "Picked up: " + itemName;
            remainingSeconds = visibleSeconds;
            canvasGroup.alpha = 1f;
        }

        private void EnsureUI()
        {
            if (canvasGroup != null)
                return;

            var canvasObject = new GameObject("Pickup Name HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 300;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var panelObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = Vector2.zero;
            panelRect.anchoredPosition = new Vector2(28f, 28f);
            panelRect.sizeDelta = new Vector2(440f, 64f);
            panelObject.GetComponent<Image>().color = new Color32(224, 216, 158, 255);
            panelRect.localRotation = Quaternion.Euler(0f, 0f, -0.8f);
            Outline outline = panelObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(4f, -3f);

            var textObject = new GameObject("Item Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 7f);
            textRect.offsetMax = new Vector2(-16f, -7f);

            itemNameText = textObject.GetComponent<TextMeshProUGUI>();
            itemNameText.text = string.Empty;
            itemNameText.font = Resources.Load<TMP_FontAsset>("Fonts/Childs Journal SDF Fixed");
            if (itemNameText.font == null)
                itemNameText.font = TMP_Settings.defaultFontAsset;
            itemNameText.fontSize = 29f;
            itemNameText.fontSizeMin = 18f;
            itemNameText.fontSizeMax = 29f;
            itemNameText.enableAutoSizing = true;
            itemNameText.fontStyle = FontStyles.Bold;
            itemNameText.color = Color.black;
            itemNameText.alignment = TextAlignmentOptions.MidlineLeft;
            itemNameText.textWrappingMode = TextWrappingModes.NoWrap;
            itemNameText.overflowMode = TextOverflowModes.Ellipsis;
        }
    }
}
