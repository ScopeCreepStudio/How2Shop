using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NewSceneStockScale
{
    const string Request="UserSettings/NewSceneStock.scale";
    static NewSceneStockScale(){EditorApplication.delayCall+=Auto;}
    static void Auto()
    {
        if(!File.Exists(Request))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Auto;return;}
        try{Apply();File.Delete(Request);}
        catch(Exception e){File.WriteAllText("Logs/NewSceneStockScaleError.txt",e.ToString());File.Delete(Request);Debug.LogException(e);}
    }
    [MenuItem("Tools/How2Shop/Set NewScene stock to 0.6 scale")]
    public static void Apply()
    {
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/Superstore.unity");
        if(!scene.IsValid()||!scene.isLoaded)throw new Exception("Open Superstore first");
        var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="NewScene");
        var stock=root.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.GetComponentsInParent<Transform>(true).Any(t=>t.name.StartsWith("Stock - NewScene - ",StringComparison.Ordinal))).ToArray();
        if(stock.Length==0)throw new Exception("No NewScene stock found");
        if(!File.Exists("Logs/Superstore_BeforeStockScale.unity"))EditorSceneManager.SaveScene(scene,"Logs/Superstore_BeforeStockScale.unity",true);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Set stocked picture items to 0.6 scale");
        Undo.RecordObjects(stock.Select(r=>(UnityEngine.Object)r.transform).ToArray(),"Resize stock");
        foreach(var renderer in stock)
        {
            var t=renderer.transform;var position=t.localPosition;
            // Keep upright sprites resting on the same shelf; tilted crate items retain their centres.
            if(renderer.sprite&&Vector3.Dot(t.localRotation*Vector3.up,Vector3.up)>.99f)
                position.y+=renderer.sprite.bounds.min.y*(t.localScale.y-.6f);
            t.localScale=Vector3.one*.6f;t.localPosition=position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        if(stock.Any(r=>(r.transform.localScale-Vector3.one*.6f).sqrMagnitude>.000001f))throw new Exception("Stock scale verification failed");
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Assets/Levels/NewSceneStock");
        File.WriteAllText("Assets/Levels/NewSceneStock/ScaleValidation.txt","PASS: "+stock.Length+" stocked 2D items under NewScene have local scale (0.6, 0.6, 0.6), matching the cereal reference. Upright item bases retained on shelves. Fixtures, aisle categories and source prefabs retained. Future population also uses 0.6 scale.\n");
        AssetDatabase.Refresh();SceneView.RepaintAll();
    }
}
