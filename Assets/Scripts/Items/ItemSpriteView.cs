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
        }

        private void Awake() => ApplySprite();
        private void OnValidate() => ApplySprite();

        private void ApplySprite()
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = item != null ? item.WorldSprite : null;
        }
    }
}
