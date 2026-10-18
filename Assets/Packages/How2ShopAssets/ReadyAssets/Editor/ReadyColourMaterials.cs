using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

[InitializeOnLoad]
public static class ReadyColourMaterials
{
    const string Root="Assets/_How2ShopAssets/ReadyAssets";
    const string Flag=Root+"/Materials/.build-request";
    static ReadyColourMaterials() { EditorApplication.delayCall+=Auto; }
    static void Auto()
    {
        if(!File.Exists(Flag)||EditorApplication.isPlayingOrWillChangePlaymode)return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}
        Build();
    }
    static bool Modular(string name) => new[]{"Rack_","Gondola_","WallShelf_","Shelf_","Cooler_","DisplayCabinet_","DoorFrame_"}.Any(name.StartsWith);
    [MenuItem("Tools/ReadyAssets/Create independent colour materials")]
    public static void Build()
    {
        foreach(var folder in new[]{"Materials/Individual","Materials/Joinable","Textures","Prefabs/Joinable","Preview"}) Directory.CreateDirectory(Root+"/"+folder);
        MakePattern(); AssetDatabase.Refresh();
        var shader=Shader.Find("How2Shop/ReadyAssets/Joined Pixel Art");
        if(!shader)throw new Exception("Joined shader missing.");
        var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
        if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(m=>m.message)));
        var pattern=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/ReadyPixel_JoinPattern.png");
        var byMesh=new Dictionary<string,Material>(); int count=0,joined=0;
        foreach(var file in Directory.GetFiles(Root+"/Prefabs","*.prefab").Where(p=>!p.Contains("CompleteSet")))
        {
            string path=file.Replace('\\','/'),name=Path.GetFileNameWithoutExtension(path);
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var renderers=root.GetComponentsInChildren<MeshRenderer>(true);
                if(renderers.Length==0)throw new Exception("No renderer in "+name);
                string materialPath=Root+"/Materials/Individual/"+name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!mat){mat=new Material(renderers[0].sharedMaterial); mat.parent=null; mat.name=name;AssetDatabase.CreateAsset(mat,materialPath);}
                // Leave the original atlas, UVs, shader and colour settings intact.
                foreach(var r in renderers){r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();byMesh[r.GetComponent<MeshFilter>().sharedMesh.name]=mat;}
                PrefabUtility.SaveAsPrefabAsset(root,path);count++;
                if(Modular(name)){
                    string jp=Root+"/Materials/Joinable/"+name+"_Joined.mat";
                    var jm=AssetDatabase.LoadAssetAtPath<Material>(jp);
                    if(!jm){jm=new Material(shader);jm.name=name+"_Joined";jm.SetTexture("_BaseMap",pattern);jm.SetColor("_BaseColor",new Color(1,.94f,.71f));jm.SetColor("_SideColor",new Color(.22f,.17f,.1f));jm.SetFloat("_PatternSize",1);AssetDatabase.CreateAsset(jm,jp);}
                    foreach(var r in renderers)r.sharedMaterials=r.sharedMaterials.Select(_=>jm).ToArray();
                    PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Joinable/"+name+"_Joined.prefab");joined++;
                }
            } finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        string complete=Root+"/Prefabs/StoreProps_CompleteSet.prefab";
        var set=PrefabUtility.LoadPrefabContents(complete);
        try{
            foreach(var r in set.GetComponentsInChildren<MeshRenderer>(true)){
                var f=r.GetComponent<MeshFilter>();
                if(f && byMesh.TryGetValue(f.sharedMesh.name,out var mat))r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();
            }
            PrefabUtility.SaveAsPrefabAsset(set,complete);
        }finally{PrefabUtility.UnloadPrefabContents(set);}
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        RenderJoinPreview();
        errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
        if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(m=>m.message)));
        foreach(var p in Directory.GetFiles(Root+"/Prefabs","*.prefab",SearchOption.AllDirectories)){
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\','/'));
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true))if(r.sharedMaterials.Any(m=>!m||!m.shader||!m.GetTexture("_BaseMap")))throw new Exception("Missing material/texture in "+p);
        }
        File.WriteAllText(Root+"/Materials/Validation.txt",$"PASS: {count} independent original-art materials assigned to individual prefabs.\nPASS: complete-set materials updated by mesh identity.\nPASS: {joined} optional joinable materials and prefabs.\nPASS: no missing material, shader or base texture references.\nPASS: joined shader compiled with no errors after preview rendering.\nJoin pattern sampled from existing ReadyAssets pixel artwork; mirrored tile edges verified.\nOriginal geometry and UVs unchanged.\n");
        if(File.Exists(Flag))File.Delete(Flag);
        AssetDatabase.Refresh();
    }
    static void MakePattern()
    {
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        source.LoadImage(File.ReadAllBytes(Root+"/Textures/StoreProps_PixelArt_Copy.png"));
        Color[] pixels=source.GetPixels(); int w=source.width; float best=-1; int bx=0,by=0;
        // Find an existing cream/shadow pixel-art tile; reject labels, checker
        // borders, saturated colour bands and almost-solid patches.
        for(int y=0;y<source.height;y+=32)for(int x=0;x<w;x+=32){
            float min=1,max=0,sum=0;int invalid=0;
            for(int j=0;j<32;j++)for(int i=0;i<32;i++){
                var c=pixels[(y+j)*w+x+i];float l=c.grayscale;min=Mathf.Min(min,l);max=Mathf.Max(max,l);sum+=l;
                if(c.r<c.g-.03f||c.g<c.b-.03f||c.r-c.g>.25f||l<.4f)invalid++;
            }
            float mean=sum/1024;
            if(invalid>0||max-min<.12f||max-min>.5f||mean<.65f)continue;
            int horizontalChanges=0,verticalChanges=0;
            for(int j=0;j<31;j++)for(int i=0;i<31;i++){
                float l=pixels[(y+j)*w+x+i].grayscale;
                if(Mathf.Abs(l-pixels[(y+j)*w+x+i+1].grayscale)>.03f)horizontalChanges++;
                if(Mathf.Abs(l-pixels[(y+j+1)*w+x+i].grayscale)>.03f)verticalChanges++;
            }
            if(horizontalChanges<20||verticalChanges<20||horizontalChanges+verticalChanges>320)continue;
            var rowCounts=new HashSet<int>();var columnCounts=new HashSet<int>();int dark=0;
            for(int j=0;j<32;j++){
                int row=0,column=0;
                for(int i=0;i<32;i++){
                    if(pixels[(y+j)*w+x+i].grayscale<max-.07f)row++;
                    if(pixels[(y+i)*w+x+j].grayscale<max-.07f)column++;
                }
                rowCounts.Add(row);columnCounts.Add(column);dark+=row;
            }
            // Prefer scattered, irregular chips over blocks, labels or grids.
            if(rowCounts.Count<6||columnCounts.Count<6||dark>460||dark<100)continue;
            float score=0;
            for(int j=0;j<32;j++)for(int i=0;i<32;i++)score+=Mathf.Abs(pixels[(y+j)*w+x+i].grayscale-mean);
            score/=1024;
            if(score>best){best=score;bx=x;by=y;}
        }
        if(best<0)throw new Exception("No suitable original pixel-art tile found.");
        float peak=0;
        for(int j=0;j<32;j++)for(int i=0;i<32;i++)peak=Mathf.Max(peak,pixels[(by+j)*w+bx+i].grayscale);
        var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);var output=new Color[4096];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++){
            int sx=x<32?x:63-x,sy=y<32?y:63-y;
            float v=pixels[(by+sy)*w+bx+sx].grayscale/peak;output[y*64+x]=new Color(v,v,v,1);
        }
        for(int i=0;i<64;i++)if(output[i*64]!=output[i*64+63]||output[i]!=output[63*64+i])throw new Exception("Tile edge mismatch.");
        tex.SetPixels(output);tex.Apply();string path=Root+"/Textures/ReadyPixel_JoinPattern.png";
        File.WriteAllBytes(path,tex.EncodeToPNG());AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.sRGBTexture=false;importer.SaveAndReimport();
        File.WriteAllText(Root+"/Materials/PatternSource.txt",$"Existing artwork: StoreProps_PixelArt_Copy.png\n32x32 tile at bottom-left pixel ({bx},{by}); mirrored to a seamless 64x64 tile, luminance normalized for recolouring.\n");
        UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(tex);
    }
    static void RenderJoinPreview()
    {
        var scene=EditorSceneManager.NewPreviewScene();var rt=new RenderTexture(1400,800,24);
        try{
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Joinable/Rack_TallWide_Joined.prefab");
            Bounds bounds=new Bounds();
            for(int k=0;k<2;k++){
                var go=UnityEngine.Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(go,scene);
                var r=go.GetComponentInChildren<Renderer>();var size=r.bounds.size;
                go.transform.position=size.x>=size.z?new Vector3(k*size.x,0,0):new Vector3(0,0,k*size.z);
                if(k==0)bounds=r.bounds;else bounds.Encapsulate(r.bounds);
            }
            var cameraGO=new GameObject("Preview camera");SceneManager.MoveGameObjectToScene(cameraGO,scene);
            var camera=cameraGO.AddComponent<Camera>();camera.scene=scene;camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.21f);
            float span=Mathf.Max(bounds.size.x,bounds.size.z);
            camera.transform.position=bounds.center+(bounds.size.x>=bounds.size.z?new Vector3(span*.25f,bounds.size.y*.5f,-span*1.5f):new Vector3(span*1.5f,bounds.size.y*.5f,span*.25f));
            camera.transform.LookAt(bounds.center);camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.size.y*.75f,span*.36f);camera.nearClipPlane=.01f;camera.farClipPlane=100;
            var lightGO=new GameObject("Preview light");SceneManager.MoveGameObjectToScene(lightGO,scene);var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(55,-60,0);
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
            var png=new Texture2D(1400,800,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1400,800),0,0);png.Apply();RenderTexture.active=previous;
            File.WriteAllBytes(Root+"/Preview/JoinedColourMaterials.png",png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);
        }finally{EditorSceneManager.ClosePreviewScene(scene);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
    }
}
