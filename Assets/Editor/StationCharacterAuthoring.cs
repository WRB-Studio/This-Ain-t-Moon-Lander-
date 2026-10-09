using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class StationCharacterAuthoring
{
    const string ImageFolder = "Assets/Images/Characters/Station";
    const string VisualFolder = "Assets/Prefabs/Characters/Station";
    const string ResidentFolder = "Assets/Prefabs/Station01/Residents";
    const string BaseResidentPath = "Assets/Prefabs/Station01/StationResident.prefab";
    const string PreviewPath = "Assets/Scenes/StationCharactersPreview.unity";

    [Serializable] class Library { public Character[] characters; }
    [Serializable] class Character
    {
        public string name;
        public float torsoWidth, headHeight, bootWidth;
        public bool backpack;
        public Part[] parts;
    }
    [Serializable] class Part
    {
        public string name;
        public int x, y, width, height;
        public float jointX, jointY, endX, endY;
    }

    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(VisualFolder);
        EnsureFolder(ResidentFolder);
        var library = JsonUtility.FromJson<Library>(File.ReadAllText($"{ImageFolder}/Characters.layout.json"));
        var visuals = new Dictionary<string, GameObject>();
        foreach (var character in library.characters)
            visuals.Add(character.name, MakeVisual(character, ImportParts(character)));
        UpdateBaseResident(visuals["Technician"]);
        var residents = new Dictionary<string, GameObject>();
        foreach (var character in library.characters)
            residents.Add(character.name, MakeResident(character.name, visuals[character.name]));
        ReplaceResidents("Assets/Prefabs/Station01/StationInterior.prefab", residents);
        ReplaceResidents("Assets/Prefabs/Station01/SpaceStation.prefab", residents);
        AddAlienResidents("Assets/Prefabs/Station01/StationInterior.prefab", residents, true);
        AddAlienResidents("Assets/Prefabs/Station01/SpaceStation.prefab", residents, false);
        MakePreviewScene(library.characters, visuals);
        AssetDatabase.SaveAssets();
        ExportPreview(library.characters, residents);
        Debug.Log($"Station characters authored: {library.characters.Length} visual and resident variants; three additional alien residents.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Dictionary<string, Sprite> ImportParts(Character character, string imageFolder = ImageFolder)
    {
        string path = $"{imageFolder}/{character.name}Parts.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
        importer.GetSourceTextureWidthAndHeight(out _, out int textureHeight);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var oldIds = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
        var rects = character.parts.Select(part => new SpriteRect
        {
            name = part.name,
            rect = new Rect(part.x, textureHeight - part.y - part.height, part.width, part.height),
            alignment = SpriteAlignment.Custom,
            pivot = new Vector2((part.jointX - part.x) / part.width, (part.y + part.height - part.jointY) / part.height),
            spriteID = oldIds.TryGetValue(part.name, out var id) ? id : GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
    }

    static GameObject MakeVisual(Character character, Dictionary<string, Sprite> sprites, string visualFolder = VisualFolder)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/BipedRig.prefab"));
        root.name = "Station" + character.name + "Visual";
        var hips = root.transform.Find("Facing/Rig/Hips");
        Apply(hips.Find("Spine/Head/Art"), "Helmet", null, character.headHeight / ((PartFor("Helmet").jointY - PartFor("Helmet").y) / 100f));
        Apply(hips.Find("Spine/Art"), "Torso", character.torsoWidth, 0.59f / ((PartFor("Torso").jointY - PartFor("Torso").y) / 100f));
        Apply(hips.Find("Art"), "Pelvis", character.torsoWidth * 0.92f, 0.34f / (PartFor("Pelvis").height / 100f));
        Apply(hips.Find("Spine/Backpack/Art"), "Backpack", 0.31f, 0.43f / (PartFor("Backpack").height / 100f));
        var packRenderer = hips.Find("Spine/Backpack/Art").GetComponent<SpriteRenderer>();
        packRenderer.enabled = character.backpack;
        PrefabUtility.RecordPrefabInstancePropertyModifications(packRenderer);
        foreach (string side in new[] { "Far", "Near" })
        {
            var arm = hips.Find("Spine/Arm" + side);
            Limb(arm.Find("Art"), "UpperArm", 0.33f);
            Limb(arm.Find("Forearm/Art"), "Forearm", 0.31f);
            Limb(arm.Find("Forearm/Hand/Art"), "Hand", 0.16f);
            Apply(arm.Find("Forearm/Elbow/Art"), "Elbow", 0.15f, 0.15f / (PartFor("Elbow").height / 100f));
            var leg = hips.Find("Leg" + side);
            Limb(leg.Find("Art"), "Thigh", 0.4f);
            Limb(leg.Find("Shin/Art"), "Shin", 0.4f);
            Apply(leg.Find("Shin/Knee/Art"), "Knee", 0.17f, 0.17f / (PartFor("Knee").height / 100f));
            var boot = PartFor("Boot");
            Apply(leg.Find("Shin/Foot/Art"), "Boot", character.bootWidth, 0.217f / ((boot.endY - boot.jointY) / 100f));
        }
        var group = root.GetComponent<SortingGroup>();
        group.sortingOrder = -1;
        PrefabUtility.RecordPrefabInstancePropertyModifications(group);
        var asset = PrefabUtility.SaveAsPrefabAsset(root, $"{visualFolder}/Station{character.name}Visual.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return asset;

        Part PartFor(string name) => character.parts.Single(p => p.name == name);

        void Limb(Transform art, string name, float length)
        {
            var part = PartFor(name);
            Vector2 delta = new(part.endX - part.jointX, part.jointY - part.endY);
            Apply(art, name, null, length / (delta.magnitude / 100f));
            art.localRotation = Quaternion.Euler(0f, 0f, -90f - Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            PrefabUtility.RecordPrefabInstancePropertyModifications(art);
        }

        void Apply(Transform art, string name, float? width, float scaleY)
        {
            var renderer = art.GetComponent<SpriteRenderer>();
            renderer.sprite = sprites[name];
            art.localScale = new Vector3(width.HasValue ? width.Value / (PartFor(name).width / 100f) : scaleY, scaleY, 1f);
            art.localRotation = Quaternion.identity;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(art);
        }
    }

    static void UpdateBaseResident(GameObject visualPrefab)
    {
        var root = PrefabUtility.LoadPrefabContents(BaseResidentPath);
        try
        {
            var old = root.transform.Find("VisualRoot");
            if (old && old.GetComponent<CharacterVisual>()) return;
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
            visual.name = "VisualRoot";
            PrefabUtility.SaveAsPrefabAsset(root, BaseResidentPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static GameObject MakeResident(string name, GameObject visualPrefab, string residentFolder = ResidentFolder)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BaseResidentPath));
        root.name = "Station" + name;
        var visual = root.transform.Find("VisualRoot");
        foreach (var source in visualPrefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(source.transform, visualPrefab.transform);
            var targetArt = visual.Find(path);
            var targetRenderer = targetArt.GetComponent<SpriteRenderer>();
            targetRenderer.sprite = source.sprite;
            targetRenderer.enabled = source.enabled;
            targetRenderer.color = source.color;
            targetRenderer.sortingOrder = source.sortingOrder;
            targetArt.localScale = source.transform.localScale;
            targetArt.localPosition = source.transform.localPosition;
            targetArt.localRotation = source.transform.localRotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetRenderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetArt);
        }
        var asset = PrefabUtility.SaveAsPrefabAsset(root, $"{residentFolder}/Station{name}.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return asset;
    }

    static void ReplaceResidents(string path, Dictionary<string, GameObject> prefabs)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var resident in root.GetComponentsInChildren<StationResident>(true))
            {
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(resident.gameObject) != BaseResidentPath) continue;
                string character = resident.Id switch
                {
                    1 => "Pilot", 2 => "Technician", 3 => "Security", 4 => "Operator",
                    5 => "Scientist", 6 => "Traveler", 7 => "Operator",
                    _ => throw new InvalidOperationException("Unexpected resident ID: " + resident.Id)
                };
                string behavior = EditorJsonUtility.ToJson(resident);
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(resident.gameObject, prefabs[character],
                    new PrefabReplacingSettings { objectMatchMode = ObjectMatchMode.ByHierarchy }, InteractionMode.AutomatedAction);
                EditorJsonUtility.FromJsonOverwrite(behavior, resident);
                PrefabUtility.RecordPrefabInstancePropertyModifications(resident);
                Debug.Log($"Resident {resident.Id}: {character}; patrol and dialogue preserved.");
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void BuildExpansion()
    {
        const string images = "Assets/Images/Characters/Expansion/Outline";
        const string visuals = "Assets/Prefabs/Characters/Expansion/Visuals";
        const string residents = "Assets/Prefabs/Characters/Expansion/Residents";
        EnsureFolder(visuals);
        EnsureFolder(residents);
        var library = JsonUtility.FromJson<Library>(File.ReadAllText(images + "/Characters.layout.json"));
        for (int i = 0; i < library.characters.Length; i++)
        {
            var character = library.characters[i];
            var visual = MakeVisual(character, ImportParts(character, images), visuals);
            var resident = MakeResident(character.name, visual, residents);
            string path = AssetDatabase.GetAssetPath(resident);
            var root = PrefabUtility.LoadPrefabContents(path);
            var data = new SerializedObject(root.GetComponent<StationResident>());
            data.FindProperty("residentId").intValue = 11 + i;
            data.FindProperty("floorY").floatValue = 0.03f;
            data.FindProperty("comments").ClearArray();
            data.ApplyModifiedPropertiesWithoutUndo();
            float scale = character.name == "ServiceBot" ? 0.7f : character.name == "WorkBot" ? 1.15f : 1f;
            root.transform.localScale = Vector3.one * scale;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log("Character expansion authored: ServiceBot, WorkBot, FoxCourier and BovineMechanic on the shared biped rig.");
    }

    static void MakePreviewScene(Character[] characters, Dictionary<string, GameObject> prefabs)
    {
        EnsureFolder("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Preview Camera").AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.CeilToInt(characters.Length / 3f) * 1.5f + 0.3f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        for (int i = 0; i < characters.Length; i++)
        {
            var character = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[characters[i].name], scene);
            character.name = characters[i].name;
            character.transform.position = Position(i, characters.Length);
            character.transform.localScale = Vector3.one * 0.9f;
            var preview = character.AddComponent<CharacterAnimationPreview>();
            var serialized = new SerializedObject(preview);
            serialized.FindProperty("character").objectReferenceValue = character.GetComponent<CharacterVisual>();
            serialized.FindProperty("walking").boolValue = true;
            serialized.FindProperty("worldSpeed").floatValue = 1.8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        if (!EditorSceneManager.SaveScene(scene, PreviewPath))
            throw new IOException("Could not save station character preview scene.");
    }

    static Vector3 Position(int index, int count) => new((index % 3 - 1) * 2.4f,
        (Mathf.CeilToInt(count / 3f) - 1) * 1.5f - index / 3 * 3f, 0f);

    static void AddAlienResidents(string path, Dictionary<string, GameObject> prefabs, bool interior)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var existing = root.GetComponentsInChildren<StationResident>(true);
            var parent = existing.Single(r => r.Id == (interior ? 4 : 1)).transform.parent;
            if (interior)
            {
                Add("Reptilian", 9, -7f, -10f, -5f);
                Add("Insectoid", 10, 9f, 5f, 11f);
            }
            else Add("Grey", 8, 7f, 6f, 12f);
            PrefabUtility.SaveAsPrefabAsset(root, path);

            void Add(string name, int id, float x, float minX, float maxX)
            {
                if (existing.Any(r => r.Id == id)) return;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[name], parent);
                instance.name = "Resident" + name;
                instance.transform.localPosition = new Vector3(x, interior ? 0.5f : 1f, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                var resident = instance.GetComponent<StationResident>();
                var settings = new SerializedObject(resident);
                settings.FindProperty("residentId").intValue = id;
                settings.FindProperty("minX").floatValue = minX;
                settings.FindProperty("maxX").floatValue = maxX;
                settings.FindProperty("floorY").floatValue = interior ? 0.03f : 0.55f;
                settings.FindProperty("walkSpeed").floatValue = 0.9f;
                settings.FindProperty("entersStationOnApproach").boolValue = false;
                settings.FindProperty("comments").arraySize = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(resident);
                Debug.Log($"Added alien resident {id}: {name} in {path}");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void ExportResidentPreview()
    {
        var library = JsonUtility.FromJson<Library>(File.ReadAllText($"{ImageFolder}/Characters.layout.json"));
        var prefabs = library.characters.ToDictionary(c => c.name,
            c => AssetDatabase.LoadAssetAtPath<GameObject>($"{ResidentFolder}/Station{c.name}.prefab"));
        ExportPreview(library.characters, prefabs);
    }

    static void ExportPreview(Character[] characters, Dictionary<string, GameObject> prefabs)
    {
        string folder = Path.GetFullPath(Environment.GetEnvironmentVariable("STATION_CHARACTER_PREVIEW_OUTPUT") ?? "Temp/StationCharactersPreview");
        Directory.CreateDirectory(folder);
        var scene = EditorSceneManager.NewPreviewScene();
        var cameraObject = new GameObject("Render Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        var camera = cameraObject.AddComponent<Camera>();
        camera.scene = scene;
        camera.orthographic = true;
        int height = Mathf.CeilToInt(characters.Length / 3f) * 400;
        camera.orthographicSize = Mathf.CeilToInt(characters.Length / 3f) * 1.5f + 0.3f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var target = new RenderTexture(1200, height, 24);
        camera.targetTexture = target;
        var pixels = new Texture2D(1200, height, TextureFormat.RGB24, false);
        var rigs = new GameObject[characters.Length];
        for (int i = 0; i < characters.Length; i++)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[characters[i].name], scene);
            root.transform.position = Position(i, characters.Length);
            root.transform.localScale = Vector3.one * 0.9f;
            root.GetComponent<StationResident>().enabled = false;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            rigs[i] = root.transform.Find("VisualRoot/Facing/Rig").gameObject;
            rigs[i].GetComponent<Animator>().enabled = false;
        }
        var walk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Characters/BipedWalk.anim");
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Characters/BipedIdle.anim");
        try
        {
            for (int frame = -1; frame < 144; frame++)
            {
                for (int i = 0; i < rigs.Length; i++)
                    (frame < 0 ? idle : walk).SampleAnimation(rigs[i], frame < 0 ? 0f : (frame / 30f) % walk.length);
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1200, height), 0, 0);
                pixels.Apply();
                string filename = frame < 0 ? "lineup.png" : $"frame-{frame:0000}.png";
                File.WriteAllBytes(Path.Combine(folder, filename), pixels.EncodeToPNG());
            }
            Debug.Log("Station character preview exported: " + folder);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(pixels);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
