using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using How2Shop.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NewspaperStandBuilder
{
    const string Items = "Assets/Packages/How2ShopAssets/items/Newspapers";
    const string Stand = "Assets/Shelf_Items/NewspaperStand";
    static readonly string[] Names = { "DailyChronicle", "444", "Telex", "Motor", "Angler", "HomeGarden", "GoodFood", "Wanderer", "SportWeekly", "TechToday" };
    static readonly string[] Labels = { "The Daily Chronicle", "444", "Telex", "Motor - Car Magazine", "Angler - Fishing Magazine", "Home & Garden", "Good Food", "Wanderer - Travel", "Sport Weekly", "Tech Today" };
    static readonly string[] Categories = { "news", "news", "news", "cars", "fishing", "home", "cooking", "travel", "sport", "technology" };
    static readonly int[] Prices = { 150, 180, 180, 499, 399, 449, 349, 499, 299, 549 };
    const string Request = "UserSettings/NewspaperStandBuild.request";
    static NewspaperStandBuilder() { EditorApplication.delayCall += AutoBuild; }
    static void AutoBuild()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
            Names.Any(name => !File.Exists(Items + "/Sprites/" + name + ".png")))
        { EditorApplication.delayCall += AutoBuild; return; }
        try { Build(); File.Delete(Request); }
        catch (Exception error)
        {
            File.WriteAllText("Logs/NewspaperStandError.txt", error.ToString());
            File.Delete(Request);
            Debug.LogException(error);
        }
    }

    [MenuItem("Tools/Shelf Items/Build newspaper stand")]
    public static void Build()
    {
        foreach (string folder in new[] { Items + "/Sprites", Items + "/Data", Items + "/Prefabs", Items + "/Materials", Items + "/Preview", Stand + "/Materials" })
            Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var definitions = new List<ItemDefinition>();
            var prefabs = new List<GameObject>();
            for (int i = 0; i < Names.Length; i++)
            {
                string imagePath = Items + "/Sprites/" + Names[i] + ".png";
                if (!File.Exists(imagePath)) throw new FileNotFoundException("Missing press cover", imagePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                // Ignore transparent padding so all ten covers fit their pockets.
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!raw.LoadImage(File.ReadAllBytes(imagePath))) throw new Exception("Invalid cover " + imagePath);
                var pixels = raw.GetPixels32();
                int left = raw.width, right = -1, bottom = raw.height, top = -1;
                for (int y = 0; y < raw.height; y++) for (int x = 0; x < raw.width; x++)
                {
                    if (pixels[y * raw.width + x].a < 128) continue;
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                }
                if (right < left) throw new Exception("Empty cover " + imagePath);
                float heightFraction = (top - bottom + 1f) / raw.height;
                float fitScale = Mathf.Min(.385f / heightFraction, .295f * raw.height / (right - left + 1f));
                importer.spritePixelsPerUnit = 256;
                importer.maxTextureSize = 64;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2((left + right + 1f) / (2 * raw.width), bottom / (float)raw.height);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                UnityEngine.Object.DestroyImmediate(raw);
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
                if (!sprite) throw new Exception("Failed to import " + imagePath);
                string dataPath = Items + "/Data/" + Names[i] + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(dataPath);
                if (!definition) { definition = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(definition, dataPath); }
                var data = new SerializedObject(definition);
                data.FindProperty("id").stringValue = "press_" + Names[i].ToLowerInvariant();
                data.FindProperty("displayName").stringValue = Labels[i];
                data.FindProperty("pricePence").intValue = Prices[i];
                data.FindProperty("icon").objectReferenceValue = sprite;
                data.FindProperty("worldSprite").objectReferenceValue = sprite;
                var tags = data.FindProperty("tags");
                string[] values = { "supermarket", "press", i < 3 ? "newspaper" : "magazine", Categories[i] };
                tags.arraySize = values.Length;
                for (int t = 0; t < values.Length; t++) tags.GetArrayElementAtIndex(t).stringValue = values[t];
                data.ApplyModifiedPropertiesWithoutUndo();
                definitions.Add(definition);

                var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(Items + "/Materials/" + Names[i] + ".mat");
                if (!spriteMaterial)
                {
                    var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shelf_Items/Materials/Cereal.mat");
                    if (!source) throw new Exception("Missing existing shelf sprite material");
                    spriteMaterial = new Material(source);
                    AssetDatabase.CreateAsset(spriteMaterial, Items + "/Materials/" + Names[i] + ".mat");
                }
                spriteMaterial.SetTexture("_Artwork", sprite.texture);
                EditorUtility.SetDirty(spriteMaterial);
                var item = new GameObject(Names[i]);
                SceneManager.MoveGameObjectToScene(item, scene);
                item.AddComponent<SpriteRenderer>().sharedMaterial = spriteMaterial;
                item.AddComponent<ItemSpriteView>().SetItem(definition);
                // Covers are flat sprites, never billboards or 3D boxes.
                item.transform.localScale = Vector3.one * (fitScale / sprite.bounds.size.y);
                prefabs.Add(PrefabUtility.SaveAsPrefabAsset(item, Items + "/Prefabs/" + Names[i] + ".prefab"));
                UnityEngine.Object.DestroyImmediate(item);
            }

            string catalogPath = Items + "/Data/NewspapersCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(catalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<ItemCatalog>(); AssetDatabase.CreateAsset(catalog, catalogPath); }
            var catalogData = new SerializedObject(catalog);
            var list = catalogData.FindProperty("items"); list.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            catalogData.ApplyModifiedPropertiesWithoutUndo();
            var errors = catalog.GetValidationErrors();
            if (errors.Count > 0) throw new Exception(string.Join("\n", errors));

            var frame = Palette("Pixel_Charcoal");
            var panel = Palette("Pixel_Slate");
            var cream = Palette("Pixel_Cream");
            var root = new GameObject("NewspaperStand_Stocked");
            SceneManager.MoveGameObjectToScene(root, scene);
            var structure = Child(root.transform, "Frame");
            var trays = Child(root.transform, "Shelves");
            var stock = Child(root.transform, "Newspapers and Magazines - 2D Items");
            foreach (float x in new[] { -.86f, .86f })
            {
                Cube(structure, "Upright", new Vector3(x, .87f, .18f), new Vector3(.045f, 1.68f, .045f), frame);
                Cube(structure, "Foot", new Vector3(x, .035f, 0), new Vector3(.09f, .07f, .68f), frame);
                Cube(structure, "Front leg", new Vector3(x, .26f, -.25f), new Vector3(.045f, .45f, .045f), frame);
            }
            Cube(structure, "Lower crossbar", new Vector3(0, .16f, .18f), new Vector3(1.72f, .05f, .045f), frame);
            Cube(structure, "Back panel", new Vector3(0, .91f, .205f), new Vector3(1.69f, 1.36f, .035f), panel);
            for (int row = 0; row < 2; row++)
            {
                float y = .49f + row * .51f;
                var tray = Child(trays, "Display tier " + (row + 1));
                Cube(tray, "Deck", new Vector3(0, y, -.04f), new Vector3(1.70f, .035f, .46f), panel);
                Cube(tray, "Cream price rail", new Vector3(0, y + .035f, -.275f), new Vector3(1.72f, .065f, .025f), cream);
                Cube(tray, "Retaining rail", new Vector3(0, y + .12f, -.235f), new Vector3(1.72f, .012f, .012f), frame);
                for (int slot = 0; slot < 5; slot++)
                {
                    int index = row * 5 + slot;
                    float x = (slot - 2) * .334f;
                    var item = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[index], scene);
                    item.transform.SetParent(stock, false);
                    item.transform.localPosition = new Vector3(x, y + .023f, -.205f);
                    item.transform.localRotation = Quaternion.Euler(12, 0, 0);
                    Text(tray, "Price " + Names[index], definitions[index].PriceLabel, new Vector3(x, y + .035f, -.291f), .10f, new Color(.12f, .14f, .16f));
                }
                foreach (int divider in new[] { -2, -1, 0, 1, 2, 3 })
                    Cube(tray, "Pocket divider", new Vector3((divider - .5f) * .334f, y + .095f, -.11f), new Vector3(.012f, .16f, .30f), frame);
            }
            var header = Child(root.transform, "Header");
            Cube(header, "Cream sign", new Vector3(0, 1.62f, .16f), new Vector3(1.69f, .19f, .035f), cream);
            foreach (float y in new[] { 1.515f, 1.725f }) Cube(header, "Sign trim", new Vector3(0, y, .155f), new Vector3(1.78f, .025f, .055f), frame);
            Text(header, "Press sign", "NEWSPAPERS & MAGAZINES", new Vector3(0, 1.62f, .135f), .13f, new Color(.12f, .14f, .16f));
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, .88f, 0); collider.size = new Vector3(1.82f, 1.76f, .68f);
            PrefabUtility.SaveAsPrefabAsset(root, Stand + "/NewspaperStand_Stocked.prefab");
            stock.gameObject.SetActive(false);
            root.name = "NewspaperStand_Empty";
            PrefabUtility.SaveAsPrefabAsset(root, Stand + "/NewspaperStand_Empty.prefab");
            stock.gameObject.SetActive(true);
            root.name = "NewspaperStand_Stocked";
            AssetDatabase.SaveAssets();
            Validate(prefabs, catalog);
            Render(root, scene);
            File.WriteAllText(Stand + "/README.md", "# Newspaper stand\n\nNewspaperStand_Stocked.prefab contains ten distinct flat SpriteRenderer items. NewspaperStand_Empty.prefab has the stock group disabled; enable or replace its items as needed. Front faces local -Z, pivot at floor level, approximately 1.82m wide x 1.76m high x 0.68m deep. Drag the prefab into the scene. Uses the existing Pixel_Charcoal, Pixel_Slate and Pixel_Cream shelf palette, with separate frame, shelves and sign children.\n\nIndividual cover sprites, item definitions, materials, prefabs and the NewspapersCatalog live in " + Items + ". Prices are editable example game prices. Items use the existing ItemDefinition and ItemSpriteView system. No billboard behavior or added item physics.\n\nRebuild: Tools > Shelf Items > Build newspaper stand. This regenerates the stand and press assets; duplicate customized versions first. Preview.png and Validation.txt record the generated output. Existing scenes are unchanged.\n");
            AssetDatabase.Refresh();
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static Material Palette(string name)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/" + name + ".mat");
        if (!material) throw new Exception("Missing shelf palette " + name);
        return material;
    }
    static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name); child.transform.SetParent(parent, false); return child.transform;
    }
    static void Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Text(Transform parent, string name, string value, Vector3 position, float height, Color color)
    {
        var text = Child(parent, name).gameObject.AddComponent<TextMesh>();
        text.transform.localPosition = position;
        text.text = value; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 64; text.characterSize = height / 10f; text.color = color;
        text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
    }
    static void Validate(List<GameObject> items, ItemCatalog catalog)
    {
        if (items.Count != 10 || catalog.Items.Count != 10) throw new Exception("Expected ten press items");
        var stand = AssetDatabase.LoadAssetAtPath<GameObject>(Stand + "/NewspaperStand_Stocked.prefab");
        var views = stand.GetComponentsInChildren<ItemSpriteView>();
        if (views.Length != 10 || views.Select(v => v.Item.Id).Distinct().Count() != 10) throw new Exception("Stand stock is incomplete");
        foreach (var prefab in items)
            if (!prefab.GetComponent<ItemSpriteView>().Item || !prefab.GetComponent<SpriteRenderer>().sprite) throw new Exception("Broken item " + prefab.name);
        foreach (var renderer in stand.GetComponentsInChildren<Renderer>())
            if (renderer.sharedMaterials.Any(m => !m)) throw new Exception("Missing material: " + renderer.name);
        if (!stand.GetComponent<BoxCollider>()) throw new Exception("Missing stand collider");
        File.WriteAllText(Stand + "/Validation.txt", "PASS: 10 unique item definitions, imported cover sprites, individual flat prefabs, valid press catalog, stocked stand with all 10 items, material references, root collider.\n");
    }
    internal static void Render(GameObject root, Scene scene)
    {
        var cameraObject = new GameObject("Preview camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene;
        camera.transform.position = new Vector3(2.2f, 1.9f, -4.5f); camera.transform.LookAt(new Vector3(0, .90f, 0));
        camera.orthographic = true; camera.orthographicSize = 1.12f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .18f, .21f);
        var lightObject = new GameObject("Preview light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
        lightObject.transform.rotation = Quaternion.Euler(35, -30, 0);
        var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
        var rt = new RenderTexture(1200, 1200, 24);
        var previous = RenderTexture.active;
        try
        {
            var warmup = new RenderTexture(64, 64, 24);
            camera.targetTexture = warmup; camera.Render(); camera.targetTexture = null;
            warmup.Release(); UnityEngine.Object.DestroyImmediate(warmup);
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var image = new Texture2D(1200, 1200, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1200, 1200), 0, 0); image.Apply();
            File.WriteAllBytes(Stand + "/Preview.png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    }
}
