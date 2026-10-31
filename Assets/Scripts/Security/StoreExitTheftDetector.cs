using How2Shop.Chaos;
using How2Shop.Items;
using How2Shop.Security;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Sounds the alarm when unpaid shopping is carried through the entrance.</summary>
[DisallowMultipleComponent]
public sealed class StoreExitTheftDetector : MonoBehaviour
{
    private Transform threshold;
    private PlayerItemInteraction items;
    private OpeningCutsceneController opening;
    private bool tracking;
    private bool wasInside;
    private float bannerUntil;
    private GameObject banner;
    private AudioSource alarmAudio;

    private void Start()
    {
        GameObject entrance = GameObject.Find("Exterior Entrance Threshold");
        if (entrance != null) threshold = entrance.transform;
        items = GetComponent<PlayerItemInteraction>();
        opening = FindFirstObjectByType<OpeningCutsceneController>();
    }

    private void Update()
    {
        if (threshold == null || items == null) return;
        if (opening != null && opening.IsRunning) { tracking = false; return; }
        float side = Vector3.Dot(transform.position - threshold.position, threshold.forward);
        if (!tracking)
        {
            tracking = true;
            wasInside = side > 0.35f;
            return;
        }
        if (side > 0.35f) wasInside = true;
        else if (wasInside && side < -0.35f)
        {
            wasInside = false;
            float sideways = Mathf.Abs(Vector3.Dot(transform.position - threshold.position, threshold.right));
            if (sideways <= 2.5f && HasUnpaidShopping()) SoundAlarm();
        }
        if (banner != null && banner.activeSelf && Time.time >= bannerUntil) banner.SetActive(false);
    }

    private bool HasUnpaidShopping()
    {
        if (GrandmaShoppingLoop.Instance != null && GrandmaShoppingLoop.Instance.RoundComplete) return false;
        if (items.IsHolding) return true;
        foreach (ShoppingCart cart in FindObjectsByType<ShoppingCart>(FindObjectsSortMode.None))
        {
            var controller = cart.GetComponent<ShoppingCartController>();
            if (controller != null && controller.IsPlayerHolding && cart.ItemCount > 0 &&
                (cart.transform.position - transform.position).sqrMagnitude < 25f) return true;
        }
        return false;
    }

    private void SoundAlarm()
    {
        if (ChaosManager.Instance != null)
            ChaosManager.Instance.ReportEvent(ChaosEventType.AttemptedTheft, gameObject, gameObject);
        foreach (SecurityGuardAI guard in FindObjectsByType<SecurityGuardAI>(FindObjectsSortMode.None))
            guard.AlertUnpaidExit(transform);
        ShowBanner();
        if (alarmAudio == null)
        {
            alarmAudio = gameObject.AddComponent<AudioSource>();
            alarmAudio.playOnAwake = false;
            alarmAudio.spatialBlend = 0f;
            int rate = 22050;
            float[] samples = new float[rate];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / rate;
                float envelope = Mathf.Min(1f, time * 25f) * Mathf.Min(1f, (1f - time) * 12f);
                float frequency = (i / (rate / 5)) % 2 == 0 ? 880f : 660f;
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.18f;
            }
            AudioClip clip = AudioClip.Create("Unpaid shopping alarm", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            alarmAudio.clip = clip;
        }
        alarmAudio.Play();
    }

    private void ShowBanner()
    {
        if (banner == null)
        {
            var canvasObject = new GameObject("Security Alarm HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            banner = new GameObject("Unpaid Shopping Warning", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            banner.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = banner.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -35);
            rect.sizeDelta = new Vector2(650, 112);
            banner.GetComponent<Image>().color = new Color32(224, 216, 158, 255);
            var label = new GameObject("Alarm Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            label.transform.SetParent(banner.transform, false);
            var textRect = label.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 4); textRect.offsetMax = new Vector2(-8, -4);
            var text = label.GetComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts/Childs Journal SDF Fixed");
            text.text = "UNPAID SHOPPING!!\nSECURITY IS COMING!";
            text.color = new Color32(172, 20, 20, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 22; text.fontSizeMax = 43;
            text.raycastTarget = false;
        }
        banner.SetActive(true);
        bannerUntil = Time.time + 4f;
    }
}
