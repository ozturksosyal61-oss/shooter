using UnityEditor;
using UnityEngine;

/// <summary>Dev aid (1.1.0): renders every landmark / set-piece prefab from the game camera's angle → CAPTURE_DIR.</summary>
public static class LandmarkShots
{
    public static void Run()
    {
        string dir = System.Environment.GetEnvironmentVariable("CAPTURE_DIR") ?? "Temp";
        System.IO.Directory.CreateDirectory(dir);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects);
        var cam = Camera.main;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.45f, 0.42f, 0.38f);
        cam.fieldOfView = 40f;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.localScale = Vector3.one * 3f;
        var rt = new RenderTexture(800, 450, 24);
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Env" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith("LM_") && !name.StartsWith("SP_")) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            cam.transform.position = new Vector3(0f, 13f, -9f);
            cam.transform.LookAt(new Vector3(0f, 1.5f, 0f));
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(go);
            Debug.Log("SHOT " + name);
        }
        cam.targetTexture = null;
    }
}
