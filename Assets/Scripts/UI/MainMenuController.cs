using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private Animator leftDancer;
    [SerializeField] private Animator rightDancer;
    [SerializeField] private string gameSceneName = "MainScene";

    private GameObject briefingCanvas;
    private GameObject difficultyCanvas;
    private GameObject briefingCard;
    private CanvasGroup cardCanvasGroup;
    private TMP_Text eyebrowText;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text controlsText;
    private TMP_Text buttonText;
    private int briefingPage;
    private Coroutine cardAnimation;

    private static readonly string[] PageTitles =
    {
        "Memorise Nan's list",
        "Find what Nan wants",
        "Get a shopping trolley"
    };

    private static readonly string[] PageBodies =
    {
        "Nan gives you a list of vague requests. Read the letter carefully: once you put it away, you'll have to rely on your memory.",
        "Find a product for each request. Search the shelves, look straight at a product and press R to pick it up. Words can have more than one meaning!",
        "Follow the gold arrow to the shop trolley. Press E beside its handle to take hold. Keep your item in hand, stand by the basket and press F to load it."
    };

    private static readonly string[] PageControls =
    {
        "READ IT TWICE. THE LIST WON'T COME BACK!",
        "WASD  MOVE      MOUSE  LOOK      R  PICK UP",
        "E  GRAB TROLLEY      F  LOAD BASKET\nAT THE TILL: R TO RETRIEVE, THEN Q AT THE CONVEYOR"
    };

    private void Start()
    {
        StyleMainMenu();
        CreateTutorialButton();

        if (leftDancer != null)
        {
            leftDancer.SetBool("Dance 1", true);
            leftDancer.Play("Dance 1", 0, 0f);
        }

        if (rightDancer != null)
        {
            rightDancer.SetBool("Dance 2", true);
            rightDancer.Play("Dance 2", 0, 0f);
        }
    }

    private static void StyleMainMenu()
    {
        GameObject canvas = GameObject.Find("Main Menu Canvas");
        Transform panel = canvas != null ? canvas.transform.Find("Buttons Panel") : null;
        if (panel == null) return;

        Image paper = panel.GetComponent<Image>();
        if (paper != null)
        {
            paper.color = new Color32(224, 216, 158, 255);
            Outline outline = paper.GetComponent<Outline>();
            if (outline == null) outline = paper.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(5f, -4f);
        }

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            Image image = button.GetComponent<Image>();
            if (image == null) continue;
            image.color = new Color32(250, 210, 0, 255);
            Outline outline = image.GetComponent<Outline>();
            if (outline == null) outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -2f);
        }

        Font handwritten = Resources.Load<Font>("Fonts/childs-journal");
        foreach (Text text in panel.GetComponentsInChildren<Text>(true))
        {
            text.color = Color.black;
            if (handwritten != null) text.font = handwritten;
        }
    }

    public void PlayGame()
    {
        ShowDifficultySelection();
    }

    private void CreateTutorialButton()
    {
        GameObject canvas = GameObject.Find("Main Menu Canvas");
        Transform panel = canvas != null ? canvas.transform.Find("Buttons Panel") : null;
        if (panel == null || panel.Find("Tutorial Button") != null) return;
        Transform play = panel.Find("Play Button");
        Transform quit = panel.Find("Quit Button");
        if (play == null || quit == null) return;
        RectTransform playRect = play.GetComponent<RectTransform>();
        RectTransform quitRect = quit.GetComponent<RectTransform>();
        Vector2 middle = (playRect.anchoredPosition + quitRect.anchoredPosition) * 0.5f;
        playRect.anchoredPosition += Vector2.up * 65f;
        quitRect.anchoredPosition += Vector2.down * 65f;
        GameObject tutorial = new GameObject("Tutorial Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        tutorial.transform.SetParent(panel, false);
        RectTransform tutorialRect = tutorial.GetComponent<RectTransform>();
        tutorialRect.anchorMin = quitRect.anchorMin;
        tutorialRect.anchorMax = quitRect.anchorMax;
        tutorialRect.pivot = quitRect.pivot;
        tutorialRect.sizeDelta = quitRect.sizeDelta;
        tutorialRect.anchoredPosition = middle;
        Image image = tutorial.GetComponent<Image>();
        image.color = new Color32(250, 210, 0, 255);
        Outline outline = tutorial.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -2f);
        Button button = tutorial.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(ShowFirstPlayBriefing);
        CreateText(tutorial.transform, "Tutorial Label", "TUTORIAL", Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 38f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    private void ShowDifficultySelection()
    {
        if (difficultyCanvas != null) return;
        EnsureEventSystem();
        difficultyCanvas = new GameObject("Difficulty Selection", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = difficultyCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        CanvasScaler scaler = difficultyCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        GameObject dim = CreatePanel("Dim", difficultyCanvas.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.8f));
        dim.GetComponent<Image>().raycastTarget = true;
        GameObject card = CreatePanel("Difficulty Card", difficultyCanvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 540f), new Color32(224, 216, 158, 255));
        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(5f, -4f);
        CreateText(card.transform, "Difficulty Title", "HOW BIG IS NAN'S LIST?", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(620f, 55f), 34f, Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
        CreateDifficultyButton(card.transform, "EASY  /  3 ITEMS", 125f, () => StartDifficulty(3));
        CreateDifficultyButton(card.transform, "MEDIUM  /  6 ITEMS", 35f, () => StartDifficulty(6));
        CreateDifficultyButton(card.transform, "HARD  /  9 ITEMS", -55f, () => StartDifficulty(9));
        CreateDifficultyButton(card.transform, "BACK", -170f, CloseDifficultySelection);
    }

    private static void CreateDifficultyButton(Transform parent, string label, float y, UnityEngine.Events.UnityAction action)
    {
        GameObject obj = CreatePanel(label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(400f, 66f), new Color32(250, 210, 0, 255));
        Image image = obj.GetComponent<Image>();
        image.raycastTarget = true;
        Outline outline = obj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3f, -2f);
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        CreateText(obj.transform, "Label", label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 25f, Color.black, FontStyles.Bold, TextAlignmentOptions.Center);
    }

    private void CloseDifficultySelection()
    {
        if (difficultyCanvas != null) Destroy(difficultyCanvas);
        difficultyCanvas = null;
    }

    private void StartDifficulty(int count)
    {
        GrandmaShoppingRequests.BeginRound(count);
        LoadGame();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    private void ShowFirstPlayBriefing()
    {
        if (briefingCanvas != null)
            return;

        GrandmaShoppingRequests.RequestGameplayTutorial();

        EnsureEventSystem();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        briefingCanvas = new GameObject(
            "First Shop Briefing",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = briefingCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = briefingCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject dim = CreatePanel("Dim", briefingCanvas.transform,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, new Color(0.025f, 0.035f, 0.04f, 0.91f));
        dim.GetComponent<Image>().raycastTarget = true;

        briefingCard = CreatePanel("Briefing Card", briefingCanvas.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(790f, 570f), new Color32(224, 216, 158, 255));
        Outline outline = briefingCard.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(5f, -4f);
        briefingCard.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.55f);
        briefingCard.GetComponent<Shadow>().effectDistance = new Vector2(0f, -14f);
        cardCanvasGroup = briefingCard.AddComponent<CanvasGroup>();

        CreatePanel("Gold Accent", briefingCard.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -4f), new Vector2(0f, 8f), new Color32(250, 210, 0, 255));

        eyebrowText = CreateText(briefingCard.transform, "Page Indicator", string.Empty,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(48f, -40f), new Vector2(690f, 30f), 17f,
            new Color32(144, 20, 7, 255), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);

        titleText = CreateText(briefingCard.transform, "Briefing Title", string.Empty,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(48f, -88f), new Vector2(690f, 66f), 40f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);

        bodyText = CreateText(briefingCard.transform, "Briefing Instructions", string.Empty,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(48f, -177f), new Vector2(690f, 210f), 25f,
            Color.black, FontStyles.Normal, TextAlignmentOptions.TopLeft);

        GameObject controlsPanel = CreatePanel("Controls Strip", briefingCard.transform,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
            new Vector2(0f, 92f), new Vector2(0f, 86f), new Color32(250, 210, 0, 255));
        controlsText = CreateText(controlsPanel.transform, "Controls", string.Empty,
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 0f), new Vector2(-48f, 64f), 16f,
            Color.black, FontStyles.Bold, TextAlignmentOptions.Center);

        GameObject buttonObject = CreatePanel("Continue Button", briefingCard.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-48f, 30f), new Vector2(285f, 62f), new Color32(250, 210, 0, 255));
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.targetGraphic.raycastTarget = true;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.82f, 0.43f, 1f);
        colors.pressedColor = new Color(0.82f, 0.52f, 0.15f, 1f);
        button.colors = colors;
        button.onClick.AddListener(AdvanceBriefing);
        buttonText = CreateText(buttonObject.transform, "Button Label", string.Empty,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, 18f, new Color(0.09f, 0.09f, 0.08f, 1f),
            FontStyles.Bold, TextAlignmentOptions.Center);

        SetBriefingPage(0);
    }

    private void SetBriefingPage(int page)
    {
        briefingPage = page;
        eyebrowText.text = "YOUR FIRST SHOP     /     " + (page + 1).ToString("00") + " — 03";
        titleText.text = PageTitles[page];
        bodyText.text = PageBodies[page];
        controlsText.text = PageControls[page];
        buttonText.text = page == PageTitles.Length - 1 ? "BACK TO MENU" : "CONTINUE     →";

        if (cardAnimation != null)
            StopCoroutine(cardAnimation);
        cardAnimation = StartCoroutine(AnimateCardIn());
    }

    private IEnumerator AnimateCardIn()
    {
        RectTransform rect = briefingCard.GetComponent<RectTransform>();
        Vector3 startScale = Vector3.one * 0.965f;
        rect.localScale = startScale;
        cardCanvasGroup.alpha = 0f;

        const float duration = 0.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            rect.localScale = Vector3.LerpUnclamped(startScale, Vector3.one, eased);
            cardCanvasGroup.alpha = eased;
            yield return null;
        }

        rect.localScale = Vector3.one;
        cardCanvasGroup.alpha = 1f;
    }

    private void AdvanceBriefing()
    {
        if (briefingPage < PageTitles.Length - 1)
        {
            SetBriefingPage(briefingPage + 1);
            return;
        }

        if (cardAnimation != null)
        {
            StopCoroutine(cardAnimation);
            cardAnimation = null;
        }
        Destroy(briefingCanvas);
        briefingCanvas = null;
        briefingCard = null;
    }

    private void LoadGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        if (Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
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
        text.font = Resources.Load<TMP_FontAsset>("Fonts/Childs Journal SDF Fixed");
        if (text.font == null)
            text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontSizeMin = Mathf.Max(14f, fontSize - 4f);
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
