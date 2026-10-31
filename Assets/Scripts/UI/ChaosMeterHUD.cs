using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using How2Shop.Chaos;
using How2Shop.Security;

namespace How2Shop.Chaos.UI
{
    /// <summary>Small, deliberately scruffy HUD built on the existing gameplay canvas.</summary>
    [DisallowMultipleComponent]
    public sealed class ChaosMeterHUD : MonoBehaviour
    {
        private static readonly float[] StarThresholds = { 10f, 25f, 50f, 75f };
        private static readonly Color32[] SegmentColors =
        {
            new Color32(255, 231, 0, 255),
            new Color32(81, 255, 0, 255),
            new Color32(255, 112, 0, 255),
            new Color32(242, 10, 20, 255)
        };

        [Header("Painted-in-the-dark HUD")]
        [SerializeField] private Vector2 topLeftOffset = new Vector2(24f, 22f);
        [SerializeField] private TMP_FontAsset handwrittenFont;
        [SerializeField, Range(0f, 1f)] private float warningVolume = 0.35f;

        private Image[] fillSegments;
        private RectTransform[] fillSegmentRects;
        private ChaosStarGraphic[] stars;
        private Coroutine[] starFlashes;

        private ChaosManager manager;
        private RectTransform hudRoot;
        private RectTransform meterShakeRoot;
        private TMP_Text percentText;
        private TMP_Text wantedText;
        private TMP_Text veryBadStamp;
        private GameObject warningBanner;
        private Image warningBannerFill;
        private GameObject spottedWarningBanner;
        private Image spottedWarningFill;
        private AudioSource warningAudio;
        private AudioClip warningClip;
        private SecurityGuardAI securityGuard;
        private Coroutine shakeRoutine;
        private Coroutine warningFlashRoutine;
        private Coroutine spottedWarningRoutine;
        private Coroutine managerConnectionRoutine;
        private bool subscribedToManager;

        private void Awake()
        {
            EnsureRuntimeArrays();
            EnsureHandwrittenFont();
            StyleExistingControls();
            BuildHud();
            EnsureWarningSound();
        }

        private void OnEnable()
        {
            EnsureRuntimeArrays();
            EnsureHandwrittenFont();
            EnsureHudBuilt();
            EnsureWarningSound();
            TrySubscribeToGuard();
            subscribedToManager = false;
            manager = null;
            if (!TrySubscribeToManager())
                managerConnectionRoutine = StartCoroutine(ConnectToChaosManager());
        }

        private void EnsureRuntimeArrays()
        {
            if (fillSegments == null || fillSegments.Length != 4)
                fillSegments = new Image[4];
            if (fillSegmentRects == null || fillSegmentRects.Length != 4)
                fillSegmentRects = new RectTransform[4];
            if (stars == null || stars.Length != 4)
                stars = new ChaosStarGraphic[4];
            if (starFlashes == null || starFlashes.Length != 4)
                starFlashes = new Coroutine[4];
        }

        private void EnsureHandwrittenFont()
        {
            if (handwrittenFont == null)
                handwrittenFont = Resources.Load<TMP_FontAsset>("Fonts/Childs Journal SDF Fixed");
            if (handwrittenFont == null && TMP_Settings.defaultFontAsset != null)
                handwrittenFont = TMP_Settings.defaultFontAsset;
        }

        private void EnsureHudBuilt()
        {
            if (hudRoot != null && fillSegmentRects[0] != null)
                return;

            Transform oldHud = transform.Find("Painted Chaos HUD");
            if (oldHud != null)
                Destroy(oldHud.gameObject);
            BuildHud();
        }

        private IEnumerator ConnectToChaosManager()
        {
            while (!TrySubscribeToManager())
                yield return null;
            managerConnectionRoutine = null;
        }

