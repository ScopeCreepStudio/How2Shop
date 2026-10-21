using System.Collections;
using How2Shop.Items;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class OpeningCutsceneController : MonoBehaviour
{
    [Header("Gameplay references")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerItemInteraction itemInteraction;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private PauseOptionsMenu pauseMenu;

    [Header("Walk to the store")]
    [SerializeField] private Vector3 outsideStartPosition = new Vector3(57.25f, 1.075f, -47f);
    [SerializeField] private Vector3 entrancePosition = new Vector3(57.25f, 1.075f, -41.15f);
    [SerializeField] private float walkDuration = 3.8f;
    [SerializeField] private float walkBobHeight = 0.025f;
    [SerializeField] private float walkBobSpeed = 11f;
    [SerializeField] private float lookDownAngle = 57f;
    [SerializeField] private float lookTime = 0.65f;
    [SerializeField] private float fadeTime = 0.45f;

    [Header("Physical letter")]
    [SerializeField] private Vector3 letterLocalPosition = new Vector3(0f, -0.05f, 1.25f);
    [SerializeField] private Vector3 letterSize = new Vector3(1.12f, 1.48f, 0.012f);
    [SerializeField] private Vector3 letterHandLocalPosition = new Vector3(0.3f, -0.17f, 1.65f);
    [SerializeField] private float letterHandScale = 0.46f;
    [SerializeField] private float letterRaiseDuration = 0.72f;
    [SerializeField] private float throwForwardSpeed = 2.25f;
    [SerializeField] private float throwUpSpeed = 2.8f;

    private Vector3 gameplayPosition;
    private Quaternion gameplayRotation;
    private Vector3 cameraLocalPosition;
    private Quaternion cameraLocalRotation;
    private bool movementWasEnabled;
    private bool interactionWasEnabled;
    private bool controllerWasEnabled;
    private bool pauseMenuWasEnabled;
    private bool controlsCanvasWasActive;
    private GameObject controlsCanvas;
    private GameObject canvasObject;
    private GameObject noteObject;
    private GameObject[] holdingHands;
    private Text promptText;
    private Image fadeImage;
    private bool isRunning;

    private const string Letter =
        "Hey sonny!\n" +
        "I'm getting a bit too old to be going to the shops myself, so I've written you a little list. Shouldn't take you more than five minutes, love.\n" +
        "Don't spend too much, and make sure you get the right things this time!\n\n" +
        "<b>Shopping List:</b>\n" +
        "- Something light, but not too light\n" +
        "- A couple of those round things\n" +
        "- Something strong, your grandad liked it strong\n" +
        "- Some water, but make sure it's dry\n" +
        "- A bit of peace and quiet\n" +
        "- Something with a kick\n" +
        "- The bread without the wires\n" +
        "- Something fresh, but not alive\n" +
        "- One of those things that does the thing\n" +
        "- A nice surprise, but nothing unexpected\n\n" +
        "Oh, and don't forget the milk!\n" +
        "Actually, I think we've got milk.\n" +
        "Never mind, get some anyway.\n\n" +
        "Lots of love,\n" +
        "<b>Nan xx</b>\n\n" +
        "P.S. You'll know what I mean when you see it.";

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (itemInteraction == null)
            itemInteraction = GetComponent<PlayerItemInteraction>();
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;
        if (pauseMenu == null)
            pauseMenu = FindAnyObjectByType<PauseOptionsMenu>();

        gameplayPosition = transform.position;
        gameplayRotation = transform.rotation;
        if (playerCamera != null)
        {
            cameraLocalPosition = playerCamera.localPosition;
            cameraLocalRotation = playerCamera.localRotation;
        }

        if (playerMovement != null)
        {
            movementWasEnabled = playerMovement.enabled;
            playerMovement.enabled = false;
        }
        if (itemInteraction != null)
        {
            interactionWasEnabled = itemInteraction.enabled;
            itemInteraction.enabled = false;
        }
        if (characterController != null)
        {
            controllerWasEnabled = characterController.enabled;
            characterController.enabled = false;
        }
        if (pauseMenu != null)
        {
            pauseMenuWasEnabled = pauseMenu.enabled;
            pauseMenu.enabled = false;
        }

        controlsCanvas = GameObject.Find("Pickup Controls");
        if (controlsCanvas != null)
        {
            controlsCanvasWasActive = controlsCanvas.activeSelf;
            controlsCanvas.SetActive(false);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        BuildCutsceneUI();
    }

    private void Start()
    {
        StartCoroutine(PlayCutscene());
    }

    private void BuildCutsceneUI()
    {
        canvasObject = new GameObject(
            "Opening Cutscene UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject promptObject = new GameObject("Trip Prompt", typeof(RectTransform));
        promptObject.transform.SetParent(canvasObject.transform, false);
        RectTransform promptRect = promptObject.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.04f);
        promptRect.anchorMax = new Vector2(0.5f, 0.04f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.sizeDelta = new Vector2(1000f, 58f);
        promptRect.anchoredPosition = Vector2.zero;

        promptText = promptObject.AddComponent<Text>();
        promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        promptText.fontSize = 28;
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.color = Color.white;
        promptText.text = "SPACE - trip into the shop";
        promptText.raycastTarget = false;

        GameObject fadeObject = new GameObject("Screen Fade", typeof(RectTransform));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        RectTransform fadeRect = fadeObject.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        fadeImage = fadeObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;
        fadeObject.transform.SetAsLastSibling();
    }

    private IEnumerator PlayCutscene()
    {
        isRunning = true;
        Time.timeScale = 1f;

        if (characterController != null)
            characterController.enabled = false;

        transform.SetPositionAndRotation(
            outsideStartPosition,
            Quaternion.Euler(0f, 0f, 0f));

        if (playerCamera != null)
        {
            playerCamera.localPosition = cameraLocalPosition;
            playerCamera.localRotation = Quaternion.Euler(8f, 0f, 0f);
        }

        CreateHeldLetter();
        yield return FadeTo(0f, fadeTime);
        yield return WalkToEntrance();

        if (playerCamera != null)
            yield return LookCameraTo(lookDownAngle, lookTime);

        yield return RaiseLetterToFace();
        yield return new WaitForSecondsRealtime(0.2f);

        float letterShownAt = Time.unscaledTime;
        bool continueKeyReleased = false;
        while (true)
        {
            Keyboard keyboard = Keyboard.current;
            if (Time.unscaledTime - letterShownAt >= 1.2f && keyboard != null)
            {
                if (!keyboard.spaceKey.isPressed &&
                    !keyboard.enterKey.isPressed &&
                    !keyboard.numpadEnterKey.isPressed)
                {
                    continueKeyReleased = true;
                }

                if (continueKeyReleased && ContinuePressed())
                    break;
            }

            yield return null;
        }

        promptText.text = "OH NO. NAN'S LIST!";
        ThrowLetter();
        yield return TripAndFall();
        yield return FadeTo(1f, fadeTime);

        transform.SetPositionAndRotation(gameplayPosition, gameplayRotation);
        if (playerCamera != null)
        {
            playerCamera.localPosition = cameraLocalPosition;
            playerCamera.localRotation = cameraLocalRotation;
        }
        if (characterController != null)
            characterController.enabled = controllerWasEnabled;

        yield return new WaitForSecondsRealtime(0.2f);
        yield return FadeTo(0f, fadeTime);

        if (playerMovement != null)
            playerMovement.enabled = movementWasEnabled;
        if (itemInteraction != null)
            itemInteraction.enabled = interactionWasEnabled;
        if (pauseMenu != null)
            pauseMenu.enabled = pauseMenuWasEnabled;
        if (controlsCanvas != null)
            controlsCanvas.SetActive(controlsCanvasWasActive);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isRunning = false;

        if (canvasObject != null)
            Destroy(canvasObject);
    }

    private void CreateHeldLetter()
    {
        if (playerCamera == null)
            return;

        if (playerMovement != null)
            playerMovement.enabled = false;
        if (itemInteraction != null)
            itemInteraction.enabled = false;
        if (pauseMenu != null)
            pauseMenu.enabled = false;
        if (controlsCanvas != null)
            controlsCanvas.SetActive(false);

        noteObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        noteObject.name = "Nan's 3D Shopping List";
        noteObject.transform.SetParent(playerCamera, false);
        noteObject.transform.localPosition = letterLocalPosition;
        noteObject.transform.localRotation = Quaternion.identity;
        noteObject.transform.localScale = letterSize;

        BoxCollider noteCollider = noteObject.GetComponent<BoxCollider>();
        noteCollider.enabled = false;

        MeshRenderer paperRenderer = noteObject.GetComponent<MeshRenderer>();
        Shader paperShader = Shader.Find("Unlit/Transparent");
        if (paperShader == null)
            paperShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (paperShader == null)
            paperShader = Shader.Find("Unlit/Color");
        if (paperShader != null)
        {
            Material paperMaterial = new Material(paperShader);
            paperMaterial.name = "Runtime Nan Letter Paper";
            Color paperColor = new Color(0.98f, 0.96f, 0.88f, 1f);
            if (paperMaterial.HasProperty("_Color"))
                paperMaterial.SetColor("_Color", paperColor);
            else
                paperMaterial.color = paperColor;
            if (paperMaterial.HasProperty("_BaseColor"))
                paperMaterial.SetColor("_BaseColor", paperColor);
            paperMaterial.renderQueue = 3000;
            paperRenderer.material = paperMaterial;
        }

        GameObject textCanvasObject = new GameObject(
            "Printed Letter",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        textCanvasObject.transform.SetParent(noteObject.transform, false);
        textCanvasObject.transform.localPosition = new Vector3(0f, 0f, -0.52f);
        // The note is viewed from its local -Z face; point the world-space
        // canvas out through that face instead of embedding it in the paper.
        textCanvasObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        textCanvasObject.transform.localScale = Vector3.one * 0.001f;

        RectTransform canvasRect = textCanvasObject.GetComponent<RectTransform>();
        // The canvas is scaled down by 0.001, while the paper cube carries its
        // physical size in its transform. Keep the printed face aligned with
        // the full sheet instead of shrinking the text with the paper twice.
        canvasRect.sizeDelta = new Vector2(1000f, 1000f);

        Canvas textCanvas = textCanvasObject.GetComponent<Canvas>();
        textCanvas.renderMode = RenderMode.WorldSpace;
        textCanvas.sortingOrder = 10;

        CanvasScaler textScaler = textCanvasObject.GetComponent<CanvasScaler>();
        textScaler.dynamicPixelsPerUnit = 100f;
        textScaler.referencePixelsPerUnit = 100f;

        GameObject letterTextObject = new GameObject("Nan's Letter Text", typeof(RectTransform));
        letterTextObject.transform.SetParent(textCanvasObject.transform, false);
        RectTransform textRect = letterTextObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 12f);
        textRect.offsetMax = new Vector2(-24f, -12f);
        // The canvas faces out through the paper's -Z side. Flip only the
        // lettering horizontally so it reads normally from the player's side.
        letterTextObject.transform.localScale = new Vector3(-1f, 1f, 1f);

        TextMeshProUGUI letterText = letterTextObject.AddComponent<TextMeshProUGUI>();
        letterText.font = Resources.Load<TMP_FontAsset>("Fonts/Childs Journal SDF Final");
        letterText.fontSize = 26f;
        letterText.fontStyle = FontStyles.Bold;
        letterText.alignment = TextAlignmentOptions.TopLeft;
        letterText.textWrappingMode = TextWrappingModes.Normal;
        letterText.overflowMode = TextOverflowModes.Overflow;
        letterText.richText = true;
        letterText.color = new Color(0.012f, 0.008f, 0.004f, 1f);
        letterText.text = Letter;
        letterText.raycastTarget = false;

        noteObject.transform.localPosition = letterHandLocalPosition;
        noteObject.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
        noteObject.transform.localScale = letterSize * letterHandScale;
        CreateLetterHands();
    }

    private void CreateLetterHands()
    {
        if (noteObject == null)
            return;

        Shader handShader = Shader.Find("Unlit/Color");
        if (handShader == null)
            handShader = Shader.Find("Standard");
        Material handMaterial = handShader != null ? new Material(handShader) : null;
        if (handMaterial != null)
        {
            handMaterial.name = "Runtime Nan Letter Holding Hands";
            Color skinColor = new Color(0.55f, 0.34f, 0.22f, 1f);
            if (handMaterial.HasProperty("_Color"))
                handMaterial.SetColor("_Color", skinColor);
            else
                handMaterial.color = skinColor;
        }

        holdingHands = new GameObject[2];
        for (int i = 0; i < holdingHands.Length; i++)
        {
            float side = i == 0 ? -1f : 1f;
            GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hand.name = i == 0 ? "Left Hand Holding Note" : "Right Hand Holding Note";
            hand.transform.SetParent(noteObject.transform, false);
            hand.transform.localPosition = new Vector3(side * 0.47f, -0.45f, -6f);
            hand.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            hand.transform.localScale = new Vector3(
                0.13f / letterSize.y,
                0.24f / (2f * letterSize.x),
                0.08f / letterSize.z);

            Collider handCollider = hand.GetComponent<Collider>();
            if (handCollider != null)
                handCollider.enabled = false;

            MeshRenderer handRenderer = hand.GetComponent<MeshRenderer>();
            if (handRenderer != null)
            {
                handRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                handRenderer.sortingOrder = 20;
                if (handMaterial != null)
                    handRenderer.material = handMaterial;
            }

            holdingHands[i] = hand;
        }
    }

    private IEnumerator RaiseLetterToFace()
    {
        if (noteObject == null)
            yield break;

        Vector3 startPosition = noteObject.transform.localPosition;
        Quaternion startRotation = noteObject.transform.localRotation;
        Vector3 startScale = noteObject.transform.localScale;
        float elapsed = 0f;

        while (elapsed < letterRaiseDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, letterRaiseDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            noteObject.transform.localPosition = Vector3.Lerp(startPosition, letterLocalPosition, eased);
            noteObject.transform.localRotation = Quaternion.Slerp(startRotation, Quaternion.identity, eased);
            noteObject.transform.localScale = Vector3.Lerp(startScale, letterSize, eased);
            yield return null;
        }

        noteObject.transform.localPosition = letterLocalPosition;
        noteObject.transform.localRotation = Quaternion.identity;
        noteObject.transform.localScale = letterSize;
    }

    private void ThrowLetter()
    {
        if (noteObject == null)
            return;

        if (holdingHands != null)
        {
            foreach (GameObject hand in holdingHands)
            {
                if (hand != null)
                    hand.SetActive(false);
            }
        }

        noteObject.transform.SetParent(null, true);
        // Lay the page flat so Nan's writing faces upward as it drops into the
        // open grate and remains readable after it lands.
        noteObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        BoxCollider noteCollider = noteObject.GetComponent<BoxCollider>();
        noteCollider.enabled = true;

        // The sheet starts in front of the camera and the player lunges
        // forward during the stumble. Ignore the player's own colliders so
        // the paper reaches the gutter instead of bouncing off the player.
        Collider[] playerColliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider != null && playerCollider != noteCollider)
                Physics.IgnoreCollision(noteCollider, playerCollider, true);
        }

        Rigidbody body = noteObject.AddComponent<Rigidbody>();
        body.mass = 0.12f;
        body.linearDamping = 0.2f;
        body.angularDamping = 0.5f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.AddForce(transform.forward * throwForwardSpeed + Vector3.up * throwUpSpeed, ForceMode.VelocityChange);
        body.angularVelocity = new Vector3(0.05f, 0.02f, 0.03f);
    }

    private IEnumerator TripAndFall()
    {
        Vector3 rootStart = transform.position;
        Quaternion cameraStart = playerCamera != null ? playerCamera.localRotation : Quaternion.identity;
        Vector3 cameraPositionStart = playerCamera != null ? playerCamera.localPosition : Vector3.zero;
        float elapsed = 0f;
        const float stumbleDuration = 0.36f;

        while (elapsed < stumbleDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / stumbleDuration);
            float eased = t * t * (3f - 2f * t);
            transform.position = rootStart + transform.forward * (0.42f * eased);

            if (playerCamera != null)
            {
                playerCamera.localPosition = cameraPositionStart +
                    Vector3.Lerp(Vector3.zero, new Vector3(0f, -0.28f, 0.08f), eased);
                playerCamera.localRotation = Quaternion.Slerp(
                    cameraStart,
                    Quaternion.Euler(78f, 0f, -18f),
                    eased);
            }

            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.16f);

        elapsed = 0f;
        const float fallDuration = 0.22f;
        Vector3 cameraPositionLow = cameraPositionStart + new Vector3(0f, -0.5f, 0.12f);
        Quaternion cameraRotationLow = Quaternion.Euler(87f, 0f, 8f);
        Quaternion cameraAtStumble = playerCamera != null ? playerCamera.localRotation : Quaternion.identity;
        Vector3 cameraAtStumblePosition = playerCamera != null ? playerCamera.localPosition : Vector3.zero;

        while (elapsed < fallDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            if (playerCamera != null)
            {
                playerCamera.localPosition = Vector3.Lerp(cameraAtStumblePosition, cameraPositionLow, t);
                playerCamera.localRotation = Quaternion.Slerp(cameraAtStumble, cameraRotationLow, t);
            }

            yield return null;
        }

        Rigidbody noteBody = noteObject != null ? noteObject.GetComponent<Rigidbody>() : null;
        float watchElapsed = 0f;
        const float maxWatchDuration = 2.6f;
        while (watchElapsed < maxWatchDuration && noteObject != null)
        {
            if (playerCamera != null)
            {
                Vector3 focusPoint = noteObject.transform.position + Vector3.up * 0.015f;
                Vector3 focusDirection = focusPoint - playerCamera.position;
                if (focusDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookAtNote = Quaternion.LookRotation(focusDirection, Vector3.up);
                    playerCamera.rotation = Quaternion.Slerp(
                        playerCamera.rotation,
                        lookAtNote,
                        Time.unscaledDeltaTime * 5f);
                }
            }

            watchElapsed += Time.unscaledDeltaTime;
            if (noteBody != null && noteBody.IsSleeping())
                break;

            yield return null;
        }

        // Let the page settle on the grate for a beat, then let it ghost
        // through the bars while fading away.
        yield return new WaitForSecondsRealtime(0.4f);
        yield return FadeLetterThroughGrate(1.35f);
    }

    private IEnumerator FadeLetterThroughGrate(float duration)
    {
        if (noteObject == null)
            yield break;

        BoxCollider noteCollider = noteObject.GetComponent<BoxCollider>();
        if (noteCollider != null)
            noteCollider.enabled = false;

        Rigidbody noteBody = noteObject.GetComponent<Rigidbody>();
        if (noteBody != null)
        {
            noteBody.linearVelocity = Vector3.zero;
            noteBody.angularVelocity = Vector3.zero;
            noteBody.useGravity = false;
            noteBody.isKinematic = true;
        }

        MeshRenderer paperRenderer = noteObject.GetComponent<MeshRenderer>();
        Material paperMaterial = paperRenderer != null ? paperRenderer.material : null;
        string paperColorProperty = paperMaterial != null && paperMaterial.HasProperty("_Color")
            ? "_Color"
            : "_BaseColor";
        Color paperStartColor = paperMaterial != null && paperMaterial.HasProperty(paperColorProperty)
            ? paperMaterial.GetColor(paperColorProperty)
            : Color.white;
        TextMeshProUGUI[] printedText = noteObject.GetComponentsInChildren<TextMeshProUGUI>(true);
        Color[] textStartColors = new Color[printedText.Length];
        for (int i = 0; i < printedText.Length; i++)
            textStartColors[i] = printedText[i].color;

        Vector3 startPosition = noteObject.transform.position;
        Quaternion startRotation = noteObject.transform.rotation;
        const float sinkStart = 0.22f;
        const float sinkDistance = 0.34f;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, 0f, 12f);
        float elapsed = 0f;

        while (elapsed < duration && noteObject != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = t * t * (3f - 2f * t);
            // Let the paper visibly fade while it rests on the grate, then
            // ease it through the bars as the last of the page disappears.
            float sinkT = Mathf.Clamp01((t - sinkStart) / (1f - sinkStart));
            float sinkEased = sinkT * sinkT * (3f - 2f * sinkT);
            noteObject.transform.position = startPosition + Vector3.down * sinkDistance * sinkEased;
            noteObject.transform.rotation = Quaternion.Slerp(startRotation, endRotation, eased);

            float alpha = 1f - eased;
            if (paperMaterial != null && paperMaterial.HasProperty(paperColorProperty))
            {
                Color color = paperStartColor;
                color.a = paperStartColor.a * alpha;
                paperMaterial.SetColor(paperColorProperty, color);
            }

            for (int i = 0; i < printedText.Length; i++)
            {
                if (printedText[i] == null)
                    continue;
                Color color = textStartColors[i];
                color.a = textStartColors[i].a * alpha;
                printedText[i].color = color;
            }

            if (playerCamera != null)
            {
                Vector3 focusDirection = noteObject.transform.position - playerCamera.position;
                if (focusDirection.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookAtNote = Quaternion.LookRotation(focusDirection, Vector3.up);
                    playerCamera.rotation = Quaternion.Slerp(
                        playerCamera.rotation,
                        lookAtNote,
                        Time.unscaledDeltaTime * 5f);
                }
            }

            yield return null;
        }

        if (noteObject != null)
        {
            Destroy(noteObject);
            noteObject = null;
        }
    }

    private IEnumerator WalkToEntrance()
    {
        Vector3 start = transform.position;
        Vector3 end = entrancePosition;
        float elapsed = 0f;

        while (elapsed < walkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, walkDuration));
            float eased = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(start, end, eased);

            if (playerCamera != null)
            {
                float bob = Mathf.Sin(elapsed * walkBobSpeed) * walkBobHeight;
                playerCamera.localPosition = cameraLocalPosition + new Vector3(0f, bob, 0f);
            }

            yield return null;
        }

        transform.position = end;
        if (playerCamera != null)
            playerCamera.localPosition = cameraLocalPosition;
    }

    private IEnumerator LookCameraTo(float pitch, float duration)
    {
        Quaternion start = playerCamera.localRotation;
        Quaternion end = Quaternion.Euler(pitch, 0f, 0f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            playerCamera.localRotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }

        playerCamera.localRotation = end;
    }

    private bool ContinuePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null &&
            (keyboard.spaceKey.wasPressedThisFrame ||
             keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame);
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        if (fadeImage == null)
            yield break;

        float startAlpha = fadeImage.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            Color color = Color.black;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            fadeImage.color = color;
            yield return null;
        }

        Color finalColor = Color.black;
        finalColor.a = targetAlpha;
        fadeImage.color = finalColor;
    }

    private void OnDisable()
    {
        if (!isRunning)
            return;

        if (characterController != null)
            characterController.enabled = controllerWasEnabled;
        if (playerMovement != null)
            playerMovement.enabled = movementWasEnabled;
        if (itemInteraction != null)
            itemInteraction.enabled = interactionWasEnabled;
        if (pauseMenu != null)
            pauseMenu.enabled = pauseMenuWasEnabled;
        if (controlsCanvas != null)
            controlsCanvas.SetActive(controlsCanvasWasActive);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isRunning = false;
    }
}

