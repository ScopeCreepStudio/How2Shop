using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace How2Shop.Items.Editor
{
    public static class InstallMisreadProducts
    {
        private const string SpriteRoot = "Assets/Art/ProductSprites/";
        private const string DataRoot = "Assets/ITEMS_1/Nonsense/Data/";
        private const string PrefabRoot = "Assets/ITEMS_1/Nonsense/Prefabs/";
        private const string MaterialRoot = "Assets/ITEMS_1/Nonsense/Materials/";
        private const string ScenePath = "Assets/Scenes/MainScene.unity";

        private sealed class Product
        {
            public string slug, name, replaces;
            public int price;
            public string[] tags;
            public Product(string slug, string name, string replaces, int price, params string[] tags)
            { this.slug = slug; this.name = name; this.replaces = replaces; this.price = price; this.tags = tags; }
        }

        private static readonly Product[] Products =
        {
            new Product("pocket-helicopter", "Pocket Helicopter", "Coffee", 399, "toy-helicopter", "toy", "flying", "lift", "non-food"),
            new Product("canned-sunshine", "Canned Sunshine", "Tomato Soup", 189, "sunshine", "light", "non-food"),
            new Product("wooden-letter-t", "Wooden Letter T", "Tea", 99, "letter-t", "wooden", "non-food"),
            new Product("toy-submarine", "Toy Submarine", "Tuna", 249, "toy-submarine", "toy", "sea", "non-food")
        };

        [MenuItem("How2Shop/Items/Install Misread Products")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play Mode before installing shelf products.");
            Directory.CreateDirectory(MaterialRoot);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Material template = AssetDatabase.LoadAssetAtPath<Material>("Assets/ITEMS_1/Tea and Coffee/Materials/Coffee.mat");
            if (template == null) throw new System.InvalidOperationException("Shelf product material is missing.");

            var definitions = new Dictionary<string, ItemDefinition>();
            var materials = new Dictionary<string, Material>();
            foreach (Product product in Products)
            {
                string imagePath = SpriteRoot + product.slug + ".png";
                TextureImporter importer = AssetImporter.GetAtPath(imagePath) as TextureImporter;
                if (importer == null) throw new System.InvalidOperationException("Missing sprite: " + imagePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                if (sprite == null) throw new System.InvalidOperationException("Sprite import failed: " + imagePath);

                string dataPath = DataRoot + product.slug + ".asset";
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(dataPath);
                if (item == null)
                {
                    item = ScriptableObject.CreateInstance<ItemDefinition>();
                    item.name = product.name;
                    AssetDatabase.CreateAsset(item, dataPath);
                }
                SerializedObject serialized = new SerializedObject(item);
                serialized.FindProperty("id").stringValue = "misread_" + product.slug;
                serialized.FindProperty("displayName").stringValue = product.name;
                serialized.FindProperty("pricePence").intValue = product.price;
                serialized.FindProperty("icon").objectReferenceValue = sprite;
                serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
                SerializedProperty tags = serialized.FindProperty("tags");
                tags.arraySize = product.tags.Length;
                for (int i = 0; i < product.tags.Length; i++) tags.GetArrayElementAtIndex(i).stringValue = product.tags[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);

                string materialPath = MaterialRoot + product.slug + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(template) { name = product.name };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                if (material.HasProperty("_Artwork")) material.SetTexture("_Artwork", sprite.texture);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", sprite.texture);
                EditorUtility.SetDirty(material);
                definitions.Add(product.slug, item);
                materials.Add(product.slug, material);
                CreatePrefab(product, item, material);
            }
            AddToCatalog(definitions.Values);
            AssetDatabase.SaveAssets();

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var allViews = new List<ItemSpriteView>();
            foreach (GameObject root in scene.GetRootGameObjects())
                allViews.AddRange(root.GetComponentsInChildren<ItemSpriteView>(true));
            var messages = new List<string>();
            foreach (Product product in Products)
            {
                ItemDefinition item = definitions[product.slug];
                int existing = allViews.Count(v => v != null && v.Item == item);
                var candidates = allViews.Where(v => v != null && v.gameObject.activeInHierarchy && v.transform.parent != null &&
                    v.transform.parent.name.StartsWith("Stock - NewScene") && v.Item != null &&
                    v.Item.DisplayName == product.replaces && v.transform.position.y >= 0.65f &&
                    v.transform.position.y <= 1.7f && v.GetComponent<SpriteRenderer>() != null)
                    .OrderBy(v => v.transform.position.x).ThenBy(v => v.transform.position.z).ThenBy(v => v.transform.position.y).ToList();
                int needed = Mathf.Min(Mathf.Max(0, 72 - existing), candidates.Count);
                for (int i = 0; i < needed; i++)
                {
                    int index = (i + 1) * candidates.Count / (needed + 1);
                    ReplaceOnShelf(candidates[index], item, materials[product.slug]);
                }
                messages.Add(product.name + ": " + needed + " shelf items replaced (" + existing + " already present)");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new System.InvalidOperationException("Could not save MainScene.");
            if (opened) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("Misread products installed: " + string.Join("; ", messages));
        }

        private static void ReplaceOnShelf(ItemSpriteView view, ItemDefinition item, Material material)
        {
            SpriteRenderer renderer = view.GetComponent<SpriteRenderer>();
            float oldBottom = renderer.bounds.min.y;
            float oldWidth = Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z);
            float desiredSize = Mathf.Clamp(Mathf.Max(oldWidth, renderer.bounds.size.y) * 1.12f, 0.22f, 0.24f);
            view.SetItem(item);
            renderer.sharedMaterial = material;
            float currentSize = Mathf.Max(renderer.bounds.size.y, Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.z));
            if (currentSize > 0.001f) view.transform.localScale *= desiredSize / currentSize;
            Vector3 position = view.transform.position;
            position.y += oldBottom - renderer.bounds.min.y;
            view.transform.position = position;
            BoxCollider box = view.GetComponent<BoxCollider>();
            if (box == null) box = view.gameObject.AddComponent<BoxCollider>();
            box.center = item.WorldSprite.bounds.center;
            Vector3 size = item.WorldSprite.bounds.size;
            size.z = Mathf.Max(0.08f, size.z);
            box.size = size;
            box.isTrigger = true;
            EditorUtility.SetDirty(view);
            EditorUtility.SetDirty(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(view.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(box);
        }

        private static void CreatePrefab(Product product, ItemDefinition item, Material material)
        {
            GameObject root = new GameObject(product.name);
            try
            {
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = item.WorldSprite;
                renderer.sharedMaterial = material;
                root.AddComponent<ItemSpriteView>().SetItem(item);
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.center = item.WorldSprite.bounds.center;
                Vector3 size = item.WorldSprite.bounds.size;
                size.z = Mathf.Max(0.08f, size.z);
                box.size = size;
                box.isTrigger = true;
                root.transform.localScale = Vector3.one * (0.26f / Mathf.Max(item.WorldSprite.bounds.size.x, item.WorldSprite.bounds.size.y));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + product.slug + ".prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void AddToCatalog(IEnumerable<ItemDefinition> additions)
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/Shelf_Items/Data/ShelfItemsCatalog.asset");
            if (catalog == null) throw new System.InvalidOperationException("Shelf item catalog is missing.");
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty items = serialized.FindProperty("items");
            foreach (ItemDefinition item in additions)
            {
                bool present = false;
                for (int i = 0; i < items.arraySize; i++)
                    if (items.GetArrayElementAtIndex(i).objectReferenceValue == item) { present = true; break; }
                if (!present)
                {
                    int index = items.arraySize;
                    items.InsertArrayElementAtIndex(index);
                    items.GetArrayElementAtIndex(index).objectReferenceValue = item;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }
    }
}
