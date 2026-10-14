using System;
using System.Collections.Generic;
using UnityEngine;

namespace How2Shop.Items
{
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "How2Shop/Item Catalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private List<ItemDefinition> items = new List<ItemDefinition>();
        private Dictionary<string, ItemDefinition> itemsById;

        public IReadOnlyList<ItemDefinition> Items => items;

        public bool TryGetById(string id, out ItemDefinition item)
        {
            item = null;
            if (string.IsNullOrEmpty(id))
                return false;

            if (itemsById == null)
                BuildIndex();

            return itemsById.TryGetValue(id, out item);
        }

        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    errors.Add($"Entry {i + 1} has no item asset.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.Id))
                    errors.Add($"{item.name} has no ID.");
                else if (!seen.Add(item.Id))
                    errors.Add($"Duplicate item ID: {item.Id}.");

                if (string.IsNullOrWhiteSpace(item.DisplayName))
                    errors.Add($"{item.name} has no display name.");
                if (item.PricePence < 0)
                    errors.Add($"{item.name} has a negative price.");
                if (item.WorldSprite == null)
                    errors.Add($"{item.name} has no 2D sprite.");
            }

            return errors;
        }

        private void BuildIndex()
        {
            var index = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.Id))
                    index.TryAdd(item.Id, item);
            }

            itemsById = index;
        }

        private void OnEnable() => itemsById = null;
        private void OnValidate() => itemsById = null;
    }
}
