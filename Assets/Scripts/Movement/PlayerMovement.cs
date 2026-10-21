using UnityEngine;
using UnityEngine.InputSystem;

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
    private const string ViewBobbingPreference = "How2Shop.ViewBobbing";
    private Vector3 cameraRestLocalPosition;
    private float bobTimer;
    private bool hasCameraRestPosition;

    public bool ViewBobbingEnabled => viewBobbingEnabled;
    private bool viewBobbingEnabled = true;

    
private CharacterController controller;

    private Vector3 velocity;
    private float cameraRotation = 0f;

void Start()
{
    controller = GetComponent<CharacterController>();

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

    void Update()
    {
        Move();
        Look();
    }

void Move()
{
    Keyboard kb = Keyboard.current;
    if (kb == null)
    {
        UpdateViewBobbing(Vector3.zero, false);
        return;
    }

    float x = 0f;
    float z = 0f;
    if (kb.dKey.isPressed) x += 1f;
    if (kb.aKey.isPressed) x -= 1f;
    if (kb.wKey.isPressed) z += 1f;
    if (kb.sKey.isPressed) z -= 1f;

    Vector3 move = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
    bool sprinting = move.sqrMagnitude > 0.01f &&
        (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
    float speed = moveSpeed * (sprinting ? sprintMultiplier : 1f);

    controller.Move(move * speed * Time.deltaTime);
    UpdateViewBobbing(move, sprinting);

    if (controller.isGrounded && velocity.y < 0)
        velocity.y = -2f;

    if (kb.spaceKey.wasPressedThisFrame && controller.isGrounded)
        velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

    velocity.y += gravity * Time.deltaTime;
    controller.Move(velocity * Time.deltaTime);
}

public void SetViewBobbingEnabled(bool enabled)
{
    viewBobbingEnabled = enabled;
    PlayerPrefs.SetInt(ViewBobbingPreference, enabled ? 1 : 0);
    if (!enabled && hasCameraRestPosition && playerCamera != null)
        playerCamera.localPosition = cameraRestLocalPosition;
}

private void UpdateViewBobbing(Vector3 move, bool sprinting)
{
    if (playerCamera == null || !hasCameraRestPosition)
        return;

    bool shouldBob = viewBobbingEnabled && controller != null &&
        controller.isGrounded && move.sqrMagnitude > 0.01f;

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

private void OnDisable()
{
    if (hasCameraRestPosition && playerCamera != null)
        playerCamera.localPosition = cameraRestLocalPosition;
}


    void Look()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue() * 0.1f;
        float mouseX = delta.x * mouseSensitivity;
        float mouseY = delta.y * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        cameraRotation -= mouseY;
        cameraRotation = Mathf.Clamp(
            cameraRotation,
            -maxLookAngle,
            maxLookAngle
        );

        playerCamera.localRotation = Quaternion.Euler(
            cameraRotation,
            0f,
            0f
        );
    }
}

