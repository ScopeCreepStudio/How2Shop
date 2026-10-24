using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using How2Shop.Items;

public static class FishStandBuilder
{
    const string Items="Assets/items/Fish";
    const string Root="Assets/Shelf_Items/FishStand";
    static readonly string[] Names={"Salmon","RainbowTrout","Cod","Mackerel","SeaBream","Herring"};
    static readonly string[] Labels={"Salmon","Rainbow trout","Cod","Mackerel","Sea bream","Herring"};
    static readonly int[] Prices={699,449,549,299,499,249};

    [MenuItem("Tools/Shelf Items/Build fish stand")]
    public static void Build()
    {
        if(File.Exists(Root+"/FishStand_Stocked.prefab"))
            throw new InvalidOperationException("The fish stand already exists. Edit its prefab directly to preserve hand-placed ice and scene overrides; this builder is for initial creation only.");
        foreach(var dir in new[]{Items+"/Sprites",Items+"/Data",Items+"/Prefabs",Items+"/Materials",Root})Directory.CreateDirectory(dir);
        AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var definitions=new ItemDefinition[6];var prefabs=new GameObject[6];
            for(int i=0;i<6;i++)
            {
                string path=Items+"/Sprites/"+Names[i]+".png";
                var texture=new Texture2D(2,2);texture.LoadImage(File.ReadAllBytes(path));
                int left=texture.width,right=-1,bottom=texture.height,top=-1,transparent=0;
                var pixels=texture.GetPixels32();
                for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)
                {
                    if(pixels[y*texture.width+x].a<16)transparent++;
                    if(pixels[y*texture.width+x].a<128)continue;
                    left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);
                }
                if(right<left||transparent==0)throw new Exception("Fish silhouette/transparency missing: "+Names[i]);
                float fraction=(right-left+1f)/texture.width;
                var imp=(TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
                imp.spritePixelsPerUnit=256;imp.maxTextureSize=256;imp.filterMode=FilterMode.Point;
                imp.mipmapEnabled=false;imp.alphaIsTransparency=true;imp.textureCompression=TextureImporterCompression.Uncompressed;
                imp.wrapMode=TextureWrapMode.Clamp;imp.npotScale=TextureImporterNPOTScale.None;
                var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2((left+right+1f)/(2*texture.width),(bottom+top+1f)/(2*texture.height));settings.spriteMeshType=SpriteMeshType.FullRect;imp.SetTextureSettings(settings);imp.SaveAndReimport();UnityEngine.Object.DestroyImmediate(texture);
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
                string dp=Items+"/Data/"+Names[i]+".asset";
                var data=AssetDatabase.LoadAssetAtPath<ItemDefinition>(dp);
                if(!data){data=ScriptableObject.CreateInstance<ItemDefinition>();AssetDatabase.CreateAsset(data,dp);}
                var so=new SerializedObject(data);so.FindProperty("id").stringValue="fish_"+Names[i].ToLowerInvariant();so.FindProperty("displayName").stringValue=Labels[i];so.FindProperty("pricePence").intValue=Prices[i];so.FindProperty("icon").objectReferenceValue=sprite;so.FindProperty("worldSprite").objectReferenceValue=sprite;
                var tags=so.FindProperty("tags");tags.arraySize=3;tags.GetArrayElementAtIndex(0).stringValue="fish";tags.GetArrayElementAtIndex(1).stringValue="fresh";tags.GetArrayElementAtIndex(2).stringValue="food";so.ApplyModifiedPropertiesWithoutUndo();definitions[i]=data;
                string mp=Items+"/Materials/"+Names[i]+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);
                if(!mat){mat=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Shelf_Items/Materials/Cereal.mat"));AssetDatabase.CreateAsset(mat,mp);}mat.SetTexture("_Artwork",sprite.texture);EditorUtility.SetDirty(mat);
                var go=new GameObject(Names[i]);SceneManager.MoveGameObjectToScene(go,scene);go.AddComponent<SpriteRenderer>().sharedMaterial=mat;go.AddComponent<ItemSpriteView>().SetItem(data);go.transform.localScale=Vector3.one*(.43f/(sprite.bounds.size.x*fraction));
                prefabs[i]=PrefabUtility.SaveAsPrefabAsset(go,Items+"/Prefabs/"+Names[i]+".prefab");UnityEngine.Object.DestroyImmediate(go);
            }
            string cp=Items+"/Data/FishCatalog.asset";var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>(cp);
            if(!catalog){catalog=ScriptableObject.CreateInstance<ItemCatalog>();AssetDatabase.CreateAsset(catalog,cp);}var cs=new SerializedObject(catalog);var list=cs.FindProperty("items");list.arraySize=6;for(int i=0;i<6;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=definitions[i];cs.ApplyModifiedPropertiesWithoutUndo();
            var root=new GameObject("FishStand_Stocked");SceneManager.MoveGameObjectToScene(root,scene);
            var metal=Palette("Slate");var trim=Palette("Charcoal");var ice=Palette("White");var blue=Palette("Sky");var navy=Palette("Navy");var cream=Palette("Cream");
            Cube(root.transform,"Stainless display pan",new Vector3(0,.86f,0),new Vector3(3.6f,.12f,1.02f),metal);
            Cube(root.transform,"Lower storage shelf",new Vector3(0,.25f,0),new Vector3(3.48f,.045f,.88f),metal);
            foreach(float x in new[]{-1.66f,0,1.66f})foreach(float z in new[]{-.4f,.4f})
            {
                Cube(root.transform,"Square metal leg",new Vector3(x,.48f,z),new Vector3(.055f,.8f,.055f),metal);
                Cube(root.transform,"Caster bracket",new Vector3(x,.09f,z),new Vector3(.09f,.09f,.07f),metal);
                Cube(root.transform,"Blocky caster wheel",new Vector3(x,.045f,z),new Vector3(.07f,.08f,.10f),trim);
            }
            Cube(root.transform,"Front charcoal rim",new Vector3(0,.96f,-.51f),new Vector3(3.64f,.1f,.045f),trim);
            Cube(root.transform,"Front blue accent",new Vector3(0,.865f,-.526f),new Vector3(3.57f,.025f,.016f),blue);
            Cube(root.transform,"Rear raised rim",new Vector3(0,1.02f,.51f),new Vector3(3.64f,.22f,.045f),metal);
            foreach(float x in new[]{-1.8f,1.8f})Cube(root.transform,"End rim",new Vector3(x,.97f,0),new Vector3(.045f,.12f,1.04f),metal);
            var stock=Group(root.transform,"Fish stock - 24 upright 2D sprites");
            var rng=new System.Random(4206);
            for(int bay=0;bay<6;bay++)
            {
                float x=(bay-2.5f)*.59f;var tray=Group(root.transform,"Ice tray - "+Labels[bay]);
                Cube(tray,"Cold blue liner",new Vector3(x,.93f,0),new Vector3(.55f,.035f,.92f),blue);
                Cube(tray,"Crushed ice bed",new Vector3(x,.951f,0),new Vector3(.53f,.03f,.90f),ice);
                for(int k=0;k<26;k++)
                {
                    var chip=Cube(tray,"Pixel ice",new Vector3(x+((float)rng.NextDouble()-.5f)*.48f,.976f,((float)rng.NextDouble()-.5f)*.84f),new Vector3(.035f+(float)rng.NextDouble()*.035f,.018f,.028f+(float)rng.NextDouble()*.04f),k%4==0?blue:ice);chip.localRotation=Quaternion.Euler(0,rng.Next(0,90),0);
                }
                for(int row=0;row<4;row++)
                {
                    var fish=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[bay],scene);fish.transform.SetParent(stock,false);var sprite=prefabs[bay].GetComponent<SpriteRenderer>().sprite; float halfHeight=sprite.bounds.size.y*prefabs[bay].transform.localScale.y*.5f; fish.transform.localPosition=new Vector3(x+(row%2==0?-.018f:.018f),.985f+halfHeight,-.32f+row*.21f);fish.transform.localRotation=Quaternion.identity;
                }
                Cube(tray,"Price ticket",new Vector3(x,.98f,-.543f),new Vector3(.50f,.125f,.012f),navy);
                Label(tray,Labels[bay].ToUpperInvariant()+"\n"+definitions[bay].PriceLabel+" EACH",new Vector3(x,.982f,-.553f),.009f,Color.white);
            }
            for(int i=0;i<7;i++)Cube(root.transform,"Tray separator",new Vector3((i-3)*.59f,.978f,0),new Vector3(.016f,.075f,.98f),metal);
            foreach(float x in new[]{-.8f,.8f})Cube(root.transform,"Sign support",new Vector3(x,1.13f,.46f),new Vector3(.025f,.3f,.025f),metal);
            Cube(root.transform,"Fresh fish header",new Vector3(0,1.28f,.46f),new Vector3(2.0f,.20f,.045f),navy);
            Label(root.transform,"FRESH FISH",new Vector3(0,1.28f,.43f),.032f,Color.white);
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.69f,0);collider.size=new Vector3(3.65f,1.38f,1.1f);
            var stocked=PrefabUtility.SaveAsPrefabAsset(root,Root+"/FishStand_Stocked.prefab");stock.gameObject.SetActive(false);root.name="FishStand_Empty";PrefabUtility.SaveAsPrefabAsset(root,Root+"/FishStand_Empty.prefab");stock.gameObject.SetActive(true);root.name="FishStand_Stocked";
            AssetDatabase.SaveAssets();
            if(catalog.GetValidationErrors().Count>0||stocked.GetComponentsInChildren<ItemSpriteView>().Length!=24)throw new Exception("Fish catalog/stock validation failed");
            foreach(var r in stocked.GetComponentsInChildren<Renderer>())if(r.sharedMaterials.Any(m=>!m))throw new Exception("Missing material: "+r.name);
            File.WriteAllText(Root+"/Validation.txt","PASS: six transparent fish sprites, six independent materials, definitions and prefabs; valid FishCatalog; 24 fish on six ice trays; all renderer materials assigned; stocked/empty stand prefabs and collider.\n");
            Render(root,scene);
            File.WriteAllText(Root+"/README.md","Fish stand: 3.65m wide, 1.38m high, 1.1m deep. Floor pivot; front faces local -Z. Six ice trays with four upright 2D fish each. Shared game palette, metal frame, casters, price tickets and lower shelf. Empty and stocked prefabs. Fish assets and editable example per-fish prices are in Assets/items/Fish. Rebuild via Tools > Shelf Items > Build fish stand. Drag FishStand_Stocked into the scene.\n");AssetDatabase.Refresh();
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static Material Palette(string n){var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_"+n+".mat");if(!m)throw new Exception("Missing palette "+n);return m;}
    static Transform Group(Transform p,string n){var g=new GameObject(n);g.transform.SetParent(p,false);return g.transform;}
    static Transform Cube(Transform p,string n,Vector3 pos,Vector3 size,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;}
    static void Label(Transform p,string value,Vector3 pos,float size,Color color){var t=Group(p,value).gameObject.AddComponent<TextMesh>();t.transform.localPosition=pos;t.text=value;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.color=color;t.GetComponent<Renderer>().sharedMaterial=t.font.material;}
    static void Render(GameObject root,Scene scene)
    {
        var c=new GameObject("Preview camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(c.gameObject,scene);c.scene=scene;c.transform.position=new Vector3(2.6f,3.3f,-4.5f);c.transform.LookAt(new Vector3(0,.7f,0));c.orthographic=true;c.orthographicSize=2.05f;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.17f,.19f,.22f);
        var l=new GameObject("Preview light").AddComponent<Light>();SceneManager.MoveGameObjectToScene(l.gameObject,scene);l.type=LightType.Directional;l.intensity=2;l.transform.rotation=Quaternion.Euler(35,-30,0);
        var oldProbe=RenderSettings.ambientProbe;var sh=new UnityEngine.Rendering.SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.5f,.52f,.54f));RenderSettings.ambientProbe=sh;
        var rt=new RenderTexture(1200,900,24);var previous=RenderTexture.active;Texture2D image=null;
        try{c.targetTexture=rt;c.Render();c.Render();RenderTexture.active=rt;image=new Texture2D(1200,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1200,900),0,0);image.Apply();File.WriteAllBytes(Root+"/Preview.png",image.EncodeToPNG());}
        finally{RenderSettings.ambientProbe=oldProbe;RenderTexture.active=previous;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}
    }
}

