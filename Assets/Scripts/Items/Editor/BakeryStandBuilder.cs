using System;
using System.IO;
using System.Linq;
using How2Shop.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BakeryStandBuilder
{
    const string Items="Assets/Packages/How2ShopAssets/items/Bakery";
    const string Root="Assets/Shelf_Items/BakeryStand";
    const string Request="UserSettings/BakeryStand.request";
    static readonly string[] Names={"SourdoughLoaf","Baguette","Croissant","Pretzel","CinnamonRoll"};
    static readonly string[] Labels={"Sourdough loaf","Baguette","Croissant","Pretzel","Cinnamon roll"};
    static readonly int[] Prices={275,125,110,95,150};
    static BakeryStandBuilder(){EditorApplication.delayCall+=Auto;}
    static void Auto()
    {
        if(!File.Exists(Request))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Auto;return;}
        try{Build();File.Delete(Request);}
        catch(Exception e){File.WriteAllText("Logs/BakeryStandError.txt",e.ToString());File.Delete(Request);Debug.LogException(e);}
    }
    [MenuItem("Tools/Shelf Items/Build bakery stand")]
    public static void Build()
    {
        foreach(var dir in new[]{Items+"/Sprites",Items+"/Data",Items+"/Prefabs",Items+"/Materials",Root})Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var definitions=new ItemDefinition[5];var prefabs=new GameObject[5];
            for(int i=0;i<5;i++)
            {
                string path=Items+"/Sprites/"+Names[i]+".png";
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                if(!imp)throw new Exception("Missing bakery image "+path);
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
                imp.spritePixelsPerUnit=64;imp.maxTextureSize=64;imp.filterMode=FilterMode.Point;
                imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.textureCompression=TextureImporterCompression.Uncompressed;
                imp.wrapMode=TextureWrapMode.Clamp;imp.npotScale=TextureImporterNPOTScale.None;imp.SaveAndReimport();
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var data=AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items+"/Data/"+Names[i]+".asset");
                if(!data){data=ScriptableObject.CreateInstance<ItemDefinition>();AssetDatabase.CreateAsset(data,Items+"/Data/"+Names[i]+".asset");}
                var so=new SerializedObject(data);so.FindProperty("id").stringValue="bakery_"+Names[i].ToLowerInvariant();
                so.FindProperty("displayName").stringValue=Labels[i];so.FindProperty("pricePence").intValue=Prices[i];
                so.FindProperty("icon").objectReferenceValue=sprite;so.FindProperty("worldSprite").objectReferenceValue=sprite;
                var tags=so.FindProperty("tags");tags.arraySize=2;tags.GetArrayElementAtIndex(0).stringValue="bakery";tags.GetArrayElementAtIndex(1).stringValue="food";so.ApplyModifiedPropertiesWithoutUndo();definitions[i]=data;
                var mat=AssetDatabase.LoadAssetAtPath<Material>(Items+"/Materials/"+Names[i]+".mat");
                if(!mat){mat=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Shelf_Items/Materials/Cereal.mat"));AssetDatabase.CreateAsset(mat,Items+"/Materials/"+Names[i]+".mat");}
                mat.SetTexture("_Artwork",sprite.texture);EditorUtility.SetDirty(mat);
                var go=new GameObject(Names[i]);SceneManager.MoveGameObjectToScene(go,scene);go.AddComponent<SpriteRenderer>().sharedMaterial=mat;go.AddComponent<ItemSpriteView>().SetItem(data);
                go.transform.localScale=Vector3.one*(.29f/Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y));
                prefabs[i]=PrefabUtility.SaveAsPrefabAsset(go,Items+"/Prefabs/"+Names[i]+".prefab");UnityEngine.Object.DestroyImmediate(go);
            }
            var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>(Items+"/Data/BakeryCatalog.asset");
            if(!catalog){catalog=ScriptableObject.CreateInstance<ItemCatalog>();AssetDatabase.CreateAsset(catalog,Items+"/Data/BakeryCatalog.asset");}
            var cat=new SerializedObject(catalog);var entries=cat.FindProperty("items");entries.arraySize=5;for(int i=0;i<5;i++)entries.GetArrayElementAtIndex(i).objectReferenceValue=definitions[i];cat.ApplyModifiedPropertiesWithoutUndo();
            var root=new GameObject("BakeryStand_Stocked");SceneManager.MoveGameObjectToScene(root,scene);
            var dark=Palette("Charcoal");var wood=Palette("Brown");var edge=Palette("Copper");var cream=Palette("Cream");
            Cube(root.transform,"Plinth",new Vector3(0,.06f,0),new Vector3(2.65f,.12f,.94f),dark);
            Cube(root.transform,"Wood back",new Vector3(0,1.04f,.40f),new Vector3(2.62f,1.94f,.06f),wood);
            foreach(float x in new[]{-1.30f,1.30f}){Cube(root.transform,"Frame upright",new Vector3(x,1.02f,.36f),new Vector3(.065f,2.04f,.10f),dark);Cube(root.transform,"Wood side",new Vector3(x,.90f,0),new Vector3(.045f,1.55f,.86f),wood);}
            var stock=Group(root.transform,"Bakery items - 75 individual 2D sprites");
            for(int tier=0;tier<3;tier++)
            {
                var tray=Group(root.transform,"Display tier "+(tier+1));tray.localPosition=new Vector3(0,.42f+tier*.46f,tier*.045f);tray.localRotation=Quaternion.Euler(-12,0,0);
                Cube(tray,"Shelf",Vector3.zero,new Vector3(2.55f,.045f,.79f),wood);
                Cube(tray,"Front lip",new Vector3(0,.05f,-.39f),new Vector3(2.58f,.10f,.045f),edge);
                for(int bay=0;bay<5;bay++)
                {
                    float x=(bay-2)*.50f;Cube(tray,"Cream tray liner",new Vector3(x,.03f,0),new Vector3(.46f,.012f,.69f),cream);
                    Cube(tray,"Price ticket",new Vector3(x,.065f,-.418f),new Vector3(.39f,.07f,.008f),cream);
                    Label(tray,Labels[bay]+" "+definitions[bay].PriceLabel,new Vector3(x,.065f,-.425f),.0052f,Color.black);
                    for(int k=0;k<5;k++)
                    {
                        var item=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[bay],scene);item.transform.SetParent(stock,false);
                        item.transform.position=tray.TransformPoint(new Vector3(x+(k%2==0?-.075f:.075f),.055f+(k/2)*.018f,-.24f+(k/2)*.22f));
                        item.transform.rotation=tray.rotation*Quaternion.Euler(65,0,(k%2==0?-13:12));
                    }
                }
                for(int d=0;d<6;d++)Cube(tray,"Bin divider",new Vector3((d-2.5f)*.5f,.08f,0),new Vector3(.016f,.12f,.77f),edge);
            }
            Cube(root.transform,"Header cream board",new Vector3(0,1.94f,.33f),new Vector3(2.60f,.23f,.075f),cream);
            Cube(root.transform,"Header top trim",new Vector3(0,2.07f,.33f),new Vector3(2.67f,.04f,.10f),dark);
            Label(root.transform,"FRESH BAKERY",new Vector3(0,1.94f,.28f),.033f,new Color(.16f,.10f,.06f));
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.05f,0);collider.size=new Vector3(2.68f,2.10f,1.0f);
            var stocked=PrefabUtility.SaveAsPrefabAsset(root,Root+"/BakeryStand_Stocked.prefab");stock.gameObject.SetActive(false);root.name="BakeryStand_Empty";PrefabUtility.SaveAsPrefabAsset(root,Root+"/BakeryStand_Empty.prefab");stock.gameObject.SetActive(true);
            AssetDatabase.SaveAssets();
            if(catalog.GetValidationErrors().Count>0||stocked.GetComponentsInChildren<ItemSpriteView>().Length!=75)throw new Exception("Invalid bakery stock");
            foreach(var r in stocked.GetComponentsInChildren<Renderer>())if(r.sharedMaterials.Any(m=>!m))throw new Exception("Missing material "+r.name);
            File.WriteAllText(Root+"/Validation.txt","PASS: five distinct 2D bakery items, five definitions and prefabs, valid BakeryCatalog, 75 stocked sprites, assigned materials, empty/stocked stand variants and root collider.\n");
            Render(root,scene);
            File.WriteAllText(Root+"/README.md","Bakery stand: 2.68m wide, 2.10m high, 1m deep. Floor pivot; front faces local -Z. Three angled shelves with five lined bins each, 75 flat sprites. Stocked and empty prefabs. Shared ColoringMaterials palette. Individual sprites, materials, definitions, prefabs and BakeryCatalog are in "+Items+". Example prices are editable. Rebuild via Tools > Shelf Items > Build bakery stand. Drag the stocked prefab into your scene.\n");AssetDatabase.Refresh();
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static Material Palette(string n){var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_"+n+".mat");if(!m)throw new Exception(n);return m;}
    static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    static void Cube(Transform p,string n,Vector3 pos,Vector3 size,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
    static void Label(Transform p,string value,Vector3 pos,float size,Color color){var t=Group(p,value).gameObject.AddComponent<TextMesh>();t.transform.localPosition=pos;t.text=value;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.color=color;t.GetComponent<Renderer>().sharedMaterial=t.font.material;}
    static void Render(GameObject root,Scene scene)
    {
        var c=new GameObject("Preview camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(c.gameObject,scene);c.scene=scene;c.transform.position=new Vector3(3,2.8f,-5);c.transform.LookAt(new Vector3(0,1,0));c.orthographic=true;c.orthographicSize=1.7f;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.17f,.19f,.22f);
        var l=new GameObject("Preview light").AddComponent<Light>();SceneManager.MoveGameObjectToScene(l.gameObject,scene);l.type=LightType.Directional;l.intensity=2;l.transform.rotation=Quaternion.Euler(35,-30,0);
        var oldProbe=RenderSettings.ambientProbe;var sh=new UnityEngine.Rendering.SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.5f,.52f,.54f));RenderSettings.ambientProbe=sh;
        var rt=new RenderTexture(1000,1000,24);var previous=RenderTexture.active;Texture2D image=null;
        try{c.targetTexture=rt;c.Render();c.Render();RenderTexture.active=rt;image=new Texture2D(1000,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,1000),0,0);image.Apply();File.WriteAllBytes(Root+"/Preview.png",image.EncodeToPNG());}
        finally{RenderSettings.ambientProbe=oldProbe;RenderTexture.active=previous;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}
    }
}
