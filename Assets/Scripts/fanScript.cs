using UnityEngine;

public class FanRotation : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float minSpeed;
    [SerializeField] private float maxSpeed;
    [SerializeField] private float dentSpeed;
    [SerializeField] private float rotationAngle = 10f;
    [SerializeField] private bool randomSpeed = true;

    private float rotationSpeed;
    private float elapsedTime;
    private Quaternion initialLocalRotation;

    void Start()
    {
        rotationSpeed = randomSpeed
            ? Random.Range(minSpeed, maxSpeed)
            : maxSpeed;
        dentSpeed = maxSpeed * 0.8f; 
        initialLocalRotation = transform.localRotation;
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

        float rotation = rotationSpeed * elapsedTime;
        float wobble = rotationSpeed > dentSpeed
            ? Mathf.Sin(rotation * Mathf.Deg2Rad) * rotationAngle
            : 0f;

        transform.localRotation = initialLocalRotation * Quaternion.Euler(wobble, rotation, 0f);
    }
}