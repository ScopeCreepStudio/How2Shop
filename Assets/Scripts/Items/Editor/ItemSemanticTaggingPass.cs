using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace How2Shop.Items.Editor
{
    public static class ItemSemanticTaggingPass
    {
        private const string ColaPath = "Assets/ITEMS_1/Drinks/Data/Cola.asset";
        private const string DietColaPath = "Assets/ITEMS_1/Drinks/Data/DietCola.asset";
        private const string ShelfCatalogPath = "Assets/Shelf_Items/Data/ShelfItemsCatalog.asset";

        [MenuItem("How2Shop/Items/Populate Semantic Tags")]
        public static void Apply()
        {
            ItemDefinition dietCola = EnsureDietCola();
            if (dietCola == null)
                return;

            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition");
            int updated = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null)
                    continue;

                List<string> tags = new List<string>();
                HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (item.Tags != null)
                {
                    for (int tagIndex = 0; tagIndex < item.Tags.Count; tagIndex++)
                        Add(tags, known, item.Tags[tagIndex]);
                }

                AddSemanticTags(item, path, tags, known);
                if (WriteTags(item, tags))
                    updated++;
            }

            AddToShelfCatalog(dietCola);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Updated semantic tags for " + updated + " item definitions. Diet Cola is in the shelf catalog.");
        }

        private static ItemDefinition EnsureDietCola()
        {
            ItemDefinition source = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ColaPath);
            if (source == null || source.WorldSprite == null)
            {
                Debug.LogError("The regular Cola item or its world sprite is missing: " + ColaPath);
                return null;
            }

            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(DietColaPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = "Diet Cola";
                AssetDatabase.CreateAsset(item, DietColaPath);
            }

            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = "shelf_items_dietcola";
            serialized.FindProperty("displayName").stringValue = "Diet Cola";
            serialized.FindProperty("pricePence").intValue = source.PricePence;
            serialized.FindProperty("icon").objectReferenceValue = source.WorldSprite;
            serialized.FindProperty("worldSprite").objectReferenceValue = source.WorldSprite;

            List<string> dietTags = new List<string>
            {
                "drinks", "drink", "soft-drink", "carbonated", "cola",
                "diet", "low-calorie", "light", "food", "supermarket"
            };
            WriteTags(serialized, dietTags);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static void AddSemanticTags(
            ItemDefinition item,
            string path,
            List<string> tags,
            HashSet<string> known)
        {
            string displayName = item.DisplayName ?? item.name;
            string name = displayName.ToLowerInvariant();
            string assetPath = path.ToLowerInvariant();

            Add(tags, known, "product");
            Add(tags, known, "item-" + Slug(displayName));

            string[] words = Regex.Split(name, "[^a-z0-9]+");
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 1)
                    Add(tags, known, words[i]);
            }

            if (assetPath.Contains("/drinks/") || assetPath.Contains("/tea and coffee/") ||
                name.Contains("cola") || name.Contains("juice") || name.Contains("water") ||
                name == "tea" || name.StartsWith("tea ") || name.Contains(" tea ") ||
                name.Contains("coffee") || Has(tags, "drinks"))
            {
                Add(tags, known, "drink");
            }

            if (name.Contains("cola") || name.Contains("soda"))
            {
                Add(tags, known, "drink");
                Add(tags, known, "cola");
                Add(tags, known, "soft-drink");
                Add(tags, known, "carbonated");
            }

            if (name.Contains("diet") || name.Contains("zero sugar") ||
                name.Contains("sugar free") || name.Contains("low calorie"))
            {
                Add(tags, known, "diet");
                Add(tags, known, "low-calorie");
                Add(tags, known, "light");
            }

            if (!assetPath.Contains("/cleaningproducts/") && !assetPath.Contains("/newspapers/"))
            {
                Add(tags, known, "food");
            }

            if (assetPath.Contains("/dairy and eggs/") || name == "milk" || name == "milk 1l" ||
                name.Contains("yogurt") || name.Contains("cheese") || name == "butter")
            {
                Add(tags, known, "dairy");
                Add(tags, known, "chilled");
            }

            if (assetPath.Contains("/fruit/") || assetPath.Contains("/vegetables/"))
            {
                Add(tags, known, "produce");
                Add(tags, known, assetPath.Contains("/fruit/") ? "fruit" : "vegetable");
            }

            if (assetPath.Contains("/frozen items/"))
                Add(tags, known, "frozen");
            if (assetPath.Contains("/tinned foods/"))
                Add(tags, known, "tinned");
            if (assetPath.Contains("/cleaningproducts/"))
            {
                Add(tags, known, "cleaning");
                Add(tags, known, "household");
                Add(tags, known, "non-food");
            }
            if (assetPath.Contains("/newspapers/"))
            {
                Add(tags, known, "reading");
                Add(tags, known, "paper");
                Add(tags, known, "non-food");
            }

            if (name == "tea" || name.StartsWith("tea ") || name.Contains(" tea ") ||
                name.Contains("chocolate bar") ||
                name.Contains("crisps") || name.Contains("biscuit") ||
                name.Contains("cookie") || name.Contains("wafer") ||
                name.Contains("doughnut") || name.Contains("muffin") ||
                name.Contains("croissant") || name.Contains("pretzel"))
            {
                Add(tags, known, "lightweight");
            }
        }

        private static string Slug(string value)
        {
            return Regex.Replace((value ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        }

        private static bool Has(List<string> tags, string value)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void Add(List<string> tags, HashSet<string> known, string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag) && known.Add(tag.Trim()))
                tags.Add(tag.Trim().ToLowerInvariant());
        }

        private static bool WriteTags(ItemDefinition item, List<string> tags)
        {
            SerializedObject serialized = new SerializedObject(item);
            WriteTags(serialized, tags);
            bool changed = serialized.ApplyModifiedPropertiesWithoutUndo();
            if (changed)
                EditorUtility.SetDirty(item);
            return changed;
        }

        private static void WriteTags(SerializedObject serialized, List<string> tags)
        {
            SerializedProperty array = serialized.FindProperty("tags");
            array.arraySize = tags.Count;
            for (int i = 0; i < tags.Count; i++)
                array.GetArrayElementAtIndex(i).stringValue = tags[i];
        }

        private static void AddToShelfCatalog(ItemDefinition item)
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ShelfCatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("Shelf item catalog not found: " + ShelfCatalogPath);
                return;
            }

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty items = serialized.FindProperty("items");
            for (int i = 0; i < items.arraySize; i++)
            {
                if (items.GetArrayElementAtIndex(i).objectReferenceValue == item)
                    return;
            }

            int index = items.arraySize;
            items.InsertArrayElementAtIndex(index);
            items.GetArrayElementAtIndex(index).objectReferenceValue = item;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
    }
}
