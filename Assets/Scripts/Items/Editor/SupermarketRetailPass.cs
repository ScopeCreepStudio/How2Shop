using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static partial class SupermarketLevelBuilder
{
    const string EnhanceRequest="UserSettings/Supermarket16Retail.request";
    static void AutoEnhance()
    {
        if(!File.Exists(EnhanceRequest))return;
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {EditorApplication.delayCall+=AutoEnhance;return;}
        try
        {
            Enhance(); File.Delete(EnhanceRequest);
            if(File.Exists("Logs/Supermarket16RetailError.txt"))File.Delete("Logs/Supermarket16RetailError.txt");
        }
        catch(Exception error)
        {
            File.WriteAllText("Logs/Supermarket16RetailError.txt",error.ToString());
            File.Delete(EnhanceRequest);Debug.LogException(error);
        }
    }

    [MenuItem("Tools/How2Shop/Populate and Light Existing Supermarket")]
    public static void Enhance()
    {
        if(!File.Exists(ScenePath))throw new Exception("Create the supermarket scene first.");
        if(!File.Exists("Logs/Supermarket16_BeforeRetailPass.unity"))
            File.Copy(ScenePath,"Logs/Supermarket16_BeforeRetailPass.unity");
        Directory.CreateDirectory(Output+"/Meshes"); AssetDatabase.Refresh();
        var prior=SceneManager.GetActiveScene();
        buildScene=SceneManager.GetSceneByPath(ScenePath);
        bool wasOpen=buildScene.IsValid() && buildScene.isLoaded;
        if(!wasOpen)buildScene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(buildScene);
        try
        {
            root=buildScene.GetRootGameObjects().First(g=>g.name=="SUPERMARKET - 16 AISLE LAYOUT").transform;
            floor=ExistingMat("Floor"); wall=ExistingMat("Walls"); frame=ExistingMat("FixtureFrame");
            panel=ExistingMat("FixtureBody"); shelf=ExistingMat("ShelvesAndTrim"); dark=ExistingMat("ConveyorAndRubber");
            glass=ExistingMat("CoolerGlass");green=ExistingMat("WayfindingGreen");red=ExistingMat("WayfindingRed");
            blue=ExistingMat("WayfindingBlue");amber=ExistingMat("WayfindingAmber");signText=ExistingMat("SignText");
            ProductCache.Clear(); productCount=0; fixtureCount=0;
            var oldPromo=root.Find("07 Promotional Displays");
            if(oldPromo)UnityEngine.Object.DestroyImmediate(oldPromo.gameObject);
            var fixtures=root.GetComponentsInChildren<Transform>().Where(t=>
                PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject).StartsWith(Source,StringComparison.Ordinal) &&
                (t.Find("Stock - replaceable 2D items") || t.Find("Stock - densely faced products"))).ToArray();
            foreach(var fixture in fixtures)
            {
                int category=CategoryOf(fixture);
                string source=Path.GetFileNameWithoutExtension(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(fixture.gameObject));
                var stock=fixture.Find("Stock - replaceable 2D items");if(!stock)stock=fixture.Find("Stock - densely faced products");
                if(stock)UnityEngine.Object.DestroyImmediate(stock.gameObject);
                bool gondola=source.StartsWith("Gondola");
                float width=source=="Gondola_Wide" || source=="WallShelf_Wide"?2.042f:source=="Cooler_Wide"?1.48f:
                    source=="DisplayCabinet_Wide"?2.123f:source=="Rack_LowWide"?1.785f:1.048f;
                float depth=gondola?.60f:source.StartsWith("Cooler")?.235f:source.StartsWith("Rack")?.385f:.30f;
                DenseStock(fixture,Products[category],width,depth,gondola,category);
                fixtureCount++;
            }
            var promo=Group(root,"07 Promotional Displays");
            for(int i=0;i<4;i++)
            {
                float x=new[]{-10.5f,-6.7f,6.7f,10.5f}[i];
                var rack=Fixture(promo,"Rack_LowWide",new Vector3(x,0,12.8f),180);
                rack.localScale=new Vector3(1,.58f,1);
                DenseStock(rack,Products[i<2?5:1],1.785f,.385f,false,i);
                Board(promo,"Promotion "+i,i<2?"WEEKLY OFFERS":"FRESH PICKS",new Vector3(x,1.85f,12.8f),180,new Vector2(1.8f,.28f),i<2?amber:green);
            }
            foreach(float x in new[]{-14.5f,14.5f})
            {
                var rack=Fixture(promo,"Rack_LowSingle",new Vector3(x,0,-14.7f),180);
                rack.localScale=new Vector3(1,.48f,1);
                DenseStock(rack,Products[5],1.048f,.385f,false,0);
            }
            var lights=root.Find("05 Lighting and Ceiling");
            if(lights)UnityEngine.Object.DestroyImmediate(lights.gameObject);
            RetailLighting(Group(root,"05 Lighting and Ceiling"));
            SetRetailAmbient();
            foreach(var camera in root.GetComponentsInChildren<Camera>(true))SetupRetailCamera(camera);
            SegmentFloor();
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(buildScene);
            EditorSceneManager.SaveScene(buildScene,ScenePath);
            int savedStock=productCount;
            RenderPreviews();
            string validation="PASS: "+savedStock+" stock items; denser facings, two/three depth rows, combined packaging backs, six additional promotional displays.\n"+
                "PASS: 36 downward ceiling luminaires, eight soft-shadow aisle lights, cool cabinet lights, warm bakery/produce accents, scene-local post-processing and reflection probe.\n"+
                "PASS: existing 16 aisles, six checkouts and entrance/exit routes retained. Source materials and source scenes are unchanged by this pass.\n";
            if(root.Find("02 Aisles - two banks of eight").childCount!=16)throw new Exception("Aisle layout changed unexpectedly");
            if(savedStock<4000)throw new Exception("Stock population below expected density");
            if(root.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>!m)))throw new Exception("Missing material after retail pass");
            if(AssetDatabase.GetDependencies(ScenePath,true).Any(p=>p.StartsWith("Assets/Packages/How2ShopAssets/ReadyAssets/",StringComparison.Ordinal)))throw new Exception("Forbidden ReadyAssets dependency");
            File.WriteAllText(Output+"/RetailValidation.txt",validation);
            const string note="\n\n## Population and lighting pass\n\nThe scene has densely stocked shelf facings and depth rows, simple combined meshes behind packaged 2D covers, four entrance promotions and two checkout impulse displays. Existing aisle spacing is retained. Household and pet stock remain neutral proxies.\n\nLighting uses downward ceiling spotlights, eight shadow-casting aisle lights, cool refrigerated lighting, warm fresh-food accents, a low-cost reflection probe, and a scene-local RetailLighting profile for restrained bloom and colour. No shared project pipeline settings are changed. Floor rendering is split into local panels so lighting works consistently with per-object light limits.\n\nTools > How2Shop > Populate and Light Existing Supermarket reapplies this pass to the existing scene. This replaces generated stock, promotion and lighting groups; other layout groups are retained.\n";
            string readme=File.ReadAllText(Output+"/README.md");
            int old=readme.IndexOf("\n\n## Population and lighting pass",StringComparison.Ordinal);
            if(old>=0)readme=readme.Substring(0,old);
            File.WriteAllText(Output+"/README.md",readme+note); AssetDatabase.Refresh();
        }
        finally
        {
            if(prior.IsValid() && prior.isLoaded)SceneManager.SetActiveScene(prior);
            if(!wasOpen && buildScene.IsValid() && buildScene.isLoaded)EditorSceneManager.CloseScene(buildScene,true);
        }
    }

    static Material ExistingMat(string name)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Materials/"+name+".mat");
        if(!material)throw new Exception("Missing scene material "+name);return material;
    }
    static int CategoryOf(Transform fixture)
    {
        for(var t=fixture;t && t!=root;t=t.parent)
            if(t.parent && t.parent.name=="02 Aisles - two banks of eight" && int.TryParse(t.name.Substring(0,2),out int index))return index-1;
        string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(fixture.gameObject);
        if(source.Contains("Cooler"))return fixture.position.x< -11.5f?9:10;
        if(source.Contains("DisplayCabinet"))return 2;
        if(fixture.position.z>15)return 1;
        if(fixture.position.x< -17)return fixture.position.z>0?11:12;
        if(fixture.position.x>17)return fixture.position.z>0?6:14;
        return fixture.position.x>0?1:5;
    }

    static void DenseStock(Transform fixture,string[] pool,float width,float depth,bool doubleSided,int seed)
    {
        Directory.CreateDirectory(Output+"/Meshes");
        var stock=Group(fixture,"Stock - densely faced products");
        var heights=ShelfHeights(fixture);
        int columns=Mathf.Max(3,Mathf.FloorToInt((width-.08f)/.225f));
        int rows=doubleSided?3:2;
        var bodies=new List<CombineInstance>();
        bool fresh=pool.Any(p=>new[]{"Apple","Banana","Orange","Pear","Tomato","Cucumber","Carrot","Broccoli"}.Contains(p));
        var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        for(int face=0;face<(doubleSided?2:1);face++)for(int level=0;level<heights.Count;level++)for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
        {
            string name=pool[Math.Abs(seed+level+col/2)%pool.Length];
            var item=(GameObject)PrefabUtility.InstantiatePrefab(Product(name),buildScene);item.transform.SetParent(stock,false);
            float x=((col+.5f)/columns-.5f)*(width-.10f);
            float z=(face==0?-1:1)*(depth-.03f-row*(doubleSided?.145f:.16f));
            item.transform.localPosition=new Vector3(x,heights[level]+.012f,z);
            item.transform.localRotation=Quaternion.Euler(0,face==0?0:180,0);
            var sprite=item.GetComponent<SpriteRenderer>();
            float itemWidth=.17f,itemHeight=.24f;
            if(sprite && sprite.sprite)
            {
                var original=Vector3.Scale(sprite.sprite.bounds.size,item.transform.localScale);
                float fit=Mathf.Min(.205f/Mathf.Max(original.x,.01f),.385f/Mathf.Max(original.y,.01f));
                item.transform.localScale*=fit;
                itemWidth=original.x*fit;itemHeight=original.y*fit;
            }
            else
            {
                item.transform.localScale=Vector3.one*2.0f;
                foreach(var renderer in item.GetComponentsInChildren<MeshRenderer>())
                    renderer.sharedMaterials=Enumerable.Repeat(name=="PaperRoll3D"?shelf:panel,renderer.sharedMaterials.Length).ToArray();
            }
            foreach(var collider in item.GetComponentsInChildren<Collider>())collider.enabled=false;
            if(sprite && !fresh && cube)
            {
                bodies.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(
                    new Vector3(x,heights[level]+.012f+itemHeight*.47f,z+(face==0?1:-1)*.048f),
                    Quaternion.identity,new Vector3(itemWidth*.84f,itemHeight*.92f,.085f))});
            }
            productCount++;
        }
        if(bodies.Count>0)
        {
            var mesh=new Mesh{name="Combined packaging backs"};mesh.indexFormat=IndexFormat.UInt32;
            mesh.CombineMeshes(bodies.ToArray(),true,true);mesh.RecalculateBounds();
            string path=Output+"/Meshes/Stock_"+Mathf.RoundToInt(fixture.position.x*100)+"_"+Mathf.RoundToInt(fixture.position.z*100)+".asset";
            mesh.name=Path.GetFileNameWithoutExtension(path);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            else{AssetDatabase.CreateAsset(mesh,path);saved=mesh;}
            var backing=Group(stock,"Combined package sides and backs");
            backing.gameObject.AddComponent<MeshFilter>().sharedMesh=saved;
            backing.gameObject.AddComponent<MeshRenderer>().sharedMaterial=shelf;
        }
    }

    static void SetRetailAmbient()
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.48f,.50f,.53f);
        RenderSettings.ambientEquatorColor=new Color(.36f,.39f,.42f);
        RenderSettings.ambientGroundColor=new Color(.34f,.36f,.39f);
        RenderSettings.ambientIntensity=1;RenderSettings.fog=false;
        // Set the probe explicitly as well: new/preview scenes have no baked SH data.
        var ambient=new SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.43f,.46f,.50f));
        RenderSettings.ambientProbe=ambient;
    }
    static void RetailLighting(Transform parent)
    {
        var ceiling=Group(parent,"Ceiling - hidden in editor for layout editing");
        Cube(ceiling,"Ceiling slab",new Vector3(0,4.55f,0),new Vector3(36.3f,.20f,36.3f),shelf,false);
        ceiling.GetComponentInChildren<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        SceneVisibilityManager.instance.Hide(ceiling.gameObject,true);
        var strip=Mat("LightDiffuser",new Color(.88f,.94f,1));
        strip.shader=Shader.Find("Universal Render Pipeline/Unlit");
        strip.SetColor("_BaseColor",new Color(2.4f,2.6f,2.9f,1));EditorUtility.SetDirty(strip);
        int shadowCount=0;
        for(int x=0;x<6;x++)for(int z=0;z<6;z++)
        {
            var fitting=Group(parent,"Ceiling luminaire "+(x*6+z+1).ToString("00"));
            fitting.localPosition=new Vector3((x-2.5f)*5.7f,4.16f,(z-2.5f)*5.7f);
            Cube(fitting,"Housing",Vector3.zero,new Vector3(2.7f,.10f,.25f),frame,false);
            Cube(fitting,"Diffuser",new Vector3(0,-.06f,0),new Vector3(2.5f,.025f,.16f),strip,false);
            bool shadows=(x==1 || x==4) && (z==1 || z==2 || z==3 || z==4);
            if(shadows)shadowCount++;
            Spot(fitting,"Downlight",new Vector3(0,-.12f,0),5000,14f,120,11,shadows);
        }
        var fill=Group(parent,"Soft bounced fill").gameObject.AddComponent<Light>();
        fill.type=LightType.Directional;fill.transform.rotation=Quaternion.Euler(70,-25,0);
        fill.intensity=.55f;fill.color=new Color(.92f,.96f,1);fill.shadows=LightShadows.None;
        var departments=Group(parent,"Department accent lighting");
        foreach(float x in new[]{4f,6.2f,8.4f})Spot(departments,"Warm bakery",new Vector3(x,3.15f,16.3f),3200,5,75,5,false);
        foreach(float x in new[]{11.4f,13.5f,15.6f})Spot(departments,"Fresh produce",new Vector3(x,3.0f,16.1f),3800,4,85,5,false);
        for(int i=0;i<6;i++)Spot(departments,"Cool cabinet",new Vector3(-15.2f+i*1.53f,2.75f,17.1f),6200,2.4f,85,4,false);
        foreach(float x in new[]{-6.75f,-1.35f,4.05f})Spot(departments,"Checkout light",new Vector3(x,3.7f,-12.1f),4600,4,100,6,false);
        var probe=Group(parent,"Retail reflection probe").gameObject.AddComponent<ReflectionProbe>();
        probe.transform.localPosition=new Vector3(0,1.9f,0);probe.size=new Vector3(36,7,36);probe.center=Vector3.zero;
        probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=128;
        probe.boxProjection=true;probe.intensity=.45f;probe.nearClipPlane=.1f;probe.farClipPlane=60;
        string path=Output+"/RetailLighting.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
        if(!profile.TryGet<Bloom>(out var bloom)){bloom=profile.Add<Bloom>();AssetDatabase.AddObjectToAsset(bloom,profile);}
        bloom.intensity.Override(.16f);bloom.threshold.Override(1.2f);bloom.scatter.Override(.5f);
        if(!profile.TryGet<ColorAdjustments>(out var color)){color=profile.Add<ColorAdjustments>();AssetDatabase.AddObjectToAsset(color,profile);}
        color.postExposure.Override(.15f);color.contrast.Override(5);color.saturation.Override(3);
        if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>();AssetDatabase.AddObjectToAsset(tone,profile);}
        tone.mode.Override(TonemappingMode.Neutral);
        EditorUtility.SetDirty(profile);EditorUtility.SetDirty(bloom);EditorUtility.SetDirty(color);EditorUtility.SetDirty(tone);
        var volume=Group(parent,"Retail colour and subtle bloom").gameObject.AddComponent<Volume>();
        volume.gameObject.layer=30;
        volume.isGlobal=true;volume.priority=10;volume.sharedProfile=profile;
    }
    static void Spot(Transform parent,string name,Vector3 position,float kelvin,float intensity,float angle,float range,bool shadows)
    {
        var light=Group(parent,name).gameObject.AddComponent<Light>();light.transform.localPosition=position;
        light.transform.localRotation=Quaternion.Euler(90,0,0);light.type=LightType.Spot;
        light.color=Mathf.CorrelatedColorTemperatureToRGB(kelvin);light.intensity=intensity;
        light.range=range;light.spotAngle=angle;light.innerSpotAngle=angle*.55f;
        light.shadows=shadows?LightShadows.Soft:LightShadows.None;light.shadowStrength=.65f;
        light.shadowBias=.03f;light.shadowNormalBias=.15f;
        var data=light.gameObject.AddComponent<UniversalAdditionalLightData>();
        var serialized=new SerializedObject(data);
        serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static void SetupRetailCamera(Camera camera)
    {
        camera.allowHDR=true;var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
        data.volumeLayerMask=1<<30;
    }
    static void SegmentFloor()
    {
        var architecture=root.Find("01 Architecture");
        if(architecture.Find("Floor lighting panels"))return;
        var original=architecture.Find("Floor - 36m x 36m");
        if(!original)return;
        var renderer=original.GetComponent<MeshRenderer>();
        var material=renderer.sharedMaterial;renderer.enabled=false;
        var panels=Group(architecture,"Floor lighting panels");
        for(int x=0;x<6;x++)for(int z=0;z<6;z++)
            Cube(panels,"Floor panel",new Vector3((x-2.5f)*6,-.12f,(z-2.5f)*6),new Vector3(6,.24f,6),material,false);
    }
}
