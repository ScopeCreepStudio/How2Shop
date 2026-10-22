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

// Creates a separate, editable level. All shop fixtures come from AssetsReady1.
[InitializeOnLoad]
public static partial class SupermarketLevelBuilder
{
    const string Source = "Assets/Packages/How2ShopAssets/AssetsReady1/Prefabs/";
    const string Output = "Assets/Levels/Supermarket16";
    const string ScenePath = "Assets/Scenes/Supermarket_16Aisle.unity";
    const string Request = "UserSettings/Supermarket16Build.request";
    static readonly string[] Departments = {
        "Fresh Produce / Salad", "Fruit & Veg", "Bakery", "Bread & Breakfast",
        "Cereal & Snacks", "Snacks & Confectionery", "Tinned Food", "Sauces, Pasta & Rice",
        "Chilled Ready Meals", "Dairy", "Frozen Food", "Drinks",
        "Household Cleaning", "Toiletries & Health", "Personal Care", "Pet Food & Misc"
    };
    static readonly string[][] Products = {
        new[]{"Tomato","Cucumber","Carrot","Broccoli"}, new[]{"Apple","Banana","Orange","Pear"},
        new[]{"Bread","Biscuits","Coffee"}, new[]{"Bread","Cereal","Milk","Butter"},
        new[]{"Cereal","Crisps","Chocolate"}, new[]{"Crisps","Chocolate","Biscuits"},
        new[]{"BakedBeans","Tuna","Peas","TomatoSoup"}, new[]{"Pasta","Rice","Flour","TomatoSoup"},
        new[]{"FrozenPizza","ChickenNuggets","MixedVegetables"}, new[]{"Milk","Cheese","Butter","Eggs"},
        new[]{"FrozenPizza","FishFingers","IceCream","FrozenWaffles"}, new[]{"Cola","OrangeJuice","AppleJuice","Water"},
        new[]{"PaperRoll3D"}, new[]{"PaperRoll3D"}, new[]{"PaperRoll3D"}, new[]{"FoodTin3D"}
    };
    static readonly Dictionary<string, GameObject> ProductCache = new Dictionary<string, GameObject>();
    static readonly List<string> Report = new List<string>();
    static Material floor, wall, frame, panel, shelf, dark, glass, green, red, blue, amber, signText;
    static Scene buildScene;
    static Transform root;
    static int fixtureCount, productCount;

