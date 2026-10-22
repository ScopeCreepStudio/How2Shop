using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using How2Shop.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class NewSceneStockBuilder
{
    const string Request="UserSettings/NewSceneStock.populate";
    const string Prefix="Stock - NewScene - ";
    static readonly string[] Departments={"Drinks","Cereal","Snacks","Chocolate and biscuits","Tinned food","Pasta and rice","Baking ingredients","Tea and coffee","Spreads","Cleaning and household","Breakfast","Pantry vegetables","Bread","Bakery"};
    static readonly string[][] Pools={
        new[]{"Cola","Water","OrangeJuice","AppleJuice"},new[]{"Cereal"},new[]{"Crisps"},new[]{"Chocolate","Biscuits"},
        new[]{"BakedBeans","Tuna","TomatoSoup","Peas"},new[]{"Pasta","Rice"},new[]{"Flour","Sugar"},new[]{"Tea","Coffee"},
        new[]{"StrawberryJam","PeanutButter"},new[]{"Cereal"},new[]{"Cereal"},new[]{"Peas","BakedBeans"},new[]{"Bread"},
        new[]{"SourdoughLoaf","Baguette","Croissant","Pretzel","CinnamonRoll"}};
    static readonly string[] Fruit={"Apple","Banana","Orange","Pear","Strawberry"};
    static readonly string[] Frozen={"FrozenPizza","FishFingers","IceCream","FrozenWaffles","FrozenChips","FrozenPeas","FrozenBroccoli","FrozenBerries","ChickenNuggets","MixedVegetables"};
    static readonly Dictionary<string,GameObject> Cache=new Dictionary<string,GameObject>();
    static readonly List<GameObject> Added=new List<GameObject>();
    static readonly StringBuilder Report=new StringBuilder();
    static NewSceneStockBuilder(){EditorApplication.delayCall+=Auto;}
    static void Auto(){if(!File.Exists(Request))return;if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Auto;return;}try{Populate();File.Delete(Request);if(File.Exists("Logs/NewSceneStockError.txt"))File.Delete("Logs/NewSceneStockError.txt");}catch(Exception e){File.WriteAllText("Logs/NewSceneStockError.txt",e.ToString());File.Delete(Request);Debug.LogException(e);}}
    [MenuItem("Tools/How2Shop/Populate NewScene by department")]
    public static void Populate()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Superstore.unity");
        if(!scene.IsValid()||!scene.isLoaded)throw new Exception("Open Superstore before populating NewScene.");
        var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="NewScene");
        var assets=root.Find("SupermarketAssets");if(!assets)throw new Exception("Missing NewScene/SupermarketAssets");
        Directory.CreateDirectory("Logs");Directory.CreateDirectory("Assets/Levels/NewSceneStock");
        if(!File.Exists("Logs/Superstore_BeforeNewSceneStock.unity"))EditorSceneManager.SaveScene(scene,"Logs/Superstore_BeforeNewSceneStock.unity",true);
        int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Populate NewScene shelves by department");
        Cache.Clear();Added.Clear();Report.Clear();
        var old=assets.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith(Prefix,StringComparison.Ordinal)).ToArray();
        foreach(var t in old)if(t)Undo.DestroyObjectImmediate(t.gameObject);
        var aisles=new List<Transform>();
        foreach(string bank in new[]{"FrontIsle","BackIsle"})
        {
            var group=assets.Find(bank);if(group)aisles.AddRange(group.Cast<Transform>().OrderBy(t=>root.InverseTransformPoint(t.position).x));
        }
        aisles.AddRange(assets.Cast<Transform>().Where(t=>t.name.StartsWith("Isle1",StringComparison.Ordinal)).OrderBy(t=>t.position.x));
        var categories=new Dictionary<Transform,int>();
        for(int i=0;i<aisles.Count;i++){categories[aisles[i]]=i%Pools.Length;Report.AppendLine(PathOf(aisles[i],root)+" => "+Departments[i%Pools.Length]);}
        var fixtures=assets.GetComponentsInChildren<Transform>(true).Where(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).ToArray();
        int shelfCount=0,crateCount=0,freezerCount=0,coolerCount=0;
        foreach(var fixture in fixtures)
        {
            string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(fixture.gameObject);
            if(path.Contains("/AssetsReady1/Prefabs/")&&(path.Contains("Gondola_")||path.Contains("WallShelf_")||path.Contains("Shelf_Corner")))
            {
                int category=-1;for(var p=fixture;p&&p!=assets;p=p.parent)if(categories.TryGetValue(p,out int assigned)){category=assigned;break;}
                if(category<0)
                {
                    if(fixture.parent.name.StartsWith("SingleShelf"))category=fixture.parent.name=="SingleShelf"?9:7;
                    else category=categories.OrderBy(kv=>Vector2.Distance(new Vector2(kv.Key.position.x,kv.Key.position.z),new Vector2(fixture.position.x,fixture.position.z))).First().Value;
                }
                StockShelf(fixture,Pools[category],Departments[category],path.Contains("Gondola"),path.Contains("Corner"));shelfCount++;
            }
            else if(path.Contains("Wooden Boxes/Prefabs/")&&!path.Contains("Lid")&&!path.Contains("_lid"))
            {
                StockCrate(fixture,Fruit[crateCount%Fruit.Length]);crateCount++;
            }
            else if(path.Contains("raystore_fresh"))
            {
                StockFreezer(fixture,freezerCount);freezerCount++;
            }
            else if(path.EndsWith("Cooler_Single.prefab",StringComparison.Ordinal)||path.EndsWith("Cooler Fridge.prefab",StringComparison.Ordinal))
            {
                bool drinks=path.Contains("Cooler Fridge");StockCooler(fixture,drinks);coolerCount++;
            }
        }
        if(shelfCount<60||crateCount<10||freezerCount!=4||Added.Count<2500)throw new Exception("Unexpected fixture/stock count: "+shelfCount+" shelves, "+crateCount+" crates, "+freezerCount+" freezers, "+Added.Count+" items");
        foreach(var item in Added)
        {
            var sr=item.GetComponent<SpriteRenderer>();if(!sr)throw new Exception("Expected a flat picture item: "+item.name);if(sr&&(!sr.sprite||!sr.sharedMaterial||!item.GetComponent<ItemSpriteView>()?.Item))throw new Exception("Broken stock "+item.name);
            if(item.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterials.Any(m=>!m)))throw new Exception("Missing material "+item.name);
            if(!item.transform.IsChildOf(root))throw new Exception("Stock escaped NewScene");
        }
        Report.AppendLine("\nPASS: "+Added.Count+" added products; "+shelfCount+" shelf fixtures; "+crateCount+" fruit crates; "+freezerCount+" freezers; "+coolerCount+" coolers; "+aisles.Count+" aisle groups.");
        Report.AppendLine("Existing stocked vegetable fridges, bakery stands and newspaper stands retained. Cleaning uses repeated existing flat picture items as temporary placeholders. Fixtures and layout retained. All new stock belongs to NewScene and has valid sprites/materials. Re-run replaces only Stock - NewScene groups.");
        File.WriteAllText("Assets/Levels/NewSceneStock/PopulationReport.txt",Report.ToString());
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.Refresh();SceneView.RepaintAll();
    }
    static Transform StockGroup(Transform fixture,string category)
    {
        var g=new GameObject(Prefix+category);Undo.RegisterCreatedObjectUndo(g,"Add department stock");g.transform.SetParent(fixture,false);return g.transform;
    }
    static GameObject Product(string name)
    {
        if(Cache.TryGetValue(name,out var cached))return cached;
        string path=NamesBakery(name)?"Assets/Packages/How2ShopAssets/items/Bakery/Prefabs/"+name+".prefab":"Assets/Shelf_Items/Prefabs/"+name+".prefab";
        var item=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!item)throw new Exception("Missing item "+path);return Cache[name]=item;
    }
    static bool NamesBakery(string name)=>Pools[13].Contains(name);
    static GameObject Put(Transform parent,string name,Vector3 pos,Quaternion rotation,float width,float height,bool bottom=true)
    {
        var item=(GameObject)PrefabUtility.InstantiatePrefab(Product(name),parent.gameObject.scene);item.transform.SetParent(parent,false);item.transform.localRotation=rotation;
        var sr=item.GetComponent<SpriteRenderer>();
        if(sr&&sr.sprite)
        {
            item.transform.localScale=Vector3.one*.6f;
            if(bottom)pos.y-=sr.sprite.bounds.min.y*item.transform.localScale.y;
        }
        else
        {
            var bounds=BoundsOf(item.transform);float fit=Mathf.Min(width/bounds.size.x,height/bounds.size.y);item.transform.localScale*=fit;
            pos.y-=bounds.min.y*fit;
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_Cream.mat");foreach(var r in item.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
        }
        item.transform.localPosition=pos;foreach(var c in item.GetComponentsInChildren<Collider>())c.enabled=false;Added.Add(item);return item;
    }
    struct Tri {public Vector3 a,b,c;public float y;}
    static List<Tri> Surfaces(Transform fixture,bool shelvesOnly)
    {
        var list=new List<Tri>();
        foreach(var f in fixture.GetComponentsInChildren<MeshFilter>())
        {
            if(!f.sharedMesh||f.transform.GetComponentsInParent<Transform>().Any(t=>t.name.StartsWith(Prefix)))continue;
            if(shelvesOnly&&f.name!="Shelves"&&f.name!="Frame")continue;
            var v=f.sharedMesh.vertices;var tris=f.sharedMesh.triangles;
            for(int i=0;i<tris.Length;i+=3)
            {
                var a=fixture.InverseTransformPoint(f.transform.TransformPoint(v[tris[i]]));var b=fixture.InverseTransformPoint(f.transform.TransformPoint(v[tris[i+1]]));var c=fixture.InverseTransformPoint(f.transform.TransformPoint(v[tris[i+2]]));
                var normal=Vector3.Cross(b-a,c-a);float y=(a.y+b.y+c.y)/3;
                if(normal.normalized.y<.97f||normal.magnitude<.002f||y<.12f||y>1.82f||(shelvesOnly&&f.name=="Frame"&&y>.4f))continue;
                list.Add(new Tri{a=a,b=b,c=c,y=y});
            }
        }
        return list;
    }
    static bool Within(Tri t,float x,float z)
    {
        Vector2 a=new Vector2(t.a.x,t.a.z),b=new Vector2(t.b.x,t.b.z),c=new Vector2(t.c.x,t.c.z),p=new Vector2(x,z);
        var v0=b-a;var v1=c-a;var v2=p-a;float den=v0.x*v1.y-v1.x*v0.y;if(Mathf.Abs(den)<.00001f)return false;
        float u=(v2.x*v1.y-v1.x*v2.y)/den,v=(v0.x*v2.y-v2.x*v0.y)/den;return u>=0&&v>=0&&u+v<=1;
    }
    static void StockShelf(Transform fixture,string[] pool,string category,bool doubleSided,bool corner)
    {
        var group=StockGroup(fixture,category);var surfaces=Surfaces(fixture,true);
        var levels=surfaces.GroupBy(t=>Mathf.RoundToInt(t.y*50)).Where(g=>g.Sum(t=>Vector3.Cross(t.b-t.a,t.c-t.a).magnitude/2)>.07f).OrderBy(g=>g.Key).ToArray();
        if(levels.Length==0)throw new Exception("No shelf surfaces: "+fixture.name);
        int count=0,levelIndex=0;
        foreach(var level in levels)
        {
            float y=level.Average(t=>t.y);var triangles=level.ToArray();
            float minX=triangles.Min(t=>Mathf.Min(t.a.x,t.b.x,t.c.x)),maxX=triangles.Max(t=>Mathf.Max(t.a.x,t.b.x,t.c.x));
            float minZ=triangles.Min(t=>Mathf.Min(t.a.z,t.b.z,t.c.z)),maxZ=triangles.Max(t=>Mathf.Max(t.a.z,t.b.z,t.c.z));
            int columns=Mathf.Max(1,Mathf.FloorToInt((maxX-minX-.06f)/.22f));int rows=Mathf.Max(1,Mathf.FloorToInt((maxZ-minZ-.04f)/.15f));
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                float x=Mathf.Lerp(minX+.12f,maxX-.12f,columns==1?.5f:col/(float)(columns-1));
                float z=Mathf.Lerp(minZ+.07f,maxZ-.07f,rows==1?.5f:row/(float)(rows-1));
                if(!triangles.Any(t=>Within(t,x,z)))continue;
                if(doubleSided&&Mathf.Abs(z)<.235f)continue;
                if(corner&&x>.03f&&z>.03f)continue;
                float yaw=doubleSided&&z>0?180:0;
                if(corner)yaw=x<z?90:0;
                string name=pool[(levelIndex+col/2)%pool.Length];
                Put(group,name,new Vector3(x,y+.008f,z),Quaternion.Euler(0,yaw,0),.19f,.28f);count++;
            }
            levelIndex++;
        }
        Report.AppendLine(PathOf(fixture,fixture.root)+" | "+category+" | "+count+" products | shelf heights "+string.Join(",",levels.Select(l=>(l.Key/50f).ToString("F2"))));
    }
    static void StockCrate(Transform crate,string fruit)
    {
        var b=BoundsOf(crate);var group=StockGroup(crate,"Fruit - "+fruit);
        float width=b.size.x*.72f,depth=b.size.z*.70f;int cols=Mathf.Max(3,Mathf.FloorToInt(width/.22f)),rows=Mathf.Max(2,Mathf.FloorToInt(depth/.20f));
        float height=b.max.y-Mathf.Min(.16f,b.size.y*.22f);
        for(int layer=0;layer<2;layer++)for(int row=0;row<rows;row++)for(int col=0;col<cols;col++)
        {
            float x=b.center.x+((col+.5f)/cols-.5f)*width,z=b.center.z+((row+.5f)/rows-.5f)*depth;
            var rotation=Quaternion.Euler(65,0,((col+row)%3-1)*13);
            var sprite=Product(fruit).GetComponent<SpriteRenderer>().sprite;
            float projectedHeight=(Mathf.Abs((rotation*Vector3.right).y)*sprite.bounds.size.x+Mathf.Abs((rotation*Vector3.up).y)*sprite.bounds.size.y)*.6f;
            float separation=Mathf.Max(.215f,projectedHeight+.035f);
            var pos=new Vector3(x,height+.055f-(layer==0?separation:0),z);Put(group,fruit,pos,rotation,Mathf.Min(.26f,width/cols*1.1f),.27f,false);
        }
    }
    static void StockFreezer(Transform fixture,int seed)
    {
        var group=StockGroup(fixture,"Frozen food");
        var shelves=fixture.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.StartsWith("RaystoreFresh_shelf",StringComparison.Ordinal)).Select(f=>BoundsOf(f.transform,fixture)).OrderBy(b=>b.center.y).ToList();
        if(shelves.Count==0)throw new Exception("No freezer shelves "+fixture.name);
        shelves.Insert(0,new Bounds(new Vector3(.04f,.42f,-.18f),new Vector3(3.9f,.02f,.65f)));
        int tier=0;
        foreach(var b in shelves)
        {
            for(int row=0;row<3;row++)for(int col=0;col<16;col++)
            {
                // Four glass doors have their fronts on local +Z.
                float x=.04f+(col-7.5f)*.235f,z=.30f-row*.20f;
                Put(group,Frozen[(seed*2+tier+col/4)%Frozen.Length],new Vector3(x,b.center.y+.015f,z),Quaternion.Euler(0,180,0),.205f,.27f);
            }
            tier++;
        }
    }
    static void StockCooler(Transform fixture,bool drinks)
    {
        var group=StockGroup(fixture,drinks?"Chilled drinks":"Dairy");var pool=drinks?Pools[0]:new[]{"Milk","Yogurt","Cheese","Butter","Eggs"};
        float[] heights=drinks?new[]{.36f,.78f,1.28f,1.76f}:new[]{.32f,.74f,1.16f,1.58f};
        int columns=drinks?3:3;
        for(int tier=0;tier<heights.Length;tier++)for(int row=0;row<2;row++)for(int col=0;col<columns;col++)
            Put(group,pool[(tier+col)%pool.Length],new Vector3((col-1)*.21f,heights[tier],(drinks?-.30f:-.16f)+row*.14f),Quaternion.identity,.18f,.30f);
    }
    static Bounds BoundsOf(Transform root)=>BoundsOf(root,root);
    static Bounds BoundsOf(Transform target,Transform relative)
    {
        bool first=true;var result=new Bounds();
        foreach(var f in target.GetComponentsInChildren<MeshFilter>())
        {
            if(!f.sharedMesh||f.transform.GetComponentsInParent<Transform>().Any(t=>t.name.StartsWith(Prefix)))continue;var b=f.sharedMesh.bounds;
            for(int i=0;i<8;i++){var v=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var p=relative.InverseTransformPoint(f.transform.TransformPoint(v));if(first){result=new Bounds(p,Vector3.zero);first=false;}else result.Encapsulate(p);}
        }
        return result;
    }
    static string PathOf(Transform t,Transform root){string p=t.name;for(var parent=t.parent;parent&&parent!=root.parent;parent=parent.parent)p=parent.name+"/"+p;return p;}
}



