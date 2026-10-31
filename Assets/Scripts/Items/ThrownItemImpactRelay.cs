using UnityEngine;
using How2Shop.Items;
using How2Shop.Chaos;

/// <summary>Turns a thrown item impact into a shopper ragdoll hit.</summary>
[DisallowMultipleComponent]
public sealed class ThrownItemImpactRelay : MonoBehaviour
{
    private Rigidbody thrownBody;
    private bool hasHitShopper;
    private bool allowCharacterRagdoll = true;
    private bool hasPlayedImpactSfx;
    [SerializeField, Min(0f)] private float playerThrowAttributionSeconds = 12f;
    private GameObject playerThrowOwner;
    private float playerThrowOwnershipExpiresAt;

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

    float impactSpeed = collision.relativeVelocity.magnitude;
    ShopperRagdoll characterRagdoll = collision.collider.GetComponentInParent<ShopperRagdoll>();
    if (!hasPlayedImpactSfx)
    {
        hasPlayedImpactSfx = true;
        if (GameplaySfxPlayer.Instance != null)
            GameplaySfxPlayer.Instance.PlayItemImpact(contactPoint, impactSpeed);
    }

    if (playerThrowOwner != null && Time.time <= playerThrowOwnershipExpiresAt)
    {
        ChaosManager chaosManager = ChaosManager.Instance;
        ItemSpriteView thrownProduct = GetComponent<ItemSpriteView>();
        if (thrownProduct == null)
            thrownProduct = GetComponentInChildren<ItemSpriteView>();

        if (thrownProduct != null && thrownProduct.Item != null && thrownProduct.Item.HasTag("fragile"))
        {
            ChaosImpactTarget fragileTarget = thrownProduct.GetComponent<ChaosImpactTarget>();
            if (fragileTarget == null)
                fragileTarget = thrownProduct.gameObject.AddComponent<ChaosImpactTarget>();

            float breakSpeed = chaosManager != null
                ? chaosManager.FragileProductBreakImpactSpeed
                : ChaosImpactTarget.DefaultFragileBreakImpactSpeed;
            fragileTarget.Configure(ChaosEventType.BreakProduct, ChaosImpactSource.ThrownItem,
                breakSpeed, true);
            fragileTarget.TryReportImpact(ChaosImpactSource.ThrownItem, playerThrowOwner, impactSpeed);
        }

        if (chaosManager != null)
        {
            if (characterRagdoll != null)
            {
                chaosManager.ReportEvent(
                    ChaosEventType.ItemHitShopper,
                    characterRagdoll.gameObject,
                    playerThrowOwner,
                    impactSpeed);
            }
            else
            {
                ChaosImpactTarget target = collision.collider.GetComponentInParent<ChaosImpactTarget>();
                if (target != null)
                    target.TryReportImpact(ChaosImpactSource.ThrownItem, playerThrowOwner, impactSpeed);
            }
        }
    }

    if (!allowCharacterRagdoll || hasHitShopper)
        return;

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

public void MarkThrownByPlayer(GameObject owner)
{
    playerThrowOwner = owner;
    playerThrowOwnershipExpiresAt = owner != null ? Time.time + playerThrowAttributionSeconds : 0f;
}

public void ClearThrowOwnership()
{
    playerThrowOwner = null;
    playerThrowOwnershipExpiresAt = 0f;
}
}
