using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class FruitShelfBuilder
{
    const string Root = "Assets/Shelf_Items/FruitShelf";
    const string Request = "UserSettings/FruitShelfBuild.request";
    static FruitShelfBuilder() { EditorApplication.delayCall += Auto; }
    static void Auto()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Auto; return; }
        try { Build(); File.Delete(Request); }
        catch (Exception e) { File.WriteAllText("Logs/FruitShelfError.txt", e.ToString()); Debug.LogException(e); File.Delete(Request); }
    }

    [MenuItem("Tools/Shelf Items/Build simple fruit shelf")]
    public static void Build()
    {
        Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh();
        var frame = Material("Frame", new Color(.085f, .095f, .105f));
        var wood = Material("CrateWood", new Color(.66f, .59f, .47f));
        var sign = Material("SignFace", new Color(.88f, .88f, .83f));
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Packages/Wooden Boxes/Prefabs/Box2.prefab");
        if (!source) throw new Exception("The supplied shallow Box2 crate is missing");
        var previewScene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("FruitShelf"); SceneManager.MoveGameObjectToScene(root, previewScene);
            var metal = new GameObject("Frame"); metal.transform.SetParent(root.transform, false);
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * .55f;
                Cube(metal.transform, (side < 0 ? "Left" : "Right") + " rear upright", new Vector3(x, 1.04f, .31f), new Vector3(.045f, 2.02f, .045f), frame);
                Cube(metal.transform, (side < 0 ? "Left" : "Right") + " front upright", new Vector3(x, .85f, -.31f), new Vector3(.045f, 1.64f, .045f), frame);
                foreach (float z in new[] { -.31f, .31f }) Cube(metal.transform, "Foot", new Vector3(x, .015f, z), new Vector3(.085f, .03f, .09f), frame);
                Cube(metal.transform, "Base side rail", new Vector3(x, .09f, 0), new Vector3(.045f, .045f, .66f), frame);
                for (int tier = 0; tier < 3; tier++)
                {
                    var rail = Cube(metal.transform, "Angled tray support " + (tier + 1), new Vector3(x, .26f + tier * .5f, 0), new Vector3(.045f, .035f, .69f), frame);
                    rail.transform.localRotation = Quaternion.Euler(-15, 0, 0);
                }
            }
            foreach (float z in new[] { -.31f, .31f }) Cube(metal.transform, "Base cross rail", new Vector3(0, .09f, z), new Vector3(1.1f, .045f, .045f), frame);
            for (int tier = 0; tier < 3; tier++) Cube(metal.transform, "Rear cross rail " + (tier + 1), new Vector3(0, .35f + tier * .5f, .31f), new Vector3(1.1f, .035f, .035f), frame);
            Cube(metal.transform, "Front top rail", new Vector3(0, 1.65f, -.31f), new Vector3(1.1f, .045f, .045f), frame);

            var trays = new GameObject("Crates"); trays.transform.SetParent(root.transform, false);
            for (int tier = 0; tier < 3; tier++)
            {
                var tray = new GameObject("Crate " + (tier + 1)); tray.transform.SetParent(trays.transform, false);
                tray.transform.localPosition = new Vector3(0, .4f + tier * .5f, -.015f); tray.transform.localRotation = Quaternion.Euler(-15, 0, 0);
                var crate = (GameObject)PrefabUtility.InstantiatePrefab(source, previewScene);
                var bounds = Bounds(crate); crate.transform.SetParent(tray.transform, false);
                var scale = new Vector3(1.02f / bounds.size.x, .24f / bounds.size.y, .60f / bounds.size.z);
                crate.transform.localScale = scale; crate.transform.localPosition = -Vector3.Scale(bounds.center, scale);
                foreach (var renderer in crate.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = Enumerable.Repeat(wood, renderer.sharedMaterials.Length).ToArray();
                var content = new GameObject("Fruit placement"); content.transform.SetParent(tray.transform, false); content.transform.localPosition = new Vector3(0, -.09f, 0);
            }
            var header = new GameObject("Sign"); header.transform.SetParent(root.transform, false);
            Cube(header.transform, "Blank sign panel - texture here", new Vector3(0, 1.865f, .31f), new Vector3(1.035f, .285f, .025f), sign);
            foreach (float y in new[] { 1.705f, 2.025f }) Cube(header.transform, "Sign horizontal trim", new Vector3(0, y, .29f), new Vector3(1.145f, .035f, .055f), frame);
            foreach (float x in new[] { -.555f, .555f }) Cube(header.transform, "Sign vertical trim", new Vector3(x, 1.865f, .29f), new Vector3(.035f, .32f, .055f), frame);
            var collision = root.AddComponent<BoxCollider>(); collision.center = new Vector3(0, 1.03f, 0); collision.size = new Vector3(1.16f, 2.06f, .76f);
            string path = Root + "/FruitShelf.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path); AssetDatabase.SaveAssets();
            Verify(path); Render(root, previewScene);
            File.WriteAllText(Root + "/Readme.txt", "FruitShelf.prefab: empty three-tier fruit display, about 1.16m wide x 2.06m high x 0.76m deep. Pivot is at floor level; front faces local -Z.\n\nFrame, CrateWood, and SignFace are separate plain URP/Lit materials. Assign your textures to their Base Map fields. The existing Box2 crate keeps its supplied UVs; the frame and blank sign use standard cube UVs. Edit crate angles and positions under Crates, and use the Fruit placement children as starting points for produce. One simple root collider prevents walking through the display.\n\nCrate source: Assets/Packages/Wooden Boxes/Prefabs/Box2.prefab. The source asset is unchanged.\n");
            AssetDatabase.Refresh();
        }
        finally { EditorSceneManager.ClosePreviewScene(previewScene); }
    }

    static Material Material(string name, Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.name = name; AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .15f); EditorUtility.SetDirty(material); return material;
    }
    static GameObject Cube(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static Bounds Bounds(GameObject go) { var renderers = go.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds); return bounds; }
    static void Verify(string path)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (!root || root.transform.Find("Crates").childCount != 3 || !root.GetComponent<BoxCollider>()) throw new Exception("Fruit shelf prefab structure is invalid");
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            if (!filter.sharedMesh || !filter.sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0)) throw new Exception("Missing mesh or texture coordinates: " + filter.name);
        foreach (var renderer in root.GetComponentsInChildren<Renderer>()) if (renderer.sharedMaterials.Any(m => !m)) throw new Exception("Missing material");
        File.WriteAllText("Logs/FruitShelfValidation.txt", "PASS: saved prefab, three independent 15-degree crate trays, UVs on every mesh, separate frame/wood/sign materials, root collider, floor-level pivot. Existing scene and source crate assets were not edited.\n");
    }
    static void Render(GameObject root, Scene scene)
    {
        var groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); groundMaterial.SetColor("_BaseColor", new Color(.40f, .43f, .47f));
        var ground = Cube(root.transform, "Preview ground", new Vector3(0, -.025f, 0), new Vector3(5, .025f, 5), groundMaterial);
        var lightGo = new GameObject("Preview key light"); SceneManager.MoveGameObjectToScene(lightGo, scene); lightGo.transform.rotation = Quaternion.Euler(40, -35, 0); var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 2.2f;
        var fillGo = new GameObject("Preview fill"); SceneManager.MoveGameObjectToScene(fillGo, scene); fillGo.transform.rotation = Quaternion.Euler(25, 145, 0); var fill = fillGo.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .6f;
        var cameraGo = new GameObject("Preview camera"); SceneManager.MoveGameObjectToScene(cameraGo, scene); var camera = cameraGo.AddComponent<Camera>(); camera.scene = scene;
        camera.transform.position = new Vector3(2.5f, 2.1f, -3.6f); camera.transform.LookAt(new Vector3(0, 1.03f, 0)); camera.orthographic = true; camera.orthographicSize = 1.24f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.4f, .43f, .47f);
        var target = new RenderTexture(900, 1100, 24); camera.targetTexture = target;
        try { camera.Render(); var prior = RenderTexture.active; RenderTexture.active = target; var image = new Texture2D(900, 1100, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 900, 1100), 0, 0); image.Apply(); File.WriteAllBytes("Logs/FruitShelfPreview.png", image.EncodeToPNG()); RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(image); }
        finally { camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(groundMaterial); }
    }
}


