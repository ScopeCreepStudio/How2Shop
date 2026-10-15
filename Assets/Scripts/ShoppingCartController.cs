
using UnityEngine;

public class ShoppingCartFollow : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Cart Settings")]
    public float forwardDistance = 1.5f;
    public float heightOffset = 0f;
    public float followSpeed = 10f;
    public float rotationSpeed = 10f;

    void Update()
    {
        if (player == null) return;

        Vector3 targetPosition =
            player.position +
            player.forward * forwardDistance +
            Vector3.up * heightOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        Quaternion targetRotation =
            Quaternion.LookRotation(player.forward, Vector3.up);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}
