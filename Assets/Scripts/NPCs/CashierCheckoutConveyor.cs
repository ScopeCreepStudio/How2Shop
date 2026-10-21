using System.Collections;
using System.Collections.Generic;
using How2Shop.Items;
using UnityEngine;

[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class CashierCheckoutConveyor : MonoBehaviour
{
    [SerializeField] private float sweepSeconds = 0.22f;
    [SerializeField] private float launchSpeed = 15f;
    [SerializeField] private float launchLift = 1.4f;
    [SerializeField] private float launchSpin = 5f;

    private readonly HashSet<ItemSpriteView> scannedItems = new HashSet<ItemSpriteView>();
    private readonly Queue<ItemSpriteView> waitingItems = new Queue<ItemSpriteView>();
    private Vector3 launchDirection = Vector3.back;
    private float sweepDistance = 2f;
    private CashierScanAnimation cashierAnimation;
    private bool processingQueue;

public void Configure(Vector3 direction, float distance, CashierScanAnimation animationController)
{
    launchDirection = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
    if (launchDirection.sqrMagnitude < 0.001f)
        launchDirection = Vector3.back;

    sweepDistance = Mathf.Max(1.2f, distance);
    cashierAnimation = animationController;
}

private void Awake()
    {
        if (cashierAnimation == null)
            cashierAnimation = GetComponentInParent<CashierScanAnimation>();

        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

private void OnTriggerEnter(Collider other)
{
    ItemSpriteView item = other.GetComponentInParent<ItemSpriteView>();
    if (item == null)
    {
        Rigidbody attachedBody = other.attachedRigidbody;
        if (attachedBody != null)
            item = attachedBody.GetComponentInChildren<ItemSpriteView>();
    }

    if (item == null || item.transform.IsChildOf(transform) || !scannedItems.Add(item))
        return;

    // Cashier-launched products still make impact sounds, but only a player-thrown
    // item should knock over a character.
    ThrownItemImpactRelay existingRelay = item.GetComponent<ThrownItemImpactRelay>();
    if (existingRelay != null)
        existingRelay.SetCharacterRagdollEnabled(false);

    waitingItems.Enqueue(item);
    if (!processingQueue)
        StartCoroutine(ProcessQueue());
}

    private IEnumerator ProcessQueue()
    {
        processingQueue = true;
        while (waitingItems.Count > 0)
        {
            ItemSpriteView item = waitingItems.Dequeue();
            if (item != null)
                yield return StartCoroutine(SwipeAndLaunch(item));
        }

        processingQueue = false;
    }

private IEnumerator SwipeAndLaunch(ItemSpriteView item)
{
    if (item == null)
        yield break;

    Transform itemTransform = item.transform;
    Rigidbody itemBody = FindItemBody(item);
    if (itemBody == null)
    {
        itemBody = item.gameObject.AddComponent<Rigidbody>();
        itemBody.mass = 0.25f;
    }

    if (itemTransform.parent != null)
        itemTransform.SetParent(null, true);

    Vector3 bodyOffset = itemBody.transform.position - itemTransform.position;
    Quaternion bodyRotationOffset = Quaternion.Inverse(itemTransform.rotation) * itemBody.rotation;

    Collider[] colliders = item.GetComponentsInChildren<Collider>(true);
    var colliderEnabled = new List<bool>(colliders.Length);
    foreach (Collider itemCollider in colliders)
    {
        colliderEnabled.Add(itemCollider != null && itemCollider.enabled);
        if (itemCollider != null)
            itemCollider.enabled = false;
    }

    itemBody.isKinematic = true;
    itemBody.useGravity = false;

    // The first product creeps along, then each scan gets more absurdly fast.
    float insanity = Mathf.InverseLerp(1f, 5f, scannedItems.Count);
    float duration = Mathf.Lerp(1.15f, Mathf.Max(0.14f, sweepSeconds), insanity);
    float finalLaunchSpeed = Mathf.Lerp(launchSpeed * 0.6f, Mathf.Max(launchSpeed, 42f), insanity);
    if (cashierAnimation != null)
        cashierAnimation.PlaySwipe(launchDirection, duration);

    if (GameplaySfxPlayer.Instance != null)
        GameplaySfxPlayer.Instance.PlayCashierScan(itemTransform.position);

    Vector3 start = itemTransform.position;
    Vector3 end = start + launchDirection * sweepDistance;
    Quaternion startRotation = itemTransform.rotation;
    float elapsed = 0f;

    while (elapsed < duration && itemTransform != null)
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = t * t * t;
        Vector3 position = Vector3.LerpUnclamped(start, end, eased) +
            Vector3.up * (0.12f * Mathf.Sin(t * Mathf.PI));
        itemTransform.SetPositionAndRotation(
            position,
            startRotation * Quaternion.Euler(0f, t * 260f, t * 180f));
        itemBody.position = position + bodyOffset;
        itemBody.rotation = itemTransform.rotation * bodyRotationOffset;
        yield return null;
    }

    if (itemTransform == null || itemBody == null)
        yield break;

    for (int i = 0; i < colliders.Length; i++)
    {
        if (colliders[i] != null)
            colliders[i].enabled = colliderEnabled[i];
    }

    IgnoreCashierCollisions(colliders);

        
itemBody.isKinematic = false;
    itemBody.useGravity = true;
    itemBody.constraints = RigidbodyConstraints.None;
    itemBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    itemBody.linearVelocity = launchDirection * finalLaunchSpeed + Vector3.up * launchLift;
    itemBody.angularVelocity = new Vector3(
        Random.Range(-launchSpin, launchSpin),
        Random.Range(-launchSpin, launchSpin),
        Random.Range(-launchSpin, launchSpin));

    ThrownItemImpactRelay impactRelay = item.GetComponent<ThrownItemImpactRelay>();
    if (impactRelay == null)
        impactRelay = item.gameObject.AddComponent<ThrownItemImpactRelay>();
    impactRelay.SetCharacterRagdollEnabled(false);
}

    private static Rigidbody FindItemBody(ItemSpriteView item)
    {
        Rigidbody body = item.GetComponent<Rigidbody>();
        if (body != null)
            return body;

        Rigidbody[] childBodies = item.GetComponentsInChildren<Rigidbody>(true);
        return childBodies.Length > 0 ? childBodies[0] : null;
    }


private void IgnoreCashierCollisions(Collider[] itemColliders)
{
    if (cashierAnimation == null || itemColliders == null)
        return;

    Collider scanTrigger = GetComponent<Collider>();
    var launchPathColliders = new List<Collider>();
    launchPathColliders.AddRange(cashierAnimation.GetComponentsInChildren<Collider>(true));

    Transform nearestCheckout = null;
    float nearestDistance = float.PositiveInfinity;
    foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>())
    {
        if (candidate.name != "Cashier1")
            continue;

        float distance = (candidate.position - cashierAnimation.transform.position).sqrMagnitude;
        if (distance < nearestDistance)
        {
            nearestDistance = distance;
            nearestCheckout = candidate;
        }
    }

    if (nearestCheckout != null)
        launchPathColliders.AddRange(nearestCheckout.GetComponentsInChildren<Collider>(true));

    foreach (Collider itemCollider in itemColliders)
    {
        if (itemCollider == null)
            continue;

        foreach (Collider pathCollider in launchPathColliders)
        {
            if (pathCollider == null || pathCollider == scanTrigger || pathCollider == itemCollider)
                continue;

            Physics.IgnoreCollision(itemCollider, pathCollider, true);
        }
    }
}
}
