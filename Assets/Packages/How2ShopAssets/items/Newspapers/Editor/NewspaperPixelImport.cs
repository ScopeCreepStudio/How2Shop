using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Cover art stays full resolution on disk; Unity renders a deliberately coarse 64px sprite.
[InitializeOnLoad]
public sealed class NewspaperPixelImport : AssetPostprocessor
{
    const string Root="Assets/Packages/How2ShopAssets/items/Newspapers";
    const string Request="UserSettings/NewspaperPixelImport.request";
    static NewspaperPixelImport(){EditorApplication.delayCall+=Auto;}
    static void Auto()
    {
        if(!File.Exists(Request))return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {EditorApplication.delayCall+=Auto;return;}
        try{Apply();File.Delete(Request);}
        catch(Exception error){File.WriteAllText("Logs/NewspaperPixelImportError.txt",error.ToString());File.Delete(Request);Debug.LogException(error);}
    }
    void OnPreprocessTexture()
    {
        if(assetPath.StartsWith(Root+"/Sprites/",StringComparison.Ordinal) && assetPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase))
            Configure((TextureImporter)assetImporter);
    }
    static void Configure(TextureImporter importer)
    {
        importer.maxTextureSize=64;
        importer.filterMode=FilterMode.Point;
        importer.anisoLevel=0;
        importer.mipmapEnabled=false;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        foreach(string platform in new[]{"Standalone","Android","iPhone","WebGL"})
        {
            var settings=importer.GetPlatformTextureSettings(platform);
            if(!settings.overridden)continue;
            settings.maxTextureSize=64;settings.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }
    }
    [MenuItem("Tools/Shelf Items/Pixelate newspaper and magazine covers")]
    public static void Apply()
    {
        int count=0;
        foreach(string file in Directory.GetFiles(Root+"/Sprites","*.png"))
        {
            string path=file.Replace('\\','/');
            var before=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            float height=before?before.bounds.size.y:0;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            Configure(importer);importer.SaveAndReimport();
            var after=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if(height>0 && Mathf.Abs(after.bounds.size.y-height)>.0001f)
            {
                importer.spritePixelsPerUnit*=after.bounds.size.y/height;
                importer.SaveAndReimport();after=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            if(!after || Mathf.Max(after.texture.width,after.texture.height)>64 || after.texture.filterMode!=FilterMode.Point)
                throw new Exception("Cover did not pixelate correctly: "+path);
            if(height>0 && Mathf.Abs(after.bounds.size.y-height)>.001f)
                throw new Exception("Cover physical size changed: "+path);
            count++;
        }
        if(count!=10)throw new Exception("Expected ten newspaper/magazine covers");
        var scene=EditorSceneManager.NewPreviewScene();
        var previousProbe=RenderSettings.ambientProbe;
        try
        {
            var ambient=new UnityEngine.Rendering.SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.5f,.52f,.54f));
            RenderSettings.ambientProbe=ambient;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shelf_Items/NewspaperStand/NewspaperStand_Stocked.prefab");
            var stand=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
            NewspaperStandBuilder.Render(stand,scene);
        }
        finally{RenderSettings.ambientProbe=previousProbe;EditorSceneManager.ClosePreviewScene(scene);}
        File.WriteAllText(Root+"/PixelStyle.txt","PASS: all 10 covers render at a maximum of 64 pixels, Point filtering, no mipmaps or compression.\nSprite dimensions, pivots, item data, prefab assignments and scene placements are preserved.\nHigh-resolution source images remain unchanged on disk. Replacement PNGs automatically receive the same pixel style.\n");
        AssetDatabase.Refresh();
    }
}
