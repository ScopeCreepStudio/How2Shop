using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using How2Shop.Items;

[InitializeOnLoad]
public static class ProduceItemsBuilder
{
    const string Root = "Assets/Shelf_Items";
    const string Request = "UserSettings/ProduceItems.request";
    static readonly string[] Names = { "Apple", "Banana", "Orange", "Pear", "Strawberry", "Carrot", "Tomato", "Potato", "Broccoli", "Cucumber" };
    static readonly int[] Prices = { 60, 30, 65, 60, 199, 25, 40, 35, 120, 85 };
    static ProduceItemsBuilder() { EditorApplication.delayCall += Auto; }
    static void Auto()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || Names.Any(n => !File.Exists(Root + "/Sprites/" + n + ".png")))
        { EditorApplication.delayCall += Auto; return; }
        try { Build(); File.Delete(Request); }
        catch (Exception e) { File.WriteAllText("Logs/ProduceItemsError.txt", e.ToString()); Debug.LogException(e); File.Delete(Request); }
    }

    [MenuItem("Tools/Shelf Items/Build fruit and vegetable prefabs")]
    public static void Build()
    {
        var sourceMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Cereal.mat");
        if (!sourceMaterial) throw new Exception("Existing shelf item material is missing");
        var added = new List<ItemDefinition>();
        for (int i = 0; i < Names.Length; i++)
        {
            string name = Names[i], imagePath = Root + "/Sprites/" + name + ".png";
            AssetDatabase.ImportAsset(imagePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport(); // ShelfArtworkAutoFit calculates the physical size and bottom pivot.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            if (!sprite) throw new Exception("Sprite import failed: " + name);

            string dataPath = Root + "/Data/" + name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<ItemDefinition>(dataPath);
            bool isNew = !data; if (isNew) { data = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(data, dataPath); }
            var serialized = new SerializedObject(data);
            serialized.FindProperty("id").stringValue = "shelf_items_" + name.ToLowerInvariant();
            serialized.FindProperty("displayName").stringValue = name;
            if (isNew) serialized.FindProperty("pricePence").intValue = Prices[i];
            serialized.FindProperty("icon").objectReferenceValue = sprite;
            serialized.FindProperty("worldSprite").objectReferenceValue = sprite;
            var tags = serialized.FindProperty("tags"); tags.arraySize = 3;
            tags.GetArrayElementAtIndex(0).stringValue = "produce";
            tags.GetArrayElementAtIndex(1).stringValue = i < 5 ? "fruit" : "vegetable";
            tags.GetArrayElementAtIndex(2).stringValue = "supermarket";
            serialized.ApplyModifiedPropertiesWithoutUndo(); added.Add(data);

            string materialPath = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(sourceMaterial); material.name = name; AssetDatabase.CreateAsset(material, materialPath); }
            material.SetTexture("_Artwork", sprite.texture); material.SetColor("_Color", Color.white); EditorUtility.SetDirty(material);
            var item = new GameObject(name);
            item.AddComponent<SpriteRenderer>().sharedMaterial = material;
            item.AddComponent<ItemSpriteView>().SetItem(data);
            PrefabUtility.SaveAsPrefabAsset(item, Root + "/Prefabs/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(item);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(Root + "/Data/ShelfItemsCatalog.asset");
        if (catalog)
        {
            var serialized = new SerializedObject(catalog); var list = serialized.FindProperty("items");
            foreach (var data in added)
            {
                bool exists = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) { exists = true; break; }
                if (!exists) { int index = list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue = data; }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        Verify(); Preview(); AssetDatabase.Refresh();
    }

    static void Verify()
    {
        var report = new List<string>();
        foreach (string name in Names)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab");
            var renderer = prefab.GetComponent<SpriteRenderer>(); var data = prefab.GetComponent<ItemSpriteView>().Item;
            if (!data || renderer.sprite != data.WorldSprite || renderer.sharedMaterial.GetTexture("_Artwork") != renderer.sprite.texture)
                throw new Exception("Invalid item links: " + name);
            var raw = new Texture2D(2, 2); raw.LoadImage(File.ReadAllBytes(Root + "/Sprites/" + name + ".png"));
            var pixels = raw.GetPixels32(); int left = raw.width, right = -1, bottom = raw.height, top = -1, clear = 0;
            for (int y = 0; y < raw.height; y++) for (int x = 0; x < raw.width; x++)
            {
                byte a = pixels[y * raw.width + x].a; if (a < 16) clear++;
                if (a < 128) continue;
                left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Min(bottom, y); top = Math.Max(top, y);
            }
            float width = renderer.bounds.size.x * (right - left + 1f) / raw.width;
            float height = renderer.bounds.size.y * (top - bottom + 1f) / raw.height;
            UnityEngine.Object.DestroyImmediate(raw);
            if (clear == 0 || right < left || width > .325f || height > .465f || (Mathf.Abs(width - .32f) > .005f && Mathf.Abs(height - .46f) > .005f))
                throw new Exception("Transparency or automatic sizing failed: " + name);
            report.Add($"PASS {name}: transparent PNG, linked material, item data, standalone prefab, automatic size {width:F3} x {height:F3}m.");
        }
        report.Add("PASS: 10 fruit and vegetable prefabs added to Shelf_Items; existing item and scene arrangements retained.");
        File.WriteAllLines("Logs/ProduceItemsValidation.txt", report);
    }

    public static void Preview()
    {
        Directory.CreateDirectory(Root + "/Preview");
        var scene = EditorSceneManager.NewPreviewScene(); var rt = new RenderTexture(1500, 700, 24);
        try
        {
            for (int i = 0; i < Names.Length; i++)
            {
                var item = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + Names[i] + ".prefab"));
                SceneManager.MoveGameObjectToScene(item, scene); item.transform.position = new Vector3(i % 5, 1.2f - i / 5 * 1.2f, 0); item.transform.localScale *= 1.8f;
                var label = new GameObject("Name"); SceneManager.MoveGameObjectToScene(label, scene); label.transform.position = item.transform.position + new Vector3(0, -.13f, 0);
                var text = label.AddComponent<TextMesh>(); text.text = Names[i]; text.fontSize = 32; text.characterSize = .02f; text.anchor = TextAnchor.UpperCenter; text.color = Color.white;
            }
            var go = new GameObject("Produce preview camera"); SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>(); camera.scene = scene; camera.transform.position = new Vector3(2, .85f, -5); camera.orthographic = true; camera.orthographicSize = 1.4f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .09f, .13f); camera.targetTexture = rt; camera.Render();
            var old = RenderTexture.active; RenderTexture.active = rt; var image = new Texture2D(1500, 700, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1500, 700), 0, 0); image.Apply();
            File.WriteAllBytes(Root + "/Preview/FruitAndVegetables.png", image.EncodeToPNG()); RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(image);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
    }
}

