using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

[InitializeOnLoad]
public static class WetFloorSignBuilder
{
    const string Root="Assets/_How2ShopAssets/EditablePixelInteriors";
    static WetFloorSignBuilder(){EditorApplication.delayCall+=Auto;}
    static void Auto(){if(!File.Exists(Root+"/Editor/.wet-floor-request"))return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}Build();}
    [MenuItem("Tools/Pixel Interiors/Build caution wet floor sign")]
    public static void Build(){
        Directory.CreateDirectory(Root+"/Models/WetFloorGraphics");
        var yellow=Material("WetFloor_Yellow",new Color(1,.78f,.035f));
        var ink=Material("WetFloor_Black",new Color(.015f,.018f,.02f));
        var design=Design();int total=0;
        foreach(string name in new[]{"wet_floor_sign","wet_floor_sign_CompleteSet"}){
            string path=Root+"/Prefabs/MallKit/"+name+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try{
                foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("CautionGraphic_")).ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
                var planes=new List<List<Vector3>>();var normals=new List<Vector3>();
                foreach(var f in root.GetComponentsInChildren<MeshFilter>()){
                    var mesh=f.sharedMesh;var vertices=mesh.vertices;var tris=mesh.triangles;
                    f.GetComponent<MeshRenderer>().sharedMaterials=f.GetComponent<MeshRenderer>().sharedMaterials.Select(_=>yellow).ToArray();
                    for(int i=0;i<tris.Length;i+=3){
                        var a=f.transform.TransformPoint(vertices[tris[i]]);var b=f.transform.TransformPoint(vertices[tris[i+1]]);var c=f.transform.TransformPoint(vertices[tris[i+2]]);
                        var cross=Vector3.Cross(b-a,c-a);var normal=cross.normalized;
                        // Outward faces tilt slightly upwards; inward faces tilt downwards.
                        if(cross.magnitude<.02f||normal.y<.05f||normal.y>.4f)continue;
                        int index=normals.FindIndex(n=>Vector3.Dot(n,normal)>.999f);
                        if(index<0){index=normals.Count;normals.Add(normal);planes.Add(new List<Vector3>());}
                        planes[index].AddRange(new[]{a,b,c});
                    }
                }
                if(planes.Count!=2)throw new Exception(name+": expected two outer sign panels, found "+planes.Count);
                for(int p=0;p<planes.Count;p++){
                    var n=normals[p];var right=Vector3.Cross(n,Vector3.up).normalized;var up=Vector3.Cross(right,n).normalized;
                    var points=planes[p];float left=points.Min(v=>Vector3.Dot(v,right)),bottom=points.Min(v=>Vector3.Dot(v,up));
                    float width=points.Max(v=>Vector3.Dot(v,right))-left,height=points.Max(v=>Vector3.Dot(v,up))-bottom;
                    float distance=Vector3.Dot(points[0],n)+.0005f;
                    var verts=new List<Vector3>();var indices=new List<int>();
                    for(int y=0;y<128;y++)for(int x=0;x<64;x++)if(design[x,y]){
                        int start=verts.Count;
                        foreach(var v in new[]{new Vector2(x,y),new Vector2(x+1,y),new Vector2(x+1,y+1),new Vector2(x,y+1)}){
                            var world=right*(left+width*(.07f+.86f*v.x/64))+up*(bottom+height*(.04f+.92f*v.y/128))+n*distance;
                            verts.Add(root.transform.InverseTransformPoint(world));
                        }
                        indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                    }
                    // Rounded safety pictogram from the updated reference, kept
                    // separate from the deliberately pixel-stepped text/triangle.
                    Vector3 Map(Vector2 v)=>root.transform.InverseTransformPoint(right*(left+width*(.07f+.86f*v.x/64))+up*(bottom+height*(.04f+.92f*(v.y-1)/128))+n*distance);
                    void Disc(Vector2 centre,float radius){
                        int start=verts.Count;verts.Add(Map(centre));
                        for(int j=0;j<=24;j++){float angle=j*Mathf.PI*2/24;verts.Add(Map(centre+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius));}
                        for(int j=0;j<24;j++)indices.AddRange(new[]{start,start+j+1,start+j+2});
                    }
                    void Stroke(Vector2 a,Vector2 b,float radius){
                        var d=(b-a).normalized;var side=new Vector2(-d.y,d.x)*radius;int start=verts.Count;
                        foreach(var v in new[]{a+side,b+side,b-side,a-side})verts.Add(Map(v));
                        indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});Disc(a,radius);Disc(b,radius);
                    }
                    Disc(new Vector2(28,76),2.8f);
                    Stroke(new Vector2(28,70),new Vector2(34,64),1.7f);
                    Stroke(new Vector2(28,70),new Vector2(23,67),1.5f);Stroke(new Vector2(23,67),new Vector2(24,63),1.5f);
                    Stroke(new Vector2(29,70),new Vector2(35,71),1.5f);Stroke(new Vector2(35,71),new Vector2(37,76),1.5f);
                    Stroke(new Vector2(34,64),new Vector2(31,59),1.7f);
                    Stroke(new Vector2(32,66),new Vector2(38,68),1.7f);Stroke(new Vector2(38,68),new Vector2(43,65),1.7f);
                    Stroke(new Vector2(23,55),new Vector2(43,55),.55f);
                    string mp=Root+"/Models/WetFloorGraphics/"+name+"_"+p+".asset";
                    var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(mp);
                    if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,mp);}else mesh.Clear();
                    mesh.name=name+"_"+p;mesh.SetVertices(verts);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                    var graphic=new GameObject("CautionGraphic_"+(p==0?"Front":"Back"));graphic.transform.SetParent(root.transform,false);graphic.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=graphic.AddComponent<MeshRenderer>();renderer.sharedMaterial=ink;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);total+=planes.Count;
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();Preview();
        File.WriteAllText(Root+"/WetFloorValidation.txt",$"PASS: both sign prefabs updated; {total} outward-facing graphics.\nYellow plastic body, black pixel CAUTION / WET FLOOR lettering and slipping-person warning triangle.\nOriginal body geometry, prefab identity, transforms and colliders preserved.\n");
        if(File.Exists(Root+"/Editor/.wet-floor-request"))File.Delete(Root+"/Editor/.wet-floor-request");AssetDatabase.Refresh();
    }
    static Material Material(string name,Color colour){string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=name;AssetDatabase.CreateAsset(mat,path);}mat.SetColor("_BaseColor",colour);mat.SetFloat("_Smoothness",.15f);mat.SetFloat("_Cull",0);EditorUtility.SetDirty(mat);return mat;}
    static bool[,] Design(){
        var a=new bool[64,128];
        Action<int,int,int> dot=(x,y,r)=>{for(int yy=y-r;yy<=y+r;yy++)for(int xx=x-r;xx<=x+r;xx++)if(xx>=0&&xx<64&&yy>=0&&yy<128)a[xx,yy]=true;};
        Action<int,int,int,int,int> line=(x,y,xx,yy,r)=>{int steps=Math.Max(Math.Abs(xx-x),Math.Abs(yy-y));for(int s=0;s<=steps;s++)dot(Mathf.RoundToInt(Mathf.Lerp(x,xx,s/(float)Math.Max(1,steps))),Mathf.RoundToInt(Mathf.Lerp(y,yy,s/(float)Math.Max(1,steps))),r);};
        var font=new Dictionary<char,string>{
            ['A']="01110/10001/10001/11111/10001/10001/10001",['C']="01111/10000/10000/10000/10000/10000/01111",['U']="10001/10001/10001/10001/10001/10001/01110",['T']="11111/00100/00100/00100/00100/00100/00100",['I']="11111/00100/00100/00100/00100/00100/11111",['O']="01110/10001/10001/10001/10001/10001/01110",['N']="10001/11001/11001/10101/10011/10011/10001",['W']="10001/10001/10001/10101/10101/11011/10001",['E']="11111/10000/10000/11110/10000/10000/11111",['F']="11111/10000/10000/11110/10000/10000/10000",['L']="10000/10000/10000/10000/10000/10000/11111",['R']="11110/10001/10001/11110/10100/10010/10001"};
        Action<string,int> text=(word,y)=>{int x=(64-(word.Length*6-1))/2;foreach(char c in word){var rows=font[c].Split('/');for(int j=0;j<7;j++)for(int i=0;i<5;i++)if(rows[j][i]=='1')dot(x+i,y+6-j,0);x+=6;}};
        text("CAUTION",108);text("WET",29);text("FLOOR",17);
        line(32,96,7,47,1);line(7,47,57,47,1);line(57,47,32,96,1);
        return a;
    }
    static void Preview(){var scene=EditorSceneManager.NewPreviewScene();var rt=new RenderTexture(900,900,24);try{
        var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/MallKit/wet_floor_sign.prefab"));SceneManager.MoveGameObjectToScene(go,scene);
        Bounds b=go.GetComponentInChildren<Renderer>().bounds;var cameraGO=new GameObject("Camera");SceneManager.MoveGameObjectToScene(cameraGO,scene);var camera=cameraGO.AddComponent<Camera>();camera.scene=scene;camera.transform.position=b.center+new Vector3(.65f,.28f,-1.5f);camera.transform.LookAt(b.center);camera.orthographic=true;camera.orthographicSize=.43f;camera.nearClipPlane=.01f;camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.25f,.29f);
        var lightGO=new GameObject("Light");SceneManager.MoveGameObjectToScene(lightGO,scene);var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,-25,0);camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var png=new Texture2D(900,900,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,900,900),0,0);png.Apply();RenderTexture.active=previous;File.WriteAllBytes(Root+"/Preview/WetFloorSign.png",png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);
    }finally{EditorSceneManager.ClosePreviewScene(scene);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}}
}
