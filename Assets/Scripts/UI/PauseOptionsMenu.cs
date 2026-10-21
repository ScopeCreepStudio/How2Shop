using How2Shop.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class PauseOptionsMenu : MonoBehaviour
{
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerItemInteraction itemInteraction;

    private Toggle viewBobbingToggle;
    private Text viewBobbingLabel;

    
public bool IsOpen => menuPanel != null && menuPanel.activeSelf;

private void Awake()
{
    if (playerMovement == null)
        playerMovement = FindAnyObjectByType<PlayerMovement>();

    if (menuPanel != null)
        menuPanel.SetActive(false);

    if (resumeButton != null)
        resumeButton.onClick.AddListener(Resume);

    CreateViewBobbingOption();
    CreateSprintHint();
}

private void CreateViewBobbingOption()
{
    if (menuPanel == null || menuPanel.transform.Find("View Bob Toggle") != null)
        return;

    Text template = menuPanel.GetComponentInChildren<Text>(true);
    if (template == null)
        return;

    GameObject labelObject = new GameObject("View Bob Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
    labelObject.transform.SetParent(menuPanel.transform, false);
    RectTransform labelRect = labelObject.GetComponent<RectTransform>();
    labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
    labelRect.pivot = new Vector2(0.5f, 0.5f);
    labelRect.anchoredPosition = new Vector2(470f, -247f);
    labelRect.sizeDelta = new Vector2(110f, 42f);

    viewBobbingLabel = labelObject.GetComponent<Text>();
    viewBobbingLabel.font = template.font;
    viewBobbingLabel.fontSize = 18;
    viewBobbingLabel.fontStyle = FontStyle.Bold;
    viewBobbingLabel.alignment = TextAnchor.MiddleLeft;
    viewBobbingLabel.color = template.color;
    viewBobbingLabel.text = "VIEW BOB";
    viewBobbingLabel.raycastTarget = false;
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
    RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
    toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
    toggleRect.pivot = new Vector2(0.5f, 0.5f);
    toggleRect.anchoredPosition = new Vector2(550f, -247f);
    toggleRect.sizeDelta = new Vector2(36f, 36f);

    Image background = toggleObject.GetComponent<Image>();
    background.color = Color.white;
    viewBobbingToggle = toggleObject.GetComponent<Toggle>();
    viewBobbingToggle.targetGraphic = background;
    viewBobbingToggle.transition = Selectable.Transition.None;

    GameObject checkObject = new GameObject("Green Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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

    GameObject hintObject = new GameObject("Sprint Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
    hintObject.transform.SetParent(menuPanel.transform, false);
    RectTransform hintRect = hintObject.GetComponent<RectTransform>();
    hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0.5f);
    hintRect.pivot = new Vector2(0.5f, 0.5f);
    hintRect.anchoredPosition = new Vector2(-360f, -315f);
    hintRect.sizeDelta = new Vector2(300f, 44f);

    Text hint = hintObject.GetComponent<Text>();
    hint.font = template.font;
    hint.fontSize = 20;
    hint.fontStyle = FontStyle.Bold;
    hint.alignment = TextAnchor.MiddleCenter;
    hint.color = template.color;
    hint.text = "LEFT SHIFT = SPRINT";
    hint.raycastTarget = false;
}

private void HandleViewBobbingChanged(bool enabled)
{
    if (playerMovement != null)
        playerMovement.SetViewBobbingEnabled(enabled);
}


    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsOpen) Resume();
            else Open();
        }
    }

    public void Open()
    {
        if (menuPanel == null || IsOpen) return;

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

private void OnDestroy()
{
    if (resumeButton != null)
        resumeButton.onClick.RemoveListener(Resume);
    if (viewBobbingToggle != null)
        viewBobbingToggle.onValueChanged.RemoveListener(HandleViewBobbingChanged);

    if (IsOpen)
        Time.timeScale = 1f;
}
}
