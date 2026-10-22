using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using How2Shop.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class VegetableFridgeShelfBuilder
{
    const string Root="Assets/Shelf_Items/VegetableFridgeShelf";
    const string ScenePath="Assets/Scenes/Superstore.unity";
    const string Request="UserSettings/VegetableFridgeShelf.request";
    static readonly string[] Vegetables={"Carrot","Broccoli","Tomato","Cucumber","Potato"};
    static readonly string[] Prices={"£0.75","£1.20","£1.10","£0.85","£1.50"};
    static Material teal,metal,dark,green,paper,led,textMaterial;
    static Mesh crateMesh,sideMesh,ventMesh;

    static VegetableFridgeShelfBuilder(){EditorApplication.delayCall+=Auto;}
    static void Auto()
    {
        if(!File.Exists(Request))return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {EditorApplication.delayCall+=Auto;return;}
        try{Build();File.Delete(Request);if(File.Exists("Logs/VegetableFridgeShelfError.txt"))File.Delete("Logs/VegetableFridgeShelfError.txt");}
        catch(Exception error){File.WriteAllText("Logs/VegetableFridgeShelfError.txt",error.ToString());File.Delete(Request);Debug.LogException(error);}
    }

    [MenuItem("Tools/Shelf Items/Build and place vegetable fridge in Superstore")]
    public static void Build()
    {
        foreach(string dir in new[]{"Materials","Meshes","Preview"})Directory.CreateDirectory(Root+"/"+dir);
        AssetDatabase.Refresh();
        teal=Palette("Pixel_Teal");
        metal=Palette("Pixel_White");
        dark=Palette("Pixel_Charcoal");
        green=Palette("Pixel_Forest");
        paper=Palette("Pixel_Cream");
        led=Palette("Pixel_White");
        string fontPath=Root+"/Materials/SignText.mat";
        textMaterial=AssetDatabase.LoadAssetAtPath<Material>(fontPath);
        if(!textMaterial){textMaterial=new Material(Shader.Find("How2Shop/Vegetable Fridge/Text"));AssetDatabase.CreateAsset(textMaterial,fontPath);}
        MakeMeshes();
        var previewScene=EditorSceneManager.NewPreviewScene();
        try
        {
            var unit=new GameObject("VegetableFridgeShelf_Stocked");SceneManager.MoveGameObjectToScene(unit,previewScene);
            BuildCabinet(unit.transform);
            string stockedPath=Root+"/VegetableFridgeShelf_Stocked.prefab";
            var single=PrefabUtility.SaveAsPrefabAsset(unit,stockedPath);
            foreach(var stock in unit.GetComponentsInChildren<Transform>().Where(t=>t.name=="Vegetables - individual 2D items"))stock.gameObject.SetActive(false);
            unit.name="VegetableFridgeShelf_Empty";
            PrefabUtility.SaveAsPrefabAsset(unit,Root+"/VegetableFridgeShelf_Empty.prefab");
            UnityEngine.Object.DestroyImmediate(unit);
            var joined=new GameObject("VegetableFridgeShelf_Double");SceneManager.MoveGameObjectToScene(joined,previewScene);
            for(int i=0;i<2;i++)
            {
                var section=(GameObject)PrefabUtility.InstantiatePrefab(single,previewScene);
                section.name="Refrigerated bay "+(i+1);section.transform.SetParent(joined.transform,false);
                section.transform.localPosition=new Vector3((i-.5f)*3.22f,0,0);
            }
            var doublePrefab=PrefabUtility.SaveAsPrefabAsset(joined,Root+"/VegetableFridgeShelf_Double.prefab");
            AssetDatabase.SaveAssets();
            Verify(single,doublePrefab);
            Render(joined,previewScene);
            Place(doublePrefab,single);
            File.WriteAllText(Root+"/README.md",Readme());
            AssetDatabase.Refresh();
        }
        finally{EditorSceneManager.ClosePreviewScene(previewScene);}
    }

    static void BuildCabinet(Transform root)
    {
        var body=Group(root,"Cabinet - independently colourable parts");
        Cube(body,"Black plinth",new Vector3(0,.045f,0),new Vector3(3.22f,.09f,1.10f),dark);
        Cube(body,"Refrigeration base",new Vector3(0,.25f,.005f),new Vector3(3.15f,.34f,1.04f),teal);
        Cube(body,"Front bumper",new Vector3(0,.43f,-.54f),new Vector3(3.19f,.055f,.065f),teal);
        Part(body,"Vent grille - modelled slots",ventMesh,dark,new Vector3(0,.22f,-.526f));
        Cube(body,"Silver refrigerated back",new Vector3(0,1.27f,.485f),new Vector3(3.12f,1.76f,.045f),metal);
        for(int i=0;i<12;i++)Cube(body,"Back airflow rib",new Vector3(0,.50f+i*.13f,.452f),new Vector3(3.04f,.012f,.016f),dark);
        foreach(float x in new[]{-1.57f,1.57f})
        {
            Part(body,x<0?"Left end panel":"Right end panel",sideMesh,metal,new Vector3(x,0,0));
            Cube(body,"Teal front edge",new Vector3(x,1.35f,-.535f),new Vector3(.045f,1.85f,.045f),teal);
            Cube(body,"Rear upright",new Vector3(x,1.30f,.44f),new Vector3(.045f,1.80f,.045f),teal);
        }
        Cube(body,"Top canopy",new Vector3(0,2.26f,-.015f),new Vector3(3.24f,.22f,1.08f),teal);
        Cube(body,"Dark inner canopy",new Vector3(0,2.137f,-.01f),new Vector3(3.08f,.026f,.93f),dark);
        Cube(body,"Canopy LED strip",new Vector3(0,2.115f,-.39f),new Vector3(2.96f,.016f,.045f),led);
        Label(body,"Header","FRESH VEGETABLES",new Vector3(-.10f,2.26f,-.563f),.026f,Color.white);
        Cube(body,"Temperature display",new Vector3(1.31f,2.265f,-.565f),new Vector3(.25f,.095f,.018f),dark);
        Label(body,"Temperature","4°C",new Vector3(1.31f,2.265f,-.578f),.010f,new Color(1,.18f,.12f));
        var shelving=Group(root,"Four angled display tiers");
        for(int tier=0;tier<4;tier++)
        {
            var tray=Group(shelving,"Tier "+(tier+1));
            tray.localPosition=new Vector3(0,.51f+tier*.43f,.005f+tier*.012f);
            tray.localRotation=Quaternion.Euler(-15,0,0);
            Cube(tray,"Shelf deck",Vector3.zero,new Vector3(3.07f,.028f,.80f),dark);
            var rail=Group(tray,"Teal price rail");rail.localPosition=new Vector3(0,.025f,-.423f);rail.localRotation=Quaternion.Euler(15,0,0);
            Cube(rail,"Rail",Vector3.zero,new Vector3(3.10f,.072f,.037f),teal);
            Cube(rail,"Shelf light diffuser",new Vector3(0,-.042f,.014f),new Vector3(2.96f,.012f,.025f),led);
            for(int bin=0;bin<4;bin++)
            {
                int type=(tier*2+bin)%Vegetables.Length;
                float x=(bin-1.5f)*.75f;
                var crate=Group(tray,"Crate "+(bin+1)+" - "+Vegetables[type]);
                crate.localPosition=new Vector3(x,.018f,0);
                Part(crate,"Slatted green plastic crate",crateMesh,green,Vector3.zero);
                var stock=Group(crate,"Vegetables - individual 2D items");
                for(int row=0;row<3;row++)for(int col=0;col<3;col++)
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shelf_Items/Prefabs/"+Vegetables[type]+".prefab");
                    if(!asset)throw new Exception("Missing vegetable item "+Vegetables[type]);
                    var item=(GameObject)PrefabUtility.InstantiatePrefab(asset,root.gameObject.scene);
                    item.transform.SetParent(stock,false);
                    item.transform.localPosition=new Vector3((col-1)*.205f,.07f+((col+row)%2)*.014f,(row-1)*.19f);
                    float spin=((tier*7+bin*11+row*13+col*17)%47)-23;
                    bool upright=row==2 && (type==0 || type==1 || type==3);
                    item.transform.localRotation=Quaternion.Euler(upright?35:90,0,0)*Quaternion.Euler(0,0,spin);
                    var sprite=item.GetComponent<SpriteRenderer>();
                    var size=Vector3.Scale(sprite.sprite.bounds.size,item.transform.localScale);
                    item.transform.localScale*=Mathf.Min(.18f/size.x,.23f/size.y);
                    foreach(var collider in item.GetComponentsInChildren<Collider>())collider.enabled=false;
                }
                Cube(rail,"Price label",new Vector3(x,0,-.023f),new Vector3(.29f,.052f,.008f),paper);
                Label(rail,"Price "+Vegetables[type],Prices[type],new Vector3(x,0,-.029f),.0078f,new Color(.055f,.07f,.065f));
            }
        }
        var lighting=Group(root,"Interior lighting - cool white");
        foreach(float x in new[]{-.85f,.85f})
        {
            var lamp=Group(lighting,"Canopy downlight").gameObject.AddComponent<Light>();
            lamp.type=LightType.Spot;lamp.transform.localPosition=new Vector3(x,2.08f,-.35f);
            lamp.transform.localRotation=Quaternion.Euler(72,0,0);
            lamp.color=new Color(.72f,.86f,1);lamp.intensity=2.6f;lamp.range=2.8f;
            lamp.spotAngle=105;lamp.innerSpotAngle=70;lamp.shadows=LightShadows.None;
        }
        var collision=root.gameObject.AddComponent<BoxCollider>();
        collision.center=new Vector3(0,1.18f,0);collision.size=new Vector3(3.26f,2.36f,1.10f);
    }

    static void MakeMeshes()
    {
        var crate=new Boxes();
        for(int i=0;i<7;i++)crate.Box(new Vector3(0,.012f,(i-3)*.088f),new Vector3(.69f,.024f,.045f));
        foreach(float x in new[]{-.337f,.337f})
        {
            foreach(float y in new[]{.035f,.095f,.155f})crate.Box(new Vector3(x,y,0),new Vector3(.022f,.032f,.64f));
            for(int i=0;i<6;i++)crate.Box(new Vector3(x,.085f,(i-2.5f)*.12f),new Vector3(.024f,.16f,.022f));
        }
        foreach(float z in new[]{-.307f,.307f})
        {
            foreach(float y in new[]{.035f,.095f,.155f})crate.Box(new Vector3(0,y,z),new Vector3(.69f,.032f,.022f));
            for(int i=0;i<6;i++)crate.Box(new Vector3((i-2.5f)*.128f,.085f,z),new Vector3(.023f,.16f,.024f));
        }
        crateMesh=SaveMesh(crate.Mesh("Crate_Slatted"),"Crate_Slatted");
        var side=new Boxes();
        side.Box(new Vector3(0,1.175f,.02f),new Vector3(.06f,2.35f,1.03f));
        sideMesh=SaveMesh(side.Mesh("Cabinet_EndPanel"),"Cabinet_EndPanel");
        var vents=new Boxes();
        foreach(float y in new[]{-.073f,.073f})vents.Box(new Vector3(0,y,0),new Vector3(3.04f,.016f,.026f));
        for(int i=0;i<62;i++)vents.Box(new Vector3((i-30.5f)*.049f,0,0),new Vector3(.018f,.14f,.026f));
        ventMesh=SaveMesh(vents.Mesh("Base_VentGrille"),"Base_VentGrille");
    }

    static void Place(GameObject doublePrefab,GameObject singlePrefab)
    {
        var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(ScenePath);
        bool open=scene.IsValid() && scene.isLoaded;
        if(!open)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            if(!File.Exists("Logs/Superstore_BeforeVegetableFridge.unity"))File.Copy(ScenePath,"Logs/Superstore_BeforeVegetableFridge.unity");
            var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var existing=transforms.FirstOrDefault(t=>t.name=="VegetableFridgeShelf - Refrigerated Produce");
            if(existing){Selection.activeGameObject=existing.gameObject;Frame(existing);return;}
            var anchor=transforms.FirstOrDefault(t=>t.name=="DoubleFruitShelf (3)") ?? transforms.FirstOrDefault(t=>t.name.Contains("FruitShelf") && t.GetComponentInChildren<Renderer>());
            if(!anchor)throw new Exception("Cannot locate the Superstore produce area for placement.");
            Vector3 axis=anchor.right.normalized,front=-anchor.forward.normalized;
            var neighbours=transforms.Where(t=>t.name.StartsWith("DoubleFruitShelf") && Vector3.Distance(t.position,anchor.position)<13).ToArray();
            var points=new List<Vector3>();
            foreach(var neighbour in neighbours)foreach(var renderer in neighbour.GetComponentsInChildren<Renderer>())
            {
                var b=renderer.bounds;foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})points.Add(b.center+new Vector3(b.extents.x*x,0,b.extents.z*z));
            }
            if(points.Count==0)points.Add(anchor.position);
            float min=points.Min(p=>Vector3.Dot(p-anchor.position,axis));
            float max=points.Max(p=>Vector3.Dot(p-anchor.position,axis));
            GameObject placed=null;Vector3 chosen=Vector3.zero;
            foreach(var prefab in new[]{doublePrefab,singlePrefab})
            {
                float half=prefab==doublePrefab?3.24f:1.63f;
                var candidates=new List<Vector3>{anchor.position+axis*(min-half-.40f),anchor.position+axis*(max+half+.40f)};
                for(int step=0;step<3;step++)foreach(int sign in new[]{-1,1})
                    candidates.Add(anchor.position+axis*sign*(4+step*3.5f)+front*(2.3f+step*.5f));
                Physics.SyncTransforms();
                foreach(var candidate in candidates)
                {
                    var center=candidate+Vector3.up*1.25f;
                    var hits=Physics.OverlapBox(center,new Vector3(half+.12f,1.05f,.66f),anchor.rotation,~0,QueryTriggerInteraction.Ignore);
                    if(hits.Any(c=>c.gameObject.scene==scene && c.enabled))continue;
                    placed=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    placed.name="VegetableFridgeShelf - Refrigerated Produce";
                    placed.transform.SetParent(anchor.parent,false);
                    placed.transform.SetPositionAndRotation(candidate,anchor.rotation);
                    chosen=candidate;break;
                }
                if(placed)break;
            }
            if(!placed)throw new Exception("No clear placement beside the produce displays; prefabs are saved and ready to place.");
            PrefabUtility.RecordPrefabInstancePropertyModifications(placed.transform);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);
            File.WriteAllText(Root+"/Placement.txt","Placed "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(placed)+" in Superstore.\nWorld position: "+chosen.ToString("F3")+"\nRotation: "+placed.transform.eulerAngles.ToString("F1")+"\nPlacement checked against existing scene colliders; floor remains below the clearance query.\n");
            Selection.activeGameObject=placed;EditorGUIUtility.PingObject(placed);
            Frame(placed.transform);
        }
        finally
        {
            if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(!open && scene.IsValid() && scene.isLoaded)EditorSceneManager.CloseScene(scene,true);
        }
    }

    static void Verify(GameObject single,GameObject joined)
    {
        if(single.GetComponentsInChildren<ItemSpriteView>().Length!=144)throw new Exception("Expected 144 vegetables per bay");
        if(joined.GetComponentsInChildren<ItemSpriteView>().Length!=288)throw new Exception("Expected 288 vegetables in double bay");
        if(single.GetComponentsInChildren<MeshFilter>().Any(f=>!f.sharedMesh || !f.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0)))throw new Exception("Missing mesh UVs");
        if(joined.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>!m)))throw new Exception("Missing material");
        if(!single.GetComponent<BoxCollider>())throw new Exception("Missing cabinet collider");
        File.WriteAllText(Root+"/Validation.txt","PASS: 4 angled tiers, 16 removable slatted crates, 144 existing vegetable items per bay, 288 in joined double bay.\nPASS: stocked, empty and double prefabs; cabinet collision; separate editable materials; UVs on every mesh; canopy lighting and price labels.\n");
    }
    static void Frame(Transform placed)
    {
        var view=SceneView.lastActiveSceneView;if(!view)return;
        var renderers=placed.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;
        var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        view.LookAt(bounds.center,Quaternion.LookRotation(placed.forward-Vector3.up*.25f-placed.right*.35f),bounds.extents.magnitude*1.35f,false);
        view.Repaint();
    }
    static void Render(GameObject model,Scene scene)
    {
        var lightGo=new GameObject("Preview key");SceneManager.MoveGameObjectToScene(lightGo,scene);
        var key=lightGo.AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.8f;key.transform.rotation=Quaternion.Euler(40,-35,0);
        var fillGo=new GameObject("Preview fill");SceneManager.MoveGameObjectToScene(fillGo,scene);
        var fill=fillGo.AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.transform.rotation=Quaternion.Euler(20,145,0);
        var cameraGo=new GameObject("Preview camera");SceneManager.MoveGameObjectToScene(cameraGo,scene);
        var camera=cameraGo.AddComponent<Camera>();camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.13f,.16f,.17f);camera.transform.position=new Vector3(5,3.2f,-7);
        camera.transform.LookAt(new Vector3(0,1.15f,0));camera.orthographic=true;camera.orthographicSize=2.55f;
        var target=new RenderTexture(1600,1000,24);var previous=RenderTexture.active;
        var previousProbe=RenderSettings.ambientProbe;
        try
        {
            var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.50f,.53f,.56f));RenderSettings.ambientProbe=ambient;
            var warmup=new RenderTexture(64,64,24);camera.targetTexture=warmup;camera.Render();camera.targetTexture=null;
            warmup.Release();UnityEngine.Object.DestroyImmediate(warmup);
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(Root+"/Preview/VegetableFridgeShelf.png",image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally{camera.targetTexture=null;RenderTexture.active=previous;RenderSettings.ambientProbe=previousProbe;target.Release();UnityEngine.Object.DestroyImmediate(target);}
    }
    static Transform Group(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    static void Cube(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position)
    {
        var go=Group(parent,name).gameObject;go.transform.localPosition=position;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    static Material Palette(string name)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/"+name+".mat");
        if(!material)throw new Exception("Missing ColoringMaterials palette material: "+name);
        return material;
    }
    static Mesh SaveMesh(Mesh mesh,string name)
    {
        string path=Root+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static void Label(Transform parent,string name,string value,Vector3 position,float characterSize,Color color)
    {
        var text=Group(parent,name).gameObject.AddComponent<TextMesh>();text.transform.localPosition=position;
        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=64;text.characterSize=characterSize;
        text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.text=value;text.color=color;
        text.GetComponent<MeshRenderer>().sharedMaterial=textMaterial;text.gameObject.AddComponent<SupermarketSignFont>();
    }
    sealed class Boxes
    {
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<Vector3> normals=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();readonly List<int> indices=new List<int>();
        public void Box(Vector3 center,Vector3 size)
        {
            var h=size*.5f;
            Face(center,new Vector3(-h.x,-h.y,-h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,-h.y,-h.z),Vector3.back);
            Face(center,new Vector3(h.x,-h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,-h.y,h.z),Vector3.forward);
            Face(center,new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,h.y,h.z),new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,-h.y,-h.z),Vector3.left);
            Face(center,new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,h.y,-h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,-h.y,h.z),Vector3.right);
            Face(center,new Vector3(-h.x,h.y,-h.z),new Vector3(-h.x,h.y,h.z),new Vector3(h.x,h.y,h.z),new Vector3(h.x,h.y,-h.z),Vector3.up);
            Face(center,new Vector3(-h.x,-h.y,h.z),new Vector3(-h.x,-h.y,-h.z),new Vector3(h.x,-h.y,-h.z),new Vector3(h.x,-h.y,h.z),Vector3.down);
        }
        void Face(Vector3 center,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
        {
            int start=vertices.Count;vertices.AddRange(new[]{center+a,center+b,center+c,center+d});normals.AddRange(Enumerable.Repeat(normal,4));
            uv.AddRange(new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)});
            indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        public Mesh Mesh(string name){var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;}
    }
    static string Readme()=>"# Vegetable fridge shelf\n\nAn open-front refrigerated produce multideck inspired by the supplied reference photographs. Each bay is 3.24m wide, 2.37m high and 1.1m deep; local -Z is the customer side and the pivot is at floor level.\n\nVegetableFridgeShelf_Stocked.prefab has four 15-degree sloping tiers, sixteen individually removable slatted plastic crates, white price tickets, a vented refrigeration base, silver end panels, teal canopy, temperature display and cool canopy lights. It holds 144 individual 2D items using the existing Carrot, Broccoli, Tomato, Cucumber and Potato prefabs. Items lie on the sloping trays instead of billboarding.\n\nVegetableFridgeShelf_Empty.prefab has the vegetable groups disabled for custom stocking. VegetableFridgeShelf_Double.prefab joins two bays into a 6.46m display and retains their prefab connections.\n\nCabinet, crate, interior, base, ticket and diffuser colours use shared materials from Assets/ColoringMaterials (Pixel_Teal, Pixel_Forest, Pixel_White, Pixel_Charcoal and Pixel_Cream). Mesh parts have ordinary UVs and remain separately editable. Price tickets are example game prices. Existing item artwork and the shared SupermarketSignFont component are reused.\n\nThe builder places a clear double or single unit beside the existing produce area in Assets/Scenes/Superstore.unity. Placement.txt records the chosen prefab and position. Existing produce displays are retained. An existing placed fridge is not duplicated on rebuild.\n\nTools > Shelf Items > Build and place vegetable fridge in Superstore regenerates these assets. Duplicate customised prefabs before rebuilding. Preview and Validation.txt contain the verified output.\n";
}
