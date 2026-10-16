using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;


public class LaneRunnerController : MonoBehaviour
{
    [Header("Lanes")]
    [SerializeField] private float laneDistance = 2.5f;
    [SerializeField] private float laneChangeSpeed = 20f;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -30f;

    [Header("Death")]
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private float restartDelay = 0.4f;
    [Tooltip("How far below an obstacle's top the player's feet can be and still count as landing on it.")]
    [SerializeField] private float landingDetect = 0.3f;

    public bool IsDead { get; private set; }


    public event Action Died;

    private CharacterController controller;
    private int currentLane = 1;      // 0 = left, 1 = middle, 2 = right
    private float centerX;            // X position of the middle lane
    private float verticalVelocity;
    private float deathTime;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        centerX = transform.position.x;
    }

    private void Update()
    {
        ReadInput(out bool left, out bool right, out bool jump);

        if (IsDead)
        {
            if ((left || right || jump) && Time.time >= deathTime + restartDelay)
            {
                Restart();
                return;
            }

            left = right = jump = false;
        }


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

    private void OnTriggerEnter(Collider other)
    {
        if (IsDead || !other.CompareTag(obstacleTag)) return;

        float feetY = controller.bounds.min.y;
        float obstacleTop = other.bounds.max.y;

        if (feetY >= obstacleTop - landingDetect) return;

        Die();
    }

    private void Die()
    {
        IsDead = true;
        deathTime = Time.time;

        if (RunnerWorld.Instance != null)
            RunnerWorld.Instance.Stop();

        Died?.Invoke();
    }

    private void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void ReadInput(out bool left, out bool right, out bool jump)
    {
        var kb = Keyboard.current;
        if (kb == null) { left = right = jump = false; return; }
        left = kb.leftArrowKey.wasPressedThisFrame;
        right = kb.rightArrowKey.wasPressedThisFrame;
        jump = kb.upArrowKey.wasPressedThisFrame;
    }
}