using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace How2Shop.Items
{
    [CreateAssetMenu(fileName = "NewItem", menuName = "How2Shop/Item Definition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [Min(0)] [SerializeField] private int pricePence;
        [SerializeField] private List<string> tags = new List<string>();
        [Tooltip("Sprite shown in menus and inventory.")]
        [SerializeField] private Sprite icon;
        [Tooltip("Sprite shown by a SpriteRenderer in the world. Uses the icon if empty.")]
        [SerializeField] private Sprite worldSprite;

        public string Id => id;
        public string DisplayName => displayName;
        public int PricePence => pricePence;
        public string PriceLabel => (pricePence / 100m).ToString("C", CultureInfo.GetCultureInfo("en-GB"));
        public IReadOnlyList<string> Tags => tags;
        public Sprite Icon => icon != null ? icon : worldSprite;
        public Sprite WorldSprite => worldSprite != null ? worldSprite : icon;

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null)
                return false;

            foreach (var itemTag in tags)
            {
                if (string.Equals(itemTag, tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
