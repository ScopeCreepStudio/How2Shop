using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Unity.Collections;

[InitializeOnLoad]
public static class AssetsReady1Builder
{
    const string Source = "Assets/Packages/How2ShopAssets/ReadyAssets";
    const string Root = "Assets/Packages/How2ShopAssets/AssetsReady1";
    const string Request = "UserSettings/AssetsReady1Build.request";
    const float TilesPerMetre = 2;
    static AssetsReady1Builder() { EditorApplication.delayCall += Auto; }
    static void Auto()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Auto; return; }
        try { Build(); File.Delete(Request); }
        catch (Exception e) { File.WriteAllText("Logs/AssetsReady1Error.txt", e.ToString()); Debug.LogException(e); File.Delete(Request); }
    }

    struct Face
    {
        public Vector3 a, b, c, na, nb, nc;
        public Vector3 Center => (a + b + c) / 3;
        public Vector3 Normal => Vector3.Cross(b - a, c - a).normalized;
    }
    sealed class Part
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> indices = new List<int>();
        public void Add(Face face, Vector3 pivot, Bounds bounds)
        {
            Vector3 normal = face.Normal;
            foreach (var pair in new[] { (face.a, face.na), (face.b, face.nb), (face.c, face.nc) })
            {
                Vector3 p = pair.Item1 - pivot;
                indices.Add(vertices.Count); vertices.Add(p); normals.Add(pair.Item2);
                Vector3 n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
                Vector3 tiled = new Vector3(p.x * Mathf.Max(TilesPerMetre, 1 / Mathf.Max(bounds.size.x, .001f)), p.y * Mathf.Max(TilesPerMetre, 1 / Mathf.Max(bounds.size.y, .001f)), p.z * Mathf.Max(TilesPerMetre, 1 / Mathf.Max(bounds.size.z, .001f)));
                Vector2 coord = n.y >= n.x && n.y >= n.z ? new Vector2(tiled.x, tiled.z) : n.x >= n.z ? new Vector2(tiled.z, tiled.y) : new Vector2(tiled.x, tiled.y);
                uv.Add(coord);
            }
        }
        public Mesh Mesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); return mesh;
        }
    }

    static string[] SourceFiles() => Directory.GetFiles(Source + "/Prefabs", "*.prefab").Where(p => !Path.GetFileName(p).Contains("CompleteSet")).OrderBy(p => p).ToArray();

    [MenuItem("Tools/AssetsReady1/Rebuild simplified colourable prefabs")]
    public static void Build()
    {
        foreach (string dir in new[] { "Meshes", "Prefabs", "Scenes", "Preview" }) Directory.CreateDirectory(Root + "/" + dir);
        AssetDatabase.Refresh();
        var report = new List<string>(); var prefabs = new List<GameObject>();
        foreach (string file in SourceFiles())
        {
            string name = Path.GetFileNameWithoutExtension(file);
            var original = PrefabUtility.LoadPrefabContents(file.Replace('\\', '/'));
            try
            {
                original.transform.position = Vector3.zero; original.transform.rotation = Quaternion.identity;
                var faces = ReadFaces(original);
                if (faces.Count == 0) throw new Exception("Empty source model: " + name);
                var bounds = BoundsOf(faces);
                Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var parts = new Dictionary<string, Part>();
                foreach (var component in Components(faces))
                {
                    Bounds componentBounds = BoundsOf(component);
                    string region = Region(name, bounds, componentBounds);
                    foreach (var face in component)
                    {
                        if (Grocery(name))
                        {
                            foreach (var piece in GroceryBands(face, bounds))
                            {
                                if (!parts.ContainsKey(piece.Item2)) parts[piece.Item2] = new Part();
                                parts[piece.Item2].Add(piece.Item1, pivot, bounds);
                            }
                        }
                        else
                        {
                            string part = SurfaceRegion(name, bounds, face, region);
                            if (!parts.ContainsKey(part)) parts[part] = new Part();
                            parts[part].Add(face, pivot, bounds);
                        }
                    }
                }
                var item = new GameObject(name); SceneManager.MoveGameObjectToScene(item, original.scene);
                foreach (var entry in parts.OrderBy(p => p.Key))
                {
                    Mesh mesh = SaveMesh(entry.Value.Mesh(name + "_" + entry.Key), Root + "/Meshes/" + name + "_" + entry.Key + ".asset");
                    var part = new GameObject(entry.Key); part.transform.SetParent(item.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    part.AddComponent<MeshRenderer>().sharedMaterial = Palette(name, entry.Key);
                }
                var collision = new Part(); foreach (var face in faces) collision.Add(face, pivot, bounds);
                item.AddComponent<MeshCollider>().sharedMesh = SaveMesh(collision.Mesh(name + "_Collision"), Root + "/Meshes/" + name + "_Collision.asset");
                string path = Root + "/Prefabs/" + name + ".prefab";
                prefabs.Add(PrefabUtility.SaveAsPrefabAsset(item, path));
                report.Add($"{name}: {faces.Count} triangles; {parts.Count} colourable parts ({string.Join(", ", parts.Keys.OrderBy(p => p))}); dimensions {bounds.size.ToString("F3")}");
                UnityEngine.Object.DestroyImmediate(item);
            }
            finally { PrefabUtility.UnloadPrefabContents(original); }
        }
        BuildCollection(prefabs);
        AssetDatabase.SaveAssets();
        RemoveUnusedGeneratedMeshes(prefabs);
        Verify(prefabs, report); BuildShowcase(prefabs); RenderOverview(prefabs); RenderColourExample(prefabs);
        File.WriteAllText(Root + "/Readme.txt", "ASSETS READY 1\n\nPrefabs contains one clean prefab for every individual item in ReadyAssets. StoreProps_CompleteSet contains all of the new items laid out together. Scenes/Showcase is an optional preview scene.\n\nTO COLOUR AN ITEM: select a clearly named child such as Body, Frame, Shelves, Panels, Label, or Cap, then drag any material from Assets/ColoringMaterials onto it or its Mesh Renderer Material slot. Open the prefab to change every instance. Drag onto each part to colour the whole item. These are ordinary MeshRenderers with ordinary material assignments; there are no custom colour scripts or joined shaders.\n\nThe familiar source shapes are retained. Geometry transforms are baked into new standalone mesh assets; roots and parts use scale 1 and rotation 0, with the front facing local -Z. Pivots are at the bottom centre. New box-projected UVs tile twice per metre on large fixtures and at least once across small props, so your colour textures cover the surfaces instead of sampling a tiny atlas region. Original atlas UVs and vertex colours have been removed. Original mesh normals are retained.\n\nThe source ReadyAssets folder and your existing scenes are untouched. The new meshes and prefabs have no dependency on ReadyAssets; they reference only the output meshes, standard Unity components, and your existing ColoringMaterials palette. The optional Joined duplicates are replaced by these same normal prefabs.\n");
        AssetDatabase.Refresh();
    }

    static List<Face> ReadFaces(GameObject source)
    {
        var faces = new List<Face>();
        foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh; if (!mesh) continue;
            var matrix = Matrix4x4.Rotate(Quaternion.Euler(0, -90, 0)) * filter.transform.localToWorldMatrix;
            var normalMatrix = matrix.inverse.transpose;
            ReadMesh(mesh, out Vector3[] vertices, out Vector3[] normals, out int[] indices);
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (matrix.determinant < 0) { int swap = b; b = c; c = swap; }
                var face = new Face { a = matrix.MultiplyPoint3x4(vertices[a]), b = matrix.MultiplyPoint3x4(vertices[b]), c = matrix.MultiplyPoint3x4(vertices[c]) };
                if (Vector3.Cross(face.b - face.a, face.c - face.a).sqrMagnitude < 1e-14f) continue;
                face.na = normals.Length == vertices.Length ? normalMatrix.MultiplyVector(normals[a]).normalized : face.Normal;
                face.nb = normals.Length == vertices.Length ? normalMatrix.MultiplyVector(normals[b]).normalized : face.Normal;
                face.nc = normals.Length == vertices.Length ? normalMatrix.MultiplyVector(normals[c]).normalized : face.Normal;
                if (Vector3.Dot(face.Normal, (face.na + face.nb + face.nc).normalized) < -.1f)
                {
                    var point = face.b; face.b = face.c; face.c = point;
                    var normal = face.nb; face.nb = face.nc; face.nc = normal;
                }
                faces.Add(face);
            }
        }
        return faces;
    }
    static void ReadMesh(Mesh mesh, out Vector3[] vertices, out Vector3[] normals, out int[] indices)
    {
        // Editor read-only access avoids changing the source FBX Read/Write import setting.
        using (var source = MeshUtility.AcquireReadOnlyMeshData(mesh))
        using (var positions = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
        using (var normalData = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Temp))
        {
            var data = source[0]; data.GetVertices(positions); vertices = positions.ToArray();
            if (mesh.HasVertexAttribute(VertexAttribute.Normal)) { data.GetNormals(normalData); normals = normalData.ToArray(); }
            else normals = new Vector3[0];
            var allIndices = new List<int>();
            for (int sub = 0; sub < data.subMeshCount; sub++)
            {
                using (var triangles = new NativeArray<int>(data.GetSubMesh(sub).indexCount, Allocator.Temp))
                { data.GetIndices(triangles, sub, true); allIndices.AddRange(triangles.ToArray()); }
            }
            indices = allIndices.ToArray();
        }
    }
    static Bounds BoundsOf(IEnumerable<Face> faces)
    {
        var list = faces.ToList(); var bounds = new Bounds(list[0].a, Vector3.zero);
        foreach (var f in list) { bounds.Encapsulate(f.a); bounds.Encapsulate(f.b); bounds.Encapsulate(f.c); } return bounds;
    }
    static List<List<Face>> Components(List<Face> faces)
    {
        var parent = Enumerable.Range(0, faces.Count).ToArray();
        Func<int, int> find = null; find = i => parent[i] == i ? i : parent[i] = find(parent[i]);
        var seen = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < faces.Count; i++) foreach (var p in new[] { faces[i].a, faces[i].b, faces[i].c })
        {
            var key = new Vector3Int(Mathf.RoundToInt(p.x * 100000), Mathf.RoundToInt(p.y * 100000), Mathf.RoundToInt(p.z * 100000));
            if (seen.TryGetValue(key, out int other)) parent[find(i)] = find(other); else seen[key] = i;
        }
        var groups = new Dictionary<int, List<Face>>();
        for (int i = 0; i < faces.Count; i++) { int key = find(i); if (!groups.ContainsKey(key)) groups[key] = new List<Face>(); groups[key].Add(faces[i]); }
        return groups.Values.ToList();
    }
    static bool Shelving(string name) => name.StartsWith("Rack_") || name.StartsWith("Gondola_") || name.StartsWith("WallShelf_") || name.StartsWith("Shelf_");
    static bool Cabinet(string name) => name.StartsWith("Cooler_") || name.StartsWith("DisplayCabinet_");
    static bool Grocery(string name) => new[] { "Food_Tin", "Juice_Bottle", "Milk_Carton", "Soda_Bottle" }.Contains(name);
    static string Region(string name, Bounds all, Bounds part)
    {
        float height = Mathf.Max(all.size.y, .001f), span = Mathf.Max(all.size.x, all.size.z);
        if (name.StartsWith("DoorFrame_")) return "Frame";
        if (name.StartsWith("Carton_") || name == "Paper_Roll") return "Body";
        if (Shelving(name))
        {
            if (part.size.y < height * .13f && Mathf.Max(part.size.x, part.size.z) > span * .25f) return "Shelves";
            if (part.size.y > height * .5f) return "Frame";
        }
        if (name.StartsWith("Register_") && part.min.y > all.min.y + height * .5f) return "Controls";
        return "Body";
    }
    static string SurfaceRegion(string name, Bounds bounds, Face face, string region)
    {
        float y = (face.Center.y - bounds.min.y) / Mathf.Max(bounds.size.y, .001f);
        if (Grocery(name)) return y > .86f ? "Cap" : y > .22f && y < .74f ? "Label" : "Body";
        if (Cabinet(name))
        {
            bool depthZ = bounds.size.z <= bounds.size.x;
            float depth = depthZ ? face.Center.z - bounds.min.z : face.Center.x - bounds.min.x;
            float span = depthZ ? bounds.size.z : bounds.size.x;
            float forward = depthZ ? face.Normal.z : face.Normal.x;
            if (depth < Mathf.Max(.025f, span * .08f) && forward < -.7f && y > .15f && y < .92f) return "Panels";
        }
        return region;
    }
    struct Point { public Vector3 p, n; public Point(Vector3 position, Vector3 normal) { p = position; n = normal; } }
    static List<Point> Clip(List<Point> input, float y, bool above)
    {
        var result = new List<Point>(); if (input.Count == 0) return result;
        var previous = input[input.Count - 1]; bool wasInside = above ? previous.p.y >= y : previous.p.y <= y;
        foreach (var current in input)
        {
            bool inside = above ? current.p.y >= y : current.p.y <= y;
            if (inside != wasInside)
            {
                float t = (y - previous.p.y) / (current.p.y - previous.p.y);
                result.Add(new Point(Vector3.Lerp(previous.p, current.p, t), Vector3.Lerp(previous.n, current.n, t).normalized));
            }
            if (inside) result.Add(current); previous = current; wasInside = inside;
        }
        return result;
    }
    static IEnumerable<Tuple<Face, string>> GroceryBands(Face face, Bounds bounds)
    {
        float[] cuts = { 0, .22f, .74f, .86f, 1 };
        string[] names = { "Body", "Label", "Body", "Cap" };
        for (int band = 0; band < 4; band++)
        {
            var polygon = new List<Point> { new Point(face.a, face.na), new Point(face.b, face.nb), new Point(face.c, face.nc) };
            if (band > 0) polygon = Clip(polygon, bounds.min.y + cuts[band] * bounds.size.y, true);
            if (band < 3) polygon = Clip(polygon, bounds.min.y + cuts[band + 1] * bounds.size.y, false);
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                var a = polygon[0]; var b = polygon[i]; var c = polygon[i + 1];
                if (Vector3.Cross(b.p - a.p, c.p - a.p).sqrMagnitude < 1e-14f) continue;
                yield return Tuple.Create(new Face { a = a.p, b = b.p, c = c.p, na = a.n, nb = b.n, nc = c.n }, names[band]);
            }
        }
    }
    static Material Palette(string name, string part)
    {
        string color = part == "Frame" || part == "Cap" || part == "Controls" ? "Charcoal" : part == "Panels" ? "Sky" : part == "Label" ? "Cream" : name.StartsWith("Carton_") ? "Brown" : name.Contains("Juice") ? "Orange" : name.Contains("Soda") ? "Red" : "White";
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_" + color + ".mat");
        if (!material) throw new Exception("Missing ColoringMaterials palette entry: " + color); return material;
    }
    static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing)
        {
            // Explicit buffer assignment also refreshes already-loaded renderers in the editor.
            existing.Clear(false); existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices; existing.normals = mesh.normals; existing.uv = mesh.uv;
            existing.triangles = mesh.triangles; existing.tangents = mesh.tangents; existing.bounds = mesh.bounds;
            existing.UploadMeshData(false);
            UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
        }
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    static void BuildCollection(List<GameObject> prefabs)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("StoreProps_CompleteSet"); SceneManager.MoveGameObjectToScene(root, scene);
            for (int i = 0; i < prefabs.Count; i++)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene); item.transform.SetParent(root.transform, false); item.transform.localPosition = new Vector3(i % 5 * 4, 0, i / 5 * 4);
            }
            PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/StoreProps_CompleteSet.prefab");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void RemoveUnusedGeneratedMeshes(List<GameObject> prefabs)
    {
        var used = new HashSet<string>(prefabs.SelectMany(p => AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(p), true)));
        string[] generatedNames = { "Body", "Frame", "Shelves", "Panels", "Cap", "Label", "Controls", "Collision" };
        foreach (var file in Directory.GetFiles(Root + "/Meshes", "*.asset"))
        {
            string path = file.Replace('\\', '/'), name = Path.GetFileNameWithoutExtension(file);
            bool generated = prefabs.Any(p => generatedNames.Any(part => name == p.name + "_" + part));
            if (generated && !used.Contains(path)) AssetDatabase.DeleteAsset(path);
        }
    }
    static void Verify(List<GameObject> prefabs, List<string> report)
    {
        if (prefabs.Count != SourceFiles().Length) throw new Exception("An item prefab is missing");
        foreach (var item in prefabs)
        {
            if (item.transform.localScale != Vector3.one || item.transform.localRotation != Quaternion.identity) throw new Exception("Unbaked root transform: " + item.name);
            foreach (var filter in item.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (!AssetDatabase.GetAssetPath(mesh).StartsWith(Root + "/Meshes/") || mesh.uv.Length != mesh.vertexCount || mesh.colors.Length != 0) throw new Exception("Invalid standalone mesh or UVs: " + item.name);
                if (mesh.uv.Any(v => float.IsNaN(v.x) || float.IsNaN(v.y))) throw new Exception("Invalid UV values");
            }
            foreach (var renderer in item.GetComponentsInChildren<MeshRenderer>())
            {
                if (!AssetDatabase.GetAssetPath(renderer.sharedMaterial).StartsWith("Assets/ColoringMaterials/")) throw new Exception("Nonstandard material: " + item.name);
                // Recolour an instance using a different ordinary palette material, with no special shader/UV changes.
                var alternative = AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_Mint.mat");
                if (!alternative || alternative.shader != renderer.sharedMaterial.shader) throw new Exception("Palette compatibility failed: " + item.name);
            }
            if (AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(item), true).Any(p => p.StartsWith(Source + "/"))) throw new Exception("Original atlas/model dependency remains: " + item.name);
        }
        report.Add($"PASS: {prefabs.Count} individual prefabs plus a complete-set prefab; standalone baked meshes, new tiled UVs, no vertex tints or ReadyAssets dependencies, original palette materials, normal material replacement supported.");
        File.WriteAllLines(Root + "/Validation.txt", report);
    }
    static void Lights(Scene scene)
    {
        var a = new GameObject("Key light"); SceneManager.MoveGameObjectToScene(a, scene); a.transform.rotation = Quaternion.Euler(45, -30, 0); var key = a.AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 2;
        var b = new GameObject("Fill light"); SceneManager.MoveGameObjectToScene(b, scene); b.transform.rotation = Quaternion.Euler(25, 135, 0); var fill = b.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .65f;
    }
    static void BuildShowcase(List<GameObject> prefabs)
    {
        var active = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/StoreProps_CompleteSet.prefab"), scene);
            Lights(scene); var camera = new GameObject("Showcase camera"); SceneManager.MoveGameObjectToScene(camera, scene); var c = camera.AddComponent<Camera>(); camera.transform.position = new Vector3(19, 22, -15); camera.transform.LookAt(new Vector3(8, 0, 10)); c.orthographic = true; c.orthographicSize = 17;
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/Showcase.unity");
        }
        finally { EditorSceneManager.CloseScene(scene, true); if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active); }
    }
    static Bounds RenderBounds(GameObject item) { var rs = item.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
    static void Label(Scene scene, string name, Vector3 position, float width)
    {
        var go = new GameObject("Name"); SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = position; var text = go.AddComponent<TextMesh>(); text.text = name.Replace('_', ' '); text.fontSize = 32; text.characterSize = .02f; text.anchor = TextAnchor.UpperCenter; text.color = Color.white;
        var b = go.GetComponent<Renderer>().bounds; if (b.size.x > width) go.transform.localScale *= width / b.size.x;
    }
    static void RenderOverview(List<GameObject> prefabs)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                var item = UnityEngine.Object.Instantiate(prefabs[i]); SceneManager.MoveGameObjectToScene(item, scene); var b = RenderBounds(item);
                float scale = .8f / Mathf.Max(b.size.x, b.size.y, b.size.z); item.transform.localScale = Vector3.one * scale; item.transform.rotation = Quaternion.Euler(15, -25, 0);
                b = RenderBounds(item); item.transform.position += new Vector3(i % 5 * 1.2f, (5 - i / 5) * 1.15f, 0) - b.center;
                Label(scene, prefabs[i].name, new Vector3(i % 5 * 1.2f, (5 - i / 5) * 1.15f - .48f, -.6f), 1.06f);
            }
            Lights(scene); Capture(scene, Root + "/Preview/AllItems.png", new Vector3(2.4f, 2.82f, -12), new Vector3(2.4f, 2.82f, 0), 3.65f, 1500, 1600);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void RenderColourExample(List<GameObject> prefabs)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var source = prefabs.First(p => p.name == "Gondola_Wide");
            string[] colors = { "Mint", "Orange", "Blue" };
            for (int i = 0; i < 3; i++)
            {
                var item = UnityEngine.Object.Instantiate(source); SceneManager.MoveGameObjectToScene(item, scene);
                foreach (var r in item.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/ColoringMaterials/Pixel_" + colors[i] + ".mat");
                item.transform.position = new Vector3(i * 3.2f, 0, 0);
            }
            Lights(scene); Capture(scene, Root + "/Preview/OrdinaryMaterialColours.png", new Vector3(8, 5, -9), new Vector3(3.2f, 1, 0), 3.4f, 1500, 800);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void Capture(Scene scene, string path, Vector3 position, Vector3 lookAt, float ortho, int width, int height)
    {
        var go = new GameObject("Preview camera"); SceneManager.MoveGameObjectToScene(go, scene); var camera = go.AddComponent<Camera>(); camera.scene = scene; go.transform.position = position; go.transform.LookAt(lookAt); camera.orthographic = true; camera.orthographicSize = ortho; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .14f, .18f);
        var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
        try { camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target; var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        finally { camera.targetTexture = null; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
    }
}






