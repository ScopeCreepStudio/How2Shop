using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace How2Shop.Items
{
    public sealed class ShoppingCart : MonoBehaviour
    {
        private sealed class Entry
        {
            public bool BodyWasKinematic;
            public Collider[] Colliders;
            public bool[] WasTrigger;
        }

        [Header("Storage")]
        [SerializeField] private int maxItems = 20;
        [Tooltip("Local-space box inside the basket where items are placed. Select the cart to see it.")]
        [SerializeField] private Vector3 volumeCenter = new Vector3(0f, 0.6f, 0f);
        [SerializeField] private Vector3 volumeSize = new Vector3(0.6f, 0.2f, 0.8f);

        private readonly List<ItemSpriteView> items = new List<ItemSpriteView>();
        private readonly Dictionary<ItemSpriteView, Entry> entries = new Dictionary<ItemSpriteView, Entry>();

        public event Action Changed;

        public IReadOnlyList<ItemSpriteView> Items => items;
        public int ItemCount => items.Count;
        public bool IsFull => items.Count >= maxItems;
        public Vector3 DropPoint => transform.TransformPoint(volumeCenter);

        public int TotalPricePence
        {
            get
            {
                int total = 0;
                foreach (var item in items)
                {
                    if (item != null && item.Item != null)
                        total += item.Item.PricePence;
                }
                return total;
            }
        }

        public string TotalPriceLabel =>
            (TotalPricePence / 100m).ToString("C", CultureInfo.GetCultureInfo("en-GB"));

        public bool Contains(ItemSpriteView item) => items.Contains(item);

        public bool TryAdd(ItemSpriteView item, bool bodyWasKinematic)
        {
            if (item == null || IsFull || items.Contains(item)) return false;

            var entry = new Entry
            {
                BodyWasKinematic = bodyWasKinematic,
                Colliders = item.GetComponentsInChildren<Collider>()
            };
            entry.WasTrigger = new bool[entry.Colliders.Length];

            for (int i = 0; i < entry.Colliders.Length; i++)
            {
                entry.WasTrigger[i] = entry.Colliders[i].isTrigger;
                entry.Colliders[i].isTrigger = true;
                entry.Colliders[i].enabled = true;
            }

            items.Add(item);
            entries[item] = entry;

            Transform t = item.transform;
            t.SetParent(transform, true);
            t.localPosition = new Vector3(
                volumeCenter.x + UnityEngine.Random.Range(-volumeSize.x, volumeSize.x) * 0.5f,
                volumeCenter.y + UnityEngine.Random.Range(-volumeSize.y, volumeSize.y) * 0.5f,
                volumeCenter.z + UnityEngine.Random.Range(-volumeSize.z, volumeSize.z) * 0.5f);
            t.localRotation = Quaternion.identity;

            Changed?.Invoke();
            return true;
        }
        public bool TryRemove(ItemSpriteView item, out bool bodyWasKinematic)
        {
            bodyWasKinematic = false;
            if (item == null || !items.Remove(item)) return false;

            if (entries.TryGetValue(item, out Entry entry))
            {
                bodyWasKinematic = entry.BodyWasKinematic;
                for (int i = 0; i < entry.Colliders.Length; i++)
                {
                    if (entry.Colliders[i] != null)
                        entry.Colliders[i].isTrigger = entry.WasTrigger[i];
                }
                entries.Remove(item);
            }

            item.transform.SetParent(null, true);

            Changed?.Invoke();
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(volumeCenter, volumeSize);
        }
    }
}
