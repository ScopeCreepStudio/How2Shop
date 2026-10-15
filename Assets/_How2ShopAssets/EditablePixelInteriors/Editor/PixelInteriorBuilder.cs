using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text;

[InitializeOnLoad]
public static class PixelInteriorBuilder
{
    const string Root = "Assets/_How2ShopAssets/EditablePixelInteriors";
    static PixelInteriorBuilder() { EditorApplication.delayCall += AutoBuild; }
    static void AutoBuild()
    {
        if (!File.Exists(Root+"/build.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += AutoBuild; return; }
        Build();
    }
    [MenuItem("Tools/Pixel Interiors/Build prefabs and materials")]
    public static void Build()
    {
        Directory.CreateDirectory(Root+"/Materials");
        var template=AssetDatabase.LoadAssetAtPath<Material>("Assets/_How2ShopAssets/ReadyAssets/RetroPixel_PixelArt_Copy.mat");
        if (!template) throw new System.Exception("ReadyAssets reference material not found.");
        var mats=new System.Collections.Generic.Dictionary<string,Material>();
        foreach(var path in Directory.GetFiles(Root+"/Textures", "*.png")) {
            var p=path.Replace('\\','/');
            var imp=(TextureImporter)AssetImporter.GetAtPath(p);
            imp.filterMode=FilterMode.Point; imp.mipmapEnabled=false; imp.textureCompression=TextureImporterCompression.Uncompressed; imp.wrapMode=TextureWrapMode.Repeat; imp.npotScale=TextureImporterNPOTScale.None; imp.SaveAndReimport();
            string name=Path.GetFileNameWithoutExtension(p), mp=Root+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(mp);
            if(!m) { m=new Material(template); m.parent=null; AssetDatabase.CreateAsset(m,mp); }
            m.name=name; m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(p));
            m.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(p));
            m.SetColor("_BaseColor",Color.white); m.SetColor("_Color",Color.white);
            m.SetFloat("_Smoothness",.15f); EditorUtility.SetDirty(m); mats[name]=m;
        }
        var report=new StringBuilder("Editable Pixel Interiors validation\n"); int count=0;
        foreach(var file in Directory.GetFiles(Root+"/Models","*.fbx",SearchOption.AllDirectories)) {
            string path=file.Replace('\\','/'), pack=Path.GetFileName(Path.GetDirectoryName(path)), name=Path.GetFileNameWithoutExtension(path);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            foreach(var pair in mats) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
            importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!model) throw new System.Exception("Missing model "+path);
            var go=Object.Instantiate(model); go.name=name;
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true)) {
                r.sharedMaterials=r.sharedMaterials.Select(m => {
                    if(!m || !mats.ContainsKey(m.name)) throw new System.Exception("Unknown material in "+path+": "+m?.name);
                    return mats[m.name];
                }).ToArray();
                var filter=r.GetComponent<MeshFilter>();
                if(!filter || !filter.sharedMesh || filter.sharedMesh.vertexCount==0) throw new System.Exception("Empty mesh "+path);
                r.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            }
            string folder=Root+"/Prefabs/"+pack; Directory.CreateDirectory(folder);
            PrefabUtility.SaveAsPrefabAsset(go,folder+"/"+name+".prefab"); Object.DestroyImmediate(go);
            count++; report.AppendLine("PASS "+pack+"/"+name);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        var dependencies=Directory.GetFiles(Root+"/Prefabs","*.prefab",SearchOption.AllDirectories).SelectMany(p=>AssetDatabase.GetDependencies(p.Replace('\\','/'))).Distinct().Where(p=>p.StartsWith("Assets/")&&!p.StartsWith(Root+"/")).ToArray();
        if(dependencies.Length>0) throw new System.Exception("External asset dependencies: "+string.Join(", ",dependencies));
        report.AppendLine($"PASS: {count} prefabs, {mats.Count} materials; no external Assets dependencies. Source meshes preserved. Static mesh colliders assigned.");
        File.WriteAllText(Root+"/Validation.txt",report.ToString());
        if(File.Exists(Root+"/build.request")) AssetDatabase.DeleteAsset(Root+"/build.request");
        AssetDatabase.Refresh();
    }
    public static void Inspect()
    {
        var report = new StringBuilder();
        var paths = new[]{"Assets/_How2ShopAssets/SimplePolygon_MiniInteriorWalls.fbx"}.Concat(Directory.GetFiles("Assets/_How2ShopAssets/psx-mall-kit-free/fbx", "*.fbx"));
        foreach(var p in paths) {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            report.AppendLine(p);
            foreach(var f in go.GetComponentsInChildren<MeshFilter>(true)) {
                report.AppendLine($"  {f.name}: verts={f.sharedMesh.vertexCount} bounds={f.sharedMesh.bounds} scale={f.transform.lossyScale}");
                foreach(var m in f.GetComponent<Renderer>().sharedMaterials) report.AppendLine($"    material={m?.name} texture={m?.mainTexture?.name} color={(m && m.HasProperty("_Color") ? m.color.ToString() : "none")}");
            }
        }
        File.WriteAllText(Root+"/inspection.txt", report.ToString());
    }
}
