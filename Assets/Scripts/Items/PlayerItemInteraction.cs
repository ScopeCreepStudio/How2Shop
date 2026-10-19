using UnityEngine;
using UnityEngine.InputSystem;

namespace How2Shop.Items
{
    public sealed class PlayerItemInteraction : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private Transform holdPoint;

        [Header("Pickup / Drop")]
        [SerializeField] private float pickupRange = 3f;
        [SerializeField] private LayerMask interactMask = ~0;
        [SerializeField] private float dropDistance = 1.5f;
        [SerializeField] private float dropForwardSpeed = 2f;

        [Header("Keys")]
        [SerializeField] private Key pickupKey = Key.E;
        [SerializeField] private Key dropKey = Key.Q;

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
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || playerCamera == null) return;

            if (kb[pickupKey].wasPressedThisFrame && !IsHolding)
                TryPickUp();
            else if (kb[dropKey].wasPressedThisFrame && IsHolding)
                Drop();
        }

        private void TryPickUp()
        {
            if (!Physics.Raycast(playerCamera.position, playerCamera.forward, out RaycastHit hit,
                    pickupRange, interactMask, QueryTriggerInteraction.Ignore))
                return;

            var item = hit.collider.GetComponentInParent<ItemSpriteView>();
            if (item == null) return;

            PickUp(item);
        }

        private void PickUp(ItemSpriteView item)
        {
            HeldItem = item;
            originalParent = item.transform.parent;

            heldColliders = item.GetComponentsInChildren<Collider>();
            foreach (var col in heldColliders)
                col.enabled = false;

            heldBody = item.GetComponentInParent<Rigidbody>();
            if (heldBody != null)
            {
                heldBodyWasKinematic = heldBody.isKinematic;
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

            foreach (var col in heldColliders)
            {
                if (col != null) col.enabled = true;
            }

            if (heldBody != null)
            {
                heldBody.isKinematic = heldBodyWasKinematic;
                if (!heldBodyWasKinematic)
                    heldBody.linearVelocity = playerCamera.forward * dropForwardSpeed;
            }

            Debug.Log($"Dropped {DescribeItem(item)}");

            HeldItem = null;
            heldColliders = null;
            heldBody = null;
            originalParent = null;
        }

        private static string DescribeItem(ItemSpriteView item) =>
            item.Item != null ? item.Item.DisplayName : item.name;
    }
}
