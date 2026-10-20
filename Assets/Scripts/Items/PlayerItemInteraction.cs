using UnityEngine;
using UnityEngine.InputSystem;

namespace How2Shop.Items
{

    public sealed class PlayerItemInteraction : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private ShoppingCart cart;

        [Header("Pickup / Drop")]
        [SerializeField] private float pickupRange = 3f;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private float dropDistance = 1.5f;
        [SerializeField] private float dropForwardSpeed = 2f;

        [Header("Cart")]
        [SerializeField] private float cartRange = 3f;

        [Header("Keys")]
        [SerializeField] private Key pickupKey = Key.R;
        [SerializeField] private Key dropKey = Key.Q;
        [SerializeField] private Key cartKey = Key.F;

        public ItemSpriteView HeldItem { get; private set; }
        public bool IsHolding => HeldItem != null;

        private Transform originalParent;
        private Collider[] heldColliders;
        private Rigidbody heldBody;
        private bool heldBodyWasKinematic;

        private void Awake()
        {
            if (playerCamera == null && Camera.main != null)
                playerCamera = Camera.main.transform;

            if (holdPoint == null && playerCamera != null)
            {
                var point = new GameObject("HoldPoint").transform;
                point.SetParent(playerCamera, false);
                point.localPosition = new Vector3(0.4f, -0.3f, 0.8f);
                holdPoint = point;
            }

            if (cart == null)
                cart = FindFirstObjectByType<ShoppingCart>();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || playerCamera == null) return;

            if (!IsHolding)
            {
                if (kb[pickupKey].wasPressedThisFrame)
                    TryPickUp();
            }
            else
            {
                if (kb[dropKey].wasPressedThisFrame)
                    Drop();
                else if (kb[cartKey].wasPressedThisFrame)
                    TryPutInCart();
            }
        }

        private void TryPickUp()
        {
            RaycastHit[] hits = Physics.RaycastAll(playerCamera.position, playerCamera.forward,
                pickupRange, interactMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var item = hit.collider.GetComponentInParent<ItemSpriteView>();
                if (item != null)
                {
                    PickUp(item);
                    return;
                }

                if (hit.collider.isTrigger) continue;
                if (hit.collider.GetComponentInParent<ShoppingCart>() != null) continue;

                return;
            }
        }

        private void PickUp(ItemSpriteView item)
        {
            bool cartKinematic = false;
            var itemCart = item.GetComponentInParent<ShoppingCart>();
            bool fromCart = itemCart != null && itemCart.TryRemove(item, out cartKinematic);

            HeldItem = item;
            originalParent = item.transform.parent;

            heldColliders = item.GetComponentsInChildren<Collider>();
            foreach (var col in heldColliders)
                col.enabled = false;

            heldBody = item.GetComponentInParent<Rigidbody>();
            if (heldBody != null)
            {
                heldBodyWasKinematic = fromCart ? cartKinematic : heldBody.isKinematic;
                heldBody.isKinematic = true;
            }

            Transform t = item.transform;
            t.SetParent(holdPoint, true);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            Debug.Log($"Picked up {DescribeItem(item)}");
        }

        private void Drop()
        {
            ItemSpriteView item = HeldItem;
            Transform t = item.transform;

            Vector3 dropPos = playerCamera.position + playerCamera.forward * dropDistance;
            if (Physics.Raycast(playerCamera.position, playerCamera.forward, out RaycastHit hit,
                    dropDistance, interactMask, QueryTriggerInteraction.Ignore))
            {
                dropPos = hit.point - playerCamera.forward * 0.2f;
            }

            t.SetParent(originalParent, true);
            t.position = dropPos;

            EnableHeldColliders();

            if (heldBody != null)
            {
                heldBody.isKinematic = heldBodyWasKinematic;
                if (!heldBodyWasKinematic)
                    heldBody.linearVelocity = playerCamera.forward * dropForwardSpeed;
            }

            Debug.Log($"Dropped {DescribeItem(item)}");
            ClearHeldState();
        }

        private void TryPutInCart()
        {
            if (cart == null) return;

            if (Vector3.Distance(playerCamera.position, cart.DropPoint) > cartRange)
                return; 

            if (cart.IsFull)
            {
                Debug.Log("The cart is full.");
                return;
            }

            ItemSpriteView item = HeldItem;
            if (!cart.TryAdd(item, heldBodyWasKinematic))
                return;

            EnableHeldColliders();

            Debug.Log($"Put {DescribeItem(item)} in the cart. Total: {cart.TotalPriceLabel}");
            ClearHeldState();
        }

        private void EnableHeldColliders()
        {
            if (heldColliders == null) return;
            foreach (var col in heldColliders)
            {
                if (col != null) col.enabled = true;
            }
        }

        private void ClearHeldState()
        {
            HeldItem = null;
            heldColliders = null;
            heldBody = null;
            originalParent = null;
        }

        private static string DescribeItem(ItemSpriteView item) =>
            item.Item != null ? item.Item.DisplayName : item.name;
    }
}
