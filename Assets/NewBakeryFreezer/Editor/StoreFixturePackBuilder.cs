using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// Initial-generation tool. Existing prefabs are never overwritten.
public static class StoreFixturePackBuilder
{
    const string Root="Assets/NewBakeryFreezer";
    static readonly Dictionary<string,Material> Mats=new Dictionary<string,Material>();
    static readonly List<string> Built=new List<string>();
    static Scene scene;
    static string current;
    static int meshNumber;
    static Mesh cylinder;
    static readonly Dictionary<char,string> Glyphs=new Dictionary<char,string>();
    static Vector3 V(float x,float y,float z){return new Vector3(x,y,z);}

    [MenuItem("Tools/Store Fixtures/Create bakery freezer and staff pack")]
    public static void Build()
    {
        if(Directory.Exists(Root+"/Prefabs")&&Directory.GetFiles(Root+"/Prefabs","*.prefab",SearchOption.AllDirectories).Length>0)
            throw new InvalidOperationException("This pack already exists. Edit the prefabs directly; generation cannot overwrite hand edits.");
        foreach(var d in new[]{"Materials","Textures","Meshes","Prefabs/Bakery","Prefabs/Freezers","Prefabs/Staff","Prefabs/Props","Prefabs/Showcases","Previews"})Directory.CreateDirectory(Root+"/"+d);
        AssetDatabase.Refresh();Mats.Clear();Built.Clear();InitFont();
        foreach(var n in new[]{"Brown","Copper","Coral","Cream","Charcoal","Slate","Navy","Sky","White","Lemon","Red","Forest","Sand","Olive","Teal","Orange"})
        {
            string mp=Root+"/Materials/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(mp);
            if(!m){m=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_"+n+".mat"));m.name=n;AssetDatabase.CreateAsset(m,mp);
                var tex=m.mainTexture;string sp=AssetDatabase.GetAssetPath(tex),tp=Root+"/Textures/"+n+".png";
                if(tex&&sp.EndsWith(".png")){AssetDatabase.CopyAsset(sp,tp);var copy=AssetDatabase.LoadAssetAtPath<Texture>(tp);m.SetTexture("_BaseMap",copy);m.SetTexture("_MainTex",copy);}
            }Mats[n]=m;
        }
        AssetDatabase.SaveAssets();
        foreach(var mat in Mats.Values){string path=AssetDatabase.GetAssetPath(mat),data=File.ReadAllText(path);foreach(var n in Mats.Keys){string original=AssetDatabase.AssetPathToGUID("Assets/ColoringMaterials/Pixel_"+n+".mat");if(!string.IsNullOrEmpty(original))data=data.Replace(original,AssetDatabase.AssetPathToGUID(Root+"/Materials/"+n+".mat"));}File.WriteAllText(path,data);AssetDatabase.ImportAsset(path);}
        MakeSpecial("Glass",new Color(.28f,.65f,.85f,.16f),true);
        MakeSpecial("ColdLight",new Color(.35f,.8f,1,1),false);
        MakeSpecial("WarmLight",new Color(1,.72f,.25f,1),false);
        cylinder=MakeCylinder();scene=EditorSceneManager.NewPreviewScene();
        try
        {
            Save("Bakery","BakeryShelf_Wide",()=>BakeryShelf(2.4f,1.9f,4));
            Save("Bakery","BakeryShelf_Low",()=>BakeryShelf(2.4f,1.15f,3));
            Save("Bakery","BakeryShelf_Narrow",()=>BakeryShelf(.85f,1.9f,4));
            Save("Bakery","BakeryShelf_Corner",()=>BakeryCorner());
            Save("Bakery","BakeryDisplay_Small",()=>BakeryTable(1.25f,false));
            Save("Bakery","BakeryDisplay_Large",()=>BakeryTable(2.25f,false));
            Save("Bakery","BakeryDisplay_Tiered",()=>BakeryTable(1.7f,true));
            Save("Bakery","BaguetteBasket_Empty",()=>Basket(.48f,.7f,.38f));
            Save("Bakery","BakerySign",()=>Sign("BAKERY",2.25f,.55f,"Brown","Cream",true));
            Save("Bakery","FreshBreadWallBoard",()=>Sign("FRESH\nBREAD\nDAILY",.65f,.9f,"Charcoal","Cream",true));
            Save("Bakery","WheatWallPlaque",()=>WheatPlaque());
            Save("Bakery","BakeryChalkAboard",()=>Aboard(false));
            Save("Freezers","Freezer_Single",()=>Freezer(1));
            Save("Freezers","Freezer_Double",()=>Freezer(2));
            Save("Freezers","Freezer_Triple",()=>Freezer(3));
            Save("Freezers","Freezer_FiveDoor",()=>Freezer(5));
            Save("Freezers","Freezer_Corner",()=>FreezerCorner());
            Save("Freezers","IslandFreezer_Small",()=>Island(1.4f));
            Save("Freezers","IslandFreezer_Long",()=>Island(2.8f));
            Save("Freezers","FrozenSign",()=>Sign("* FROZEN *",2.2f,.48f,"Navy","White",false));
            Save("Freezers","SnowflakeSign",()=>Sign("*",.48f,.6f,"Navy","White",false));
            Save("Freezers","IceCreamSign",()=>Sign("ICE\nCREAM",.48f,.9f,"Navy","White",false));
            Save("Staff","StaffDoors_Blue",()=>StaffDoor("Navy",0,false));
            Save("Staff","StaffDoors_Grey",()=>StaffDoor("Slate",0,false));
            Save("Staff","StaffDoors_Open",()=>StaffDoor("Navy",70,false));
            Save("Staff","StaffDoors_Porthole",()=>StaffDoor("Navy",0,true));
            Save("Staff","StaffDoors_TallWindows",()=>StaffDoor("Slate",0,false,true));
            Save("Staff","StaffDoor_Single",()=>StaffSingle());
            Save("Staff","StaffOnlySign",()=>Sign("STAFF ONLY",1.2f,.3f,"Cream","Charcoal",false));
            Save("Staff","NoEntrySign",()=>SafetySign(false));
            Save("Staff","FireExitSign",()=>SafetySign(true));
            Save("Staff","ElectricalBox",()=>Electrical());
            Save("Staff","Pipe_Straight",()=>Pipe(0));
            Save("Staff","Pipe_Elbow",()=>Pipe(1));
            Save("Staff","Pipe_TJunction",()=>Pipe(2));
            Save("Props","WallLight_Warm",()=>Lamp(true));
            Save("Props","WallLight_Cold",()=>Lamp(false));
            Save("Props","DisplayTray_Empty",()=>Tray(.6f,.1f,.4f));
            Save("Props","TrayStack",()=>TrayStack());
            Save("Props","PaperBag_Empty",()=>Bag());
            Save("Props","Planter",()=>Plant());
            Save("Props","Carton_Small",()=>Carton(.45f,.35f,.38f));
            Save("Props","Carton_Large",()=>Carton(.65f,.55f,.5f));
            Save("Props","CartonStack",()=>CartonStack(false));
            Save("Props","Pallet_Empty",()=>Pallet());
            Save("Props","Pallet_Cartons",()=>CartonStack(true));
            Save("Props","RollCage_Empty",()=>RollCage(false));
            Save("Props","RollCage_Cartons",()=>RollCage(true));
            Save("Props","HandTruck",()=>HandTruck());
            Save("Props","PalletJack",()=>PalletJack());
            Save("Props","StepLadder",()=>Ladder());
            Save("Props","WetFloorSign",()=>Aboard(true));
            Save("Props","FireExtinguisher",()=>Extinguisher());
            Save("Props","WheelieBin",()=>Bin());
            Save("Showcases","Bakery_ExampleLayout",()=>Showcase(0));
            Save("Showcases","Frozen_ExampleLayout",()=>Showcase(1));
            Save("Showcases","Staff_ExampleLayout",()=>Showcase(2));
            AssetDatabase.SaveAssets();
            foreach(string cat in new[]{"Bakery","Freezers","Staff","Props","Showcases"})Contact(cat);
            Validate();
            File.WriteAllText(Root+"/README.md","# Bakery, freezer and staff-area pack\n\n57 editable prefabs, including three example layouts. All display units are empty for future 2D product artwork. Front faces local -Z; floor props have floor pivots and wall props use wall pivots. Dimensions are metres. Bakery shelves and upright freezer doors have named StockingSlots transforms. Door leaves sit under named hinges for manual posing; no interaction scripts are included. Upright freezers use 0.78m door bays; bakery wide shelves are 2.4m wide.\n\nPrefabs are grouped into Bakery, Freezers, Staff, Props and Showcases. Contact sheets are in Previews. Copied pixel textures/materials, generated text meshes, the glass shader, and the generator are all within this folder. Uses Unity URP and built-in primitive meshes.\n\nThe generator refuses to replace an existing pack, protecting hand edits. Edit existing prefabs directly. Example layouts are prefab assets and have not been placed into Superstore.\n");
            AssetDatabase.Refresh();
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static Material M(string n){return Mats[n];}
    static void MakeSpecial(string name,Color color,bool glass)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find(glass?"How2Shop/NewBakeryFreezer/Glass":"Universal Render Pipeline/Unlit"));m.name=name;AssetDatabase.CreateAsset(m,path);}m.SetColor(glass?"_Color":"_BaseColor",color);Mats[name]=m;
    }
    static GameObject New(string name){var g=new GameObject(name);SceneManager.MoveGameObjectToScene(g,scene);return g;}
    static Transform G(Transform p,string name){var g=new GameObject(name);g.transform.SetParent(p,false);return g.transform;}
    static Transform Box(Transform p,string name,Vector3 pos,Vector3 size,string material)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=M(material);UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;
    }
    static Transform Rod(Transform p,string name,Vector3 a,Vector3 b,float radius,string mat)
    {
        var t=G(p,name);t.localPosition=(a+b)*.5f;t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);t.localScale=V(radius,(b-a).magnitude*.5f,radius);t.gameObject.AddComponent<MeshFilter>().sharedMesh=cylinder;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=M(mat);return t;
    }
    static void Wheel(Transform p,float x,float y,float z,float radius)
    {Rod(p,"Eight sided rubber wheel",V(x-.035f,y,z),V(x+.035f,y,z),radius,"Charcoal");Rod(p,"Metal axle",V(x-.045f,y,z),V(x+.045f,y,z),radius*.3f,"Slate");}
    static Mesh MakeCylinder()
    {
        string path=Root+"/Meshes/OctagonalCylinder.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh)return mesh;
        var verts=new List<Vector3>();var triangles=new List<int>();
        for(int i=0;i<8;i++){float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;int j=verts.Count;verts.Add(V(Mathf.Cos(a),-1,Mathf.Sin(a)));verts.Add(V(Mathf.Cos(a),1,Mathf.Sin(a)));verts.Add(V(Mathf.Cos(b),1,Mathf.Sin(b)));verts.Add(V(Mathf.Cos(b),-1,Mathf.Sin(b)));triangles.AddRange(new[]{j,j+1,j+2,j,j+2,j+3});j=verts.Count;verts.Add(V(0,1,0));verts.Add(V(Mathf.Cos(b),1,Mathf.Sin(b)));verts.Add(V(Mathf.Cos(a),1,Mathf.Sin(a)));triangles.AddRange(new[]{j,j+1,j+2});j=verts.Count;verts.Add(V(0,-1,0));verts.Add(V(Mathf.Cos(a),-1,Mathf.Sin(a)));verts.Add(V(Mathf.Cos(b),-1,Mathf.Sin(b)));triangles.AddRange(new[]{j,j+1,j+2});}
        mesh=new Mesh();mesh.name="OctagonalCylinder";mesh.SetVertices(verts);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static void Save(string category,string name,Func<GameObject> make)
    {
        current=name;meshNumber=0;var go=make();go.name=name;
        if(!go.GetComponentInChildren<Collider>())
        {var rs=go.GetComponentsInChildren<Renderer>();if(rs.Length>0){var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);var c=go.AddComponent<BoxCollider>();c.center=b.center;c.size=b.size;}}
        string path=Root+"/Prefabs/"+category+"/"+name+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);Built.Add(path);UnityEngine.Object.DestroyImmediate(go);
    }
    static void Frame(Transform p,float w,float h,float d,string back="Brown")
    {
        Box(p,"Floor plinth",V(0,.075f,0),V(w,.15f,d),"Charcoal");Box(p,"Back panel",V(0,h*.5f,d*.45f),V(w-.06f,h,.055f),back);
        foreach(float x in new[]{-w*.5f+.04f,w*.5f-.04f}){Box(p,"Timber upright",V(x,h*.5f,0),V(.075f,h,d),"Brown");Box(p,"Upright front trim",V(x,h*.5f,-d*.5f),V(.045f,h,.025f),"Copper");}
    }
    static GameObject BakeryShelf(float width,float height,int tiers)
    {
        var root=New("Bakery shelf");Frame(root.transform,width,height,.65f);var slots=G(root.transform,"StockingSlots");
        for(int i=0;i<tiers;i++)
        {
            float y=.25f+i*(height-.4f)/tiers;var tray=G(root.transform,"Sloping shelf "+(i+1));tray.localPosition=V(0,y,0);tray.localRotation=Quaternion.Euler(-8,0,0);
            Box(tray,"Shelf base",Vector3.zero,V(width-.14f,.045f,.58f),"Brown");Box(tray,"Raised front lip",V(0,.045f,-.29f),V(width-.1f,.09f,.045f),"Copper");
            int bays=Mathf.Max(1,Mathf.RoundToInt(width/.57f));float bw=(width-.18f)/bays;
            for(int j=0;j<bays;j++){float x=(j-(bays-1)*.5f)*bw;Box(tray,"Empty tray liner",V(x,.032f,0),V(bw-.035f,.012f,.51f),"Cream");Box(tray,"Blank price ticket",V(x,.05f,-.319f),V(.22f,.055f,.008f),"Cream");var s=G(slots,"Tier"+(i+1)+"_Bay"+(j+1));s.localPosition=V(x,y+.04f,0);}
            for(int j=1;j<bays;j++)Box(tray,"Bin divider",V((j-bays*.5f)*bw,.065f,0),V(.014f,.1f,.55f),"Copper");
        }
        return root;
    }
    static GameObject BakeryCorner()
    {
        var r=New("Corner shelf");var a=BakeryShelf(1.15f,1.9f,4);a.transform.SetParent(r.transform,false);a.transform.localPosition=V(-.575f,0,0);var b=BakeryShelf(1.15f,1.9f,4);b.transform.SetParent(r.transform,false);b.transform.localPosition=V(.30f,0,.875f);b.transform.localRotation=Quaternion.Euler(0,90,0);return r;
    }
    static GameObject BakeryTable(float w,bool tiered)
    {
        var r=New("Bakery table");Box(r.transform,"Dark base",V(0,.06f,0),V(w,.12f,.95f),"Charcoal");
        for(int i=0;i<4;i++)Box(r.transform,"Front timber plank",V(0,.2f+i*.15f,-.45f),V(w-.04f,.135f,.07f),"Brown");
        foreach(float x in new[]{-w*.5f+.04f,w*.5f-.04f})Box(r.transform,"Crate end",V(x,.43f,0),V(.07f,.75f,.92f),"Brown");
        Box(r.transform,"Table top",V(0,.82f,0),V(w,.07f,.95f),"Copper");
        int bins=Mathf.RoundToInt(w/.55f);for(int i=0;i<bins;i++){float x=(i-(bins-1)*.5f)*(w/bins);var t=Tray(w/bins-.025f,.1f,.86f);t.transform.SetParent(r.transform,false);t.transform.localPosition=V(x,.86f,0);Box(r.transform,"Blank price ticket",V(x,.75f,-.496f),V(.25f,.07f,.01f),"Cream");}
        for(int i=0;i<3;i++)Box(r.transform,"Crate corner band",V((i-1)*(w*.44f),.42f,-.5f),V(.055f,.70f,.025f),"Copper");
        if(tiered){Box(r.transform,"Raised rear tier",V(0,1.04f,.22f),V(w-.16f,.3f,.44f),"Brown");var t=Tray(w-.12f,.1f,.43f);t.transform.SetParent(r.transform,false);t.transform.localPosition=V(0,1.2f,.22f);}G(r.transform,"StockingSlots").localPosition=V(0,.94f,0);return r;
    }
    static GameObject Basket(float w,float h,float d)
    {
        var r=New("Empty basket");Box(r.transform,"Basket bottom",V(0,.025f,0),V(w,.05f,d),"Brown");
        for(int row=0;row<7;row++){float y=.065f+row*(h-.08f)/7;foreach(float z in new[]{-d*.5f,d*.5f})Box(r.transform,"Woven horizontal band",V(0,y,z),V(w,.045f,.025f),row%2==0?"Sand":"Copper");foreach(float x in new[]{-w*.5f,w*.5f})Box(r.transform,"Woven side",V(x,y,0),V(.025f,.045f,d),"Copper");}
        for(int i=0;i<7;i++)foreach(float z in new[]{-d*.5f-.006f,d*.5f+.006f})Box(r.transform,"Woven upright",V((i-3)*w/7,h*.5f,z),V(.022f,h,.015f),"Brown");return r;
    }
    static GameObject Tray(float w,float h,float d)
    {
        var r=New("Empty tray");Box(r.transform,"Tray bottom",V(0,.015f,0),V(w,.03f,d),"Cream");foreach(float z in new[]{-d*.5f,d*.5f})Box(r.transform,"Timber lip",V(0,h*.5f,z),V(w,h,.025f),"Brown");foreach(float x in new[]{-w*.5f,w*.5f})Box(r.transform,"End lip",V(x,h*.5f,0),V(.025f,h,d),"Brown");return r;
    }
    static GameObject TrayStack(){var r=New("Tray stack");for(int i=0;i<8;i++){var t=Tray(.58f,.045f,.42f);t.transform.SetParent(r.transform,false);t.transform.localPosition=V(0,i*.045f,0);}return r;}

    static GameObject Sign(string text,float w,float h,string bg,string fg,bool wood)
    {
        var r=New("Sign");Box(r.transform,"Sign board",V(0,h*.5f,0),V(w,h,.06f),bg);
        foreach(float x in new[]{-w*.5f,w*.5f})Box(r.transform,"Vertical frame",V(x,h*.5f,-.014f),V(.035f,h+.04f,.08f),wood?"Copper":"Slate");
        foreach(float y in new[]{0f,h})Box(r.transform,"Horizontal frame",V(0,y,-.014f),V(w+.04f,.035f,.08f),wood?"Copper":"Slate");
        Text(r.transform,text,V(0,h*.5f,-.045f),w-.13f,h-.1f,fg);return r;
    }
    static GameObject WheatPlaque()
    {
        var r=Sign("",.4f,.75f,"Brown","Cream",true);Rod(r.transform,"Wheat stalk",V(0,.13f,-.055f),V(0,.63f,-.055f),.008f,"Lemon");for(int i=0;i<4;i++)foreach(int side in new[]{-1,1}){var t=Box(r.transform,"Wheat grain",V(side*.045f,.25f+i*.08f,-.06f),V(.04f,.085f,.016f),"Lemon");t.localRotation=Quaternion.Euler(0,0,side*-36);}return r;
    }
    static GameObject Freezer(int doors)
    {
        float w=doors*.78f+.08f;var r=New("Upright freezer");var p=r.transform;
        Box(p,"Insulated back",V(0,1.14f,.37f),V(w,2.28f,.1f),"Charcoal");Box(p,"Top canopy",V(0,2.23f,0),V(w+.06f,.13f,.88f),"Slate");Box(p,"Ventilated plinth",V(0,.1f,0),V(w,.2f,.86f),"Charcoal");
        foreach(float x in new[]{-w*.5f+.04f,w*.5f-.04f})Box(p,"Insulated side",V(x,1.14f,0),V(.08f,2.16f,.82f),"Slate");var slots=G(p,"StockingSlots");
        for(int d=0;d<doors;d++)
        {
            float x=(d-(doors-1)*.5f)*.78f;Box(p,"Cold interior",V(x,1.18f,.305f),V(.73f,1.93f,.018f),"Navy");
            for(int tier=0;tier<5;tier++){float y=.31f+tier*.365f;Box(p,"Empty product shelf",V(x,y,.015f),V(.70f,.025f,.61f),"Slate");Box(p,"Shelf light strip",V(x,y+.012f,-.30f),V(.68f,.014f,.017f),"ColdLight");var s=G(slots,"Door"+(d+1)+"_Shelf"+(tier+1));s.localPosition=V(x,y+.025f,0);}
            foreach(float sx in new[]{-.35f,.35f})Box(p,"Vertical LED",V(x+sx,1.22f,.27f),V(.018f,1.85f,.02f),"ColdLight");
            var hinge=G(p,"Door_"+(d+1)+"_Hinge");hinge.localPosition=V(x-.365f,.22f,-.395f);var leaf=G(hinge,"Door leaf");leaf.localPosition=V(.365f,0,0);
            Box(leaf,"Transparent blue glass",V(0,.96f,0),V(.66f,1.84f,.012f),"Glass");
            foreach(float xx in new[]{-.365f,.365f})Box(leaf,"Door vertical frame",V(xx,.96f,0),V(.045f,1.96f,.055f),"Charcoal");foreach(float y in new[]{0f,1.92f})Box(leaf,"Door horizontal frame",V(0,y,0),V(.76f,.045f,.055f),"Charcoal");
            Box(leaf,"Metal handle",V(.285f,.96f,-.055f),V(.025f,.34f,.04f),"Slate");Box(leaf,"Glass glint",V(-.26f,1.52f,-.014f),V(.012f,.4f,.006f),"Sky");
            for(int j=0;j<4;j++)Box(p,"Plinth vent",V(x,.065f+j*.032f,-.439f),V(.62f,.009f,.009f),"Slate");
        }return r;
    }
    static GameObject FreezerCorner(){var r=New("Freezer corner");var a=Freezer(2);a.transform.SetParent(r.transform,false);a.transform.localPosition=V(-.8f,0,0);var b=Freezer(1);b.transform.SetParent(r.transform,false);b.transform.localPosition=V(.41f,0,.82f);b.transform.localRotation=Quaternion.Euler(0,90,0);return r;}
    static GameObject Island(float w)
    {
        var r=New("Island freezer");Box(r.transform,"Insulated chest",V(0,.44f,0),V(w,.78f,.91f),"Slate");Box(r.transform,"Dark base",V(0,.06f,0),V(w-.06f,.12f,.88f),"Charcoal");
        Box(r.transform,"Recessed cold interior",V(0,.84f,0),V(w-.16f,.018f,.76f),"Navy");
        foreach(float z in new[]{-.46f,.46f})Box(r.transform,"Top rim",V(0,.91f,z),V(w,.09f,.05f),"Charcoal");foreach(float x in new[]{-w*.5f,w*.5f})Box(r.transform,"End rim",V(x,.91f,0),V(.055f,.09f,.94f),"Charcoal");
        int bays=Mathf.RoundToInt(w/.7f);for(int i=0;i<bays;i++){float x=(i-(bays-1)*.5f)*(w/bays);var lid=G(r.transform,"Sliding glass lid "+(i+1));Box(lid,"Glass",V(x,.914f,0),V(w/bays-.045f,.012f,.82f),"Glass");Box(lid,"Lid handle",V(x,.939f,-.25f),V(.18f,.025f,.025f),"Charcoal");Box(lid,"Lid rail",V(x-w/bays*.5f,.92f,0),V(.022f,.035f,.88f),"Sky");Box(r.transform,"Blank price strip",V(x,.72f,-.47f),V(.22f,.055f,.01f),"Cream");G(r.transform,"StockingSlot_"+(i+1)).localPosition=V(x,.86f,0);}
        foreach(float x in new[]{-w*.5f+.11f,w*.5f-.11f})foreach(float z in new[]{-.34f,.34f})Box(r.transform,"Adjustable foot",V(x,.025f,z),V(.1f,.05f,.1f),"Charcoal");return r;
    }
    static GameObject StaffDoor(string color,float angle,bool round,bool tall=false)
    {
        var r=New("Staff double doors");float w=1.8f,h=2.25f;
        foreach(float x in new[]{-w*.5f-.065f,w*.5f+.065f})Box(r.transform,"Concrete jamb",V(x,h*.5f,0),V(.13f,h,.22f),"Slate");Box(r.transform,"Concrete lintel",V(0,h+.09f,0),V(2.06f,.18f,.22f),"Slate");
        var sign=Sign("STAFF ONLY",1.48f,.28f,"Cream","Charcoal",false);sign.transform.SetParent(r.transform,false);sign.transform.localPosition=V(0,2.4f,0);
        foreach(int side in new[]{-1,1})
        {
            var hinge=G(r.transform,(side<0?"Left":"Right")+"Door_Hinge");hinge.localPosition=V(side*.89f,0,0);hinge.localRotation=Quaternion.Euler(0,side*angle,0);var leaf=G(hinge,"Door leaf");leaf.localPosition=V(-side*.445f,0,0);
            Box(leaf,"Door panel",V(0,1.10f,0),V(.875f,2.2f,.08f),color);Box(leaf,"Kick plate",V(0,.2f,-.049f),V(.83f,.3f,.014f),"Slate");Box(leaf,"Push bar",V(0,1.02f,-.08f),V(.72f,.055f,.045f),"Copper");Box(leaf,"Handle",V(-side*.31f,1.0f,-.10f),V(.03f,.22f,.035f),"Slate");
            if(round){Rod(leaf,"Porthole rim",V(0,1.62f,-.065f),V(0,1.62f,-.085f),.19f,"Charcoal");Rod(leaf,"Blue porthole",V(0,1.62f,-.087f),V(0,1.62f,-.091f),.145f,"Sky");}
            else{float wh=tall?.64f:.44f;Box(leaf,"Window surround",V(0,1.62f,-.049f),V(tall?.21f:.36f,wh,.025f),"Charcoal");Box(leaf,"Frosted safety window",V(0,1.62f,-.066f),V(tall?.15f:.30f,wh-.06f,.01f),"Sky");var glint=Box(leaf,"Window reflection",V(-.035f,1.66f,-.073f),V(.025f,wh*.62f,.005f),"White");glint.localRotation=Quaternion.Euler(0,0,-27);}
            var col=leaf.gameObject.AddComponent<BoxCollider>();col.center=V(0,1.1f,0);col.size=V(.875f,2.2f,.08f);
        }return r;
    }
    static GameObject StaffSingle(){var r=New("Staff single door");foreach(float x in new[]{-.5f,.5f})Box(r.transform,"Door jamb",V(x,1.1f,0),V(.08f,2.2f,.16f),"Slate");Box(r.transform,"Lintel",V(0,2.24f,0),V(1.08f,.08f,.16f),"Slate");var hinge=G(r.transform,"Door_Hinge");hinge.localPosition=V(-.45f,0,0);Box(hinge,"Door panel",V(.45f,1.08f,0),V(.88f,2.16f,.07f),"Navy");Box(hinge,"Window frame",V(.45f,1.6f,-.045f),V(.38f,.47f,.025f),"Charcoal");Box(hinge,"Blue window",V(.45f,1.6f,-.06f),V(.31f,.40f,.012f),"Sky");Box(hinge,"Push bar",V(.45f,1.0f,-.07f),V(.7f,.045f,.04f),"Copper");return r;}
    static GameObject SafetySign(bool exit){return Sign(exit?"!\nFIRE EXIT\nKEEP CLEAR":"X\nNO\nENTRY",.42f,.65f,exit?"Navy":"Red","White",false);}
    static GameObject Electrical(){var r=New("Electrical cabinet");Box(r.transform,"Steel box",V(0,.29f,0),V(.46f,.58f,.16f),"Slate");Box(r.transform,"Door seam",V(0,.29f,-.087f),V(.40f,.52f,.014f),"Charcoal");Box(r.transform,"Inset front",V(0,.29f,-.1f),V(.38f,.5f,.015f),"Slate");Text(r.transform,"!",V(0,.36f,-.114f),.12f,.19f,"Charcoal");Box(r.transform,"Lock",V(.13f,.20f,-.12f),V(.035f,.045f,.015f),"Charcoal");for(int i=0;i<3;i++)Box(r.transform,"Vent",V(-.07f,.12f+i*.028f,-.115f),V(.14f,.01f,.01f),"Charcoal");return r;}
    static GameObject Pipe(int shape){var r=New("Pipe");Rod(r.transform,"Vertical conduit",V(0,0,0),V(0,shape==1?.48f:1f,0),.026f,"Slate");if(shape>0)Rod(r.transform,"Branch conduit",V(0,.48f,0),V(.5f,.48f,0),.026f,"Slate");foreach(float y in new[]{.08f,.4f})Box(r.transform,"Wall clamp",V(0,y,.018f),V(.09f,.045f,.08f),"Charcoal");return r;}
    static GameObject Lamp(bool warm){var r=New("Wall light");Box(r.transform,"Wall mounting plate",V(0,.10f,.035f),V(.2f,.14f,.04f),"Charcoal");Rod(r.transform,"Lamp arm",V(0,.1f,0),V(0,.2f,-.18f),.022f,"Charcoal");Box(r.transform,"Lamp housing",V(0,.16f,-.2f),V(.55f,.085f,.17f),"Charcoal");Box(r.transform,"Glowing lens",V(0,.11f,-.22f),V(.46f,.018f,.12f),warm?"WarmLight":"ColdLight");return r;}
    static GameObject Carton(float w,float h,float d){var r=New("Carton");Box(r.transform,"Cardboard box",V(0,h*.5f,0),V(w,h,d),"Sand");Box(r.transform,"Top packing tape",V(0,h+.003f,0),V(.07f,.006f,d+.006f),"Copper");Box(r.transform,"Front tape",V(0,h*.5f,-d*.5f-.003f),V(.07f,h,.006f),"Copper");Box(r.transform,"White shipping label",V(w*.24f,h*.56f,-d*.5f-.007f),V(w*.23f,h*.27f,.008f),"Cream");for(int i=0;i<3;i++)Box(r.transform,"Label barcode",V(w*.20f+i*.018f,h*.56f,-d*.5f-.013f),V(.006f,h*.16f,.004f),"Charcoal");return r;}
    static GameObject Pallet(){var r=New("Wood pallet");foreach(float x in new[]{-.42f,0f,.42f})foreach(float z in new[]{-.32f,0f,.32f})Box(r.transform,"Pallet block",V(x,.065f,z),V(.13f,.1f,.13f),"Brown");for(int i=0;i<5;i++)Box(r.transform,"Deck board",V((i-2)*.22f,.14f,0),V(.18f,.04f,.85f),"Copper");foreach(float x in new[]{-.42f,0,.42f})Box(r.transform,"Bottom runner",V(x,.015f,0),V(.14f,.03f,.85f),"Brown");return r;}
    static GameObject CartonStack(bool pallet){var r=New("Stacked cartons");float y=pallet?.17f:0;if(pallet){var p=Pallet();p.transform.SetParent(r.transform,false);}for(int i=0;i<5;i++){var c=Carton(.45f,.34f,.36f);c.transform.SetParent(r.transform,false);c.transform.localPosition=V((i%2==0?-.25f:.25f),y+(i/2)*.34f,i==4?.05f:0);if(i==4)c.transform.localRotation=Quaternion.Euler(0,-8,0);}return r;}
    static GameObject RollCage(bool loaded){var r=New("Roll cage");Box(r.transform,"Rolling platform",V(0,.15f,0),V(.84f,.07f,.64f),"Slate");foreach(float x in new[]{-.35f,.35f})foreach(float z in new[]{-.25f,.25f}){Wheel(r.transform,x,.07f,z,.07f);Rod(r.transform,"Corner upright",V(x,.18f,z),V(x,1.5f,z),.018f,"Slate");}foreach(float y in new[]{.2f,.58f,.96f,1.48f}){foreach(float x in new[]{-.36f,.36f})Rod(r.transform,"Side rail",V(x,y,-.27f),V(x,y,.27f),.014f,"Slate");Rod(r.transform,"Rear rail",V(-.36f,y,.27f),V(.36f,y,.27f),.014f,"Slate");}foreach(float x in new[]{-.18f,0,.18f})Rod(r.transform,"Rear mesh bar",V(x,.18f,.27f),V(x,1.48f,.27f),.009f,"Slate");foreach(float y in new[]{.58f,.96f})Box(r.transform,"Shelf",V(0,y,0),V(.73f,.025f,.54f),"Slate");if(loaded)for(int i=0;i<6;i++){var c=Carton(.29f,.28f,.4f);c.transform.SetParent(r.transform,false);c.transform.localPosition=V(i%2==0?-.17f:.17f,.20f+(i/2)*.38f,0);}return r;}
    static GameObject HandTruck(){var r=New("Hand truck");Box(r.transform,"Load footplate",V(0,.10f,-.13f),V(.48f,.045f,.38f),"Red");foreach(float x in new[]{-.19f,.19f}){Rod(r.transform,"Red upright",V(x,.14f,0),V(x,1.18f,0),.022f,"Red");Rod(r.transform,"Handle bend",V(x,1.18f,0),V(x,1.26f,.12f),.022f,"Red");Rod(r.transform,"Black hand grip",V(x,1.26f,.12f),V(x,1.12f,.17f),.027f,"Charcoal");Wheel(r.transform,x,.105f,.11f,.105f);}foreach(float y in new[]{.3f,.55f,.8f})Rod(r.transform,"Cross rung",V(-.19f,y,0),V(.19f,y,0),.018f,"Red");return r;}
    static GameObject PalletJack(){var r=New("Pallet jack");foreach(float x in new[]{-.24f,.24f}){Box(r.transform,"Yellow fork",V(x,.11f,-.45f),V(.18f,.10f,1.10f),"Lemon");Wheel(r.transform,x,.055f,-.88f,.05f);}Box(r.transform,"Fork crossbeam",V(0,.19f,.1f),V(.72f,.18f,.27f),"Lemon");Rod(r.transform,"Hydraulic piston",V(0,.16f,.15f),V(0,.5f,.15f),.065f,"Slate");Rod(r.transform,"Steering handle",V(0,.35f,.18f),V(0,1.2f,.38f),.025f,"Charcoal");foreach(float x in new[]{-.16f,.16f})Rod(r.transform,"Handle sides",V(0,1.1f,.36f),V(x,1.38f,.42f),.022f,"Charcoal");Rod(r.transform,"Handle grip",V(-.16f,1.38f,.42f),V(.16f,1.38f,.42f),.027f,"Charcoal");foreach(float x in new[]{-.12f,.12f})Wheel(r.transform,x,.09f,.22f,.09f);return r;}
    static GameObject Ladder(){var r=New("Step ladder");foreach(float x in new[]{-.25f,.25f}){Rod(r.transform,"Front ladder rail",V(x,.03f,-.38f),V(x,1.15f,0),.025f,"Slate");Rod(r.transform,"Back ladder rail",V(x,.03f,.38f),V(x,1.15f,0),.025f,"Slate");}for(int i=0;i<4;i++)Box(r.transform,"Tread step",V(0,.2f+i*.25f,-.31f+i*.084f),V(.52f,.035f,.17f),"Slate");Box(r.transform,"Top platform",V(0,1.15f,0),V(.58f,.06f,.25f),"Charcoal");foreach(float x in new[]{-.25f,.25f})Rod(r.transform,"Safety brace",V(x,.5f,-.22f),V(x,.5f,.22f),.012f,"Charcoal");return r;}
    static GameObject Aboard(bool wet){var r=New("A board");foreach(int side in new[]{-1,1}){var board=G(r.transform,"Folding sign panel");board.localPosition=V(0,.48f,side*.15f);board.localRotation=Quaternion.Euler(side*18,0,0);Box(board,"Panel",Vector3.zero,V(.48f,.78f,.045f),wet?"Lemon":"Brown");Box(board,"Inset face",V(0,0,-.028f),V(.38f,.64f,.01f),wet?"Lemon":"Charcoal");Text(board,wet?"!\nCAUTION\nWET FLOOR":"FRESH\nBREAD\nDAILY",V(0,0,-.037f),.33f,.53f,wet?"Charcoal":"Cream");}return r;}
    static GameObject Extinguisher(){var r=New("Fire extinguisher");Rod(r.transform,"Red cylinder",V(0,.08f,0),V(0,.5f,0),.10f,"Red");Rod(r.transform,"Top shoulder",V(0,.5f,0),V(0,.56f,0),.07f,"Red");Box(r.transform,"Bottom shoe",V(0,.04f,0),V(.18f,.05f,.18f),"Charcoal");Box(r.transform,"Valve lever",V(0,.60f,0),V(.17f,.035f,.07f),"Charcoal");Box(r.transform,"White instructions",V(0,.3f,-.102f),V(.105f,.2f,.012f),"Cream");Text(r.transform,"FIRE",V(0,.32f,-.111f),.09f,.09f,"Red");Rod(r.transform,"Hose",V(.08f,.57f,0),V(.16f,.24f,0),.013f,"Charcoal");return r;}
    static GameObject Bin(){var r=New("Wheelie bin");Box(r.transform,"Green bin body",V(0,.5f,0),V(.57f,.86f,.56f),"Olive");Box(r.transform,"Hinged lid",V(0,.96f,0),V(.64f,.065f,.63f),"Forest");Box(r.transform,"Lid grip",V(0,1.01f,-.26f),V(.20f,.04f,.045f),"Charcoal");foreach(float x in new[]{-.24f,.24f})Wheel(r.transform,x,.09f,.22f,.09f);foreach(float x in new[]{-.2f,.2f})Box(r.transform,"Moulded rib",V(x,.5f,-.285f),V(.02f,.7f,.02f),"Forest");return r;}
    static GameObject Bag(){var r=New("Empty paper bag");Box(r.transform,"Front paper",V(0,.25f,-.09f),V(.27f,.5f,.012f),"Sand");Box(r.transform,"Back paper",V(0,.25f,.09f),V(.27f,.5f,.012f),"Sand");foreach(float x in new[]{-.13f,.13f})Box(r.transform,"Folded side",V(x,.24f,0),V(.014f,.48f,.18f),"Copper");Box(r.transform,"Bag bottom",V(0,.01f,0),V(.27f,.02f,.18f),"Sand");Text(r.transform,"BAKERY",V(0,.22f,-.1f),.21f,.12f,"Brown");return r;}
    static GameObject Plant(){var r=New("Potted plant");Box(r.transform,"Terracotta pot",V(0,.17f,0),V(.35f,.34f,.35f),"Copper");Box(r.transform,"Pot rim",V(0,.33f,0),V(.4f,.07f,.4f),"Brown");Box(r.transform,"Soil",V(0,.37f,0),V(.32f,.01f,.32f),"Charcoal");Rod(r.transform,"Stem",V(0,.36f,0),V(0,1.05f,0),.016f,"Forest");for(int i=0;i<10;i++){float a=i*2.4f;var p=V(Mathf.Cos(a)*.15f,.52f+i*.055f,Mathf.Sin(a)*.15f);Rod(r.transform,"Branch",V(0,p.y-.06f,0),p,.008f,"Forest");var leaf=Box(r.transform,"Blocky leaf",p,V(.11f,.045f,.19f),i%2==0?"Forest":"Olive");leaf.localRotation=Quaternion.Euler(-25,i*137,20);}return r;}

    static void Place(Transform parent,string category,string name,Vector3 pos,float yaw=0)
    {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+category+"/"+name+".prefab");var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(0,yaw,0);}
    static GameObject Showcase(int type)
    {
        var r=New("Example layout");Box(r.transform,"Floor",V(0,-.06f,0),V(7,.12f,4.8f),"Slate");Box(r.transform,"Back wall",V(0,1.6f,2.3f),V(7,3.2f,.14f),type==0?"Brown":"Charcoal");
        if(type==0){Place(r.transform,"Bakery","BakeryShelf_Wide",V(-1.23f,0,1.85f));Place(r.transform,"Bakery","BakeryShelf_Wide",V(1.23f,0,1.85f));Place(r.transform,"Bakery","BakeryShelf_Narrow",V(2.94f,0,1.85f));Place(r.transform,"Bakery","BakeryDisplay_Large",V(-1.35f,0,-.7f));Place(r.transform,"Bakery","BakeryDisplay_Tiered",V(1.25f,0,-.7f));Place(r.transform,"Bakery","BakerySign",V(0,2.2f,2.15f));Place(r.transform,"Props","Planter",V(-3,0,1.5f));Place(r.transform,"Bakery","BakeryChalkAboard",V(-2.9f,0,-.8f));}
        if(type==1){Place(r.transform,"Freezers","Freezer_FiveDoor",V(-.8f,0,1.7f));Place(r.transform,"Freezers","Freezer_Double",V(2.1f,0,1.7f));Place(r.transform,"Freezers","FrozenSign",V(0,2.48f,2.15f));Place(r.transform,"Freezers","IslandFreezer_Long",V(-1.4f,0,-.65f));Place(r.transform,"Freezers","IslandFreezer_Small",V(1.3f,0,-.65f));Place(r.transform,"Props","WetFloorSign",V(2.7f,0,-.8f));}
        if(type==2){Place(r.transform,"Staff","StaffDoors_Blue",V(-.5f,0,2.05f));Place(r.transform,"Staff","ElectricalBox",V(1.2f,1.9f,2.15f));Place(r.transform,"Staff","Pipe_Straight",V(1.6f,1.1f,2.12f));Place(r.transform,"Staff","Pipe_Elbow",V(1.6f,2.1f,2.12f));Place(r.transform,"Props","RollCage_Cartons",V(2.4f,0,1.4f));Place(r.transform,"Props","HandTruck",V(-2.5f,0,1.9f));Place(r.transform,"Props","WetFloorSign",V(-1.9f,0,.6f));Place(r.transform,"Props","FireExtinguisher",V(-1.85f,.8f,2.13f));Place(r.transform,"Props","CartonStack",V(1.4f,0,.4f));Place(r.transform,"Props","WheelieBin",V(2.85f,0,-.5f));}
        for(int i=0;i<3;i++)Place(r.transform,"Props",type==0?"WallLight_Warm":"WallLight_Cold",V((i-1)*2,2.9f,2.17f));return r;
    }

    static void InitFont()
    {
        if(Glyphs.Count>0)return;
        string letters="ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string[] rows={"01110/10001/10001/11111/10001/10001/10001","11110/10001/10001/11110/10001/10001/11110","01111/10000/10000/10000/10000/10000/01111","11110/10001/10001/10001/10001/10001/11110","11111/10000/10000/11110/10000/10000/11111","11111/10000/10000/11110/10000/10000/10000","01111/10000/10000/10111/10001/10001/01111","10001/10001/10001/11111/10001/10001/10001","11111/00100/00100/00100/00100/00100/11111","00111/00010/00010/00010/10010/10010/01100","10001/10010/10100/11000/10100/10010/10001","10000/10000/10000/10000/10000/10000/11111","10001/11011/10101/10101/10001/10001/10001","10001/11001/10101/10011/10001/10001/10001","01110/10001/10001/10001/10001/10001/01110","11110/10001/10001/11110/10000/10000/10000","01110/10001/10001/10001/10101/10010/01101","11110/10001/10001/11110/10100/10010/10001","01111/10000/10000/01110/00001/00001/11110","11111/00100/00100/00100/00100/00100/00100","10001/10001/10001/10001/10001/10001/01110","10001/10001/10001/10001/10001/01010/00100","10001/10001/10001/10101/10101/11011/10001","10001/10001/01010/00100/01010/10001/10001","10001/10001/01010/00100/00100/00100/00100","11111/00001/00010/00100/01000/10000/11111","01110/10001/10011/10101/11001/10001/01110","00100/01100/00100/00100/00100/00100/01110","01110/10001/00001/00010/00100/01000/11111","11110/00001/00001/01110/00001/00001/11110","00010/00110/01010/10010/11111/00010/00010","11111/10000/10000/11110/00001/00001/11110","01110/10000/10000/11110/10001/10001/01110","11111/00001/00010/00100/01000/01000/01000","01110/10001/10001/01110/10001/10001/01110","01110/10001/10001/01111/00001/00001/01110"};
        for(int i=0;i<letters.Length;i++)Glyphs[letters[i]]=rows[i];Glyphs['!']="00100/00100/00100/00100/00100/00000/00100";Glyphs['*']="00100/10101/01110/11111/01110/10101/00100";
    }
    static void Text(Transform parent,string value,Vector3 pos,float width,float height,string material)
    {
        if(string.IsNullOrEmpty(value))return;string[] lines=value.ToUpperInvariant().Split('\n');int max=lines.Max(l=>l.Length);float unit=Mathf.Min(width/Mathf.Max(1,max*6-1),height/(lines.Length*9-2));var verts=new List<Vector3>();var tris=new List<int>();
        for(int line=0;line<lines.Length;line++)for(int c=0;c<lines[line].Length;c++)
        {
            string code;if(!Glyphs.TryGetValue(lines[line][c],out code))continue;var rows=code.Split('/');
            for(int y=0;y<7;y++)for(int x=0;x<5;x++)if(rows[y][x]=='1')
            {float xx=(c*6+x-(lines[line].Length*6-1)*.5f)*unit,yy=((lines.Length*9-2)*.5f-line*9-y-1)*unit;int j=verts.Count;verts.Add(V(xx,yy,0));verts.Add(V(xx+unit,yy,0));verts.Add(V(xx+unit,yy+unit,0));verts.Add(V(xx,yy+unit,0));tris.AddRange(new[]{j,j+2,j+1,j,j+3,j+2});}
        }
        var mesh=new Mesh();mesh.name=current+"_PixelText_"+(meshNumber++);mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Root+"/Meshes/"+mesh.name+".asset");var t=G(parent,"Pixel lettering - "+value.Replace('\n',' '));t.localPosition=pos;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=M(material);
    }

    [MenuItem("Tools/Store Fixtures/Refresh pack previews")]
    public static void RefreshPreviews()
    {
        Built.Clear();Built.AddRange(File.ReadAllLines(Root+"/AssetManifest.txt"));InitFont();scene=EditorSceneManager.NewPreviewScene();
        try{foreach(string cat in new[]{"Bakery","Freezers","Staff","Props","Showcases"})Contact(cat);Validate();AssetDatabase.Refresh();}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void Contact(string category)
    {
        var paths=Built.Where(p=>p.Contains("/"+category+"/")).ToArray();int columns=category=="Showcases"?3:4;int rows=Mathf.CeilToInt(paths.Length/(float)columns);int cell=480;var sheet=new Texture2D(columns*cell,rows*cell,TextureFormat.RGB24,false);
        var bg=new Color(.13f,.15f,.18f);sheet.SetPixels(Enumerable.Repeat(bg,sheet.width*sheet.height).ToArray());
        for(int i=0;i<paths.Length;i++)
        {
            var obj=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]));SceneManager.MoveGameObjectToScene(obj,scene);
            var rs=obj.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var rr in rs)b.Encapsulate(rr.bounds);var cam=New("Contact camera").AddComponent<Camera>();cam.scene=scene;cam.orthographic=true;cam.aspect=1;cam.transform.position=b.center+V(paths[i].Contains("Corner")?-2.5f:2.5f,1.7f,-4)*Mathf.Max(1,b.size.magnitude);cam.transform.LookAt(b.center);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=bg;
            float extent=0;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2){var point=cam.transform.InverseTransformPoint(b.center+Vector3.Scale(b.extents,V(x,y,z)));extent=Mathf.Max(extent,Mathf.Abs(point.x),Mathf.Abs(point.y));}cam.orthographicSize=Mathf.Max(extent*1.16f,.15f);
            var light=New("Preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(40,-35,0);
            var old=RenderSettings.ambientProbe;var sh=new UnityEngine.Rendering.SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.55f,.55f,.55f));RenderSettings.ambientProbe=sh;
            int picture=cell-64;var rt=new RenderTexture(picture,picture,24);var prev=RenderTexture.active;var img=new Texture2D(picture,picture,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;img.ReadPixels(new Rect(0,0,picture,picture),0,0);img.Apply();int cx=(i%columns)*cell,cy=(rows-1-i/columns)*cell;sheet.SetPixels(cx+32,cy+64,picture,picture,img.GetPixels());Caption(sheet,Path.GetFileNameWithoutExtension(paths[i]).ToUpperInvariant().Replace('_',' '),cx,cy,cell);}
            finally{RenderSettings.ambientProbe=old;RenderTexture.active=prev;cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(img);UnityEngine.Object.DestroyImmediate(cam.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);UnityEngine.Object.DestroyImmediate(obj);}
        }sheet.Apply();File.WriteAllBytes(Root+"/Previews/"+category+".png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);
    }
    static void Caption(Texture2D sheet,string value,int cx,int cy,int cell)
    {
        int scale=2;var lines=new List<string>();if(value.Length>36){int split=value.LastIndexOf(' ',Math.Min(30,value.Length-1));if(split<1)split=30;lines.Add(value.Substring(0,split));lines.Add(value.Substring(split).Trim());}else lines.Add(value);
        for(int line=0;line<lines.Count;line++){string text=lines[line];int start=cx+(cell-(text.Length*6-1)*scale)/2;for(int c=0;c<text.Length;c++){string code;if(!Glyphs.TryGetValue(text[c],out code))continue;var rows=code.Split('/');for(int y=0;y<7;y++)for(int x=0;x<5;x++)if(rows[y][x]=='1')for(int dy=0;dy<scale;dy++)for(int dx=0;dx<scale;dx++)sheet.SetPixel(start+(c*6+x)*scale+dx,cy+38-line*20+(6-y)*scale+dy,new Color(.82f,.86f,.9f));}}
    }
    static void Validate()
    {
        var report=new System.Text.StringBuilder();foreach(string path in Built){var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!p)throw new Exception("Missing prefab "+path);foreach(var r in p.GetComponentsInChildren<Renderer>(true)){if(r.sharedMaterials.Any(m=>!m||!m.shader||!m.shader.isSupported))throw new Exception("Broken material "+path+"/"+r.name);}foreach(var mf in p.GetComponentsInChildren<MeshFilter>(true))if(!mf.sharedMesh)throw new Exception("Missing mesh "+path);if(p.GetComponentsInChildren<Collider>(true).Length==0)throw new Exception("Missing collider "+path);report.AppendLine("PASS "+path);}report.AppendLine("PASS "+Built.Count+" prefabs; renderer materials, meshes and colliders valid. No product items generated; existing scene untouched.");File.WriteAllText(Root+"/Validation.txt",report.ToString());
        File.WriteAllLines(Root+"/AssetManifest.txt",Built);
    }
}
