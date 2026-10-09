using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;

public static class StationStairAuthoring
{
    const string Image = "Assets/Images/World/Stairs/StationStairs.png";
    const string Prefabs = "Assets/Prefabs/World/Stairs";
    const float PixelsPerUnit = 80f;
    [Serializable] class Layout { public Stair[] stairs; }
    [Serializable] class Stair
    {
        public string name;
        public int x, y, width, height;
        public float startX, startY, landingX, landingY, endX, endY;
    }
    [MenuItem("Tools/Art/Build Station Stairs")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Folder(Prefabs);
        var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(Path.ChangeExtension(Image, ".layout.json")));
        var importer = (TextureImporter)AssetImporter.GetAtPath(Image);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear; importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
        importer.SaveAndReimport(); importer.GetSourceTextureWidthAndHeight(out _, out int textureHeight);
        var factory = new SpriteDataProviderFactories(); factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var oldIds = provider.GetSpriteRects().ToDictionary(rect => rect.name, rect => rect.spriteID);
        var rects = layout.stairs.Select(stair => new SpriteRect
        {
            name = stair.name, rect = new Rect(stair.x, textureHeight - stair.y - stair.height, stair.width, stair.height),
            alignment = SpriteAlignment.Custom,
            pivot = new Vector2((stair.startX - stair.x) / stair.width, (stair.y + stair.height - stair.startY) / stair.height),
            spriteID = oldIds.TryGetValue(stair.name, out var id) ? id : GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Image).OfType<Sprite>().ToDictionary(sprite => sprite.name);
        foreach (var stair in layout.stairs)
        {
            Create(stair, sprites[stair.name], false);
            Create(stair, sprites[stair.name], true);
        }
        AssetDatabase.SaveAssets();
        Preview(layout);
        Debug.Log("Station stairs authored: 4 designs x 2 directions, continuous walk colliders, lower/upper connection markers.");
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static void Create(Stair stair, Sprite sprite, bool left)
    {
        string suffix = left ? "Left" : "Right";
        var root = new GameObject(stair.name + suffix, typeof(SpriteRenderer), typeof(PolygonCollider2D), typeof(PlatformEffector2D));
        float direction = left ? -1f : 1f;
        var renderer = root.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.flipX = left; renderer.sortingOrder = 0;
        Vector2 landing = new(direction * (stair.landingX - stair.startX) / PixelsPerUnit, (stair.startY - stair.landingY) / PixelsPerUnit);
        Vector2 upper = new(direction * (stair.endX - stair.startX) / PixelsPerUnit, (stair.startY - stair.endY) / PixelsPerUnit);
        var collider = root.GetComponent<PolygonCollider2D>();
        // A smooth slope avoids catching the EVA controller on individual risers.
        collider.points = new[] { Vector2.zero, landing, upper, upper + Vector2.down * .16f, landing + Vector2.down * .16f, Vector2.down * .16f };
        collider.usedByEffector = true;
        var effector = root.GetComponent<PlatformEffector2D>(); effector.useOneWay = true; effector.surfaceArc = 160f;
        Marker(root.transform, "LowerConnection", Vector3.zero);
        Marker(root.transform, "UpperLandingStart", landing);
        Marker(root.transform, "UpperConnection", upper);
        PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + root.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }
    static void Marker(Transform parent, string name, Vector3 position)
    {
        var marker = new GameObject(name).transform; marker.SetParent(parent, false); marker.localPosition = position;
    }
    static void Preview(Layout layout)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        for (int i = 0; i < layout.stairs.Length; i++)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + layout.stairs[i].name + "Right.prefab"), scene);
            root.transform.position = new Vector3(i % 2 * 12f - 10f, i < 2 ? 3f : -4f, 0f);
        }
        var camera = new GameObject("Stair Preview Camera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 7.5f; camera.aspect = 1.5f;
        camera.transform.position = new Vector3(0f, 1f, -10f); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/StationStairsPreview.unity");
        var target = new RenderTexture(1800, 1200, 24); camera.targetTexture = target;
        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
        RenderTexture.active = target;
        var pixels = new Texture2D(1800, 1200, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0,0,1800,1200),0,0); pixels.Apply();
        File.WriteAllBytes("Documentation/StationStairs.png", pixels.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null; target.Release();
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        Debug.Log("Station stairs preview exported in Edit Mode. No Play Mode or gameplay tests started.");
    }
}
