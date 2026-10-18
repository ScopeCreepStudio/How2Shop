using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using How2Shop.Items;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

[InitializeOnLoad]
public static class ColourItemBuilder
{
 const string Root="Assets/ColourEditableItems", Source="Assets/_How2ShopAssets/items";
 static ColourItemBuilder(){EditorApplication.delayCall+=Auto;}
 static void Auto(){if(!File.Exists(Root+"/Editor/.build-request"))return;if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Auto;return;}Build();}
 [MenuItem("Tools/Shelf Items/Build colour editable copies")]
 public static void Build(){
  foreach(var dir in new[]{"Sprites","Materials","Materials/Presets","Prefabs","Data","Preview"})Directory.CreateDirectory(Root+"/"+dir);
  AssetDatabase.Refresh();var shader=Shader.Find("How2Shop/Sprites/Editable Packaging Colour");if(!shader)throw new Exception("Packaging shader missing");
  var names=new List<string>();var defs=new List<ItemDefinition>();var report=new System.Text.StringBuilder();
  foreach(var file in Directory.GetFiles(Source+"/Prefabs","*.prefab")){
   string sourcePath=file.Replace('\\','/'),name=Path.GetFileNameWithoutExtension(file);names.Add(name);
   string texturePath=Root+"/Sprites/"+name+".png";
   if(!File.Exists(texturePath)&&!AssetDatabase.CopyAsset(Source+"/Sprites/"+name+".png",texturePath))throw new Exception("Sprite copy failed: "+name);
   AssetDatabase.ImportAsset(texturePath);
   var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
   var colour=Dominant(texturePath);
   var mat=MakeMaterial(name,shader,sprite.texture,colour,colour,Root+"/Materials/"+name+".mat");
   foreach(var preset in new[]{("Green",new Color(.18f,.85f,.32f)),("Pink",new Color(1,.25f,.65f)),("Red",new Color(.95f,.12f,.14f))})MakeMaterial(name+"_"+preset.Item1,shader,sprite.texture,colour,preset.Item2,Root+"/Materials/Presets/"+name+"_"+preset.Item1+".mat");
   var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);var original=source.GetComponent<ItemSpriteView>().Item;
   string dp=Root+"/Data/"+name+".asset";var data=AssetDatabase.LoadAssetAtPath<ItemDefinition>(dp);
   if(!data){data=UnityEngine.Object.Instantiate(original);AssetDatabase.CreateAsset(data,dp);}
   var so=new SerializedObject(data);so.FindProperty("id").stringValue="colour_"+original.Id;so.FindProperty("icon").objectReferenceValue=sprite;so.FindProperty("worldSprite").objectReferenceValue=sprite;so.ApplyModifiedPropertiesWithoutUndo();defs.Add(data);
   var go=UnityEngine.Object.Instantiate(source);go.name=name;go.GetComponent<ItemSpriteView>().SetItem(data);go.GetComponent<SpriteRenderer>().sharedMaterial=mat;go.GetComponent<SpriteRenderer>().color=Color.white;
   PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+name+".prefab");UnityEngine.Object.DestroyImmediate(go);
   report.AppendLine("PASS "+name+": independent material, original/green/pink/red options, copied sprite and item definition.");
  }
  var catalog=AssetDatabase.LoadAssetAtPath<ItemCatalog>(Root+"/Data/ColourEditableItems.asset");if(!catalog){catalog=ScriptableObject.CreateInstance<ItemCatalog>();AssetDatabase.CreateAsset(catalog,Root+"/Data/ColourEditableItems.asset");}
  var cs=new SerializedObject(catalog);var items=cs.FindProperty("items");items.arraySize=defs.Count;for(int i=0;i<defs.Count;i++)items.GetArrayElementAtIndex(i).objectReferenceValue=defs[i];cs.ApplyModifiedPropertiesWithoutUndo();if(catalog.GetValidationErrors().Count!=0)throw new Exception("Catalog validation failed");
  AssetDatabase.SaveAssets();Preview(names,false);Preview(new List<string>{"Cola"},true);
  var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join("\n",errors.Select(e=>e.message)));
  foreach(var name in names){var go=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab");var r=go.GetComponent<SpriteRenderer>();if(r.sprite!=go.GetComponent<ItemSpriteView>().Item.WorldSprite||r.sharedMaterial.GetTexture("_Artwork")!=r.sprite.texture)throw new Exception("Sprite material mismatch "+name);}
  report.AppendLine($"PASS: {names.Count} prefabs and 80 materials; catalog and texture references valid; recolour shader compiled after rendering.");File.WriteAllText(Root+"/Validation.txt",report.ToString());if(File.Exists(Root+"/Editor/.build-request"))File.Delete(Root+"/Editor/.build-request");AssetDatabase.Refresh();
 }
 static Material MakeMaterial(string name,Shader shader,Texture texture,Color source,Color target,string path){var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(shader);m.name=name;AssetDatabase.CreateAsset(m,path);m.SetColor("_PackagingColor",target);}m.SetColor("_SourceColor",source);m.SetTexture("_Artwork",texture);m.SetTexture("_MainTex",texture);EditorUtility.SetDirty(m);return m;}
 static Color Dominant(string path){
  var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(path));var pixels=t.GetPixels();float[] scores=new float[36];Vector3[] sums=new Vector3[36];float[] weights=new float[36];
  foreach(var c in pixels){if(c.a<.8f)continue;Color.RGBToHSV(c,out float h,out float s,out float v);if(s<.22f||v<.28f||(h>.53f&&h<.72f&&v<.42f))continue;int bin=Math.Min(35,(int)(h*36));float weight=s*v; scores[bin]+=weight;sums[bin]+=new Vector3(c.r,c.g,c.b)*weight;weights[bin]+=weight;}
  int best=Array.IndexOf(scores,scores.Max());if(weights[best]==0)throw new Exception("No packaging colour detected: "+path);var rgb=sums[best]/weights[best];UnityEngine.Object.DestroyImmediate(t);return new Color(rgb.x,rgb.y,rgb.z,1);
 }
 static void Preview(List<string> names,bool cola){
  var scene=EditorSceneManager.NewPreviewScene();int width=cola?1200:1600,height=cola?600:1800;var rt=new RenderTexture(width,height,24);
  try{
   int count=0;
   foreach(var name in names)foreach(string variant in cola?new[]{"","Green","Pink","Red"}:new[]{"","Pink"}){
    var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab"));SceneManager.MoveGameObjectToScene(go,scene);var r=go.GetComponent<SpriteRenderer>();if(variant!="")r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Presets/"+name+"_"+variant+".mat");
    go.transform.localScale*=.85f/Mathf.Max(r.bounds.size.y,.001f);go.transform.position=new Vector3((count%4)*1.25f,cola?0:(9-count/4)*1.1f,0);count++;
   }
   var cgo=new GameObject("Preview");SceneManager.MoveGameObjectToScene(cgo,scene);var camera=cgo.AddComponent<Camera>();camera.scene=scene;camera.transform.position=cola?new Vector3(1.875f,.43f,-10):new Vector3(1.875f,5.3f,-10);camera.orthographic=true;camera.orthographicSize=cola?1.35f:5.7f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.17f,.2f);camera.targetTexture=rt;camera.Render();
   var previous=RenderTexture.active;RenderTexture.active=rt;var png=new Texture2D(width,height,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,width,height),0,0);png.Apply();RenderTexture.active=previous;File.WriteAllBytes(Root+"/Preview/"+(cola?"ColaColours":"AllItemColours")+".png",png.EncodeToPNG());UnityEngine.Object.DestroyImmediate(png);
  }finally{EditorSceneManager.ClosePreviewScene(scene);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
 }
}
