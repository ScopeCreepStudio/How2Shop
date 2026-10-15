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

    private CharacterController controller;

    private Vector3 velocity;
    private float cameraRotation = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // Lock mouse to the centre of the screen
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
        if (kb == null) return;

        // WASD input
        float x = 0f;
        float z = 0f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.wKey.isPressed) z += 1f;
        if (kb.sKey.isPressed) z -= 1f;

        Vector3 move = transform.right * x + transform.forward * z;

        // Prevent diagonal movement from being faster
        move = Vector3.ClampMagnitude(move, 1f);

        controller.Move(move * moveSpeed * Time.deltaTime);

        // Check if we're standing on the ground
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Jump
        if (kb.spaceKey.wasPressedThisFrame && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity
        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    void Look()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // New Input System gives raw pixel delta, so scale it down
        // to feel similar to the old Input.GetAxis("Mouse X/Y")
        Vector2 delta = mouse.delta.ReadValue() * 0.1f;
        float mouseX = delta.x * mouseSensitivity;
        float mouseY = delta.y * mouseSensitivity;

        // Rotate player left/right
        transform.Rotate(Vector3.up * mouseX);

        // Rotate camera up/down
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