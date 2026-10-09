using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FreighterScalePreviewAuthoring
{
    const string ImagePath = "Assets/Images/Spacecraft/SketchBased/FreighterRefined.png";
    const float Height = 14f;
    [MenuItem("Tools/Spacecraft/Build Freighter Scale Preview")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath);
        importer.textureType = TextureImporterType.Default; importer.isReadable = true;
        importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
        var pixels = texture.GetPixels32();
        int left = texture.width, right = 0, bottom = texture.height, top = 0;
        for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
        {
            Color32 pixel = pixels[y * texture.width + x];
            if (Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) < 90) continue;
            left = Mathf.Min(left, x); right = Mathf.Max(right, x); bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
        }
        float ppu = (top - bottom + 1) / Height;
        var sprite = Sprite.Create(texture, new Rect(left, bottom, right-left+1, top-bottom+1), Vector2.one*.5f, ppu, 0, SpriteMeshType.FullRect);
        sprite.name = "FreighterRefined";
        string spritePath = "Assets/Images/Spacecraft/SketchBased/FreighterRefined.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (existing) { EditorUtility.CopySerialized(sprite, existing); Object.DestroyImmediate(sprite); sprite = existing; }
        else AssetDatabase.CreateAsset(sprite, spritePath);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var pad = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Spacecraft/AssistedFlight/DockingBay.prefab"));
        pad.name = "Original test pad - width 22";
        foreach (var component in pad.GetComponentsInChildren<MonoBehaviour>(true)) Disable(component);
        var ship = new GameObject("Freighter visual - height 14", typeof(SpriteRenderer));
        var renderer = ship.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Spacecraft/AssistedOutline.mat");
        renderer.sortingOrder = 5; ship.transform.position = new Vector3(0f, Height*.5f);
        var astronaut = ReferenceActor("Assets/Prefabs/Astronaut.prefab", "Original astronaut", 6f);
        var lander = ReferenceActor("Assets/Prefabs/Lander/Lander01.prefab", "Original Lander 01", -8f);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
        Label("Frachter: 14 Einheiten hoch", new Vector3(0f,15.5f), font);
        Label("Astronaut", new Vector3(6f,2.5f), font);
        Label("Alter Lander", new Vector3(-8f,3f), font);
        Label("Original-Testpad: 22 Einheiten breit", new Vector3(0f,-4.2f), font);
        var camera = new GameObject("Scale Preview Camera").AddComponent<Camera>(); camera.tag="MainCamera";
        camera.orthographic=true; camera.orthographicSize=22f; camera.transform.position=new Vector3(0f,6f,-10f);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/FreighterScalePreview.unity"); AssetDatabase.SaveAssets();
        Render(camera,"Documentation/AssistedSpacecraft/SketchBased/ScaleGameView.png");
        camera.orthographicSize=10.5f;
        Render(camera,"Documentation/AssistedSpacecraft/SketchBased/ScaleDetail.png");
        camera.orthographicSize=22f;
        var astronautBounds = VisualBounds(astronaut); var landerBounds=lander.GetComponent<PolygonCollider2D>().bounds;
        string info=$"Frachter: {Height:0.00} hoch, {sprite.bounds.size.x:0.00} breit. Astronaut: {astronautBounds.size.y:0.00} hoch. Original-Lander-Collider: {landerBounds.size.y:0.00} hoch (Sprite-Rechteck enthält transparente Ränder). Pad: 22 breit. Kameragröße Spielansicht: 22; Detail: 10.5. Nur statisch gerendert, keine Physiksimulation.\n";
        File.WriteAllText("Documentation/AssistedSpacecraft/SketchBased/ScaleMeasurements.txt",info);
        Debug.Log(info);
    }
    static void Disable(MonoBehaviour component)
    {
        component.enabled=false; PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }
    static GameObject ReferenceActor(string path,string name,float x)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); root.name=name;
        foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true)) Disable(component);
        foreach(var body in root.GetComponentsInChildren<Rigidbody2D>(true))
        { body.bodyType=RigidbodyType2D.Static; PrefabUtility.RecordPrefabInstancePropertyModifications(body); }
        foreach(Transform child in root.transform) if(child.name.Contains("ThrustEffect"))
        { child.gameObject.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject); }
        Physics2D.SyncTransforms();
        var collider=root.GetComponent<PolygonCollider2D>();
        var bounds=collider ? collider.bounds : VisualBounds(root);
        root.transform.position=new Vector3(x,root.transform.position.y-bounds.min.y);
        PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
        return root;
    }
    static Bounds VisualBounds(GameObject root)
    {
        var renderers=root.GetComponentsInChildren<SpriteRenderer>().Where(renderer=>renderer.enabled && renderer.sprite).ToArray();
        Bounds bounds=renderers[0].bounds; foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
    }
    static void Label(string content,Vector3 position,TMP_FontAsset font)
    {
        var text=new GameObject(content).AddComponent<TextMeshPro>(); text.font=font; text.text=content; text.fontSize=5f;
        text.alignment=TextAlignmentOptions.Center; text.rectTransform.sizeDelta=new Vector2(24f,2f); text.transform.position=position;
        text.GetComponent<MeshRenderer>().sortingOrder=10;
    }
    static void Render(Camera camera,string path)
    {
        var target=new RenderTexture(1920,1080,24); camera.targetTexture=target;
        RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
        RenderTexture.active=target; var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,image.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=null; Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
    }
}
