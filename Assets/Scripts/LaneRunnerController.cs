using UnityEngine;
using UnityEngine.InputSystem;

public class LaneRunnerController : MonoBehaviour
{
    [Header("Lanes")]
    [SerializeField] private float laneDistance = 2.5f; 
    [SerializeField] private float laneChangeSpeed = 20f;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -30f;

    private CharacterController controller;
    private int currentLane = 1;      
    private float centerX;            
    private float verticalVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        centerX = transform.position.x;
    }

    private void Update()
    {
        
        ReadInput(out bool left, out bool right, out bool jump);

        if (left) currentLane = Mathf.Max(0, currentLane - 1);
        if (right) currentLane = Mathf.Min(2, currentLane + 1);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (jump && controller.isGrounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        float targetX = centerX + (currentLane - 1) * laneDistance;
        float newX = Mathf.MoveTowards(transform.position.x, targetX, laneChangeSpeed * Time.deltaTime);
        float deltaX = newX - transform.position.x;

        controller.Move(new Vector3(deltaX, verticalVelocity * Time.deltaTime, 0f));
    }

    private void ReadInput(out bool left, out bool right, out bool jump)
    {
        var kb = Keyboard.current;
        if (kb == null) { left = right = jump = false; return; }
        left  = kb.leftArrowKey.wasPressedThisFrame;
        right = kb.rightArrowKey.wasPressedThisFrame;
        jump  = kb.upArrowKey.wasPressedThisFrame;
    }
    public void StopRunning() => enabled = false;
}
