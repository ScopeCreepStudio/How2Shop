using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using How2Shop.Chaos.UI;
using How2Shop.Chaos;
using How2Shop.Items;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GrandmaShoppingLoop : MonoBehaviour
{
    private const string GameplayTutorialCompleteKey = "How2Shop.GameplayTutorialComplete";
    private const string PersonalBestScoreKeyPrefix = "How2Shop.PersonalBest.";

    [Header("Shopping Round Rules")]
    [SerializeField, Min(30f)] private float roundDurationSeconds = 300f;
    [SerializeField, Min(0)] private int startingBudgetPence = 500;
    [SerializeField, Min(0)] private int penaltyPerPoundOverBudget = 10;

    private enum TutorialStep
    {
        Off,
        FindNanItem,
        FindTrolley,
        LoadTrolley,
        Checkout
    }

    public static GrandmaShoppingLoop Instance { get; private set; }

    private GameObject listCard;
    private GameObject resultBackdrop;
    private GameObject resultShadow;
    private GameObject resultCard;
    private GameObject checkoutCard;
    private GameObject checkoutShadow;
    private GameObject timeBudgetCard;
    private GameObject timeBudgetShadow;
    private GameObject dareCard;
    private GameObject receiptCard;
    private TMP_Text dareText;
    private TMP_Text receiptText;
    private Button receiptButton;
    private int dareType;
    private int peakWantedLevel;
    private TMP_Text listHint;
    private TMP_Text timerText;
    private TMP_Text budgetText;
    private TMP_Text spentText;
    private TMP_Text resultDetails;
    private TMP_Text resultPersonalBest;
    private Image timerFill;
    private TMP_Text resultItem;
    private TMP_Text resultScore;
    private TMP_Text resultFeedback;
    private TMP_Text resultGrade;
    private TMP_Text resultRubric;
    private TMP_Text resultRank;
    private TMP_Text checkoutItemText;
    private TMP_Text checkoutPriceText;
    private TMP_Text checkoutStatusText;
    private TMP_Text checkoutPercentText;
    private ChaosStarGraphic[] resultStars;
    private RectTransform[] rankBurstBits;
    private CanvasGroup[] rankBurstFades;
    private Button resultRetryButton;
    private Button resultMenuButton;
    private TMP_FontAsset reportFont;
    private CanvasGroup resultCanvasGroup;
    private CanvasGroup checkoutCanvasGroup;
    private Image resultScoreFill;
    private Image checkoutFill;
    private RectTransform checkoutScanLine;
    private AudioSource uiAudio;
    private AudioClip scanBeep;
    private AudioClip rankChime;
    private Coroutine endSequenceRoutine;
    private readonly List<ItemDefinition> checkoutItems = new List<ItemDefinition>();
    private bool roundComplete;
    private bool timerRunning;
    private bool finalSequenceRunning;
    private bool gameplayFrozenForResults;
    private float timeRemainingSeconds;
    private PlayerItemInteraction playerItems;
    private PlayerMovement playerMovement;
    private Transform playerCamera;
    private Transform entranceThreshold;
    private ShoppingCart tutorialTrolley;
    private CashierCheckoutConveyor[] cashiers;
    private ShoppingCartController trolleyController;
    private TutorialStep tutorialStep;
    private GameObject trolleyCompassCard;
    private RectTransform trolleyCompassArrow;
    private TMP_Text trolleyCompassDistance;
    private GameObject trolleyWorldMarker;
    private Mesh trolleyMarkerMesh;
    private Material trolleyMarkerMaterial;
    private bool trolleyGuidanceVisible;

    public bool RoundComplete => roundComplete;
    public int ScannedCheckoutItemCount => checkoutItems.Count;
    public bool HasScannedCheckoutItems => checkoutItems.Count > 0;
    public int LastScore { get; private set; }
    public int TotalSpentPence { get; private set; }
    private int RoundBudgetPence => startingBudgetPence * GrandmaShoppingRequests.CurrentList.Count;
    private float RoundDuration => roundDurationSeconds + 45f * (GrandmaShoppingRequests.CurrentList.Count - 1);
    public int BudgetRemainingPence => RoundBudgetPence - TotalSpentPence;
    public float TimeRemainingSeconds => timeRemainingSeconds;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
    }

    private IEnumerator Start()
    {
        // Let the opening cutscene start before checking its state.
        yield return null;

        OpeningCutsceneController opening = FindAnyObjectByType<OpeningCutsceneController>();
        while (opening != null && opening.IsRunning)
            yield return null;

        playerItems = FindAnyObjectByType<PlayerItemInteraction>();
        playerMovement = FindAnyObjectByType<PlayerMovement>();
        playerCamera = Camera.main != null ? Camera.main.transform : null;
        tutorialTrolley = FindAnyObjectByType<ShoppingCart>();
        cashiers = FindObjectsByType<CashierCheckoutConveyor>(FindObjectsSortMode.None);
        trolleyController = tutorialTrolley != null
            ? tutorialTrolley.GetComponent<ShoppingCartController>()
            : null;
        GameObject threshold = GameObject.Find("Exterior Entrance Threshold");
        entranceThreshold = threshold != null ? threshold.transform : null;
        tutorialStep = GrandmaShoppingRequests.ConsumeGameplayTutorial()
            ? TutorialStep.FindNanItem
            : TutorialStep.Off;
        ResetShoppingRound();
        if (timeBudgetCard != null)
            timeBudgetCard.SetActive(true);
        if (timeBudgetShadow != null)
            timeBudgetShadow.SetActive(true);
        if (listCard != null)
            listCard.SetActive(true);
    }

    private void Update()
    {
        if (!roundComplete && timerRunning)
        {
            timeRemainingSeconds = Mathf.Max(0f, timeRemainingSeconds - Time.deltaTime);
            UpdateTimeBudgetHud();
            UpdateDareHud();
            if (timeRemainingSeconds <= 0f)
            {
                FinishTimedOutRound();
                return;
            }
        }

        if (roundComplete)
        {
            if (finalSequenceRunning)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.enterKey.wasPressedThisFrame)
                StartAnotherAttempt();
            return;
        }

        if (listHint == null || playerItems == null)
            return;

        if (tutorialStep != TutorialStep.Off)
        {
            UpdateFirstRunTutorial();
            UpdateTrolleyCompass();
            return;
        }

        if (HasScannedCheckoutItems)
        {
            bool cashierBusy = IsAnyCashierScanning();
            string progress = ScannedCheckoutItemCount + " / " + GrandmaShoppingRequests.CurrentList.Count + " ITEMS SCANNED. ";
            listHint.text = cashierBusy
                ? progress + "WAIT FOR THE BELT TO CLEAR."
                : progress + "SCAN MORE OR LOOK AT THE CASHIER AND PRESS Q OR CLICK TO FINISH.";
            return;
        }

        int collected = (tutorialTrolley != null ? tutorialTrolley.ItemCount : 0) + (playerItems.IsHolding ? 1 : 0);
        int needed = GrandmaShoppingRequests.CurrentList.Count;
        if (entranceThreshold != null && playerItems.transform.position.z < entranceThreshold.position.z)
            listHint.text = "GO INTO THE SHOP  •  Find " + needed + " items for Nan. Remember the list!";
        else if (playerItems.IsHolding)
            listHint.text = "SHOPPING " + collected + " / " + needed + "  •  F: put item in trolley. Q at the conveyor: scan.";
        else if (collected >= needed)
            listHint.text = "SHOPPING " + collected + " / " + needed + "  •  Take your choices to a cashier. R: take from trolley.";
        else
            listHint.text = "SHOPPING " + collected + " / " + needed + "  •  Find Nan's items. Look at a product and press R.";
    }

    public void RecordCheckout(ItemSpriteView item)
    {
        if (roundComplete || item == null || item.Item == null)
            return;

        checkoutItems.Add(item.Item);
        TotalSpentPence += Mathf.Max(0, item.Item.PricePence);
        UpdateTimeBudgetHud();

        string itemCount = checkoutItems.Count == 1 ? "1 item" : checkoutItems.Count + " items";
        if (listHint != null)
            listHint.text = "CASHIER SCANNED " + itemCount + ". Wait for the belt, then look at the cashier and press Q or click to finish checkout.";
    }

    public bool CompleteCheckoutTransaction()
    {
        if (roundComplete || checkoutItems.Count == 0)
            return false;

        timerRunning = false;
        int requestedCount = GrandmaShoppingRequests.CurrentList.Count;
        var basket = GrandmaShoppingRequests.EvaluateBasket(checkoutItems);
        int baseScore = basket.Score;
        int overBudgetPence = Mathf.Max(0, TotalSpentPence - RoundBudgetPence);
        int budgetPenalty = overBudgetPence == 0
            ? 0
            : Mathf.Min(50, ((overBudgetPence + 99) / 100) * penaltyPerPoundOverBudget);
        roundComplete = true;
        bool dareWon = IsDareComplete();
        // A bonus can help a good run, but cannot turn incorrect shopping into a perfect score.
        int dareBonus = dareWon && baseScore >= 50 ? Mathf.Min(5, Mathf.Max(0, 99 - baseScore)) : 0;
        LastScore = Mathf.Clamp(baseScore - budgetPenalty + dareBonus, 0, 100);
        PopulateReceipt(basket, budgetPenalty, dareBonus);
        resultItem.text = "YOU BOUGHT " + checkoutItems.Count + " / " + requestedCount + " REQUESTED ITEMS";
        resultScore.text = LastScore + " / 100";
        resultFeedback.text = baseScore >= 80 ? "Nan is pleased with your shopping!" :
            baseScore >= 50 ? "Some of those are what Nan had in mind." : "Nan isn't quite sure what you bought, love.";
        string misreadFeedback = GrandmaShoppingRequests.FindMisreadFeedback(checkoutItems);
        if (!string.IsNullOrEmpty(misreadFeedback))
            resultFeedback.text = misreadFeedback;
        if (checkoutItems.Count < requestedCount)
            resultFeedback.text += "\nYou missed " + (requestedCount - checkoutItems.Count) + " item(s) from the list.";
        else if (checkoutItems.Count > requestedCount)
            resultFeedback.text += "\nYou bought " + (checkoutItems.Count - requestedCount) + " extra item(s).";
        if (budgetPenalty > 0)
            resultFeedback.text += "\nNan says you went " + FormatMoney(overBudgetPence) +
                " over budget. (-" + budgetPenalty + " points)";
        resultGrade.text = GetLetterGrade(LastScore);
        resultRubric.text = dareWon
            ? (dareBonus > 0 ? "NAN'S DARE COMPLETE!  +" + dareBonus + " POINTS" : (baseScore < 50 ? "DARE COMPLETE! GET 50+ SHOPPING POINTS FOR THE BONUS" : "DARE COMPLETE! BONUS CAPPED BELOW A PERFECT SCORE"))
            : "NAN'S DARE MISSED  •  CHECK THE RECEIPT TO SEE YOUR MATCHES";

        UpdatePersonalBest(true);
        UpdateResultDetails();
        checkoutItemText.text = checkoutItems.Count + " ITEMS SCANNED • " + requestedCount + " REQUESTED";
        checkoutPriceText.text = "TOTAL " + FormatMoney(TotalSpentPence);
        checkoutStatusText.text = "ADDING UP YOUR BASKET...";
        checkoutPercentText.text = "0%";
        checkoutFill.fillAmount = 0f;
        checkoutScanLine.anchoredPosition = new Vector2(-312f, -23f);
        checkoutCanvasGroup.alpha = 0f;
        checkoutCard.transform.localScale = Vector3.one * 0.82f;
        if (dareCard != null) dareCard.SetActive(false);
        PauseGameplayForEndSequence();
        resultBackdrop.SetActive(true);
        checkoutShadow.SetActive(true);
        checkoutCard.SetActive(true);
        resultShadow.SetActive(false);
        resultCard.SetActive(false);
        finalSequenceRunning = true;
        endSequenceRoutine = StartCoroutine(CheckoutThenRevealResults());
        listHint.text = "Nan has seen your choice.";

        if (tutorialStep != TutorialStep.Off)
        {
            tutorialStep = TutorialStep.Off;
            PlayerPrefs.SetInt(GameplayTutorialCompleteKey, 1);
            PlayerPrefs.Save();
            SetTrolleyGuidanceVisible(false);
        }

        return true;
    }

    private void FinishTimedOutRound()
    {
        if (roundComplete)
            return;

        timerRunning = false;
        timeRemainingSeconds = 0f;
        roundComplete = true;
        LastScore = 0;
        resultItem.text = "Nan's timer ran out before checkout.";
        resultScore.text = "0 / 100";
        resultFeedback.text = "You didn't reach the checkout in time.\nNan says she'll do the shopping herself.";
        resultGrade.text = "F";
        resultRubric.text = "GET TO THE CHECKOUT BEFORE THE TIMER RUNS OUT";
        if (dareCard != null) dareCard.SetActive(false);
        if (receiptButton != null) receiptButton.interactable = false;

        UpdatePersonalBest(false);
        UpdateResultDetails();
        checkoutCard.SetActive(false);
        checkoutShadow.SetActive(false);
        PauseGameplayForEndSequence();
        finalSequenceRunning = true;
        endSequenceRoutine = StartCoroutine(RevealFinalResults());
        if (listHint != null)
            listHint.text = "The shopping time ran out.";
        SetTrolleyGuidanceVisible(false);
    }

    private void PauseGameplayForEndSequence()
    {
        if (gameplayFrozenForResults)
            return;

        gameplayFrozenForResults = true;
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerItems != null) playerItems.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ShowResults()
    {
        checkoutCard.SetActive(false);
        checkoutShadow.SetActive(false);
        resultBackdrop.SetActive(true);
        resultShadow.SetActive(true);
        resultCard.SetActive(true);
    }

    private void CreateRankBurstBits()
    {
        rankBurstBits = new RectTransform[16];
        rankBurstFades = new CanvasGroup[rankBurstBits.Length];
        var root = new GameObject("Rank Celebration Confetti", typeof(RectTransform));
        root.transform.SetParent(resultCard.transform, false);
        root.transform.SetAsFirstSibling();
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Color[] colors =
        {
            new Color32(255, 218, 46, 255), new Color32(236, 51, 42, 255),
            new Color32(69, 167, 111, 255), new Color32(86, 151, 225, 255)
        };
        for (int i = 0; i < rankBurstBits.Length; i++)
        {
            var bit = new GameObject("Paper Confetti " + (i + 1),
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            bit.transform.SetParent(root.transform, false);
            RectTransform rect = bit.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = i % 3 == 0 ? new Vector2(16f, 8f) : new Vector2(10f, 12f);
            rect.localScale = Vector3.zero;
            Image image = bit.GetComponent<Image>();
            image.color = colors[i % colors.Length];
            image.raycastTarget = false;
            rankBurstFades[i] = bit.GetComponent<CanvasGroup>();
            rankBurstFades[i].alpha = 0f;
            bit.SetActive(false);
            rankBurstBits[i] = rect;
        }
    }

    private void CreateCheckoutCard(Transform canvasRoot)
    {
        checkoutShadow = CreatePanel("Checkout Card Ink Shadow", canvasRoot,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-7f, -6f), new Vector2(778f, 354f), new Color32(30, 22, 12, 255));
        checkoutCard = CreatePanel("Nan's Checkout Scan", canvasRoot,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(770f, 348f), new Color32(224, 216, 158, 255));
        checkoutCard.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -0.9f);
        Outline outline = checkoutCard.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(5f, -4f);
        checkoutCanvasGroup = checkoutCard.AddComponent<CanvasGroup>();

        checkoutStatusText = CreateCenteredUiText(checkoutCard.transform, "Checkout Scan Status",
            "SCANNING ITEM...", new Vector2(0f, -91f), new Vector2(690f, 38f), 25f,
            new Color32(119, 20, 12, 255), FontStyles.Bold, TextAlignmentOptions.Center);
        CreateCenteredUiText(checkoutCard.transform, "Checkout Heading", "NAN'S SELF CHECKOUT",
            new Vector2(0f, 111f), new Vector2(680f, 46f), 34f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
        checkoutItemText = CreateCenteredUiText(checkoutCard.transform, "Checkout Item Name",
            "Your choice", new Vector2(0f, 64f), new Vector2(680f, 38f), 25f,
            new Color32(35, 29, 15, 255), FontStyles.Bold, TextAlignmentOptions.Center);
        checkoutPriceText = CreateCenteredUiText(checkoutCard.transform, "Checkout Item Price",
            "PRICE £0.00", new Vector2(0f, 21f), new Vector2(500f, 28f), 20f,
            new Color32(86, 64, 30, 255), FontStyles.Normal, TextAlignmentOptions.Center);

        CreatePanel("Checkout Progress Track", checkoutCard.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -28f), new Vector2(570f, 24f), new Color32(58, 48, 29, 255));
        GameObject fillObject = CreatePanel("Checkout Progress Fill", checkoutCard.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -28f), new Vector2(570f, 24f), new Color32(250, 210, 0, 255));
        checkoutFill = fillObject.GetComponent<Image>();
        checkoutFill.type = Image.Type.Filled;
        checkoutFill.fillMethod = Image.FillMethod.Horizontal;
        checkoutFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        checkoutFill.fillAmount = 0f;
        checkoutPercentText = CreateCenteredUiText(checkoutCard.transform, "Checkout Percent",
            "0%", new Vector2(326f, -28f), new Vector2(80f, 36f), 23f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
        GameObject scanLine = CreatePanel("Red Laser Sweep", checkoutCard.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-281f, -28f), new Vector2(5f, 38f), new Color32(201, 24, 15, 255));
        checkoutScanLine = scanLine.GetComponent<RectTransform>();
        checkoutCard.SetActive(false);
        checkoutShadow.SetActive(false);
    }

    private TMP_Text CreateCenteredUiText(Transform parent, string name, string value,
        Vector2 position, Vector2 size, float fontSize, Color color,
        FontStyles style, TextAlignmentOptions alignment)
    {
        TMP_Text text = CreateText(parent, name, value,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            position, size, fontSize, color, style, alignment);
        text.font = reportFont;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.margin = Vector4.zero;
        return text;
    }

    private void CreateUiAudio()
    {
        uiAudio = gameObject.AddComponent<AudioSource>();
        uiAudio.playOnAwake = false;
        uiAudio.loop = false;
        uiAudio.spatialBlend = 0f;
        uiAudio.volume = 0.55f;
        uiAudio.ignoreListenerPause = true;
        scanBeep = CreateToneClip("Soft Checkout Scan Beep", new[] { 760f }, 0.075f);
        rankChime = CreateToneClip("Nan Approves Rank Chime", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.105f);
    }

    private static AudioClip CreateToneClip(string clipName, float[] notes, float secondsPerNote)
    {
        const int sampleRate = 22050;
        int samplesPerNote = Mathf.Max(1, Mathf.RoundToInt(secondsPerNote * sampleRate));
        var samples = new float[samplesPerNote * notes.Length];
        double phase = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            int noteIndex = i / samplesPerNote;
            float notePosition = (i % samplesPerNote) / (float)samplesPerNote;
            float envelope = Mathf.Clamp01(notePosition * 14f) * Mathf.Clamp01((1f - notePosition) * 8f);
            phase += 2.0 * Mathf.PI * notes[noteIndex] / sampleRate;
            samples[i] = Mathf.Sin((float)phase) * envelope * 0.24f;
        }

        AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void ResetShoppingRound()
    {
        checkoutItems.Clear();
        dareType = Random.Range(0, 3);
        peakWantedLevel = 0;
        if (receiptCard != null) receiptCard.SetActive(false);
        if (dareCard != null) dareCard.SetActive(true);
        if (receiptButton != null) receiptButton.interactable = false;
        TotalSpentPence = 0;
        timeRemainingSeconds = Mathf.Max(1f, RoundDuration);
        timerRunning = true;
        if (listCard != null)
            listCard.SetActive(true);
        UpdateTimeBudgetHud();
        UpdateDareHud();
        UpdateResultDetails();
    }

    private void UpdateDareHud()
    {
        if (dareText == null) return;
        if (ChaosManager.Instance != null)
            peakWantedLevel = Mathf.Max(peakWantedLevel, ChaosManager.Instance.WantedLevel);
        switch (dareType)
        {
            case 0:
                dareText.text = "EARLY BIRD: finish with half your time left.\nBonus needs 50+ shopping points.";
                break;
            case 1:
                dareText.text = "PENNY PINCHER: finish with 25% of budget left.\nBonus needs 50+ shopping points.";
                break;
            default:
                dareText.text = "MAKE A SCENE: reach 1 chaos star.\n" + (peakWantedLevel >= 1 ? "STAR REACHED!" : "STAR NOT YET REACHED") + "  •  50+ shopping points needed.";
                break;
        }
    }

    private bool IsDareComplete()
    {
        if (ChaosManager.Instance != null)
            peakWantedLevel = Mathf.Max(peakWantedLevel, ChaosManager.Instance.WantedLevel);
        switch (dareType)
        {
            case 0: return timeRemainingSeconds >= RoundDuration * 0.5f;
            case 1: return TotalSpentPence <= RoundBudgetPence * 0.75f;
            default: return peakWantedLevel >= 1;
        }
    }

    private void PopulateReceipt(GrandmaShoppingRequests.BasketResult basket, int budgetPenalty, int dareBonus)
    {
        if (receiptText == null) return;
        var lines = new System.Text.StringBuilder();
        var requests = GrandmaShoppingRequests.CurrentList;
        for (int i = 0; i < requests.Count; i++)
        {
            var item = basket.MatchedItems[i];
            lines.Append(i + 1).Append(". ").Append(requests[i].Prompt.Replace("\n", " "));
            lines.Append("\n    ").Append(item != null ? item.DisplayName : "NOT FOUND")
                .Append("  —  ").Append(basket.MatchScores[i]).Append("/100  |  Nan meant ")
                .Append(requests[i].IntendedMeaning).Append(".\n");
        }
        if (basket.ExtraItems > 0)
        {
            lines.Append("\nUnmatched shopping: ");
            for (int i = 0; i < Mathf.Min(4, basket.UnmatchedItems.Count); i++)
            {
                if (i > 0) lines.Append(", ");
                var extra = basket.UnmatchedItems[i];
                lines.Append(extra != null ? extra.DisplayName : "unknown item");
            }
            if (basket.UnmatchedItems.Count > 4) lines.Append(" and ").Append(basket.UnmatchedItems.Count - 4).Append(" more");
            lines.Append(". ");
        }
        lines.Append("\nShopping ").Append(basket.Score).Append("  •  budget -").Append(budgetPenalty)
            .Append("  •  dare +").Append(dareBonus).Append("  =  ").Append(LastScore).Append("/100");
        receiptText.text = lines.ToString();
        if (receiptButton != null) receiptButton.interactable = true;
    }

    private void CreateReceiptCard(Transform parent)
    {
        receiptCard = CreatePanel("Nan's Receipt", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(970f, 850f), new Color32(224, 216, 158, 255));
        var outline = receiptCard.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(5f, -4f);
        var heading = CreateText(receiptCard.transform, "Receipt Heading", "NAN'S SHOPPING RECEIPT",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -16f), new Vector2(850f, 55f), 34f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
        heading.font = reportFont;
        receiptText = CreateText(receiptCard.transform, "Receipt Matches", "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -76f), new Vector2(880f, 670f), 25f,
            Color.black, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        receiptText.font = reportFont;
        receiptText.enableAutoSizing = true;
        receiptText.fontSizeMin = 14f;
        receiptText.fontSizeMax = 25f;
        receiptText.textWrappingMode = TextWrappingModes.Normal;
        var close = new GameObject("Close Receipt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        close.transform.SetParent(receiptCard.transform, false);
        var rect = close.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 20f);
        rect.sizeDelta = new Vector2(270f, 55f);
        close.GetComponent<Image>().color = new Color32(250, 210, 0, 255);
        close.GetComponent<Button>().onClick.AddListener(() => receiptCard.SetActive(false));
        var closeText = CreateText(close.transform, "Close Label", "BACK TO RESULTS",
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 22f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
        closeText.font = reportFont;
        closeText.raycastTarget = false;
        receiptCard.SetActive(false);
    }

    private void UpdateTimeBudgetHud()
    {
        if (timerText == null)
            return;

        int shownSeconds = Mathf.CeilToInt(timeRemainingSeconds);
        timerText.text = (shownSeconds / 60).ToString("00") + ":" + (shownSeconds % 60).ToString("00");
        float remainingRatio = Mathf.Clamp01(timeRemainingSeconds / Mathf.Max(1f, RoundDuration));
        timerFill.fillAmount = remainingRatio;
        if (remainingRatio <= 0.15f)
        {
            timerText.color = new Color32(164, 20, 9, 255);
            timerFill.color = new Color32(185, 23, 12, 255);
        }
        else if (remainingRatio <= 0.35f)
        {
            timerText.color = new Color32(145, 75, 8, 255);
            timerFill.color = new Color32(230, 146, 20, 255);
        }
        else
        {
            timerText.color = Color.black;
            timerFill.color = new Color32(67, 117, 34, 255);
        }

        int estimatedTotal = TotalSpentPence;
        if (tutorialTrolley != null) estimatedTotal += tutorialTrolley.TotalPricePence;
        if (playerItems != null && playerItems.IsHolding && playerItems.HeldItem.Item != null)
        {
            bool alreadyScanned = false;
            if (cashiers != null)
                foreach (var cashier in cashiers)
                    if (cashier != null && cashier.HasScanned(playerItems.HeldItem)) { alreadyScanned = true; break; }
            if (!alreadyScanned) estimatedTotal += playerItems.HeldItem.Item.PricePence;
        }
        int remaining = RoundBudgetPence - estimatedTotal;
        budgetText.color = remaining < 0
            ? new Color32(164, 20, 9, 255)
            : remaining <= 100
                ? new Color32(145, 75, 8, 255)
                : Color.black;
        budgetText.text = remaining >= 0
            ? "LEFT " + FormatMoney(remaining)
            : "OVER BY " + FormatMoney(-remaining);
        spentText.text = "EST. " + FormatMoney(estimatedTotal) + "  /  " + FormatMoney(RoundBudgetPence) + "  (SCANNED " + FormatMoney(TotalSpentPence) + ")";
    }

    private static bool IsAnyCashierScanning()
    {
        CashierCheckoutConveyor[] cashiers = FindObjectsByType<CashierCheckoutConveyor>(FindObjectsSortMode.None);
        for (int i = 0; i < cashiers.Length; i++)
        {
            if (cashiers[i] != null && cashiers[i].IsProcessing)
                return true;
        }

        return false;
    }

    private void UpdateResultDetails()
    {
        if (resultDetails == null)
            return;

        int remaining = BudgetRemainingPence;
        string budgetSummary = remaining >= 0
            ? "BUDGET LEFT " + FormatMoney(remaining)
            : "OVER BUDGET " + FormatMoney(-remaining);
        int shownSeconds = Mathf.CeilToInt(timeRemainingSeconds);
        string clock = (shownSeconds / 60).ToString("00") + ":" + (shownSeconds % 60).ToString("00");
        resultDetails.text = "SPENT " + FormatMoney(TotalSpentPence) + "   •   " + budgetSummary + "   •   TIME " + clock;
    }

    private void UpdatePersonalBest(bool recordCompletedCheckout)
    {
        if (resultPersonalBest == null)
            return;

        string requestKey = PersonalBestScoreKeyPrefix + GrandmaShoppingRequests.CurrentList.Count + "_items";
        bool hasRecord = PlayerPrefs.HasKey(requestKey);
        int bestScore = hasRecord ? PlayerPrefs.GetInt(requestKey) : -1;
        bool isNewBest = recordCompletedCheckout && (!hasRecord || LastScore > bestScore);

        if (isNewBest)
        {
            bestScore = LastScore;
            PlayerPrefs.SetInt(requestKey, bestScore);
            PlayerPrefs.Save();
        }

        if (isNewBest)
        {
            resultPersonalBest.text = "NEW PERSONAL BEST FOR THIS DIFFICULTY  •  " + bestScore + " / 100";
            resultPersonalBest.color = new Color32(144, 20, 7, 255);
        }
        else if (hasRecord)
        {
            resultPersonalBest.text = "BEST FOR THIS DIFFICULTY  •  " + bestScore + " / 100";
            resultPersonalBest.color = new Color32(73, 49, 23, 255);
        }
        else
        {
            resultPersonalBest.text = "NO CHECKOUT RECORD FOR THIS DIFFICULTY YET";
            resultPersonalBest.color = new Color32(73, 49, 23, 255);
        }
    }

    private static string FormatMoney(int pence)
    {
        return (pence / 100m).ToString("C", CultureInfo.GetCultureInfo("en-GB"));
    }

    public static int ScoreItem(ItemDefinition item)
    {
        int best = 0;
        foreach (var request in GrandmaShoppingRequests.CurrentList)
            best = Mathf.Max(best, request.Score(item));
        return best;
    }

    private static string GetLetterGrade(int score)
    {
        if (score >= 100) return "S";
        if (score >= 90) return "A";
        if (score >= 75) return "B";
        if (score >= 50) return "C";
        if (score >= 25) return "D";
        return "F";
    }

    private static string GetRankText(int score)
    {
        if (score >= 100) return "S RANK!!1!";
        if (score >= 90) return "A RANK!";
        if (score >= 75) return "B RANK!";
        if (score >= 50) return "C RANK!";
        if (score >= 25) return "D RANK";
        return "F RANK...";
    }

    private static Color GetRankColor(int score)
    {
        if (score >= 100) return new Color32(190, 16, 22, 255);
        if (score >= 90) return new Color32(39, 110, 37, 255);
        if (score >= 75) return new Color32(31, 78, 137, 255);
        if (score >= 50) return new Color32(147, 91, 15, 255);
        return new Color32(150, 27, 16, 255);
    }

    private IEnumerator CheckoutThenRevealResults()
    {
        const float scanDuration = 2.1f;
        float elapsed = 0f;
        int previousStage = -1;
        RectTransform checkoutRect = checkoutCard.GetComponent<RectTransform>();
        string[] stages =
        {
            "SCANNING ITEM...",
            "CHECKING NAN'S CLUE...",
            "CHECKING THE PRICE...",
            "CALCULATING BADNESS..."
        };

        while (elapsed < scanDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / scanDuration);
            float smoothProgress = progress * progress * (3f - 2f * progress);
            float entrance = Mathf.Clamp01(elapsed / 0.24f);
            checkoutCanvasGroup.alpha = entrance;
            checkoutCard.transform.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, entrance);
            checkoutFill.fillAmount = smoothProgress;
            checkoutPercentText.text = Mathf.RoundToInt(smoothProgress * 100f) + "%";
            checkoutScanLine.anchoredPosition = new Vector2(Mathf.Lerp(-312f, 312f, smoothProgress), -23f);
            checkoutRect.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Sin(Time.unscaledTime * 12f) * 0.45f);

            int stage = progress < 0.28f ? 0 : progress < 0.54f ? 1 : progress < 0.8f ? 2 : 3;
            if (stage != previousStage)
            {
                previousStage = stage;
                checkoutStatusText.text = stages[stage];
                if (uiAudio != null && scanBeep != null)
                    uiAudio.PlayOneShot(scanBeep, 0.22f);
            }
            yield return null;
        }

        checkoutFill.fillAmount = 1f;
        checkoutPercentText.text = "100%";
        checkoutScanLine.anchoredPosition = new Vector2(312f, -23f);
        checkoutCanvasGroup.alpha = 1f;
        checkoutCard.transform.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(0.18f);
        yield return StartCoroutine(RevealFinalResults());
    }

    private IEnumerator RevealFinalResults()
    {
        ShowResults();
        RectTransform cardRect = resultCard.GetComponent<RectTransform>();
        resultCanvasGroup.alpha = 0f;
        resultCanvasGroup.interactable = false;
        resultCanvasGroup.blocksRaycasts = false;
        cardRect.localScale = Vector3.one * 0.78f;
        cardRect.localRotation = Quaternion.Euler(0f, 0f, 3.2f);
        resultScore.text = "0 / 100";
        resultScoreFill.fillAmount = 0f;
        resultScoreFill.color = GetRankColor(LastScore);
        resultRank.text = GetRankText(LastScore);
        resultRank.color = GetRankColor(LastScore);
        resultRank.rectTransform.localScale = Vector3.zero;
        resultGrade.rectTransform.localScale = Vector3.zero;
        resultGrade.color = GetRankColor(LastScore);
        for (int i = 0; i < resultStars.Length; i++)
        {
            resultStars[i].SetFilled(false);
            resultStars[i].transform.localScale = Vector3.one * 0.72f;
        }
        ResetRankBurst();

        const float entranceDuration = 0.42f;
        float elapsed = 0f;
        while (elapsed < entranceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / entranceDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * (1f - t) * 0.12f;
            resultCanvasGroup.alpha = t;
            cardRect.localScale = Vector3.one * Mathf.Lerp(0.78f, 1f, eased) * overshoot;
            cardRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(3.2f, -0.65f, eased));
            yield return null;
        }

        cardRect.localScale = Vector3.one;
        cardRect.localRotation = Quaternion.Euler(0f, 0f, -0.65f);
        const float scoreDuration = 1.18f;
        elapsed = 0f;
        int displayedScore = 0;
        int[] starThresholds = { 25, 50, 75, 90 };
        bool[] starShown = new bool[resultStars.Length];
        while (elapsed < scoreDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / scoreDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            displayedScore = Mathf.RoundToInt(LastScore * eased);
            resultScore.text = displayedScore + " / 100";
            resultScoreFill.fillAmount = displayedScore / 100f;
            for (int i = 0; i < resultStars.Length; i++)
            {
                if (!starShown[i] && displayedScore >= starThresholds[i])
                {
                    starShown[i] = true;
                    resultStars[i].SetFilled(true);
                    StartCoroutine(PopResultStar(i));
                }
            }
            yield return null;
        }
        resultScore.text = LastScore + " / 100";
        resultScoreFill.fillAmount = LastScore / 100f;
        for (int i = 0; i < resultStars.Length; i++)
        {
            if (!starShown[i] && LastScore >= starThresholds[i])
            {
                starShown[i] = true;
                resultStars[i].SetFilled(true);
                StartCoroutine(PopResultStar(i));
            }
        }

        if (uiAudio != null && rankChime != null)
            uiAudio.PlayOneShot(rankChime, LastScore >= 50 ? 0.42f : 0.2f);
        yield return AnimateRankStamp();

        int burstCount = LastScore >= 100 ? 16 : LastScore >= 90 ? 12 : LastScore >= 75 ? 8 : LastScore >= 50 ? 4 : 0;
        if (burstCount > 0)
            StartCoroutine(AnimateRankBurst(burstCount));

        float hold = 0f;
        while (hold < 0.38f)
        {
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        resultCanvasGroup.interactable = true;
        resultCanvasGroup.blocksRaycasts = true;
        finalSequenceRunning = false;
        endSequenceRoutine = null;
    }

    private IEnumerator AnimateRankStamp()
    {
        float elapsed = 0f;
        const float duration = 0.48f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float backOut = 1f + 2.70158f * Mathf.Pow(t - 1f, 3f) + 1.70158f * Mathf.Pow(t - 1f, 2f);
            resultRank.rectTransform.localScale = Vector3.one * Mathf.Max(0f, backOut);
            resultGrade.rectTransform.localScale = Vector3.one * Mathf.Max(0f, backOut * 0.86f);
            yield return null;
        }
        resultRank.rectTransform.localScale = Vector3.one;
        resultGrade.rectTransform.localScale = Vector3.one;
    }

    private IEnumerator PopResultStar(int index)
    {
        ChaosStarGraphic star = resultStars[index];
        float elapsed = 0f;
        const float duration = 0.24f;
        star.SetFlashing(true);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.55f;
            star.transform.localScale = Vector3.one * pulse;
            yield return null;
        }
        star.SetFlashing(false);
        star.transform.localScale = Vector3.one;
    }

    private IEnumerator AnimateRankBurst(int count)
    {
        Vector2 origin = resultRank.rectTransform.anchoredPosition;
        float[] angles = { 0f, 29f, 56f, 82f, 108f, 137f, 164f, 191f, 218f, 243f, 269f, 294f, 318f, 340f, 42f, 226f };
        const float duration = 0.68f;
        float elapsed = 0f;
        for (int i = 0; i < rankBurstBits.Length; i++)
        {
            bool active = i < count;
            rankBurstBits[i].gameObject.SetActive(active);
            rankBurstFades[i].alpha = active ? 1f : 0f;
            rankBurstBits[i].anchoredPosition = origin;
            rankBurstBits[i].localScale = active ? Vector3.one : Vector3.zero;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < count; i++)
            {
                float angle = angles[i] * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                rankBurstBits[i].anchoredPosition = origin + direction * Mathf.Lerp(8f, 112f, t);
                rankBurstBits[i].localRotation = Quaternion.Euler(0f, 0f, angles[i] + t * 250f);
                rankBurstBits[i].localScale = Vector3.one * (0.9f * (1f - t));
                rankBurstFades[i].alpha = 1f - t;
            }
            yield return null;
        }
        ResetRankBurst();
    }

    private void ResetRankBurst()
    {
        if (rankBurstBits == null)
            return;
        for (int i = 0; i < rankBurstBits.Length; i++)
        {
            if (rankBurstBits[i] != null)
            {
                rankBurstBits[i].gameObject.SetActive(false);
                rankBurstBits[i].localScale = Vector3.zero;
                rankBurstFades[i].alpha = 0f;
            }
        }
    }

    private void StartAnotherAttempt()
    {
        // Reload the store so bought items, shoppers, security and the list are reset together.
        int count = GrandmaShoppingRequests.CurrentList.Count;
        GrandmaShoppingRequests.BeginRound(count);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void BuildUI()
    {
        var canvasObject = new GameObject(
            "Grandma Shopping HUD",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 420;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        reportFont = Resources.Load<TMP_FontAsset>("Fonts/Childs Journal SDF Fixed");
        if (reportFont == null)
            reportFont = TMP_Settings.defaultFontAsset;

        listCard = CreatePanel(
            "Shopping Guide", canvasObject.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -24f), new Vector2(460f, 126f),
            new Color32(224, 216, 158, 255));
        listCard.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -0.6f);
        Outline guideOutline = listCard.AddComponent<Outline>();
        guideOutline.effectColor = Color.black;
        guideOutline.effectDistance = new Vector2(4f, -3f);

        TMP_Text guideTitle = CreateText(listCard.transform, "Guide Title", "SHOPPING GUIDE",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(18f, -10f), new Vector2(424f, 30f), 24f,
            new Color32(144, 20, 7, 255), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        guideTitle.font = reportFont;

        listHint = CreateText(listCard.transform, "Checkout Hint",
            "Find an item, then take it to a cashier.",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(18f, 10f), new Vector2(424f, 72f), 20f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        listHint.font = reportFont;
        listCard.SetActive(false);

        trolleyCompassCard = CreatePanel(
            "Trolley Direction Guide", canvasObject.transform,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 112f), new Vector2(480f, 84f),
            new Color32(224, 216, 158, 255));
        Outline compassOutline = trolleyCompassCard.AddComponent<Outline>();
        compassOutline.effectColor = Color.black;
        compassOutline.effectDistance = new Vector2(4f, -3f);

        TMP_Text arrowText = CreateText(trolleyCompassCard.transform, "Trolley Arrow", "▲",
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(54f, 0f), new Vector2(62f, 62f), 44f,
            new Color32(144, 20, 7, 255), FontStyles.Bold, TextAlignmentOptions.Center);
        trolleyCompassArrow = arrowText.rectTransform;

        TMP_Text trolleyTitle = CreateText(trolleyCompassCard.transform, "Trolley Direction Title", "FOLLOW THE ARROW",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(104f, -13f), new Vector2(345f, 27f), 16f,
            new Color32(144, 20, 7, 255), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        trolleyTitle.font = reportFont;

        trolleyCompassDistance = CreateText(trolleyCompassCard.transform, "Trolley Distance", "TROLLEY  •  -- m",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(104f, 12f), new Vector2(345f, 32f), 22f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        trolleyCompassDistance.font = reportFont;
        trolleyCompassCard.SetActive(false);

        timeBudgetShadow = CreatePanel(
            "Crooked Time and Budget Shadow", canvasObject.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-15f, -17f), new Vector2(382f, 184f), new Color32(28, 22, 12, 255));
        timeBudgetCard = CreatePanel(
            "Nan's Time and Budget", canvasObject.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-22f, -22f), new Vector2(378f, 180f), new Color32(224, 216, 158, 255));
        timeBudgetCard.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 0.8f);
        Outline timeBudgetOutline = timeBudgetCard.AddComponent<Outline>();
        timeBudgetOutline.effectColor = Color.black;
        timeBudgetOutline.effectDistance = new Vector2(4f, -3f);
        CreateHudText(timeBudgetCard.transform, "Time and Money Heading", "TIME LEFT",
            new Vector2(15f, -10f), new Vector2(170f, 28f), 21f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        timerText = CreateHudText(timeBudgetCard.transform, "Round Timer", "05:00",
            new Vector2(204f, -8f), new Vector2(158f, 34f), 30f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.MidlineRight);

        CreatePanel("Timer Bar Background", timeBudgetCard.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(15f, -48f), new Vector2(347f, 10f), new Color32(67, 54, 31, 255));
        GameObject timerFillObject = CreatePanel("Timer Remaining Fill", timeBudgetCard.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(15f, -48f), new Vector2(347f, 10f), new Color32(67, 117, 34, 255));
        timerFill = timerFillObject.GetComponent<Image>();
        timerFill.type = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        timerFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        timerFill.fillAmount = 1f;

        CreatePanel("Hand Drawn Budget Divider", timeBudgetCard.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(15f, -69f), new Vector2(346f, 2f), Color.black);
        CreateHudText(timeBudgetCard.transform, "Budget Heading", "NAN'S BUDGET",
            new Vector2(15f, -79f), new Vector2(180f, 25f), 17f,
            new Color32(144, 20, 7, 255), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        budgetText = CreateHudText(timeBudgetCard.transform, "Budget Remaining", "LEFT £5.00",
            new Vector2(15f, -103f), new Vector2(345f, 32f), 27f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        spentText = CreateHudText(timeBudgetCard.transform, "Budget Spent", "SPENT £0.00  /  £5.00",
            new Vector2(15f, -140f), new Vector2(345f, 25f), 18f,
            new Color32(61, 47, 27, 255), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        timeBudgetCard.SetActive(false);
        timeBudgetShadow.SetActive(false);

        dareCard = CreatePanel("Nan's Bonus Dare", canvasObject.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-22f, -216f), new Vector2(378f, 96f), new Color32(224, 216, 158, 255));
        var dareOutline = dareCard.AddComponent<Outline>();
        dareOutline.effectColor = Color.black;
        dareOutline.effectDistance = new Vector2(4f, -3f);
        CreateHudText(dareCard.transform, "Dare Heading", "NAN'S BONUS DARE  •  UP TO +5",
            new Vector2(12f, -8f), new Vector2(355f, 25f), 19f,
            new Color32(144, 20, 7, 255), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        dareText = CreateHudText(dareCard.transform, "Dare Progress", "",
            new Vector2(12f, -35f), new Vector2(355f, 53f), 20f,
            Color.black, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        dareText.textWrappingMode = TextWrappingModes.Normal;
        dareCard.SetActive(false);

        resultBackdrop = CreatePanel(
            "Nan's Report Dimmer", canvasObject.transform,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.64f));
        Image dimmer = resultBackdrop.GetComponent<Image>();
        dimmer.raycastTarget = true;
        resultBackdrop.SetActive(false);

        resultShadow = CreatePanel(
            "Crooked Report Shadow", canvasObject.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-7f, -6f), new Vector2(958f, 678f), new Color32(30, 22, 12, 255));
        resultShadow.SetActive(false);

        resultCard = CreatePanel(
            "Nan's Verdict", canvasObject.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(952f, 672f), new Color32(224, 216, 158, 255));
        resultCard.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -0.65f);
        Outline reportOutline = resultCard.AddComponent<Outline>();
        reportOutline.effectColor = Color.black;
        reportOutline.effectDistance = new Vector2(5f, -4f);
        resultCanvasGroup = resultCard.AddComponent<CanvasGroup>();
        CreateRankBurstBits();

        CreatePanel("Yellow Report Header", resultCard.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -19f), new Vector2(876f, 70f), new Color32(250, 210, 0, 255));
        CreateReportText("Verdict Title", "BADNESS  /  NAN'S REPORT CARD",
            new Vector2(0f, -22f), new Vector2(830f, 58f), 36f, Color.black,
            FontStyles.Bold, TextAlignmentOptions.Center, -1.3f);
        CreateReportText("Report Subtitle", "NAN'S VERDICT IS IN...",
            new Vector2(0f, -98f), new Vector2(700f, 34f), 20f,
            new Color32(144, 20, 7, 255), FontStyles.Bold, TextAlignmentOptions.Center, 2f);

        resultStars = new ChaosStarGraphic[4];
        float[] starTilts = { -5f, 4f, -2f, 7f };
        for (int i = 0; i < resultStars.Length; i++)
        {
            var starObject = new GameObject("Report Star " + (i + 1),
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ChaosStarGraphic));
            starObject.transform.SetParent(resultCard.transform, false);
            RectTransform starRect = starObject.GetComponent<RectTransform>();
            starRect.anchorMin = starRect.anchorMax = new Vector2(0.5f, 1f);
            starRect.pivot = new Vector2(0.5f, 0.5f);
            starRect.anchoredPosition = new Vector2((i - 1.5f) * 53f, -143f);
            starRect.sizeDelta = new Vector2(42f, 42f + ((i % 2 == 0) ? 2f : -1f));
            starRect.localRotation = Quaternion.Euler(0f, 0f, starTilts[i]);
            resultStars[i] = starObject.GetComponent<ChaosStarGraphic>();
            resultStars[i].SetSeed(i * 1.71f + 0.43f);
        }

        resultItem = CreateReportText("Checked Item", string.Empty,
            new Vector2(0f, -187f), new Vector2(700f, 42f), 28f,
            new Color32(28, 24, 13, 255), FontStyles.Bold, TextAlignmentOptions.Center);
        resultScore = CreateReportText("Score", string.Empty,
            new Vector2(-62f, -229f), new Vector2(475f, 70f), 52f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.Center, -1f);
        CreatePanel("Score Progress Track", resultCard.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-30f, -304f), new Vector2(610f, 16f), new Color32(84, 71, 41, 255));
        GameObject scoreFillObject = CreatePanel("Score Progress Fill", resultCard.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-30f, -304f), new Vector2(610f, 16f), new Color32(250, 210, 0, 255));
        resultScoreFill = scoreFillObject.GetComponent<Image>();
        resultScoreFill.type = Image.Type.Filled;
        resultScoreFill.fillMethod = Image.FillMethod.Horizontal;
        resultScoreFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        resultScoreFill.fillAmount = 0f;
        resultRank = CreateReportText("Animated Rank Reveal", "S RANK!!1!",
            new Vector2(0f, -327f), new Vector2(760f, 40f), 32f,
            new Color32(190, 16, 22, 255), FontStyles.Bold, TextAlignmentOptions.Center, -1.6f);
        Outline rankOutline = resultRank.gameObject.AddComponent<Outline>();
        rankOutline.effectColor = Color.black;
        rankOutline.effectDistance = new Vector2(2f, -2f);
        Shadow rankShadow = resultRank.gameObject.AddComponent<Shadow>();
        rankShadow.effectColor = new Color(1f, 0.85f, 0.15f, 0.8f);
        rankShadow.effectDistance = new Vector2(-3f, 3f);
        CreatePanel("Grade Stamp", resultCard.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(-125f, -241f), new Vector2(112f, 112f), new Color32(250, 210, 0, 255));
        resultGrade = CreateReportText("Grade Letter", "?",
            new Vector2(350f, -241f), new Vector2(112f, 112f), 76f,
            new Color32(144, 20, 7, 255), FontStyles.Bold, TextAlignmentOptions.Center, 7f);
        resultFeedback = CreateReportText("Grandma's Feedback", string.Empty,
            new Vector2(0f, -381f), new Vector2(800f, 72f), 24f,
            Color.black, FontStyles.Normal, TextAlignmentOptions.Center, 0.7f);
        resultRubric = CreateReportText("Grading Rubric", string.Empty,
            new Vector2(0f, -469f), new Vector2(860f, 30f), 19f,
            new Color32(73, 49, 23, 255), FontStyles.Bold, TextAlignmentOptions.Center, -1f);
        resultDetails = CreateReportText("Time and Budget Result", string.Empty,
            new Vector2(0f, -508f), new Vector2(880f, 27f), 17f,
            new Color32(73, 49, 23, 255), FontStyles.Bold, TextAlignmentOptions.Center, 0.5f);
        resultPersonalBest = CreateReportText("Request Personal Best", string.Empty,
            new Vector2(0f, -540f), new Vector2(880f, 27f), 17f,
            new Color32(73, 49, 23, 255), FontStyles.Bold, TextAlignmentOptions.Center);

        resultRetryButton = CreateReportButton("Shop Again Button", "SHOP AGAIN",
            new Vector2(-286f, 27f), new Color32(250, 210, 0, 255));
        resultRetryButton.onClick.AddListener(StartAnotherAttempt);
        resultMenuButton = CreateReportButton("Report Main Menu Button", "MAIN MENU",
            new Vector2(286f, 27f), new Color32(247, 237, 190, 255));
        resultMenuButton.onClick.AddListener(GoToMainMenu);
        receiptButton = CreateReportButton("Receipt Button", "VIEW RECEIPT", new Vector2(0f, 27f), new Color32(247, 237, 190, 255));
        receiptButton.onClick.AddListener(() => receiptCard.SetActive(true));
        receiptButton.interactable = false;
        CreateReceiptCard(canvasObject.transform);
        CreateReportText("Keyboard Retry Hint", "OR PRESS ENTER TO SHOP AGAIN",
            new Vector2(0f, -650f), new Vector2(500f, 18f), 13f,
            new Color32(82, 63, 29, 255), FontStyles.Normal, TextAlignmentOptions.Center);

        resultFeedback.fontSize = 22f;

        resultCard.SetActive(false);
        CreateCheckoutCard(canvasObject.transform);
        CreateUiAudio();
    }

    private TMP_Text CreateHudText(Transform parent, string name, string value,
        Vector2 position, Vector2 size, float fontSize, Color color,
        FontStyles style, TextAlignmentOptions alignment)
    {
        TMP_Text text = CreateText(parent, name, value,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            position, size, fontSize, color, style, alignment);
        text.font = reportFont;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.margin = Vector4.zero;
        return text;
    }

    private TMP_Text CreateReportText(string name, string value, Vector2 topPosition,
        Vector2 size, float fontSize, Color color, FontStyles style,
        TextAlignmentOptions alignment, float rotation = 0f)
    {
        TMP_Text text = CreateText(resultCard.transform, name, value,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            topPosition, size, fontSize, color, style, alignment);
        text.font = reportFont;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.margin = new Vector4(8f, 2f, 8f, 2f);
        text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        return text;
    }

    private Button CreateReportButton(string name, string label, Vector2 position, Color color)
    {
        var buttonObject = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(resultCard.transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(270f, 62f);
        rect.localRotation = Quaternion.Euler(0f, 0f, position.x < 0f ? -1.8f : 1.4f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.25f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        button.colors = colors;
        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -2f);

        TMP_Text labelText = CreateReportText(name + " Label", label,
            Vector2.zero, Vector2.zero, 22f, Color.black, FontStyles.Bold,
            TextAlignmentOptions.Center);
        labelText.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = Vector2.zero;
        labelText.raycastTarget = false;
        return button;
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("MainMenu");
    }

    private void UpdateFirstRunTutorial()
    {
        if (playerItems == null)
            return;

        switch (tutorialStep)
        {
            case TutorialStep.FindNanItem:
                SetTrolleyGuidanceVisible(false);
                listHint.text = "STEP 1 OF 3  •  FIND NAN'S ITEM\nLook along the shelves, aim at one product and press R to pick it up.";
                if (playerItems.IsHolding)
                    tutorialStep = TutorialStep.FindTrolley;
                break;

            case TutorialStep.FindTrolley:
                if (tutorialTrolley == null)
                {
                    listHint.text = "STEP 2 OF 3  •  GET A TROLLEY\nFind a shop trolley and press E beside its handle.";
                    SetTrolleyGuidanceVisible(false);
                    break;
                }

                listHint.text = "STEP 2 OF 3  •  GET A TROLLEY\nFollow the gold arrow. Press E beside the trolley handle to grab it.";
                SetTrolleyGuidanceVisible(true);
                if (trolleyController != null && trolleyController.IsPlayerHolding)
                {
                    tutorialStep = TutorialStep.LoadTrolley;
                    SetTrolleyGuidanceVisible(false);
                }
                break;

            case TutorialStep.LoadTrolley:
                SetTrolleyGuidanceVisible(false);
                if (tutorialTrolley == null)
                {
                    listHint.text = "Keep your item with you and head to the checkout.";
                    break;
                }

                if (tutorialTrolley.ItemCount > 0)
                {
                    tutorialStep = TutorialStep.Checkout;
                    break;
                }

                if (!playerItems.IsHolding)
                {
                    tutorialStep = TutorialStep.FindNanItem;
                    break;
                }

                listHint.text = "STEP 3 OF 3  •  LOAD THE TROLLEY\nStay by the basket and press F to put your item in.";
                break;

            case TutorialStep.Checkout:
                SetTrolleyGuidanceVisible(false);
                if (HasScannedCheckoutItems)
                {
                    listHint.text = IsAnyCashierScanning()
                        ? "LAST STEP  •  WAIT FOR THE CASHIER\nThe belt is scanning Nan's items. Wait for it to clear."
                        : ScannedCheckoutItemCount + " / " + GrandmaShoppingRequests.CurrentList.Count + " SCANNED\nScan more, or look at the cashier and press Q or click to finish.";
                    break;
                }

                if (playerItems.IsHolding)
                {
                    listHint.text = "LAST STEP  •  TAKE IT TO THE CHECKOUT\nLook toward a cashier's conveyor and press Q or click to hand over your item.";
                }
                else if (tutorialTrolley != null && tutorialTrolley.ItemCount > 0)
                {
                    listHint.text = "YOUR ITEM IS IN THE TROLLEY\nAim at it and press R to pick it back up, then take it to a cashier.";
                }
                else
                {
                    listHint.text = "Pick up your chosen item with R, then take it to a cashier.";
                }
                break;
        }
    }

    private void SetTrolleyGuidanceVisible(bool visible)
    {
        if (trolleyGuidanceVisible == visible)
            return;

        trolleyGuidanceVisible = visible;
        if (trolleyCompassCard != null)
            trolleyCompassCard.SetActive(visible);

        if (visible && tutorialTrolley != null)
            CreateTrolleyWorldMarker();
        else if (trolleyWorldMarker != null)
            trolleyWorldMarker.SetActive(false);
    }

    private void UpdateTrolleyCompass()
    {
        if (!trolleyGuidanceVisible || tutorialTrolley == null || playerItems == null)
            return;

        Transform cameraTransform = playerCamera != null
            ? playerCamera
            : Camera.main != null ? Camera.main.transform : null;
        if (cameraTransform == null)
            return;

        Vector3 toTrolley = tutorialTrolley.transform.position - playerItems.transform.position;
        toTrolley.y = 0f;
        float distance = toTrolley.magnitude;
        trolleyCompassDistance.text = "TROLLEY  •  " + distance.ToString("0") + " m";

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        if (cameraForward.sqrMagnitude > 0.001f && toTrolley.sqrMagnitude > 0.001f)
        {
            cameraForward.Normalize();
            cameraRight.Normalize();
            float angle = Mathf.Atan2(Vector3.Dot(toTrolley.normalized, cameraRight),
                Vector3.Dot(toTrolley.normalized, cameraForward)) * Mathf.Rad2Deg;
            trolleyCompassArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        if (trolleyWorldMarker != null)
        {
            float bob = Mathf.Sin(Time.unscaledTime * 3.2f) * 0.12f;
            Vector3 markerPosition = tutorialTrolley.transform.TransformPoint(
                new Vector3(0f, 3.15f + bob, 0f));
            trolleyWorldMarker.transform.position = markerPosition;
            trolleyWorldMarker.transform.rotation = Quaternion.LookRotation(
                markerPosition - cameraTransform.position, Vector3.up);

            LineRenderer beacon = trolleyWorldMarker.GetComponent<LineRenderer>();
            if (beacon != null)
            {
                beacon.SetPosition(0, tutorialTrolley.transform.position + Vector3.up * 1.1f);
                beacon.SetPosition(1, markerPosition + Vector3.down * 0.45f);
            }
        }
    }

    private void CreateTrolleyWorldMarker()
    {
        if (trolleyWorldMarker != null)
        {
            trolleyWorldMarker.SetActive(true);
            return;
        }

        trolleyWorldMarker = new GameObject("First Run Trolley Arrow");
        trolleyWorldMarker.transform.SetParent(tutorialTrolley.transform, false);
        trolleyWorldMarker.AddComponent<MeshFilter>().sharedMesh = CreateDownArrowMesh();
        trolleyMarkerMesh = trolleyWorldMarker.GetComponent<MeshFilter>().sharedMesh;

        MeshRenderer meshRenderer = trolleyWorldMarker.AddComponent<MeshRenderer>();
        trolleyMarkerMaterial = CreateMarkerMaterial();
        meshRenderer.sharedMaterial = trolleyMarkerMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        var beacon = trolleyWorldMarker.AddComponent<LineRenderer>();
        beacon.useWorldSpace = true;
        beacon.positionCount = 2;
        beacon.startWidth = 0.045f;
        beacon.endWidth = 0.025f;
        beacon.numCapVertices = 4;
        beacon.sharedMaterial = trolleyMarkerMaterial;
        beacon.startColor = new Color(1f, 0.73f, 0.18f, 0.9f);
        beacon.endColor = new Color(1f, 0.73f, 0.18f, 0.35f);
    }

    private static Mesh CreateDownArrowMesh()
    {
        var mesh = new Mesh { name = "Tutorial Trolley Arrow Mesh" };
        mesh.vertices = new[]
        {
            new Vector3(-0.13f, 0.55f, 0f),
            new Vector3(0.13f, 0.55f, 0f),
            new Vector3(0.13f, 0.05f, 0f),
            new Vector3(-0.13f, 0.05f, 0f),
            new Vector3(-0.38f, 0.05f, 0f),
            new Vector3(0.38f, 0.05f, 0f),
            new Vector3(0f, -0.48f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 5, 4, 6 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateMarkerMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");

        var material = new Material(shader) { name = "Tutorial Trolley Gold" };
        Color gold = new Color(1f, 0.72f, 0.16f, 1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", gold);
        if (material.HasProperty("_Color")) material.SetColor("_Color", gold);
        return material;
    }

    private void OnDestroy()
    {
        if (resultRetryButton != null)
            resultRetryButton.onClick.RemoveListener(StartAnotherAttempt);
        if (resultMenuButton != null)
            resultMenuButton.onClick.RemoveListener(GoToMainMenu);
        // The player belongs to the scene being unloaded; re-enabling movement here
        // would recreate its crosshair while Unity is destroying scene objects.
        if (roundComplete)
            Time.timeScale = 1f;
        if (scanBeep != null)
            Destroy(scanBeep);
        if (rankChime != null)
            Destroy(rankChime);
        if (Instance == this)
            Instance = null;
        if (trolleyMarkerMesh != null)
            Destroy(trolleyMarkerMesh);
        if (trolleyMarkerMaterial != null)
            Destroy(trolleyMarkerMaterial);
    }

    private static GameObject CreatePanel(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size,
        Color color)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(14f, fontSize - 5f);
        text.fontSizeMax = fontSize;
        text.enableAutoSizing = true;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }
}
