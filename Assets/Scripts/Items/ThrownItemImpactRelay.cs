using UnityEngine;
using How2Shop.Items;

/// <summary>Turns a thrown item impact into a shopper ragdoll hit.</summary>
[DisallowMultipleComponent]
public sealed class ThrownItemImpactRelay : MonoBehaviour
{
    private Rigidbody thrownBody;
    private bool hasHitShopper;
    private bool allowCharacterRagdoll = true;
    private bool hasPlayedImpactSfx;

    private void Awake()
    {
        thrownBody = GetComponent<Rigidbody>();
    }

private void OnCollisionEnter(Collision collision)
{
    if (collision == null || collision.collider == null)
        return;

    if (collision.collider.GetComponentInParent<PlayerItemInteraction>() != null)
        return;

    Vector3 contactPoint = collision.contactCount > 0
        ? collision.GetContact(0).point
        : collision.collider.bounds.center;

    if (!hasPlayedImpactSfx)
    {
        hasPlayedImpactSfx = true;
        if (GameplaySfxPlayer.Instance != null)
            GameplaySfxPlayer.Instance.PlayItemImpact(contactPoint, collision.relativeVelocity.magnitude);
    }

    if (!allowCharacterRagdoll || hasHitShopper)
        return;

    ShopperRagdoll characterRagdoll = collision.collider.GetComponentInParent<ShopperRagdoll>();
    if (characterRagdoll == null)
        return;

    hasHitShopper = true;
    // The projectile moves opposite Unity's contact-to-item relative-velocity vector.
    Vector3 incomingVelocity = -collision.relativeVelocity;
    if (incomingVelocity.sqrMagnitude < 0.01f && thrownBody != null)
        incomingVelocity = thrownBody.linearVelocity;

    characterRagdoll.HandleThrownItemImpact(incomingVelocity, contactPoint);
}


public void SetCharacterRagdollEnabled(bool enabled)
{
    allowCharacterRagdoll = enabled;
}
}