        private bool TrySubscribeToManager()
        {
            if (subscribedToManager)
                return true;

            manager = ChaosManager.Instance;
            if (manager == null)
                return false;

            manager.ChaosChanged += OnChaosChanged;
            manager.WantedLevelChanged += OnWantedLevelChanged;
            subscribedToManager = true;
            UpdateMeter(manager.CurrentChaos);
            UpdateWantedLevel(manager.WantedLevel);
            return true;
        }

        private void OnDisable()
        {
            if (securityGuard != null)
                securityGuard.PlayerSpotted -= OnPlayerSpotted;
            securityGuard = null;
            if (manager != null && subscribedToManager)
            {
                manager.ChaosChanged -= OnChaosChanged;
                manager.WantedLevelChanged -= OnWantedLevelChanged;
            }
            subscribedToManager = false;
            manager = null;
            if (managerConnectionRoutine != null)
            {
                StopCoroutine(managerConnectionRoutine);
                managerConnectionRoutine = null;
            }
            if (spottedWarningRoutine != null)
            {
                StopCoroutine(spottedWarningRoutine);
                spottedWarningRoutine = null;
            }
        }

        private void OnDestroy()
        {
            if (warningClip != null)
                Destroy(warningClip);
        }

        private void StyleExistingControls()
        {
            Font handwritten = Resources.Load<Font>("Fonts/childs-journal");
            Transform instructions = transform.Find("Pickup Instructions");
            if (instructions != null)
            {
                Image paper = instructions.GetComponent<Image>();
                if (paper != null)
                {
                    paper.color = new Color32(224, 216, 158, 255);
                    Outline outline = paper.GetComponent<Outline>();
                    if (outline == null) outline = paper.gameObject.AddComponent<Outline>();
                    outline.effectColor = Color.black;
                    outline.effectDistance = new Vector2(4f, -3f);
                }
                foreach (Text text in instructions.GetComponentsInChildren<Text>(true))
                {
                    text.color = Color.black;
                    if (handwritten != null) text.font = handwritten;
                }
                // The shopping guide and pause menu now carry these controls.
                instructions.gameObject.SetActive(false);
            }

            Transform throwBar = transform.Find("Throw Force Bar");
            if (throwBar == null) return;
            foreach (Text text in throwBar.GetComponentsInChildren<Text>(true))
            {
                text.color = Color.black;
                if (handwritten != null) text.font = handwritten;
                RectTransform label = text.rectTransform;
                GameObject backing = new GameObject(text.name + " Paper", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                backing.transform.SetParent(label.parent, false);
                RectTransform rect = backing.GetComponent<RectTransform>();
                rect.anchorMin = label.anchorMin;
                rect.anchorMax = label.anchorMax;
                rect.pivot = label.pivot;
                rect.anchoredPosition = label.anchoredPosition;
                rect.sizeDelta = label.sizeDelta;
                rect.localRotation = label.localRotation;
                backing.transform.SetSiblingIndex(label.GetSiblingIndex());
                Image image = backing.GetComponent<Image>();
                image.color = new Color32(224, 216, 158, 255);
                image.raycastTarget = false;
                Outline outline = backing.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(3f, -2f);
            }
        }

        private void BuildHud()
        {
            hudRoot = CreateRect("Painted Chaos HUD", transform, topLeftOffset,
                new Vector2(545f, 224f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            // Two misregistered black passes make the panel look outlined by hand.
            CreateImage("crooked black outline", hudRoot, new Vector2(-1f, 0f),
                new Vector2(544f, 218f), new Color32(0, 0, 0, 255), 1.1f);
            CreateImage("other crooked black outline", hudRoot, new Vector2(5f, 4f),
                new Vector2(533f, 216f), new Color32(22, 17, 7, 255), -1.7f);
            CreateImage("yellowed paper", hudRoot, new Vector2(3f, 2f),
                new Vector2(534f, 212f), new Color32(224, 216, 158, 255), 0.55f);

            CreateText("BADNESS", new Vector2(14f, 5f), new Vector2(235f, 43f),
                37f, Color.black, TextAlignmentOptions.MidlineLeft, -2.3f);
            CreateText("VERY BAD", new Vector2(250f, 18f), new Vector2(111f, 27f),
                18f, new Color32(144, 20, 7, 255), TextAlignmentOptions.Center, 3.7f);

            Image uhOhCard = CreateImage("uh-oh crooked yellow card", hudRoot,
                new Vector2(365f, 8f), new Vector2(84f, 35f),
                new Color32(250, 210, 0, 255), 3.8f);
            AddUnevenBorder(uhOhCard.rectTransform, new Vector2(84f, 35f));
            CreateText("UH OH", new Vector2(366f, 9f), new Vector2(82f, 33f),
                19f, Color.black, TextAlignmentOptions.Center, -3.2f);

            CreateText("▲", new Vector2(476f, 0f), new Vector2(43f, 45f),
                42f, new Color32(255, 226, 0, 255), TextAlignmentOptions.Center, 5f,
                useHandwrittenFont: false);
            CreateText("!", new Vector2(490f, 14f), new Vector2(16f, 20f),
                17f, Color.black, TextAlignmentOptions.Center, -4f);

            float[] starSizes = { 39f, 35f, 42f, 37f };
            float[] starTilts = { -5f, 4f, -2f, 7f };
            for (int i = 0; i < stars.Length; i++)
            {
                var starObject = new GameObject($"hand cut star {i + 1}",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(ChaosStarGraphic));
                RectTransform rect = starObject.GetComponent<RectTransform>();
                rect.SetParent(hudRoot, false);
                SetTopLeftRect(rect, new Vector2(17f + i * 45f, 51f),
                    new Vector2(starSizes[i], starSizes[i] + ((i % 2 == 0) ? 2f : -1f)));
                rect.localRotation = Quaternion.Euler(0f, 0f, starTilts[i]);
                stars[i] = starObject.GetComponent<ChaosStarGraphic>();
                stars[i].SetSeed(i * 1.71f + 0.43f);
                stars[i].SetFilled(false);
            }

            CreateText("----->", new Vector2(199f, 56f), new Vector2(70f, 30f),
                19f, Color.black, TextAlignmentOptions.Center, -7f);

            // A crooked red banner appears only at four stars.
            warningBanner = new GameObject("OH NO HES COMING warning", typeof(RectTransform));
            RectTransform bannerRect = warningBanner.GetComponent<RectTransform>();
            bannerRect.SetParent(hudRoot, false);
            SetTopLeftRect(bannerRect, new Vector2(280f, 53f), new Vector2(245f, 37f));
            bannerRect.localRotation = Quaternion.Euler(0f, 0f, -1.6f);
            CreateImageOn("black banner outline", bannerRect, new Vector2(-2f, -1f),
                new Vector2(247f, 39f), Color.black, 1.8f);
            warningBannerFill = CreateImageOn("red warning paint", bannerRect, Vector2.zero,
                new Vector2(242f, 35f), new Color32(238, 0, 16, 255), -1.1f);
            CreateText("! OH NO HES COMING !", new Vector2(4f, 1f), new Vector2(237f, 33f),
                18f, Color.white, TextAlignmentOptions.Center, 1.3f, bannerRect);
            warningBanner.SetActive(false);

            hudRoot.sizeDelta = new Vector2(545f, 270f);
            spottedWarningBanner = new GameObject("THE GUARD SAW YOU warning", typeof(RectTransform));
            RectTransform spottedRect = spottedWarningBanner.GetComponent<RectTransform>();
            spottedRect.SetParent(hudRoot, false);
            SetTopLeftRect(spottedRect, new Vector2(18f, 225f), new Vector2(323f, 35f));
            spottedRect.localRotation = Quaternion.Euler(0f, 0f, 2.2f);
            CreateImageOn("spotted warning ink outline", spottedRect, new Vector2(-2f, -2f),
                new Vector2(327f, 39f), Color.black, -1.4f);
            spottedWarningFill = CreateImageOn("spotted warning yellow paint", spottedRect,
                Vector2.zero, new Vector2(323f, 35f), new Color32(255, 235, 0, 255), 1.2f);
            CreateText("THE GUARD SAW YOU!!", new Vector2(2f, 1f), new Vector2(316f, 33f),
                18f, new Color32(183, 0, 0, 255), TextAlignmentOptions.Center, -1.3f, spottedRect);
            spottedWarningBanner.SetActive(false);

            meterShakeRoot = CreateRect("meter that wiggles", hudRoot,
                new Vector2(10f, 101f), new Vector2(372f, 48f),
                new Vector2(0f, 1f), new Vector2(0f, 1f));
            meterShakeRoot.localRotation = Quaternion.Euler(0f, 0f, -0.6f);
            CreateImageOn("bar thick black border", meterShakeRoot, Vector2.zero,
                new Vector2(368f, 39f), Color.black, -0.7f);
            CreateImageOn("bar old grey background", meterShakeRoot, new Vector2(5f, 5f),
                new Vector2(356f, 27f), new Color32(126, 119, 104, 255), 0.6f);

            const float segmentWidth = 85f;
            const float segmentGap = 3f;
            for (int i = 0; i < fillSegments.Length; i++)
            {
                Vector2 position = new Vector2(9f + i * (segmentWidth + segmentGap), 8f);
                CreateImageOn($"empty meter color {i}", meterShakeRoot, position,
                    new Vector2(segmentWidth, 20f), new Color32(76, 68, 54, 255), 0f);
                fillSegments[i] = CreateImageOn($"bad color fills {i}", meterShakeRoot,
                    position, new Vector2(segmentWidth, 20f), SegmentColors[i], 0f);
                fillSegmentRects[i] = fillSegments[i].rectTransform;
            }

            percentText = CreateText("chaos percent", new Vector2(390f, 99f),
                new Vector2(111f, 43f), 30f, Color.black, TextAlignmentOptions.Center, 1.7f);
            wantedText = CreateText("wanted label", new Vector2(17f, 150f),
                new Vector2(340f, 42f), 26f, Color.black,
                TextAlignmentOptions.MidlineLeft, 1.4f);
            veryBadStamp = CreateText("VERY BAD! side stamp", new Vector2(360f, 157f),
                new Vector2(135f, 35f), 20f, new Color32(177, 13, 0, 255),
                TextAlignmentOptions.Center, -4.5f);
            veryBadStamp.gameObject.SetActive(false);

            // Scribbly short marks; each is independently crooked and cheap to render.
            CreateImage("random black paint dash one", hudRoot, new Vector2(485f, 91f),
                new Vector2(21f, 3f), Color.black, 15f);
            CreateImage("random black paint dash two", hudRoot, new Vector2(487f, 99f),
                new Vector2(16f, 2f), Color.black, -11f);
            CreateImage("random black paint dash three", hudRoot, new Vector2(461f, 197f),
                new Vector2(36f, 3f), Color.black, 7f);
        }

        private void UpdateMeter(float chaos)
        {
            float normalized = Mathf.Clamp01(chaos / 100f);
            percentText.text = $"{Mathf.RoundToInt(normalized * 100f)}%";
            for (int i = 0; i < fillSegments.Length; i++)
            {
                float segmentFill = Mathf.Clamp01(normalized * 4f - i);
                fillSegmentRects[i].sizeDelta = new Vector2(85f * segmentFill, 20f);
            }
        }

        private void UpdateWantedLevel(int level)
        {
            level = Mathf.Clamp(level, 0, 4);
            string[] labels =
            {
                "GOOD BOY", "BIT NAUGHTY", "STOP THAT", "GUARD IS MAD", "YOU ARE COOKED"
            };
            wantedText.text = labels[level];
            wantedText.color = level >= 3
                ? new Color32(181, 0, 0, 255)
                : Color.black;
            veryBadStamp.gameObject.SetActive(level >= 2 && level < 4);

            for (int i = 0; i < stars.Length; i++)
                stars[i].SetFilled(i < level);

            bool shouldWarn = level == 4;
            if (warningBanner.activeSelf != shouldWarn)
                warningBanner.SetActive(shouldWarn);
            if (shouldWarn)
            {
                if (warningFlashRoutine != null)
                    StopCoroutine(warningFlashRoutine);
                warningFlashRoutine = StartCoroutine(FlashWarningBanner());
            }
            else if (warningFlashRoutine != null)
            {
                StopCoroutine(warningFlashRoutine);
                warningFlashRoutine = null;
                warningBannerFill.color = new Color32(238, 0, 16, 255);
            }
        }

        private void OnChaosChanged(float previous, float current)
        {
            UpdateMeter(current);
            if (current <= previous)
                return;

            if (warningAudio != null && warningClip != null)
                warningAudio.PlayOneShot(warningClip, warningVolume);
            if (shakeRoutine != null)
                StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(ShakeMeter());
            FlashAffectedStar(previous, current);
        }

        private void OnWantedLevelChanged(int previous, int current)
        {
            UpdateWantedLevel(current);
            // A falling star flashes too; upward changes were already flashed with the chaos event.
            if (current < previous)
                FlashStar(Mathf.Clamp(previous - 1, 0, stars.Length - 1));
        }

        private void TrySubscribeToGuard()
        {
            if (securityGuard != null)
                return;
            securityGuard = FindAnyObjectByType<SecurityGuardAI>();
            if (securityGuard != null)
                securityGuard.PlayerSpotted += OnPlayerSpotted;
        }

        private void OnPlayerSpotted(SecurityGuardAI guard)
        {
            if (spottedWarningBanner == null)
                return;
            if (spottedWarningRoutine != null)
                StopCoroutine(spottedWarningRoutine);
            spottedWarningRoutine = StartCoroutine(FlashSpottedWarning());
        }

        private IEnumerator FlashSpottedWarning()
        {
            spottedWarningBanner.SetActive(true);
            Color yellow = new Color32(255, 235, 0, 255);
            Color red = new Color32(255, 25, 0, 255);
            for (int i = 0; i < 10; i++)
            {
                spottedWarningFill.color = (i % 2 == 0) ? red : yellow;
                spottedWarningBanner.transform.localScale = (i % 2 == 0)
                    ? Vector3.one * 1.04f
                    : Vector3.one * 0.98f;
                if (warningAudio != null && warningClip != null && i == 0)
                    warningAudio.PlayOneShot(warningClip, warningVolume);
                yield return new WaitForSecondsRealtime(0.12f);
            }
            spottedWarningBanner.transform.localScale = Vector3.one;
            spottedWarningBanner.SetActive(false);
            spottedWarningRoutine = null;
        }

        private void FlashAffectedStar(float previous, float current)
        {
            for (int i = StarThresholds.Length - 1; i >= 0; i--)
            {
                if (previous < StarThresholds[i] && current >= StarThresholds[i])
                {
                    FlashStar(i);
                    return;
                }
            }

            int nextStar = 0;
            while (nextStar < StarThresholds.Length - 1 && current >= StarThresholds[nextStar])
                nextStar++;
            FlashStar(nextStar);
        }

        private void FlashStar(int index)
        {
            if (index < 0 || index >= stars.Length || !isActiveAndEnabled)
                return;
            if (starFlashes[index] != null)
                StopCoroutine(starFlashes[index]);
            starFlashes[index] = StartCoroutine(FlashStarRoutine(index));
        }

        private IEnumerator FlashStarRoutine(int index)
        {
            ChaosStarGraphic star = stars[index];
            Vector3 normalScale = star.transform.localScale;
            for (int flash = 0; flash < 3; flash++)
            {
                star.SetFlashing(true);
                star.transform.localScale = normalScale * 1.24f;
                yield return new WaitForSecondsRealtime(0.075f);
                star.SetFlashing(false);
                star.transform.localScale = normalScale * 0.9f;
                yield return new WaitForSecondsRealtime(0.065f);
            }
            star.transform.localScale = normalScale;
            starFlashes[index] = null;
        }

        private IEnumerator ShakeMeter()
        {
            const float duration = 0.28f;
            const float strength = 4.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float fade = 1f - elapsed / duration;
                meterShakeRoot.anchoredPosition = new Vector2(
                    10f + Random.Range(-strength, strength) * fade,
                    101f + Random.Range(-strength, strength) * fade);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            meterShakeRoot.anchoredPosition = new Vector2(10f, 101f);
            shakeRoutine = null;
        }

        private IEnumerator FlashWarningBanner()
        {
            Color red = new Color32(238, 0, 16, 255);
            Color yellow = new Color32(255, 238, 0, 255);
            for (int i = 0; i < 8; i++)
            {
                warningBannerFill.color = (i % 2 == 0) ? Color.white : yellow;
                yield return new WaitForSecondsRealtime(0.12f);
            }
            warningBannerFill.color = red;
            warningFlashRoutine = null;
        }

        private void EnsureWarningSound()
        {
            if (warningAudio == null)
                warningAudio = GetComponent<AudioSource>();
            if (warningAudio == null)
                warningAudio = gameObject.AddComponent<AudioSource>();
            warningAudio.playOnAwake = false;
            warningAudio.loop = false;
            warningAudio.spatialBlend = 0f;
            warningAudio.volume = 1f;
            warningAudio.ignoreListenerPause = true;

            if (warningClip != null)
                return;

            const int sampleRate = 22050;
            const float duration = 0.22f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float frequency = Mathf.Lerp(720f, 260f, t / duration);
                phase += frequency / sampleRate;
                float saw = (phase - Mathf.Floor(phase)) * 2f - 1f;
                float envelope = Mathf.Pow(1f - t / duration, 1.7f);
                samples[i] = saw * envelope * 0.42f;
            }

            warningClip = AudioClip.Create("awful chaos warning", sampleCount, 1, sampleRate, false);
            warningClip.SetData(samples, 0);
        }

        private RectTransform CreateRect(string name, Transform parent, Vector2 topLeft,
            Vector2 size, Vector2 anchor, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchor.y > 0.5f
                ? new Vector2(topLeft.x, -topLeft.y)
                : topLeft;
            return rect;
        }

        private Image CreateImage(string name, Transform parent, Vector2 topLeft,
            Vector2 size, Color color, float rotation = 0f)
        {
            return CreateImageOn(name, parent, topLeft, size, color, rotation);
        }

        private Image CreateImageOn(string name, Transform parent, Vector2 topLeft,
            Vector2 size, Color color, float rotation = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetTopLeftRect(rect, topLeft, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text CreateText(string value, Vector2 topLeft, Vector2 size, float fontSize,
            Color color, TextAlignmentOptions alignment, float rotation = 0f,
            Transform parent = null, bool useHandwrittenFont = true)
        {
            var go = new GameObject(value, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent != null ? parent : hudRoot, false);
            SetTopLeftRect(rect, topLeft, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.font = useHandwrittenFont ? handwrittenFont : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            return text;
        }

        private static void SetTopLeftRect(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(topLeft.x, -topLeft.y);
            rect.sizeDelta = size;
        }

        private void AddUnevenBorder(RectTransform parent, Vector2 size)
        {
            CreateImageOn("ink edge top", parent, new Vector2(-1f, -1f),
                new Vector2(size.x + 2f, 3f), Color.black, 1.5f);
            CreateImageOn("ink edge bottom", parent, new Vector2(0f, size.y - 2f),
                new Vector2(size.x - 1f, 4f), Color.black, -1.1f);
        }
    }
}
