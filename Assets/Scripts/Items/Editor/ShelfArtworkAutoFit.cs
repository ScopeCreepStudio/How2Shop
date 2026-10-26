using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using How2Shop.Items;

// Keep product size in metres independent of a replacement PNG's resolution or transparent padding.
[InitializeOnLoad]
public sealed class ShelfArtworkAutoFit : AssetPostprocessor
{
    const string Sprites = "Assets/Shelf_Items/Sprites/";
    const string Prefabs = "Assets/Shelf_Items/Prefabs/";
    const string Bays = "Assets/Shelf_Items/Stocked_Bays/";
    const string Request = "UserSettings/ShelfArtworkAutoFit.request";
    const float Width = .32f, Height = .46f;

    static ShelfArtworkAutoFit() { EditorApplication.delayCall += InstallIfRequested; }

    static void InstallIfRequested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += InstallIfRequested; return; }
        try { FitAll(); File.Delete(Request); }
        catch (Exception e) { File.WriteAllText("Logs/ShelfArtworkAutoFitError.txt", e.ToString()); Debug.LogException(e); }
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Sprites, StringComparison.Ordinal) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return;
        Configure((TextureImporter)assetImporter, assetPath);
    }

    static RectInt VisiblePixels(string path, out int width, out int height)
    {
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!image.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException("Cannot read " + path);
            width = image.width; height = image.height;
            var pixels = image.GetPixels32();
            int left = width, bottom = height, right = -1, top = -1;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a < 128) continue;
                left = Math.Min(left, x); right = Math.Max(right, x);
                bottom = Math.Min(bottom, y); top = Math.Max(top, y);
            }
            if (right < left) throw new InvalidDataException("Picture is completely transparent: " + path);
            return new RectInt(left, bottom, right - left + 1, top - bottom + 1);
        }
        finally { UnityEngine.Object.DestroyImmediate(image); }
    }

    static float PixelsPerUnit(RectInt visible) => Mathf.Max(visible.width / Width, visible.height / Height);

    static void Configure(TextureImporter importer, string path)
    {
        var visible = VisiblePixels(path, out int width, out int height);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit(visible);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(visible.center.x / width, visible.yMin / (float)height);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }

    [MenuItem("Tools/Shelf Items/Fit all replacement pictures")]
    public static void FitAll()
    {
        foreach (string path in Directory.GetFiles(Sprites, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            Configure(importer, path); importer.SaveAndReimport();
        }
        // The old prefabs baked image resolution into their root scale. Use physical sprite units instead.
        foreach (string file in Directory.GetFiles(Prefabs, "*.prefab"))
        {
            string path = file.Replace('\\', '/');
            var root = PrefabUtility.LoadPrefabContents(path);
            try { root.transform.localScale = Vector3.one; PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        foreach (string file in Directory.GetFiles(Bays, "*.prefab"))
        {
            string path = file.Replace('\\', '/');
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                float scale = Path.GetFileName(path).StartsWith("Freezer_", StringComparison.Ordinal) ? .83f : 1f;
                foreach (var view in root.GetComponentsInChildren<ItemSpriteView>(true)) view.transform.localScale = Vector3.one * scale;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // Preserve scene positions and rotations, including any hand-placed stock. Repair only its old image scale.
        int repaired = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || scene.path != "Assets/Scenes/Superstore.unity") continue;
            foreach (var view in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ItemSpriteView>(true)))
            {
                string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(view.gameObject);
                if (!source.StartsWith(Prefabs, StringComparison.Ordinal)) continue;
                bool freezer = false;
                for (var t = view.transform.parent; t; t = t.parent)
                    if (t.name.StartsWith("raystore_fresh", StringComparison.Ordinal) || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith(Bays + "Freezer_", StringComparison.Ordinal)) { freezer = true; break; }
                view.transform.localScale = Vector3.one * (freezer ? .83f : 1f); repaired++;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Validate(repaired);
    }

    static void Validate(int repaired)
    {
        int count = 0;
        foreach (string file in Directory.GetFiles(Prefabs, "*.prefab"))
        {
            var product = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
            var renderer = product.GetComponent<SpriteRenderer>();
            var visible = VisiblePixels(AssetDatabase.GetAssetPath(renderer.sprite), out int width, out int height);
            float w = renderer.bounds.size.x * visible.width / width;
            float h = renderer.bounds.size.y * visible.height / height;
            if (w > Width + .005f || h > Height + .005f || (Mathf.Abs(w - Width) > .005f && Mathf.Abs(h - Height) > .005f))
                throw new Exception($"Picture did not fit: {product.name} ({w} x {h}m)");
            if (renderer.sharedMaterial.GetTexture("_Artwork") != renderer.sprite.texture) throw new Exception("Picture material mismatch: " + product.name);
            count++;
        }
        // Same visible artwork at different resolutions and with different padding must have the same physical size.
        foreach (int size in new[] { 32, 128, 512, 2048 })
        {
            var visible = new RectInt(size / 4, size / 4, size / 2, size / 3);
            float ppu = PixelsPerUnit(visible);
            if (Mathf.Abs(visible.width / ppu - Width) > .0001f || visible.height / ppu > Height + .0001f) throw new Exception("Resolution-independent fitting failed");
        }
        File.WriteAllText("Logs/ShelfArtworkAutoFitValidation.txt", $"PASS: {count} product pictures fit within {Width} x {Height}m; transparent padding ignored, bottom pivots recalculated, material references intact. Resolution tests passed. Repaired {repaired} loaded scene product scales. Future PNG replacements use this importer automatically.\n");
    }
}
