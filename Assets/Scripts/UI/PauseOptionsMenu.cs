using How2Shop.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PauseOptionsMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerItemInteraction itemInteraction;
    [SerializeField] private string gameSceneName = "MainScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private Toggle viewBobbingToggle;
    private Button restartButton;
    private Button mainMenuButton;

    public bool IsOpen => menuPanel != null && menuPanel.activeSelf;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement>();

        if (menuPanel != null)
        {
            StylePausePanel();
            menuPanel.SetActive(false);
        }

        if (resumeButton != null)
            resumeButton.onClick.AddListener(Resume);

        CreateViewBobbingOption();
        CreateSprintHint();
        CreateRestartAndMenuButtons();
        CreateIntentionallyBadControlsSticker();
    }

    private void StylePausePanel()
    {
        Image paper = menuPanel.GetComponent<Image>();
        if (paper != null)
        {
            paper.color = new Color32(224, 216, 158, 255);
            Outline outline = paper.GetComponent<Outline>();
            if (outline == null) outline = paper.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(5f, -4f);
        }

        foreach (Image image in menuPanel.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "Ugly blue header" || image.name.StartsWith("Key ") || image.name == "Resume Button")
                image.color = new Color32(250, 210, 0, 255);
        }

        Font handwritten = Resources.Load<Font>("Fonts/childs-journal");
        foreach (Text text in menuPanel.GetComponentsInChildren<Text>(true))
        {
            text.color = Color.black;
            if (handwritten != null) text.font = handwritten;
        }
    }

    private void CreateViewBobbingOption()
    {
        if (menuPanel == null || menuPanel.transform.Find("View Bob Toggle") != null)
            return;

        Text template = menuPanel.GetComponentInChildren<Text>(true);
        if (template == null)
            return;

        GameObject labelObject = CreateLegacyTextObject("View Bob Label", menuPanel.transform);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        SetCenteredRect(labelRect, new Vector2(470f, -247f), new Vector2(110f, 42f));

        Text label = labelObject.GetComponent<Text>();
        CopyTextStyle(template, label, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
        label.color = template.color;
        label.text = "VIEW BOB";
        label.raycastTarget = false;
        Outline outline = labelObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);

        GameObject toggleObject = new GameObject(
            "View Bob Toggle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Toggle));
        toggleObject.transform.SetParent(menuPanel.transform, false);
        SetCenteredRect(toggleObject.GetComponent<RectTransform>(),
            new Vector2(550f, -247f), new Vector2(36f, 36f));

        Image background = toggleObject.GetComponent<Image>();
        background.color = Color.white;
        viewBobbingToggle = toggleObject.GetComponent<Toggle>();
        viewBobbingToggle.targetGraphic = background;
        viewBobbingToggle.transition = Selectable.Transition.None;

        GameObject checkObject = new GameObject(
            "Green Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        checkObject.transform.SetParent(toggleObject.transform, false);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = new Vector2(6f, 6f);
        checkRect.offsetMax = new Vector2(-6f, -6f);
        Image checkImage = checkObject.GetComponent<Image>();
        checkImage.color = new Color(0.05f, 1f, 0.08f, 1f);
        checkImage.raycastTarget = false;
        viewBobbingToggle.graphic = checkImage;
        viewBobbingToggle.isOn = PlayerPrefs.GetInt("How2Shop.ViewBobbing", 1) != 0;
        viewBobbingToggle.onValueChanged.AddListener(HandleViewBobbingChanged);
    }

    private void CreateSprintHint()
    {
        if (menuPanel == null || menuPanel.transform.Find("Sprint Hint") != null)
            return;

        Text template = menuPanel.GetComponentInChildren<Text>(true);
        if (template == null)
            return;

        GameObject hintObject = CreateLegacyTextObject("Sprint Hint", menuPanel.transform);
        SetCenteredRect(hintObject.GetComponent<RectTransform>(),
            new Vector2(-360f, -315f), new Vector2(300f, 44f));

        Text hint = hintObject.GetComponent<Text>();
        CopyTextStyle(template, hint, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        hint.color = template.color;
        hint.text = "LEFT SHIFT = SPRINT";
        hint.raycastTarget = false;
    }

    private void CreateRestartAndMenuButtons()
    {
        if (menuPanel == null)
            return;

        Text template = menuPanel.GetComponentInChildren<Text>(true);
        if (template == null)
            return;

        restartButton = CreatePauseButton(
            "Restart Game Button", "START AGAIN??", new Vector2(-420f, -380f),
            new Vector2(230f, 58f), new Color32(226, 77, 34, 255), template);
        restartButton.onClick.AddListener(RestartGame);

        mainMenuButton = CreatePauseButton(
            "Go To Main Menu Button", "GO MENU", new Vector2(420f, -380f),
            new Vector2(230f, 58f), new Color32(246, 208, 50, 255), template);
        mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    private void CreateIntentionallyBadControlsSticker()
    {
        if (menuPanel == null || menuPanel.transform.Find("Bad Controls Sticker") != null)
            return;

        Text template = menuPanel.GetComponentInChildren<Text>(true);
        if (template == null)
            return;

        GameObject sticker = new GameObject(
            "Bad Controls Sticker",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        sticker.transform.SetParent(menuPanel.transform, false);
        SetCenteredRect(sticker.GetComponent<RectTransform>(),
            new Vector2(420f, 56f), new Vector2(302f, 278f));
        sticker.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 2.7f);

        Image paper = sticker.GetComponent<Image>();
        paper.color = new Color32(255, 224, 45, 248);
        paper.raycastTarget = false;
        Outline blackOutline = sticker.AddComponent<Outline>();
        blackOutline.effectColor = Color.black;
        blackOutline.effectDistance = new Vector2(4f, -3f);
        Shadow redMisprint = sticker.AddComponent<Shadow>();
        redMisprint.effectColor = new Color(0.78f, 0.03f, 0.02f, 0.7f);
        redMisprint.effectDistance = new Vector2(-5f, 3f);

        GameObject titleObject = CreateLegacyTextObject("Wrong Angle Controls Heading", sticker.transform);
        SetCenteredRect(titleObject.GetComponent<RectTransform>(),
            new Vector2(0f, 105f), new Vector2(280f, 36f));
        titleObject.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -5.5f);
        Text title = titleObject.GetComponent<Text>();
        CopyTextStyle(template, title, 21, FontStyle.Bold, TextAnchor.MiddleCenter);
        title.color = new Color32(177, 0, 0, 255);
        title.text = "CONTROLS?? READ IT";
        title.raycastTarget = false;

        GameObject listObject = CreateLegacyTextObject("Cramped Controls List", sticker.transform);
        SetCenteredRect(listObject.GetComponent<RectTransform>(),
            new Vector2(0f, -28f), new Vector2(270f, 220f));
        listObject.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, -1.4f);
        Text list = listObject.GetComponent<Text>();
        CopyTextStyle(template, list, 16, FontStyle.Bold, TextAnchor.UpperLeft);
        list.color = new Color32(20, 16, 8, 255);
        list.text = "WASD: WALK\nMOUSE: LOOK\nSPACE: JUMP\nSHIFT: SPRINT\nR: PICK UP\nQ: DROP / CASHIER\nE: GRAB TROLLEY\nF: PUT ITEM IN\nHOLD RMB: CHARGE\nRELEASE RMB: YEET";
        list.raycastTarget = false;
        list.horizontalOverflow = HorizontalWrapMode.Wrap;
        list.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject scribbleObject = CreateLegacyTextObject("Unhelpful Margin Note", sticker.transform);
        SetCenteredRect(scribbleObject.GetComponent<RectTransform>(),
            new Vector2(96f, -122f), new Vector2(100f, 25f));
        scribbleObject.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, 8f);
        Text scribble = scribbleObject.GetComponent<Text>();
        CopyTextStyle(template, scribble, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
        scribble.color = new Color32(210, 0, 0, 255);
        scribble.text = "F AGAIN??";
        scribble.raycastTarget = false;
    }

    private Button CreatePauseButton(
        string objectName,
        string label,
        Vector2 position,
        Vector2 size,
        Color color,
        Text template)
    {
        GameObject buttonObject = new GameObject(
            objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(menuPanel.transform, false);
        SetCenteredRect(buttonObject.GetComponent<RectTransform>(), position, size);

        Image background = buttonObject.GetComponent<Image>();
        background.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.22f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
        colors.selectedColor = Color.Lerp(color, Color.white, 0.1f);
        button.colors = colors;

        GameObject textObject = CreateLegacyTextObject("Button Label", buttonObject.transform);
        RectTransform labelRect = textObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(6f, 3f);
        labelRect.offsetMax = new Vector2(-6f, -3f);

        Text text = textObject.GetComponent<Text>();
        CopyTextStyle(template, text, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
        text.color = Color.black;
        text.text = label;
        text.raycastTarget = false;
        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.72f);
        outline.effectDistance = new Vector2(1f, -1f);
        return button;
    }

    private static GameObject CreateLegacyTextObject(string name, Transform parent)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        return textObject;
    }

    private static void CopyTextStyle(Text source, Text destination, int size, FontStyle style, TextAnchor alignment)
    {
        destination.font = source.font;
        destination.fontSize = size;
        destination.fontStyle = style;
        destination.alignment = alignment;
        destination.horizontalOverflow = HorizontalWrapMode.Wrap;
        destination.verticalOverflow = VerticalWrapMode.Overflow;
        destination.supportRichText = true;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void HandleViewBobbingChanged(bool enabled)
    {
        if (playerMovement != null)
            playerMovement.SetViewBobbingEnabled(enabled);
    }

    private void Update()
    {
        if (GrandmaShoppingLoop.Instance != null && GrandmaShoppingLoop.Instance.RoundComplete)
            return;

        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        if (IsOpen) Resume();
        else Open();
    }

    public void Open()
    {
        if (menuPanel == null || IsOpen)
            return;

        menuPanel.SetActive(true);
        if (playerMovement != null) playerMovement.enabled = false;
        if (itemInteraction != null) itemInteraction.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        Time.timeScale = 1f;
        if (playerMovement != null) playerMovement.enabled = true;
        if (itemInteraction != null) itemInteraction.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(gameSceneName);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnDestroy()
    {
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(Resume);
        if (restartButton != null)
            restartButton.onClick.RemoveListener(RestartGame);
        if (mainMenuButton != null)
            mainMenuButton.onClick.RemoveListener(GoToMainMenu);
        if (viewBobbingToggle != null)
            viewBobbingToggle.onValueChanged.RemoveListener(HandleViewBobbingChanged);
        if (IsOpen)
            Time.timeScale = 1f;
    }
}
