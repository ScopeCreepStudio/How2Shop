using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using How2Shop.Items;
using System;
using System.IO;
using System.Collections.Generic;

[InitializeOnLoad]
public static class CleaningProductsBuilder
{
    const string Root="Assets/items/CleaningProducts";
    [Serializable] class Entry { public string name,label,tag;public int price;public float height; }
    [Serializable] class Entries { public Entry[] items; }
    static CleaningProductsBuilder(){EditorApplication.delayCall+=Auto;}
    static void Auto(){if(!File.Exists(Root+"/Editor/.build-request"))return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}Build();}
    [MenuItem("Tools/Shelf Items/Build cleaning products")]
    public static void Build()
    {
        var entries=JsonUtility.FromJson<Entries>(File.ReadAllText(Root+"/Editor/items.json")).items;
        var milk=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shelf_Items/Prefabs/Cereal.prefab");
        var sourceMaterial=milk.GetComponent<SpriteRenderer>().sharedMaterial;
        if(!sourceMaterial)sourceMaterial=AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        string mp=Root+"/Materials/ShelfItems.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(mp);
        if(!material){material=new Material(sourceMaterial);material.name="ShelfItems";AssetDatabase.CreateAsset(material,mp);}
        var definitions=new List<ItemDefinition>();var report=new System.Text.StringBuilder();
        foreach(var e in entries){
            string path=Root+"/Sprites/"+e.name+".png";
            var raw=new Texture2D(2,2,TextureFormat.RGBA32,false);raw.LoadImage(File.ReadAllBytes(path));
            int minX=raw.width,minY=raw.height,maxX=-1,maxY=-1,transparent=0;
            var pixels=raw.GetPixels32();
            for(int y=0;y<raw.height;y++)for(int x=0;x<raw.width;x++){
                if(pixels[y*raw.width+x].a<16){transparent++;continue;}
                if(pixels[y*raw.width+x].a<128)continue;
                minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
            }
            if(maxX<minX||transparent==0)throw new Exception("Missing sprite silhouette or transparency: "+e.name);
            float widthFraction=(maxX-minX+1f)/raw.width; float heightFraction=(maxY-minY+1f)/raw.height;
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;importer.maxTextureSize=256;importer.npotScale=TextureImporterNPOTScale.None;importer.spritePixelsPerUnit=64;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2((minX+maxX+1f)/(2*raw.width),minY/(float)raw.height);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.SaveAndReimport();UnityEngine.Object.DestroyImmediate(raw);
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(!sprite)throw new Exception("Failed sprite import: "+e.name);
            string dp=Root+"/Data/"+e.name+".asset";var definition=AssetDatabase.LoadAssetAtPath<ItemDefinition>(dp);
            if(!definition){definition=ScriptableObject.CreateInstance<ItemDefinition>();AssetDatabase.CreateAsset(definition,dp);}
            var serialized=new SerializedObject(definition);serialized.FindProperty("id").stringValue="cleaning_"+e.name.ToLowerInvariant();serialized.FindProperty("displayName").stringValue=e.label;serialized.FindProperty("pricePence").intValue=e.price;
            var tags=serialized.FindProperty("tags");tags.arraySize=2;tags.GetArrayElementAtIndex(0).stringValue=e.tag;tags.GetArrayElementAtIndex(1).stringValue="supermarket";
            serialized.FindProperty("icon").objectReferenceValue=sprite;serialized.FindProperty("worldSprite").objectReferenceValue=sprite;serialized.ApplyModifiedPropertiesWithoutUndo();definitions.Add(definition);
            string itemMaterialPath=Root+"/Materials/"+e.name+".mat";
            var itemMaterial=AssetDatabase.LoadAssetAtPath<Material>(itemMaterialPath);
            if(!itemMaterial){itemMaterial=new Material(material);itemMaterial.name=e.name;AssetDatabase.CreateAsset(itemMaterial,itemMaterialPath);}
            itemMaterial.SetTexture("_Artwork",sprite.texture);EditorUtility.SetDirty(itemMaterial);
            var go=new GameObject(e.name);var renderer=go.AddComponent<SpriteRenderer>();renderer.sharedMaterial=itemMaterial;renderer.sortingLayerID=milk.GetComponent<SpriteRenderer>().sortingLayerID;renderer.sortingOrder=milk.GetComponent<SpriteRenderer>().sortingOrder;
            go.AddComponent<ItemSpriteView>().SetItem(definition);go.transform.localScale=Vector3.one*(Mathf.Min(.32f/(sprite.bounds.size.x*widthFraction),e.height/(sprite.bounds.size.y*heightFraction)));
            PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+e.name+".prefab");UnityEngine.Object.DestroyImmediate(go);
            report.AppendLine("PASS "+e.name+": transparent sprite, bottom pivot, material, item data and prefab.");
        }
        string cp=Root+"/Data/CleaningProducts.asset";var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>(cp);
        if(!catalog){catalog=ScriptableObject.CreateInstance<ItemCatalog>();AssetDatabase.CreateAsset(catalog,cp);}
        var cs=new SerializedObject(catalog);var list=cs.FindProperty("items");list.arraySize=definitions.Count;for(int i=0;i<definitions.Count;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=definitions[i];cs.ApplyModifiedPropertiesWithoutUndo();
        var errors=catalog.GetValidationErrors();if(errors.Count>0)throw new Exception(string.Join("\n",errors));
        AssetDatabase.SaveAssets();
        RenderPreview(entries);
        foreach(var e in entries){var go=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+e.name+".prefab");if(!go.GetComponent<ItemSpriteView>().Item||!go.GetComponent<SpriteRenderer>().sprite||!go.GetComponent<SpriteRenderer>().sharedMaterial)throw new Exception("Broken prefab "+e.name);}
        report.AppendLine("PASS: "+entries.Length+" prefabs and definitions; independent catalog has no validation errors. Cleaning product assets validated.");File.WriteAllText(Root+"/Validation.txt",report.ToString());if(File.Exists(Root+"/Editor/.build-request"))File.Delete(Root+"/Editor/.build-request");AssetDatabase.Refresh();
    }
    static void RenderPreview(Entry[] entries){
        var scene=EditorSceneManager.NewPreviewScene();var rt=new RenderTexture(1200,900,24);
        try{
            for(int i=0;i<entries.Length;i++){var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+entries[i].name+".prefab"));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=new Vector3(i%5*1.3f,(2-i/5)*1.3f,0);}
            var cameraGo=new GameObject("PreviewCamera");SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.AddComponent<Camera>();camera.scene=scene;camera.transform.position=new Vector3(2.6f,1.7f,-10);camera.orthographic=true;camera.orthographicSize=2.6f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.17f,.2f,.24f);camera.targetTexture=rt;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=rt;var png=new Texture2D(1200,900,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1200,900),0,0);png.Apply();RenderTexture.active=previous;File.WriteAllBytes(Root+"/Preview/AllItems.png",png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);
        }finally{EditorSceneManager.ClosePreviewScene(scene);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
}


