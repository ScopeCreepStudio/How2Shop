using UnityEngine;

/// <summary>Forwards collisions against a shopper's cart to its ragdoll controller.</summary>
[DisallowMultipleComponent]
public sealed class ShopperCartImpactRelay : MonoBehaviour
{
    private ShopperRagdoll shopperRagdoll;

    public void Initialize(ShopperRagdoll owner)
    {
        shopperRagdoll = owner;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (shopperRagdoll != null)
            shopperRagdoll.HandleCartImpact(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (shopperRagdoll != null)
            shopperRagdoll.HandleCartTriggerImpact(other);
    }
}