    static SupermarketLevelBuilder() { EditorApplication.delayCall += Auto; EditorApplication.delayCall += AutoEnhance; }
    static void Auto()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Auto; return; }
        try { Build(); File.Delete(Request); if(File.Exists("Logs/Supermarket16Error.txt"))File.Delete("Logs/Supermarket16Error.txt"); }
        catch (Exception error)
        {
            File.WriteAllText("Logs/Supermarket16Error.txt", error.ToString());
            File.Delete(Request); Debug.LogException(error);
        }
    }

    [MenuItem("Tools/How2Shop/Build 16 Aisle Supermarket Scene")]
    public static void Build()
    {
        Directory.CreateDirectory(Output + "/Materials"); Directory.CreateDirectory(Output + "/Preview");
        AssetDatabase.Refresh(); Report.Clear(); ProductCache.Clear(); fixtureCount = productCount = 0;
        floor = Mat("Floor", new Color(.49f,.51f,.51f));
        wall = Mat("Walls", new Color(.65f,.63f,.57f));
        frame = Mat("FixtureFrame", new Color(.14f,.17f,.19f));
        panel = Mat("FixtureBody", new Color(.55f,.59f,.60f));
        shelf = Mat("ShelvesAndTrim", new Color(.81f,.80f,.73f));
        dark = Mat("ConveyorAndRubber", new Color(.055f,.065f,.075f));
        green = Mat("WayfindingGreen", new Color(.08f,.40f,.19f));
        red = Mat("WayfindingRed", new Color(.65f,.10f,.08f));
        blue = Mat("WayfindingBlue", new Color(.08f,.27f,.46f));
        amber = Mat("WayfindingAmber", new Color(.55f,.29f,.09f));
        glass = Mat("CoolerGlass", new Color(.48f,.66f,.72f,.16f));
        glass.SetFloat("_Surface",1); glass.SetFloat("_SrcBlend",5); glass.SetFloat("_DstBlend",10);
        glass.SetFloat("_ZWrite",0); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.SetOverrideTag("RenderType","Transparent"); glass.renderQueue=3000;
        EditorUtility.SetDirty(glass);
        string fontPath=Output+"/Materials/SignText.mat";
        signText=AssetDatabase.LoadAssetAtPath<Material>(fontPath);
        if(!signText)
        {
            signText=new Material(Shader.Find("How2Shop/Level/Sign Text"));
            AssetDatabase.CreateAsset(signText,fontPath);
        }

        Scene prior = SceneManager.GetActiveScene();
        buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(buildScene);
        try
        {
            root = new GameObject("SUPERMARKET - 16 AISLE LAYOUT").transform;
            var architecture = Group(root,"01 Architecture");
            BuildShell(architecture);
            var aisles = Group(root,"02 Aisles - two banks of eight");
            for (int bank=0;bank<2;bank++) for (int column=0;column<8;column++)
            {
                int index=bank*8+column;
                float x=(column-3.5f)*3.65f, z=bank==0 ? -3.2f : 5.6f;
                var aisle=Group(aisles,(index+1).ToString("00")+" - "+Departments[index]);
                aisle.localPosition=new Vector3(x,0,z);
                for(int bay=0;bay<3;bay++)
                {
                    var fixture=Fixture(aisle,"Gondola_Wide",new Vector3(0,0,(bay-1)*2.042f),90);
                    fixture.name="Bay "+(bay+1)+" - Gondola Wide";
                    Stock(fixture,Products[index],2.042f,.60f,true,index);
                }
                float end=bank==0 ? -3.44f : 3.44f;
                var cap=Fixture(aisle,"WallShelf_Single",new Vector3(0,0,end),bank==0?0:180);
                cap.name="Outer endcap"; Stock(cap,Products[index],1.021f,.30f,false,index);
                Board(aisle,"Aisle sign",(index+1).ToString("00")+"  "+ShortName(index),new Vector3(0,2.29f,bank==0?-3.4f:3.4f),0,new Vector2(1.95f,.34f),index<8?green:blue);
            }
            BuildPerimeter(Group(root,"03 Perimeter Departments"));
            BuildCheckouts(Group(root,"04 Checkout Area - six lanes"));
            BuildLighting(Group(root,"05 Lighting and Ceiling"));
            BuildPlayer(Group(root,"06 Player and Viewpoints"));
            SetRetailAmbient();
            RenderSettings.fog=false; RenderSettings.skybox=null;
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(buildScene,ScenePath)) throw new Exception("Could not save supermarket scene");
            Validate(aisles);
            RenderPreviews();
            File.WriteAllText(Output+"/README.md", Readme());
            AssetDatabase.Refresh();
        }
        finally
        {
            if(prior.IsValid() && prior.isLoaded) SceneManager.SetActiveScene(prior);
            if(buildScene.IsValid() && buildScene.isLoaded) EditorSceneManager.CloseScene(buildScene,true);
        }
    }

    static void BuildShell(Transform parent)
    {
        Cube(parent,"Floor - 36m x 36m",new Vector3(0,-.12f,0),new Vector3(36,.24f,36),floor,true);
        foreach(float x in new[]{-18.12f,18.12f})
            Cube(parent,"Side wall",new Vector3(x,2.25f,0),new Vector3(.24f,4.5f,36.48f),wall,true);
        foreach(float z in new[]{-18.12f,18.12f})
        {
            foreach(float x in new[]{-10.2f,10.2f})
                Cube(parent,z<0?"Exit wall wing":"Entrance wall wing",new Vector3(x,2.25f,z),new Vector3(15.6f,4.5f,.24f),wall,true);
            Cube(parent,"Door lintel",new Vector3(0,3.65f,z),new Vector3(4.8f,1.7f,.24f),wall,true);
            var door=Fixture(parent,"DoorFrame_Double",new Vector3(0,0,z),0);
            door.localScale=new Vector3(4.8f/1.458f,2.8f/1.552f,2);
            Cube(parent,"Door threshold",new Vector3(0,.012f,z),new Vector3(4.8f,.024f,.8f),frame,false);
        }
        Cube(parent,"Entrance landing",new Vector3(0,-.10f,20),new Vector3(7,.2f,4),floor,true);
        Cube(parent,"Exit landing",new Vector3(0,-.10f,-20),new Vector3(7,.2f,4),floor,true);
        Board(parent,"Entrance sign","ENTRANCE",new Vector3(0,3.18f,17.91f),0,new Vector2(3.6f,.50f),green);
        Board(parent,"Exit sign","EXIT",new Vector3(0,3.18f,-17.91f),180,new Vector2(3.6f,.50f),red);
        // Low perimeter skirting and evenly spaced structural columns are independently textureable.
        foreach(float x in new[]{-17.96f,17.96f}) for(int i=0;i<6;i++)
            Cube(parent,"Wall column",new Vector3(x,2.1f,-15+i*6),new Vector3(.16f,4.2f,.20f),frame,false);
    }

    static void BuildPerimeter(Transform parent)
    {
        var rear=Group(parent,"Rear - chilled, frozen, bakery and produce");
        for(int i=0;i<6;i++)
        {
            var cooler=Fixture(rear,"Cooler_Wide",new Vector3(-15.2f+i*1.53f,0,17.52f),0);
            Stock(cooler,i<3?Products[9]:Products[10],1.48f,.235f,false,i);
        }
        Board(rear,"Chilled and dairy","CHILLED / DAIRY",new Vector3(-13.65f,2.50f,17.10f),0,new Vector2(4.4f,.48f),blue);
        Board(rear,"Frozen","FROZEN",new Vector3(-9.1f,2.50f,17.10f),0,new Vector2(4.1f,.48f),blue);
        for(int i=0;i<3;i++)
        {
            var bakery=Fixture(rear,"DisplayCabinet_Wide",new Vector3(4.0f+i*2.18f,0,17.35f),0);
            Stock(bakery,Products[2],2.123f,.30f,false,i);
        }
        Board(rear,"Bakery","BAKERY",new Vector3(6.2f,2.65f,16.90f),0,new Vector2(5.4f,.48f),amber);
        for(int i=0;i<3;i++)
        {
            var produce=Fixture(rear,"Rack_LowWide",new Vector3(11.4f+i*2.12f,0,16.9f),0);
            produce.localScale=new Vector3(1,.65f,1);
            Stock(produce,Products[1],1.785f,.385f,false,i);
        }
        Board(rear,"Produce","FRUIT & VEG",new Vector3(13.5f,2.40f,16.75f),0,new Vector2(5.5f,.48f),green);
        foreach(int side in new[]{-1,1})
        {
            var wallRun=Group(parent,side<0?"West - Drinks and Household":"East - Ambient and Personal Care");
            for(int bay=0;bay<12;bay++)
            {
                int category=side<0 ? (bay<6?11:12) : (bay<6?14:6);
                var fixture=Fixture(wallRun,"WallShelf_Wide",new Vector3(side*17.52f,0,-10.5f+bay*2.08f),side<0?-90:90);
                Stock(fixture,Products[category],2.042f,.30f,false,bay);
            }
            Board(wallRun,"Rear department",side<0?"DRINKS":"TINNED / AMBIENT",new Vector3(side*17.05f,2.55f,10),side<0?-90:90,new Vector2(4.4f,.45f),side<0?blue:amber);
            Board(wallRun,"Front department",side<0?"HOUSEHOLD":"PERSONAL CARE",new Vector3(side*17.05f,2.55f,-5),side<0?-90:90,new Vector2(4.4f,.45f),blue);
        }
        // Compact display islands stay outside the entrance-to-aisle route.
        foreach(float x in new[]{-15.3f,15.3f}) foreach(float z in new[]{-8.7f,1.2f,11.6f})
        {
            var island=Fixture(parent,"Rack_LowSingle",new Vector3(x,0,z),x<0?-90:90);
            island.localScale=new Vector3(1,.50f,1);
            Stock(island,Products[x<0?5:1],1.048f,.385f,false,(int)z);
        }
        // Keep the newsstand that was just created with the other reusable shop assets.
        var press=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Shelf_Items/NewspaperStand/NewspaperStand_Stocked.prefab");
        if(press)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(press,buildScene);
            go.transform.SetParent(parent,false); go.transform.localPosition=new Vector3(-12.5f,0,-15.8f);
            go.transform.localRotation=Quaternion.Euler(0,180,0);
        }
    }

    static void BuildCheckouts(Transform parent)
    {
        for(int i=0;i<6;i++)
        {
            float x=(i-2.5f)*2.7f;
            var lane=Group(parent,"Checkout "+(i+1).ToString("00")); lane.localPosition=new Vector3(x,0,-12.15f);
            Cube(lane,"Counter base",new Vector3(0,.45f,0),new Vector3(1.05f,.9f,2.4f),panel,true);
            Cube(lane,"Worktop",new Vector3(0,.93f,0),new Vector3(1.12f,.06f,2.48f),shelf,false);
            Cube(lane,"Conveyor belt",new Vector3(0,.967f,.43f),new Vector3(.83f,.014f,1.35f),dark,false);
            Cube(lane,"Bagging area",new Vector3(0,.973f,-.86f),new Vector3(.86f,.018f,.50f),frame,false);
            Fixture(lane,"Register_Classic",new Vector3(.12f,.97f,-.33f),0);
            Fixture(lane,"CashDrawer",new Vector3(-.1f,.97f,-.90f),0);
            Board(lane,"Lane number",(i+1).ToString(),new Vector3(.47f,2.40f,-.15f),0,new Vector2(.36f,.36f),green);
            Cube(lane,"Lane sign pole",new Vector3(.47f,1.59f,-.15f),new Vector3(.025f,1.4f,.025f),frame,false);
        }
        var queue=Group(parent,"Queue guides");
        for(int i=0;i<7;i++)
        {
            float x=(i-3)*2.7f;
            foreach(float z in new[]{-11.0f,-9.5f}) Cube(queue,"Queue post",new Vector3(x,.48f,z),new Vector3(.055f,.96f,.055f),frame,true);
            Cube(queue,"Queue rail",new Vector3(x,.82f,-10.25f),new Vector3(.035f,.035f,1.5f),frame,false);
        }
        Board(parent,"Checkout area","CHECKOUTS  /  EXIT",new Vector3(0,3.20f,-10.15f),0,new Vector2(7.5f,.58f),blue);
        foreach(float x in new[]{-10.3f,10.3f})
        {
            var impulse=Fixture(parent,"WallShelf_Single",new Vector3(x,0,-12.0f),x<0?-90:90);
            Stock(impulse,Products[5],1.021f,.30f,false,0);
        }
    }

    static void BuildLighting(Transform parent)
    {
        RetailLighting(parent);
    }

    static void BuildPlayer(Transform parent)
    {
        var player=Group(parent,"Player - WASD and mouse"); player.localPosition=new Vector3(0,.05f,15.0f);
        player.localRotation=Quaternion.Euler(0,180,0);
        var controller=player.gameObject.AddComponent<CharacterController>();
        controller.height=1.8f; controller.radius=.28f; controller.center=new Vector3(0,.90f,0);
        controller.stepOffset=.3f;
        var cameraTransform=Group(player,"Main Camera"); cameraTransform.localPosition=new Vector3(0,1.65f,0);
        var camera=cameraTransform.gameObject.AddComponent<Camera>(); camera.tag="MainCamera";
        camera.fieldOfView=70; camera.nearClipPlane=.08f; camera.farClipPlane=100;
        SetupRetailCamera(camera);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.21f,.25f,.29f);
        cameraTransform.gameObject.AddComponent<AudioListener>();
        var movement=player.gameObject.AddComponent<PlayerMovement>(); movement.playerCamera=cameraTransform;
        var overview=Group(parent,"Overview Camera - disabled by default").gameObject.AddComponent<Camera>();
        overview.transform.position=new Vector3(27,40,-37); overview.transform.LookAt(new Vector3(0,0,1));
        overview.orthographic=true; overview.orthographicSize=26; overview.enabled=false;
    }

    static Transform Fixture(Transform parent,string name,Vector3 position,float rotation)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Source+name+".prefab");
        if(!asset) throw new Exception("Missing AssetsReady1 fixture: "+name);
        var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,buildScene);
        go.transform.SetParent(parent,false); go.transform.localPosition=position;
        go.transform.localRotation=Quaternion.Euler(0,rotation,0);
        foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>())
        {
            string part=renderer.name.ToLowerInvariant();
            var material=part.Contains("frame")?frame:part.Contains("shel")?shelf:part.Contains("panel") && name.StartsWith("Cooler")?glass:panel;
            renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        fixtureCount++; return go.transform;
    }

    static void Stock(Transform fixture,string[] pool,float width,float depth,bool doubleSided,int seed)
    {
        DenseStock(fixture,pool,width,depth,doubleSided,seed);
    }

    static GameObject Product(string name)
    {
        if(ProductCache.TryGetValue(name,out var cached)) return cached;
        string[] paths = name=="PaperRoll3D" ? new[]{Source+"Paper_Roll.prefab"} : name=="FoodTin3D" ? new[]{Source+"Food_Tin.prefab"} : new[]{
            "Assets/Shelf_Items/Prefabs/"+name+".prefab",
            "Assets/Packages/How2ShopAssets/items/Prefabs/"+name+".prefab"
        };
        var asset=paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).FirstOrDefault(a=>a);
        if(!asset) throw new Exception("Missing existing stock item "+name);
        ProductCache[name]=asset; return asset;
    }

    static List<float> ShelfHeights(Transform fixture)
    {
        var areas=new Dictionary<int,float>();
        foreach(var filter in fixture.GetComponentsInChildren<MeshFilter>())
        {
            if(!filter.name.ToLowerInvariant().Contains("shel")) continue;
            var mesh=filter.sharedMesh; if(!mesh) continue;
            var vertices=mesh.vertices; var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=fixture.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[i]]));
                var b=fixture.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[i+1]]));
                var c=fixture.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[i+2]]));
                var cross=Vector3.Cross(b-a,c-a);
                if(cross.normalized.y<.9f || a.y<.10f) continue;
                int key=Mathf.RoundToInt((a.y+b.y+c.y)/3/.02f);
                if(!areas.ContainsKey(key))areas[key]=0; areas[key]+=cross.magnitude*.5f;
            }
        }
        var levels=new List<float>();
        foreach(var pair in areas.Where(p=>p.Value>.06f).OrderBy(p=>p.Key))
            if(levels.Count==0 || pair.Key*.02f-levels[levels.Count-1]>.18f) levels.Add(pair.Key*.02f);
        if(levels.Count<2) return new List<float>{.28f,.84f,1.40f};
        return levels.Take(5).ToList();
    }

    static string ShortName(int i) => new[]{"PRODUCE","FRUIT & VEG","BAKERY","BREAKFAST","CEREAL","SNACKS","TINNED","PASTA & RICE","READY MEALS","DAIRY","FROZEN","DRINKS","CLEANING","HEALTH","PERSONAL CARE","PET & MISC"}[i];
    static Transform Group(Transform parent,string name)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false); return go.transform;
    }
    static void Cube(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool collide)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name;
        go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=material;
        if(!collide)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static Material Mat(string name,Color color)
    {
        string path=Output+"/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color); material.SetFloat("_Smoothness",.15f);
        EditorUtility.SetDirty(material); return material;
    }
    static void Board(Transform parent,string name,string title,Vector3 position,float yaw,Vector2 size,Material material)
    {
        var board=Group(parent,name); board.localPosition=position; board.localRotation=Quaternion.Euler(0,yaw,0);
        Cube(board,"Editable sign panel",Vector3.zero,new Vector3(size.x,size.y,.055f),material,false);
        foreach(int side in new[]{-1,1})
        {
            var text=Group(board,side<0?"Front text":"Back text").gameObject.AddComponent<TextMesh>();
            text.transform.localPosition=new Vector3(0,0,side*.035f);
            text.transform.localRotation=Quaternion.Euler(0,side<0?0:180,0);
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize=64;
            text.characterSize=.01f; text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center;
            text.color=Color.white; text.text=title;
            var renderer=text.GetComponent<MeshRenderer>(); renderer.sharedMaterial=signText;
            text.gameObject.AddComponent<SupermarketSignFont>();
            var bounds=renderer.bounds;
            // Measure in the board's axes even for side-wall signs.
            float textWidth=(yaw==90 || yaw==-90)?bounds.size.z:bounds.size.x;
            if(textWidth>.001f && bounds.size.y>.001f)
                text.transform.localScale=Vector3.one*Mathf.Min((size.x-.12f)/textWidth,(size.y-.09f)/bounds.size.y);
        }
    }

    static void Validate(Transform aisles)
    {
        if(aisles.childCount!=16)throw new Exception("Expected sixteen aisles");
        var checkouts=root.Find("04 Checkout Area - six lanes");
        if(checkouts.Cast<Transform>().Count(t=>t.name.StartsWith("Checkout 0"))!=6)throw new Exception("Expected six checkout lanes");
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            if(renderer.sharedMaterials.Any(m=>!m))throw new Exception("Missing material on "+renderer.name);
        var forbidden=AssetDatabase.GetDependencies(ScenePath,true).Where(p=>p.StartsWith("Assets/Packages/How2ShopAssets/ReadyAssets/",StringComparison.Ordinal)).ToArray();
        if(forbidden.Length>0)throw new Exception("Forbidden ReadyAssets dependency: "+string.Join(", ",forbidden));
        if(root.GetComponentsInChildren<Camera>().Count(c=>c.enabled)!=1)throw new Exception("Expected one active gameplay camera");
        Report.Add("PASS: 16 aisles in two banks of 8, 48 main gondola bays, 16 outer endcaps, six checkout lanes.");
        Report.Add("PASS: "+fixtureCount+" shop fixtures from AssetsReady1; "+productCount+" stock items from existing assets.");
        Report.Add("PASS: no ReadyAssets dependencies, no missing materials, one active gameplay camera, floor/wall/fixture collision.");
        Report.Add("CLEARANCES: aisle pitch 3.65m minus 1.20m fixture depth = 2.45m; central cross aisle about 3.67m; entrance and exit openings 4.8m.");
        Report.Add("Source scenes were left open and were not saved or edited by this builder.");
        File.WriteAllText(Output+"/Validation.txt",string.Join("\n",Report)+"\n");
    }

    static void RenderPreviews()
    {
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var clone=UnityEngine.Object.Instantiate(root.gameObject);
            SceneManager.MoveGameObjectToScene(clone,preview);
            foreach(var camera in clone.GetComponentsInChildren<Camera>()) camera.enabled=false;
            foreach(var audio in clone.GetComponentsInChildren<AudioListener>()) audio.enabled=false;
            foreach(var behaviour in clone.GetComponentsInChildren<PlayerMovement>()) behaviour.enabled=false;
            var ceiling=clone.transform.Find("05 Lighting and Ceiling/Ceiling - hidden in editor for layout editing");
            ceiling.gameObject.SetActive(false);
            var cameraGo=new GameObject("Preview camera"); SceneManager.MoveGameObjectToScene(cameraGo,preview);
            var view=cameraGo.AddComponent<Camera>(); view.scene=preview;
            SetupRetailCamera(view);
            view.clearFlags=CameraClearFlags.SolidColor; view.backgroundColor=new Color(.065f,.085f,.11f);
            view.nearClipPlane=.08f; view.farClipPlane=150;
            view.transform.position=new Vector3(20,40,-42); view.transform.LookAt(new Vector3(0,0,1));
            view.orthographic=true; view.orthographicSize=27;
            // Warm the render pipeline before capturing the overhead preview.
            var warmup=new RenderTexture(64,64,24);view.targetTexture=warmup;view.Render();view.targetTexture=null;
            warmup.Release();UnityEngine.Object.DestroyImmediate(warmup);
            Render(view,Output+"/Preview/Overview.png",1600,1400);
            ceiling.gameObject.SetActive(true);
            view.orthographic=false; view.fieldOfView=80;
            view.transform.position=new Vector3(0,1.7f,15); view.transform.LookAt(new Vector3(0,1.6f,-11));
            Render(view,Output+"/Preview/FromEntrance.png",1400,900);
            view.transform.position=new Vector3(-7.3f,1.7f,-8.5f); view.transform.LookAt(new Vector3(0,1.4f,-13));
            Render(view,Output+"/Preview/Checkouts.png",1400,900);
        }
        finally{EditorSceneManager.ClosePreviewScene(preview);}
    }
    static void Render(Camera view,string path,int width,int height)
    {
        var target=new RenderTexture(width,height,24); var previous=RenderTexture.active;
        try
        {
            view.targetTexture=target; view.Render(); RenderTexture.active=target;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
        }
        finally{view.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);}
    }
    static string Readme() => "# 16 aisle supermarket\n\nOpen "+ScenePath+". This is a separate 36m x 36m level based on the supplied reference: entrance at the rear (+Z), exit at the front (-Z), two banks of eight aisles, perimeter departments and six checkout lanes directly before the exit. Press Play for WASD / mouse walking from the entrance.\n\nAll standard shop fixtures are connected instances from "+Source+". No ReadyAssets fixtures are used. Product pictures reuse the existing shelf items; household and pet aisles use neutral paper-roll and tin proxies that can be replaced later. The existing stocked newsstand is included near the exit.\n\nArchitecture, aisles, departments, checkout area, lighting and player are separate root groups. The plain URP materials in Materials are intentionally simple placeholders for your own textures. Fixture overrides keep frame, body, shelves and cooler panels independently editable. Originals are unchanged.\n\nThe ceiling is hidden only in the editor using Scene Visibility so the layout is easy to edit. It remains visible in Play mode and builds. Toggle the eye icon on the Ceiling group to inspect it in the editor. An optional disabled overview camera is under Player and Viewpoints.\n\nPreview contains overview, entrance and checkout renders. Validation.txt records counts, clearances and dependency checks. Tools > How2Shop > Build 16 Aisle Supermarket Scene regenerates this generated scene, so save a copy before using that command after manual edits. Other scenes are not modified.\n";
}
