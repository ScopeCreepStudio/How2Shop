using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using How2Shop.Chaos;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpHeight = 2f;
    public float gravity = -20f;

    [Header("Camera")]
    public Transform playerCamera;
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 90f;

    [Header("Sprint and view bob")]
    [SerializeField, Min(1f)] private float sprintMultiplier = 1.8f;
    [SerializeField, Min(0f)] private float walkBobFrequency = 9.5f;
    [SerializeField, Min(0f)] private float sprintBobFrequency = 13f;
    [SerializeField, Min(0f)] private float walkBobAmount = 0.045f;
    [SerializeField, Min(0f)] private float sprintBobAmount = 0.075f;

    [Header("HUD")]
    [SerializeField] private float crosshairYOffset = -12f;

    private const string ViewBobbingPreference = "How2Shop.ViewBobbing";
    private const string CrosshairCanvasName = "Crosshair Canvas";
    private const float GroundedStickVelocity = -2f;

    private static Sprite crosshairSprite;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 cameraRestLocalPosition;
    private float cameraRotation;
    private float bobTimer;
    private bool hasCameraRestPosition;
    private bool viewBobbingEnabled = true;
    private bool isSprinting;
    private bool movementInputLocked;
    private bool cartBoostActive;
    private float cartBoostMultiplier = 1f;

    public bool ViewBobbingEnabled => viewBobbingEnabled;
    public bool IsSprinting => isSprinting;
    public bool IsMovementInputLocked => movementInputLocked;

    public void SetCartBoost(bool active, float multiplier)
    {
        cartBoostActive = active;
        cartBoostMultiplier = Mathf.Max(1f, multiplier);
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            CreateCrosshair();
    }

    private void Start()
    {
        if (playerCamera != null)
        {
            cameraRotation = Mathf.DeltaAngle(0f, playerCamera.localEulerAngles.x);
            cameraRestLocalPosition = playerCamera.localPosition;
            hasCameraRestPosition = true;
        }

        viewBobbingEnabled = PlayerPrefs.GetInt(ViewBobbingPreference, 1) != 0;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        Move();
        Look();
    }

    private void Move()
    {
        isSprinting = false;
        if (controller == null || !controller.enabled)
            return;

        if (movementInputLocked)
        {
            if (controller.isGrounded && velocity.y < 0f)
                velocity.y = GroundedStickVelocity;
            else
                velocity.y += gravity * Time.deltaTime;

            controller.Move(Vector3.up * velocity.y * Time.deltaTime);
            if (controller.isGrounded && velocity.y < 0f)
                velocity.y = GroundedStickVelocity;

            UpdateViewBobbing(Vector3.zero, false, controller.isGrounded);
            return;
        }

        Keyboard keyboard = Keyboard.current;
        float x = 0f;
        float z = 0f;
        bool sprintInput = false;
        bool jumpPressed = false;

        if (keyboard != null)
        {
            if (keyboard.dKey.isPressed) x += 1f;
            if (keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.wKey.isPressed) z += 1f;
            if (keyboard.sKey.isPressed) z -= 1f;

            sprintInput = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        }

        Vector3 move = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
        bool sprinting = move.sqrMagnitude > 0.01f && sprintInput;
        isSprinting = sprinting;
        float speed = moveSpeed * (sprinting ? sprintMultiplier : 1f) *
            (cartBoostActive ? cartBoostMultiplier : 1f);

        // Capture grounding before Move. A horizontal CharacterController.Move can
        // replace the previous frame's collision flags before jump logic reads them.
        bool groundedBeforeMove = controller.isGrounded;
        if (groundedBeforeMove && velocity.y < 0f)
            velocity.y = GroundedStickVelocity;

        if (jumpPressed && groundedBeforeMove && gravity < 0f)
        {
            velocity.y = Mathf.Sqrt(Mathf.Max(0f, jumpHeight * -2f * gravity));
            bobTimer = 0f;
        }

        velocity.y += gravity * Time.deltaTime;
        Vector3 frameMotion = move * speed + Vector3.up * velocity.y;
        controller.Move(frameMotion * Time.deltaTime);

        bool groundedAfterMove = controller.isGrounded;
        if (groundedAfterMove && velocity.y < 0f)
            velocity.y = GroundedStickVelocity;

        UpdateViewBobbing(move, sprinting, groundedAfterMove);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit == null || hit.collider == null || controller == null)
            return;

        float impactSpeed = Mathf.Max(
            controller.velocity.magnitude,
            hit.moveLength / Mathf.Max(Time.deltaTime, 0.001f));

        ShopperRagdoll shopper = hit.collider.GetComponentInParent<ShopperRagdoll>();
        if (shopper != null)
            shopper.ReportPlayerMovementImpact(this, impactSpeed);

        ChaosImpactTarget chaosTarget = hit.collider.GetComponentInParent<ChaosImpactTarget>();
        if (chaosTarget != null)
            chaosTarget.TryReportImpact(ChaosImpactSource.PlayerBody, gameObject, impactSpeed);
    }

    public void SetViewBobbingEnabled(bool enabled)
    {
        viewBobbingEnabled = enabled;
        PlayerPrefs.SetInt(ViewBobbingPreference, enabled ? 1 : 0);

        if (!enabled && hasCameraRestPosition && playerCamera != null)
        {
            playerCamera.localPosition = cameraRestLocalPosition;
            bobTimer = 0f;
        }
    }

    /// <summary>Locks keyboard movement while preserving mouse look and the active camera.</summary>
    public void SetMovementInputLocked(bool locked)
    {
        movementInputLocked = locked;
        if (locked)
            isSprinting = false;
    }

    private void UpdateViewBobbing(Vector3 move, bool sprinting, bool grounded)
    {
        if (playerCamera == null || !hasCameraRestPosition)
            return;

        Vector3 planarVelocity = controller != null ? controller.velocity : Vector3.zero;
        planarVelocity.y = 0f;

        bool shouldBob = viewBobbingEnabled && grounded &&
            move.sqrMagnitude > 0.01f && planarVelocity.sqrMagnitude > 0.01f;

        Vector3 target = cameraRestLocalPosition;
        if (shouldBob)
        {
            float frequency = sprinting ? sprintBobFrequency : walkBobFrequency;
            float amount = sprinting ? sprintBobAmount : walkBobAmount;
            bobTimer += Time.deltaTime * frequency;
            target += Vector3.up * Mathf.Abs(Mathf.Sin(bobTimer)) * amount;
            target += Vector3.right * Mathf.Sin(bobTimer * 0.5f) * amount * 0.32f;
        }

        float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
        playerCamera.localPosition = Vector3.Lerp(playerCamera.localPosition, target, blend);
    }

    private void CreateCrosshair()
    {
        if (GameObject.Find(CrosshairCanvasName) != null)
            return;

        GameObject canvasObject = new GameObject(
            CrosshairCanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        GameObject crosshairObject = new GameObject("Crosshair", typeof(RectTransform));
        crosshairObject.transform.SetParent(canvasObject.transform, false);

        RectTransform crosshairRect = crosshairObject.GetComponent<RectTransform>();
        crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
        crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
        crosshairRect.pivot = new Vector2(0.5f, 0.5f);
        crosshairRect.anchoredPosition = new Vector2(0f, crosshairYOffset);
        crosshairRect.sizeDelta = Vector2.zero;

        Color outline = new Color(0f, 0f, 0f, 0.8f);
        Color fill = Color.white;

        CreateCrosshairArm(crosshairRect, "Top Outline", new Vector2(4f, 6f), new Vector2(0f, 5f), outline, 2f);
        CreateCrosshairArm(crosshairRect, "Bottom Outline", new Vector2(4f, 6f), new Vector2(0f, -5f), outline, 2f);
        CreateCrosshairArm(crosshairRect, "Left Outline", new Vector2(6f, 4f), new Vector2(-5f, 0f), outline, 2f);
        CreateCrosshairArm(crosshairRect, "Right Outline", new Vector2(6f, 4f), new Vector2(5f, 0f), outline, 2f);
        CreateCrosshairArm(crosshairRect, "Top", new Vector2(2f, 4f), new Vector2(0f, 5f), fill, 0f);
        CreateCrosshairArm(crosshairRect, "Bottom", new Vector2(2f, 4f), new Vector2(0f, -5f), fill, 0f);
        CreateCrosshairArm(crosshairRect, "Left", new Vector2(4f, 2f), new Vector2(-5f, 0f), fill, 0f);
        CreateCrosshairArm(crosshairRect, "Right", new Vector2(4f, 2f), new Vector2(5f, 0f), fill, 0f);
    }

    private static void CreateCrosshairArm(
        RectTransform parent,
        string name,
        Vector2 size,
        Vector2 position,
        Color color,
        float outlinePadding)
    {
        GameObject armObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        armObject.transform.SetParent(parent, false);

        RectTransform armRect = armObject.GetComponent<RectTransform>();
        armRect.anchorMin = new Vector2(0.5f, 0.5f);
        armRect.anchorMax = new Vector2(0.5f, 0.5f);
        armRect.pivot = new Vector2(0.5f, 0.5f);
        armRect.anchoredPosition = position;
        armRect.sizeDelta = size + Vector2.one * outlinePadding;

        Image image = armObject.GetComponent<Image>();
        image.sprite = GetCrosshairSprite();
        image.color = color;
        image.raycastTarget = false;
        image.maskable = false;
    }

    private static Sprite GetCrosshairSprite()
    {
        if (crosshairSprite == null)
        {
            crosshairSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f));
        }

        return crosshairSprite;
    }

    private void OnDisable()
    {
        if (hasCameraRestPosition && playerCamera != null)
            playerCamera.localPosition = cameraRestLocalPosition;
    }

    private void Look()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || playerCamera == null)
            return;

        Vector2 delta = mouse.delta.ReadValue() * 0.1f;
        float mouseX = delta.x * mouseSensitivity;
        float mouseY = delta.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraRotation -= mouseY;
        cameraRotation = Mathf.Clamp(cameraRotation, -maxLookAngle, maxLookAngle);
        playerCamera.localRotation = Quaternion.Euler(cameraRotation, 0f, 0f);
    }
}
