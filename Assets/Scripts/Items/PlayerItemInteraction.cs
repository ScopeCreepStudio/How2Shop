using UnityEngine;
using UnityEngine.InputSystem;
using How2Shop.Chaos;

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
        [SerializeField] private float throwSpeed = 9f;
        [SerializeField] private float throwLift = 3f;

        [Header("Charged Throw")]
        [SerializeField] private ThrowChargeBar throwChargeBar;
        [SerializeField] private float fullChargeSeconds = 1.5f;
        [SerializeField] private float minimumChargeHoldSeconds = 0.45f;
        private const float minimumChargeTapSeconds = 0.45f;
        [SerializeField] private float maxThrowSpeed = 25f;

        [Header("Hover Outline")]
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private Color outlineColor = new Color(1f, 0.9f, 0.1f, 1f);
        [SerializeField] private float outlineScale = 1.23f;

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
        private ItemSpriteView hoveredItem;
        private GameObject outlineObject;
        private SpriteRenderer outlineRenderer;
        private bool chargingThrow;
        private float throwCharge;
        private float throwChargeStartedAt;

        public bool IsChargingThrow => chargingThrow;
        public float ThrowCharge => throwCharge;

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

            if (throwChargeBar != null)
                throwChargeBar.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (playerCamera == null) return;

            SetHoveredItem(IsHolding ? null : FindLookedAtItem());

            if (!IsHolding)
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb[pickupKey].wasPressedThisFrame)
                    TryPickUp();
            }
            else
            {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb[dropKey].wasPressedThisFrame)
                    Drop();
                else if (kb != null && kb[cartKey].wasPressedThisFrame)
                    TryPutInCart();
                else
                    UpdateThrowInput();
            }
        }

