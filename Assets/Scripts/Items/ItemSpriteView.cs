using UnityEngine;

namespace How2Shop.Items
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ItemSpriteView : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;

        public ItemDefinition Item => item;

        public void SetItem(ItemDefinition definition)
        {
            item = definition;
            ApplySprite();
            if (Application.isPlaying)
                EnsurePickupCollider();
        }

        private void Awake()
        {
            ApplySprite();
            EnsurePickupCollider();
        }
        private void OnValidate() => ApplySprite();

        private void ApplySprite()
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = item != null ? item.WorldSprite : null;
        }

        private void EnsurePickupCollider()
        {
            if (GetComponent<Collider>() != null)
                return;

            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null)
                return;

            var pickupCollider = gameObject.AddComponent<BoxCollider>();
            Bounds bounds = spriteRenderer.sprite.bounds;
            Vector3 size = bounds.size;
            size.x = Mathf.Max(size.x, 0.08f);
            size.y = Mathf.Max(size.y, 0.08f);
            size.z = Mathf.Max(size.z, 0.08f);
            pickupCollider.center = bounds.center;
            pickupCollider.size = size;
            pickupCollider.isTrigger = true;
        }
    }
}
