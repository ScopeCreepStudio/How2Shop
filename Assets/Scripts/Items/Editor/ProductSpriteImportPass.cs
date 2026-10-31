using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace How2Shop.Items.Editor
{
    public static class ProductSpriteImportPass
    {
        private const string SpriteRoot = "Assets/Art/ProductSprites/";
        private const string NonsenseRoot = "Assets/ITEMS_1/Nonsense/Data/";
        private const string NonsensePrefabRoot = "Assets/ITEMS_1/Nonsense/Prefabs/";
        private const string NonsenseDisplayPath = "Assets/ITEMS_1/Nonsense/NonsenseEndcap_Stocked.prefab";
        private const string ShelfCatalogPath = "Assets/Shelf_Items/Data/ShelfItemsCatalog.asset";

        private sealed class JokeItem
        {
            public readonly string Name;
            public readonly string File;
            public readonly int Price;
            public readonly string[] Tags;

            public JokeItem(string name, string file, int price, params string[] tags)
            {
                Name = name;
                File = file;
                Price = price;
                Tags = tags;
            }
        }

        private static readonly JokeItem[] JokeItems =
        {
            new JokeItem("Half a Fish", "half-a-fish", 199, "food", "fish", "half", "fishy", "seafood"),
            new JokeItem("Family Sized Pea", "family-sized-pea", 49, "food", "vegetable", "pea", "peas", "tiny", "small", "family-size"),
            new JokeItem("Emotional Support Brick", "emotional-support-brick", 299, "brick", "heavy", "household", "emotional-support", "building-material"),
            new JokeItem("Diet Sugar", "diet-sugar", 179, "food", "sugar", "sweetener", "diet", "low-calorie", "light"),
            new JokeItem("Instant Soup", "instant-soup", 129, "food", "soup", "instant", "quick-meal", "warm"),
            new JokeItem("Pre-Owned Egg", "pre-owned-egg", 39, "food", "egg", "protein", "pre-owned", "second-hand", "fragile"),
            new JokeItem("Light Bulb", "light-bulb", 499, "light", "light-bulb", "lightweight", "electrical", "household", "non-food")
        };

        [MenuItem("How2Shop/Items/Import Product Doodles")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Path.GetFullPath(NonsenseRoot));
            Directory.CreateDirectory(Path.GetFullPath(NonsensePrefabRoot));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string[] itemGuids = AssetDatabase.FindAssets("t:ItemDefinition");
            int updated = 0;
            int missing = 0;
            List<string> missingNames = new List<string>();

            for (int i = 0; i < itemGuids.Length; i++)
            {
                string itemPath = AssetDatabase.GUIDToAssetPath(itemGuids[i]);
                ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
                if (item == null || string.IsNullOrWhiteSpace(item.DisplayName))
                    continue;

                string spritePath = SpriteRoot + Slug(item.DisplayName) + ".png";
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                {
                    missing++;
                    missingNames.Add(item.DisplayName + " -> " + spritePath);
                    continue;
                }

                SerializedObject serialized = new SerializedObject(item);
                SerializedProperty icon = serialized.FindProperty("icon");
                SerializedProperty worldSprite = serialized.FindProperty("worldSprite");
                bool changed = icon.objectReferenceValue != sprite || worldSprite.objectReferenceValue != sprite;
                icon.objectReferenceValue = sprite;
                worldSprite.objectReferenceValue = sprite;
                if (changed)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(item);
                    updated++;
                }
            }

            int updatedPrefabs = RefreshPrefabSpriteRenderers();

            List<ItemDefinition> jokes = new List<ItemDefinition>();
            for (int i = 0; i < JokeItems.Length; i++)
            {
                ItemDefinition item = EnsureJokeItem(JokeItems[i]);
                if (item != null)
                    jokes.Add(item);
                else
                {
                    missing++;
                    missingNames.Add(JokeItems[i].Name + " -> " + SpriteRoot + JokeItems[i].File + ".png");
                }
            }

            if (jokes.Count == JokeItems.Length)
                BuildNonsenseDisplay(jokes);
            AddToShelfCatalog(jokes);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            bool endcapPlaced = jokes.Count == JokeItems.Length && PlaceNonsenseDisplayInMainScene();

            string summary = "Imported product sprites. Updated " + updated + " item definitions and " + updatedPrefabs +
                             " prefab sprite renderers; " + jokes.Count +
                             " joke items ready; " + missing + " sprite link(s) missing; endcap placed: " + endcapPlaced + ".";
            if (missingNames.Count > 0)
                summary += " Missing: " + string.Join(" | ", missingNames);
            Directory.CreateDirectory("Temp");
            File.WriteAllText("Temp/ProductSpriteImportValidation.txt",
                "Completed UTC: " + DateTime.UtcNow.ToString("O") + Environment.NewLine +
                "Item definitions found: " + itemGuids.Length + Environment.NewLine +
                "Item definitions updated: " + updated + Environment.NewLine +
                "Prefab roots updated: " + updatedPrefabs + Environment.NewLine +
                "Joke items ready: " + jokes.Count + "/" + JokeItems.Length + Environment.NewLine +
                "Missing sprite links: " + missing + Environment.NewLine +
                "MainScene endcap placed: " + endcapPlaced + Environment.NewLine +
                summary);
            if (missing > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }

        private static ItemDefinition EnsureJokeItem(JokeItem spec)
        {
            string spritePath = SpriteRoot + spec.File + ".png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                return null;

            Directory.CreateDirectory(Path.GetFullPath(NonsenseRoot));
            string assetPath = NonsenseRoot + Slug(spec.Name) + ".asset";
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.name = spec.Name;
                AssetDatabase.CreateAsset(item, assetPath);
            }

            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("id").stringValue = "grandma_" + Slug(spec.Name);
            serialized.FindProperty("displayName").stringValue = spec.Name;
            serialized.FindProperty("pricePence").intValue = spec.Price;
            serialized.FindProperty("icon").objectReferenceValue = sprite;
            serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
            SerializedProperty tags = serialized.FindProperty("tags");
            tags.arraySize = spec.Tags.Length;
            for (int i = 0; i < spec.Tags.Length; i++)
                tags.GetArrayElementAtIndex(i).stringValue = spec.Tags[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            return item;
        }

        private static int RefreshPrefabSpriteRenderers()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[]
            {
                "Assets/ITEMS_1",
                "Assets/Gameplay/ColourEditableItems",
                "Assets/Packages/How2ShopAssets/items",
                "Assets/Prefabs"
            });
            int updatedPrefabs = 0;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!File.ReadAllText(path).Contains("Assembly-CSharp::How2Shop.Items.ItemSpriteView"))
                    continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                try
                {
                    ItemSpriteView[] views = root.GetComponentsInChildren<ItemSpriteView>(true);
                    for (int viewIndex = 0; viewIndex < views.Length; viewIndex++)
                    {
                        ItemSpriteView view = views[viewIndex];
                        if (view.Item == null || PrefabUtility.IsPartOfPrefabInstance(view.gameObject))
                            continue;

                        SpriteRenderer renderer = view.GetComponent<SpriteRenderer>();
                        if (renderer == null || renderer.sprite == view.Item.WorldSprite)
                            continue;

                        renderer.sprite = view.Item.WorldSprite;
                        EditorUtility.SetDirty(renderer);
                        changed = true;
                    }

                    for (int viewIndex = 0; viewIndex < views.Length; viewIndex++)
                    {
                        ItemSpriteView view = views[viewIndex];
                        if (view.Item == null || PrefabUtility.IsPartOfPrefabInstance(view.gameObject))
                            continue;

                        SpriteRenderer renderer = view.GetComponent<SpriteRenderer>();
                        if (renderer == null || view.Item.WorldSprite == null)
                            continue;

                        Material material = renderer.sharedMaterial;
                        if (material == null)
                            continue;

                        bool materialChanged = false;
                        if (material.HasProperty("_Artwork") && material.GetTexture("_Artwork") != view.Item.WorldSprite.texture)
                        {
                            material.SetTexture("_Artwork", view.Item.WorldSprite.texture);
                            materialChanged = true;
                        }
                        if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != view.Item.WorldSprite.texture)
                        {
                            material.SetTexture("_MainTex", view.Item.WorldSprite.texture);
                            materialChanged = true;
                        }
                        if (materialChanged)
                        {
                            EditorUtility.SetDirty(material);
                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        updatedPrefabs++;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return updatedPrefabs;
        }

        private static void AddToShelfCatalog(List<ItemDefinition> additions)
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ShelfCatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("Shelf item catalog not found: " + ShelfCatalogPath);
                return;
            }

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty items = serialized.FindProperty("items");
            bool changed = false;
            for (int a = 0; a < additions.Count; a++)
            {
                bool found = false;
                for (int i = 0; i < items.arraySize; i++)
                {
                    if (items.GetArrayElementAtIndex(i).objectReferenceValue == additions[a])
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    int index = items.arraySize;
                    items.InsertArrayElementAtIndex(index);
                    items.GetArrayElementAtIndex(index).objectReferenceValue = additions[a];
                    changed = true;
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(catalog);
            }
        }

        private static void BuildNonsenseDisplay(List<ItemDefinition> items)
        {
            Directory.CreateDirectory(Path.GetFullPath(NonsensePrefabRoot));
            GameObject root = new GameObject("NonsenseEndcap_Stocked");
            try
            {
                Material frame = AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_Charcoal.mat");
                Material board = AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_Cream.mat");
                Material shelf = AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_Brown.mat");
                if (frame == null || board == null || shelf == null)
                    throw new InvalidOperationException("The existing shelf palette is incomplete.");

                AddCube(root.transform, "Display back", new Vector3(0f, 0.89f, 0.12f), new Vector3(1.42f, 1.78f, 0.08f), frame);
                AddCube(root.transform, "Display back panel", new Vector3(0f, 0.94f, 0.07f), new Vector3(1.30f, 1.62f, 0.025f), board);
                AddCube(root.transform, "Display base", new Vector3(0f, 0.12f, 0f), new Vector3(1.50f, 0.24f, 0.55f), frame);

                for (int tier = 0; tier < 3; tier++)
                {
                    float y = 0.36f + tier * 0.48f;
                    AddCube(root.transform, "Shelf " + (tier + 1), new Vector3(0f, y, -0.05f), new Vector3(1.42f, 0.055f, 0.42f), shelf);
                    AddCube(root.transform, "Shelf lip " + (tier + 1), new Vector3(0f, y + 0.06f, -0.255f), new Vector3(1.42f, 0.11f, 0.04f), frame);
                }

                float[] xPositions = { -0.45f, 0f, 0.45f, -0.45f, 0f, 0.45f, 0f };
                float[] yPositions = { 0.55f, 0.55f, 0.55f, 1.03f, 1.03f, 1.03f, 1.51f };
                for (int i = 0; i < items.Count; i++)
                {
                    ItemDefinition definition = items[i];
                    if (definition == null || definition.WorldSprite == null)
                        continue;

                    GameObject product = new GameObject(definition.DisplayName);
                    product.transform.SetParent(root.transform, false);
                    product.transform.localPosition = new Vector3(xPositions[i], yPositions[i], -0.31f);
                    float maxSpriteDimension = Mathf.Max(definition.WorldSprite.bounds.size.x, definition.WorldSprite.bounds.size.y);
                    product.transform.localScale = Vector3.one * (0.30f / Mathf.Max(0.001f, maxSpriteDimension));
                    SpriteRenderer renderer = product.AddComponent<SpriteRenderer>();
                    renderer.sprite = definition.WorldSprite;
                    renderer.sortingOrder = 2;
                    ItemSpriteView view = product.AddComponent<ItemSpriteView>();
                    view.SetItem(definition);

                    BoxCollider pickup = product.AddComponent<BoxCollider>();
                    pickup.center = definition.WorldSprite.bounds.center;
                    pickup.size = definition.WorldSprite.bounds.size;
                    pickup.isTrigger = true;
                }

                PrefabUtility.SaveAsPrefabAsset(root, NonsenseDisplayPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            for (int i = 0; i < items.Count; i++)
                BuildJokeItemPrefab(items[i]);
        }

        private static void BuildJokeItemPrefab(ItemDefinition definition)
        {
            if (definition == null || definition.WorldSprite == null)
                return;

            GameObject product = new GameObject(definition.DisplayName);
            try
            {
                SpriteRenderer renderer = product.AddComponent<SpriteRenderer>();
                renderer.sprite = definition.WorldSprite;
                ItemSpriteView view = product.AddComponent<ItemSpriteView>();
                view.SetItem(definition);
                BoxCollider pickup = product.AddComponent<BoxCollider>();
                pickup.center = definition.WorldSprite.bounds.center;
                pickup.size = definition.WorldSprite.bounds.size;
                pickup.isTrigger = true;
                float maxSpriteDimension = Mathf.Max(definition.WorldSprite.bounds.size.x, definition.WorldSprite.bounds.size.y);
                product.transform.localScale = Vector3.one * (0.30f / Mathf.Max(0.001f, maxSpriteDimension));
                string path = NonsensePrefabRoot + Slug(definition.DisplayName) + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(product, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(product);
            }
        }

        private static bool PlaceNonsenseDisplayInMainScene()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "MainScene")
            {
                Debug.LogWarning("Joke items and their display prefab are ready. Open MainScene and run How2Shop/Items/Place Joke Display to add the pickup display to the store.");
                return false;
            }

            if (scene.GetRootGameObjects().Any(go => go.name == "Nonsense Endcap - Nan's Misreadings"))
                return true;

            GameObject displayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NonsenseDisplayPath);
            if (displayPrefab == null)
                return false;

            Vector3 position = new Vector3(55.75f, 0f, -39.15f);
            OpeningCutsceneController opening = UnityEngine.Object.FindFirstObjectByType<OpeningCutsceneController>();
            if (opening != null)
            {
                SerializedObject serialized = new SerializedObject(opening);
                SerializedProperty entrance = serialized.FindProperty("entrancePosition");
                if (entrance != null)
                    position = entrance.vector3Value + new Vector3(-1.5f, -entrance.vector3Value.y, 2f);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(displayPrefab, scene);
            instance.name = "Nonsense Endcap - Nan's Misreadings";
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 180f, 0f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        [MenuItem("How2Shop/Items/Place Joke Display")]
        private static void PlaceJokeDisplayMenu()
        {
            PlaceNonsenseDisplayInMainScene();
        }

        private static void AddCube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
        }

        private static string Slug(string value)
        {
            return Regex.Replace((value ?? string.Empty).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        }
    }

    [InitializeOnLoad]
    internal static class ProductSpriteImportRequest
    {
        private const string RequestPath = "UserSettings/ProductSpriteImport.request";
        private const string RunningKey = "How2Shop.ProductSpriteImportPass.Running";

        static ProductSpriteImportRequest()
        {
            EditorApplication.delayCall += BeginIfRequested;
        }

        private static void BeginIfRequested()
        {
            if (!File.Exists(RequestPath))
                return;

            EditorApplication.update += WaitUntilReady;
        }

        private static void WaitUntilReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EditorApplication.update -= WaitUntilReady;
            if (SessionState.GetBool(RunningKey, false) || !File.Exists(RequestPath))
                return;

            SessionState.SetBool(RunningKey, true);
            try
            {
                ProductSpriteImportPass.ImportAll();
                File.Delete(RequestPath);
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText("Temp/ProductSpriteImportError.txt", exception.ToString());
                File.Delete(RequestPath);
                Debug.LogException(exception);
            }
            finally
            {
                SessionState.SetBool(RunningKey, false);
            }
        }
    }

    public sealed class ProductSpriteImportSettings : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/ProductSprites/", StringComparison.OrdinalIgnoreCase))
                return;

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.maxTextureSize = 1024;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