private void UpdateThrowInput()
{
    Mouse mouse = Mouse.current;
    if (mouse == null) return;

    // Charge oscillates between weak and full power while the button is held.
    // This makes timing the release part of the intentionally silly throw.
    if (!chargingThrow && (mouse.rightButton.wasPressedThisFrame || mouse.rightButton.isPressed))
    {
        chargingThrow = true;
        throwCharge = 0f;
        throwChargeStartedAt = Time.unscaledTime;
        if (throwChargeBar != null)
            throwChargeBar.FillAmount = 0f;
    }

    if (!chargingThrow)
        return;

    float heldFor = Mathf.Max(0f, Time.unscaledTime - throwChargeStartedAt);
    if (mouse.rightButton.isPressed)
    {
        throwCharge = Mathf.PingPong(heldFor / Mathf.Max(0.1f, fullChargeSeconds), 1f);
        if (throwChargeBar != null)
            throwChargeBar.FillAmount = throwCharge;
        return;
    }

    if (heldFor >= Mathf.Max(minimumChargeHoldSeconds, minimumChargeTapSeconds))
    {
        throwCharge = Mathf.PingPong(heldFor / Mathf.Max(0.1f, fullChargeSeconds), 1f);
        if (throwChargeBar != null)
            throwChargeBar.FillAmount = throwCharge;
        Throw(Mathf.Lerp(throwSpeed, maxThrowSpeed, throwCharge));
    }
    else
    {
        ResetCharge();
    }
}

        private void TryPickUp()
        {
            var item = FindLookedAtItem();
            if (item != null)
                PickUp(item);
        }

        private ItemSpriteView FindLookedAtItem()
        {
            RaycastHit[] hits = Physics.RaycastAll(playerCamera.position, playerCamera.forward,
                pickupRange, interactMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var item = hit.collider.GetComponentInParent<ItemSpriteView>();
                if (item != null) return item;

                if (hit.collider.isTrigger) continue;
                if (hit.collider.GetComponentInParent<ShoppingCart>() != null) continue;

                return null;
            }

            return null;
        }

        private void SetHoveredItem(ItemSpriteView item)
        {
            if (hoveredItem == item) return;
            hoveredItem = item;

            if (outlineObject != null)
                outlineObject.SetActive(false);

            if (item == null) return;

            var source = item.GetComponent<SpriteRenderer>();
            if (source == null || source.sprite == null) return;

            if (outlineObject == null)
            {
                outlineObject = new GameObject("Hover Outline");
                outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
            }

            outlineObject.transform.SetParent(item.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one * outlineScale;
            outlineRenderer.sprite = source.sprite;
            outlineRenderer.color = Color.white;
            outlineRenderer.sharedMaterial = outlineMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", outlineColor);
            outlineRenderer.SetPropertyBlock(properties);
            outlineRenderer.sortingLayerID = source.sortingLayerID;
            outlineRenderer.sortingOrder = source.sortingOrder - 1;
            outlineRenderer.flipX = source.flipX;
            outlineRenderer.flipY = source.flipY;
            outlineObject.SetActive(true);
        }

        private void OnDisable()
        {
            SetHoveredItem(null);
            ResetCharge();
            if (throwChargeBar != null)
                throwChargeBar.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (throwChargeBar != null && IsHolding)
                throwChargeBar.gameObject.SetActive(true);
        }

        private void PickUp(ItemSpriteView item)
        {
            SetHoveredItem(null);
            bool cartKinematic = false;
            var itemCart = item.GetComponentInParent<ShoppingCart>();
            bool fromCart = itemCart != null && itemCart.TryRemove(item, out cartKinematic);

        ThrownItemImpactRelay priorImpactRelay = item.GetComponent<ThrownItemImpactRelay>();
        if (priorImpactRelay != null)
            priorImpactRelay.ClearThrowOwnership();

            HeldItem = item;
            originalParent = item.transform.parent;
            ResetCharge();
            if (throwChargeBar != null)
                throwChargeBar.gameObject.SetActive(true);

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

            if (GameplaySfxPlayer.Instance != null)
                GameplaySfxPlayer.Instance.PlayPickup(t.position);

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
            EnsureItemImpactRelay(item);

            if (heldBody != null)
            {
                heldBody.isKinematic = heldBodyWasKinematic;
                if (!heldBodyWasKinematic)
                    heldBody.linearVelocity = playerCamera.forward * dropForwardSpeed;
            }

            Debug.Log($"Dropped {DescribeItem(item)}");
            ClearHeldState();
        }

        private void Throw(float speed)
        {
            ItemSpriteView item = HeldItem;
            Transform t = item.transform;

            // Release the item where it is held so it flies out from the camera.
            if (GameplaySfxPlayer.Instance != null)
                GameplaySfxPlayer.Instance.PlayThrow(t.position);

            t.SetParent(null, true);
            EnableHeldColliders();

            Rigidbody body = heldBody;
            if (body == null)
            {
                body = item.gameObject.AddComponent<Rigidbody>();
                body.mass = 0.25f;
            }

            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = playerCamera.forward * speed + Vector3.up * throwLift;
            body.angularVelocity = playerCamera.right * 3f;
            ThrownItemImpactRelay impactRelay = EnsureItemImpactRelay(item);
            impactRelay.MarkThrownByPlayer(gameObject);

            ChaosManager chaosManager = ChaosManager.Instance;
            if (chaosManager != null)
                chaosManager.ReportEvent(ChaosEventType.ItemThrown, item.gameObject, gameObject);

            Debug.Log($"Threw {DescribeItem(item)} at {speed:0.0} m/s");
            ClearHeldState();
        }

        private void ResetCharge()
        {
            chargingThrow = false;
            throwCharge = 0f;
            throwChargeStartedAt = 0f;
            if (throwChargeBar != null)
                throwChargeBar.FillAmount = 0f;
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

private static ThrownItemImpactRelay EnsureItemImpactRelay(ItemSpriteView item)
{
    if (item == null)
        return null;

    ThrownItemImpactRelay relay = item.GetComponent<ThrownItemImpactRelay>();
    if (relay == null)
        relay = item.gameObject.AddComponent<ThrownItemImpactRelay>();
    relay.SetCharacterRagdollEnabled(true);
    return relay;
}

        private void ClearHeldState()
        {
            ResetCharge();
            if (throwChargeBar != null)
                throwChargeBar.gameObject.SetActive(false);
            HeldItem = null;
            heldColliders = null;
            heldBody = null;
            originalParent = null;
        }

        private static string DescribeItem(ItemSpriteView item) =>
            item.Item != null ? item.Item.DisplayName : item.name;
    }
}
