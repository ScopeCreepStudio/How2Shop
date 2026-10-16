using UnityEngine;

public class RunnerWorld : MonoBehaviour
{
    public static RunnerWorld Instance { get; private set; }

    [SerializeField] private float startSpeed = 12f;
    [SerializeField] private float acceleration = 0.05f;
    [SerializeField] private float maxSpeed = 30f;

    public float Speed { get; private set; }
    public float Distance { get; private set; }
    public bool IsRunning { get; private set; } = true;

    private void Awake()
    {
        Instance = this;
        Speed = startSpeed;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsRunning) return;

        Speed = Mathf.Min(Speed + acceleration * Time.deltaTime, maxSpeed);
        Distance += Speed * Time.deltaTime;
    }

    public void Stop()
    {
        IsRunning = false;
        Speed = 0f;
    }

    public void Restart()
    {
        IsRunning = true;
        Speed = startSpeed;
        Distance = 0f;
    }
}
