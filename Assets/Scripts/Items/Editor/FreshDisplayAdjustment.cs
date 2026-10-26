using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class FreshDisplayAdjustment
{
    const string Request="UserSettings/FreshDisplayAdjustment.request";
    static FreshDisplayAdjustment(){EditorApplication.delayCall+=Auto;}
    static void Auto()
    {
        if(!File.Exists(Request))return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode){EditorApplication.delayCall+=Auto;return;}
        try{Apply();File.Delete(Request);if(File.Exists("Logs/FreshDisplayAdjustmentError.txt"))File.Delete("Logs/FreshDisplayAdjustmentError.txt");}
        catch(Exception e){File.WriteAllText("Logs/FreshDisplayAdjustmentError.txt",e.ToString());File.Delete(Request);Debug.LogException(e);}
    }
    static int Stand(Transform root,bool undo)
    {
        int count=0;
        foreach(var stock in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Vegetables - individual 2D items"))
        foreach(var sprite in stock.GetComponentsInChildren<SpriteRenderer>(true))
        {
            var t=sprite.transform;if(undo)Undo.RecordObject(t,"Stand vegetables upright");
            t.localRotation=Quaternion.Euler(15,0,0);var p=t.localPosition;
            p.y=.04f-sprite.sprite.bounds.min.y*t.localScale.y;t.localPosition=p;
            if(undo)PrefabUtility.RecordPrefabInstancePropertyModifications(t);count++;
        }
        return count;
    }
    static float VerticalSize(SpriteRenderer s)
    {
        var t=s.transform;var size=Vector3.Scale(s.sprite.bounds.size,t.localScale);
        return Mathf.Abs((t.localRotation*Vector3.right).y)*size.x+Mathf.Abs((t.localRotation*Vector3.up).y)*size.y;
    }
    [MenuItem("Tools/How2Shop/Stand vegetables and separate fruit layers")]
    public static void Apply()
    {
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/Superstore.unity");
        if(!scene.IsValid()||!scene.isLoaded)throw new Exception("Open Superstore first");
        var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="NewScene");
        if(!File.Exists("Logs/Superstore_BeforeFreshDisplayAdjustment.unity"))EditorSceneManager.SaveScene(scene,"Logs/Superstore_BeforeFreshDisplayAdjustment.unity",true);
        Undo.IncrementCurrentGroup();int undo=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Stand vegetables and separate fruit layers");
        int vegetables=Stand(root,true),lowered=0;
        var fruitGroups=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Stock - NewScene - Fruit - ",StringComparison.Ordinal)).ToArray();
        foreach(var group in fruitGroups)
        foreach(var column in group.GetComponentsInChildren<SpriteRenderer>(true).GroupBy(s=>new Vector2Int(Mathf.RoundToInt(s.transform.localPosition.x*1000),Mathf.RoundToInt(s.transform.localPosition.z*1000))))
        {
            var items=column.OrderBy(s=>s.transform.localPosition.y).ToArray();if(items.Length!=2)continue;
            var bottom=items[0].transform;var top=items[1].transform;
            float separation=Mathf.Max(.215f,(VerticalSize(items[0])+VerticalSize(items[1]))*.5f+.035f);
            Undo.RecordObject(bottom,"Lower bottom fruit");var p=bottom.localPosition;p.y=Mathf.Min(p.y,top.localPosition.y-separation);bottom.localPosition=p;PrefabUtility.RecordPrefabInstancePropertyModifications(bottom);lowered++;
            if(top.localPosition.y-bottom.localPosition.y<separation-.001f)throw new Exception("Fruit separation failed");
        }
        if(vegetables==0||lowered==0)throw new Exception("No fresh display items found");
        // Also keep the reusable fridge prefabs upright for future placements.
        foreach(string name in new[]{"VegetableFridgeShelf_Stocked","VegetableFridgeShelf_Double"})
        {
            string path="Assets/Shelf_Items/VegetableFridgeShelf/"+name+".prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
            try{Stand(prefab.transform,false);PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }
        Undo.CollapseUndoOperations(undo);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Assets/Levels/NewSceneStock/FreshDisplayValidation.txt","PASS: "+vegetables+" vegetable fridge sprites standing vertically; "+lowered+" bottom fruit sprites lowered in "+fruitGroups.Length+" crates with visible separation. Upper fruit positions and item scales retained. Reusable fridge prefabs and future population updated.\n");AssetDatabase.Refresh();SceneView.RepaintAll();
    }
}

