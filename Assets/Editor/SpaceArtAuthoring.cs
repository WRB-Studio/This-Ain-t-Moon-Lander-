using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;

public static class SpaceArtAuthoring
{
    const string Modules = "Assets/Prefabs/World/Modules";
    const string Stations = "Assets/Prefabs/World/Stations";
    const string Ships = "Assets/Prefabs/Spacecraft";
    [Serializable] class Atlas { public Element[] assets; }
    [Serializable] class Element { public string name; public int x, y, width, height; }
    static Dictionary<string, GameObject> modules;
    static Dictionary<string, Sprite> fleet;

    [MenuItem("Tools/Art/Build Space Art Expansion")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Folder(Modules); Folder(Stations); Folder(Ships); Folder("Assets/Images/Spacecraft/Sprites"); Folder("Assets/Materials/World");
        var architecture = ImportAtlas("Assets/Images/World/ArchitectureOutline.png", 100f);
        fleet = ImportAtlas("Assets/Images/Spacecraft/FleetOutline.png", 100f);
        modules = new Dictionary<string, GameObject>();
        foreach (var item in architecture)
        {
            var root = new GameObject(item.Key, typeof(SpriteRenderer));
            root.GetComponent<SpriteRenderer>().sprite = item.Value;
            if (item.Key == "Deck")
            {
                var collider = root.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(item.Value.bounds.size.x, item.Value.bounds.size.y * 0.45f);
                collider.offset = new Vector2(0f, item.Value.bounds.max.y - collider.size.y * 0.5f);
                collider.usedByEffector = true;
                var effector = root.AddComponent<PlatformEffector2D>(); effector.useOneWay = true; effector.surfaceArc = 160f;
            }
            modules.Add(item.Key, PrefabUtility.SaveAsPrefabAsset(root, Modules + "/" + item.Key + ".prefab"));
            UnityEngine.Object.DestroyImmediate(root);
        }
        UpdateOutpost();
        for (int variant = 0; variant < 3; variant++) BuildStation(variant);
        BuildSpacecraft();
        StationCharacterAuthoring.BuildExpansion();
        OutlineWorldAuthoring.RestoreMissionStyle();
        UpdateMainScene();
        AssetDatabase.SaveAssets();
        BuildGallery();
        Debug.Log("Space art expansion authored: outpost, 3 stations, original 7 landers, 5 outline spacecraft, vector moon and 4 outline residents.");
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static Dictionary<string, Sprite> ImportAtlas(string path, float pixelsPerUnit)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        TextureSettings(importer);
        importer.SaveAndReimport();
        var atlas = JsonUtility.FromJson<Atlas>(File.ReadAllText(Path.ChangeExtension(path, ".layout.json")));
        importer.GetSourceTextureWidthAndHeight(out _, out int height);
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var old = provider.GetSpriteRects().ToDictionary(rect => rect.name, rect => rect.spriteID);
        var rects = atlas.assets.Select(item => new SpriteRect
        {
            name = item.name, rect = new Rect(item.x, height - item.y - item.height, item.width, item.height),
            alignment = SpriteAlignment.Center, pivot = Vector2.one * 0.5f,
            spriteID = old.TryGetValue(item.name, out var id) ? id : GUID.Generate()
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
        provider.Apply(); importer.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(sprite => sprite.name);
    }
    static void TextureSettings(TextureImporter importer)
    {
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096; importer.npotScale = TextureImporterNPOTScale.None;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true; android.maxTextureSize = 4096; android.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(android);
    }
    static Transform Child(Transform parent, string name, Vector3 position)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false); root.transform.localPosition = position;
        return root.transform;
    }
    static GameObject Module(Transform parent, string type, string name, Vector2 position, float width, int order, float height = 0f)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(modules[type], parent);
        root.name = name;
        var renderer = root.GetComponent<SpriteRenderer>();
        root.transform.localScale = new Vector3(width / renderer.sprite.bounds.size.x,
            height > 0f ? height / renderer.sprite.bounds.size.y : width / renderer.sprite.bounds.size.x, 1f);
        root.transform.localPosition = new Vector3(position.x, position.y - renderer.sprite.bounds.min.y * root.transform.localScale.y, 0f);
        renderer.sortingOrder = order;
        foreach (var meshRenderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            meshRenderer.sortingOrder = order;
            PrefabUtility.RecordPrefabInstancePropertyModifications(meshRenderer);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        return root;
    }
    static void UpdateOutpost()
    {
        const string path = "Assets/Prefabs/Missions/AsteroidOutpost.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var geometry = root.transform.Find("Geometry");
            foreach (string group in new[] { "BackgroundBuildings", "ForegroundEquipment" })
            {
                var old = geometry.Find(group); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var background = Child(geometry, "BackgroundBuildings", Vector3.zero);
            var foreground = Child(geometry, "ForegroundEquipment", Vector3.zero);
            const float floor = 17.85f;
            Module(background, "Workshop", "AbandonedWorkshop", new Vector2(-6.5f, floor), 10f, -12);
            Module(background, "Habitat", "SealedHabitat", new Vector2(4.5f, floor), 9f, -12);
            Module(background, "CommsTower", "SilentCommunicationsTower", new Vector2(24f, floor), 6f, -12);
            Module(foreground, "FuelTanks", "AutomaticFuelSupply", new Vector2(-11.3f, floor), 4f, 1);
            Module(foreground, "CargoShelter", "OpenCargoShelter", new Vector2(11f, floor), 8f, -4);
            Module(foreground, "Debris", "DiscardedEquipment", new Vector2(30f, floor), 3f, 0);
            foreach (string name in new[] { "AbandonedControl", "SealedHabitat", "OldWorkshop", "FuelTanks", "OpenCargoShelter", "AbandonedEquipment" })
            {
                var old = geometry.Find(name); if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            foreach (var old in geometry.Cast<Transform>().Where(t => t.name.StartsWith("DeckSupport")).ToArray())
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            for (int i = 0; i < 5; i++) Module(background, "Truss", "DeckSupport" + i, new Vector2(-28f + i * 14f, 10.7f), 5.5f, -14, 7f);
            ReplaceDeck(geometry.Find("OutpostDeck"), 67f, 0.7f);
            ReplaceDeck(geometry.Find("LandingPlatform"), 16f, 0.55f);
            var sign = geometry.Find("OutpostName"); if (sign) sign.localPosition = new Vector3(0f, 35f, 0f);
            var closed = geometry.Find("ClosedSign"); if (closed) closed.localPosition = new Vector3(4.5f, 28.4f, 0f);
            OutlineWorldAuthoring.BuildAsteroidField(geometry.Find("AsteroidField"));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void ReplaceDeck(Transform root, float width, float height)
    {
        var renderer = root.GetComponent<SpriteRenderer>();
        renderer.sprite = modules["Deck"].GetComponent<SpriteRenderer>().sprite;
        renderer.color = Color.white;
        root.localScale = new Vector3(width / renderer.sprite.bounds.size.x, height / renderer.sprite.bounds.size.y, 1f);
        root.GetComponent<BoxCollider2D>().size = renderer.sprite.bounds.size;
    }
    static void BuildStation(int variant)
    {
        string[] names = { "WayfarerTradeStation", "KeplerResearchStation", "AtlasIndustrialStation" };
        int levels = variant + 2;
        float halfWidth = variant == 0 ? 30f : variant == 1 ? 23f : 37f;
        var root = new GameObject(names[variant]);
        var back = Child(root.transform, "Background", Vector3.zero);
        var supports = Child(root.transform, "Structure", Vector3.zero);
        var floors = Child(root.transform, "ExteriorWalkways", Vector3.zero);
        var front = Child(root.transform, "Foreground", Vector3.zero);
        var doors = Child(root.transform, "Airlocks", Vector3.zero);
        var pads = Child(root.transform, "LandingPads", Vector3.zero);
        var anchors = Child(root.transform, "InteriorConnectionPoints", Vector3.zero);
        for (int level = 0; level < levels; level++)
        {
            float y = level * 8f;
            for (int bay = 0; bay < 3; bay++)
                Module(back, variant == 1 && bay == 1 ? "Atrium" : "FacadeBay", "Facade_L" + level + "_" + bay,
                    new Vector2((bay - 1) * 12f, y + 0.1f), 12f, -20, 7f);
            Module(floors, "Deck", "Walkway_L" + level, new Vector2(0f, y - 0.8f), halfWidth * 2f, 0, 0.8f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (halfWidth + 7f + (level % 2) * 3f);
                if (level % 2 == 1) Module(floors, "Deck", "PadConnection_L" + level + "_" + side,
                    new Vector2(side * (halfWidth + 1.5f), y - 0.8f), 3f, 0, 0.8f);
                Module(pads, "Deck", "LandingPad_L" + level + (side < 0 ? "_Left" : "_Right"), new Vector2(x, y - 0.5f), 14f, 1, 0.5f).tag = "LandingPad";
                Module(supports, "Truss", "PadSupport_L" + level + "_" + side, new Vector2(x, y - 4f), 10f, -10, 3.5f);
                Module(front, "Railing", "Rail_L" + level + "_" + side, new Vector2(side * 15f, y + 0.05f), 18f, 6, 1.2f);
            }
            for (int entry = 0; entry < 2; entry++)
            {
                float x = entry == 0 ? -5f : 7f;
                Module(doors, "Airlock", "Airlock_L" + level + "_" + entry, new Vector2(x, y), 2.6f, -5, 3f);
                Child(anchors, "Entry_L" + level + "_" + entry, new Vector3(x, y + 0.05f));
            }
            if (level < levels - 1) Ramp(floors, "Ramp_L" + level, new Vector2(halfWidth - 14f, y), new Vector2(halfWidth - 2f, y + 8f));
        }
        Module(back, "LiftShaft", "ElevatorBackground", new Vector2(-halfWidth + 4f, 0f), 4.5f, -18, levels * 8f);
        Module(back, "Antenna", "CommunicationsArray", new Vector2(0f, levels * 8f - 1f), 5f, -18);
        Module(supports, "FuelTanks", "FuelStorage", new Vector2(-12f, -7f), 7f, -9);
        Module(supports, "Corridor", "ServiceConduit", new Vector2(8f, -6f), 16f, -10, 4f);
        Module(back, "FacadeBay", "UndersideServiceBay", new Vector2(0f, -6f), halfWidth * 1.35f, -20, 6f);
        PrefabUtility.SaveAsPrefabAsset(root, Stations + "/" + names[variant] + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
    }
    static void Ramp(Transform parent, string name, Vector2 start, Vector2 end)
    {
        var root = Child(parent, name, start);
        Vector2 delta = end - start;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        var deck = Module(root, "Deck", "RampDeck", Vector2.zero, delta.magnitude, 1, 0.4f);
        deck.transform.localPosition = delta * 0.5f - normal * 0.2f;
        deck.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        deck.GetComponent<BoxCollider2D>().enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(deck.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(deck.GetComponent<BoxCollider2D>());
        root.gameObject.AddComponent<PolygonCollider2D>().points = new[] { Vector2.zero, delta, delta + Vector2.down * 0.25f, Vector2.down * 0.25f };
        var rail = Module(root, "Railing", "Handrail", Vector2.zero, delta.magnitude, 6, 1.1f);
        rail.transform.localPosition = delta * 0.5f + normal * 0.6f;
        rail.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rail.transform);
    }
    static Sprite ShipSprite(string name, float width, float bottom)
    {
        string path = "Assets/Images/Spacecraft/Sprites/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var source = fleet[name];
        float ppu = source.rect.width / width;
        var pivot = new Vector2(0.5f, bottom < 0f ? Mathf.Clamp01(-bottom / (source.rect.height / ppu)) : 0.5f);
        var sprite = Sprite.Create(source.texture, source.rect, pivot, ppu, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        var paths = new List<Vector2[]>(); var points = new List<Vector2>();
        for (int i = 0; i < source.GetPhysicsShapeCount(); i++)
        {
            source.GetPhysicsShape(i, points);
            paths.Add(points.Select(point => point * source.pixelsPerUnit / ppu + (source.pivot - sprite.pivot) / ppu).ToArray());
        }
        if (paths.Count > 0) sprite.OverridePhysicsShape(paths);
        if (existing)
        {
            EditorUtility.CopySerialized(sprite, existing); UnityEngine.Object.DestroyImmediate(sprite); EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(sprite, path); return sprite;
    }
    static void ApplyHull(PolygonCollider2D collider, Sprite sprite)
    {
        var shapes = new List<Vector2[]>(); var points = new List<Vector2>();
        for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
        {
            sprite.GetPhysicsShape(i, points); if (points.Count >= 3) shapes.Add(points.ToArray());
        }
        if (shapes.Count == 0) return;
        var hull = shapes.OrderByDescending(points => Mathf.Abs(points.Select((point, i) =>
            point.x * points[(i + 1) % points.Length].y - points[(i + 1) % points.Length].x * point.y).Sum())).First();
        collider.pathCount = 1; collider.offset = Vector2.zero; collider.SetPath(0, hull);
    }
    static void BuildSpacecraft()
    {
        string[] names = { "Courier", "Surveyor", "CargoTug", "Interceptor", "RescueShuttle" };
        var root = new GameObject("SpacecraftHull", typeof(SpriteRenderer), typeof(PolygonCollider2D), typeof(Rigidbody2D));
        root.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        root.GetComponent<Rigidbody2D>().gravityScale = 0f;
        root.GetComponent<SpriteRenderer>().sprite = ShipSprite("Courier", 3f, 0f);
        ApplyHull(root.GetComponent<PolygonCollider2D>(), root.GetComponent<SpriteRenderer>().sprite);
        var sockets = Child(root.transform, "AttachmentPoints", Vector3.zero);
        foreach (string socket in new[] { "ForwardThruster", "RearThruster", "PortThruster", "StarboardThruster", "DockingSocket", "TowSocket" }) Child(sockets, socket, Vector3.zero);
        var basePrefab = PrefabUtility.SaveAsPrefabAsset(root, Ships + "/SpacecraftHull.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        foreach (string name in names)
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab); root.name = name;
            var renderer = root.GetComponent<SpriteRenderer>();
            renderer.sprite = ShipSprite(name, name == "CargoTug" ? 4.5f : name == "RescueShuttle" ? 3.7f : 3f, 0f);
            ApplyHull(root.GetComponent<PolygonCollider2D>(), renderer.sprite);
            var bounds = renderer.sprite.bounds;
            sockets = root.transform.Find("AttachmentPoints");
            sockets.Find("ForwardThruster").localPosition = new Vector3(0f, bounds.max.y);
            sockets.Find("RearThruster").localPosition = new Vector3(0f, bounds.min.y);
            sockets.Find("PortThruster").localPosition = new Vector3(bounds.min.x, 0f);
            sockets.Find("StarboardThruster").localPosition = new Vector3(bounds.max.x, 0f);
            sockets.Find("DockingSocket").localPosition = new Vector3(0f, bounds.min.y);
            sockets.Find("TowSocket").localPosition = new Vector3(0f, bounds.min.y - 0.1f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.GetComponent<PolygonCollider2D>());
            foreach (Transform socket in sockets) PrefabUtility.RecordPrefabInstancePropertyModifications(socket);
            PrefabUtility.SaveAsPrefabAsset(root, Ships + "/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static void UpdateMainScene()
    {
        const string starPath = "Assets/Prefabs/StarField.prefab";
        var stars = PrefabUtility.LoadPrefabContents(starPath);
        stars.GetComponent<ParticleSystemRenderer>().sortingOrder = -100;
        PrefabUtility.SaveAsPrefabAsset(stars, starPath); PrefabUtility.UnloadPrefabContents(stars);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var starRenderer = UnityEngine.Object.FindFirstObjectByType<StarField>().GetComponent<ParticleSystemRenderer>();
        starRenderer.sortingOrder = -100; PrefabUtility.RecordPrefabInstancePropertyModifications(starRenderer);
        var chooser = UnityEngine.Object.FindFirstObjectByType<LanderChooserManager>(FindObjectsInactive.Include);
        foreach (var lander in UnityEngine.Object.FindObjectsByType<LanderController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var prefab = chooser.landerPrefabs.FirstOrDefault(item => item.GetComponent<LanderController>().landerIndex == lander.landerIndex);
            if (!prefab) continue;
            lander.GetComponent<SpriteRenderer>().sprite = prefab.GetComponent<SpriteRenderer>().sprite;
            var currentHull = lander.GetComponent<PolygonCollider2D>();
            var originalHull = prefab.GetComponent<PolygonCollider2D>();
            currentHull.offset = originalHull.offset; currentHull.pathCount = originalHull.pathCount;
            for (int i = 0; i < originalHull.pathCount; i++) currentHull.SetPath(i, originalHull.GetPath(i));
            PrefabUtility.RecordPrefabInstancePropertyModifications(lander.GetComponent<SpriteRenderer>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(lander.GetComponent<PolygonCollider2D>());
        }
        for (int i = 0; i < chooser.optionButtons.Length; i++)
        {
            var image = chooser.optionButtons[i].transform.GetChild(0).GetComponent<UnityEngine.UI.Image>();
            image.sprite = chooser.landerPrefabs[i].GetComponent<SpriteRenderer>().sprite;
            image.preserveAspect = true; EditorUtility.SetDirty(image);
        }
        OutlineWorldAuthoring.BuildMoon(UnityEngine.Object.FindFirstObjectByType<GravityManager2D>().gameObject);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    static Material Material(string name, Texture texture, Color color)
    {
        string path = "Assets/Materials/World/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, path); }
        material.mainTexture = texture; material.color = color; EditorUtility.SetDirty(material); return material;
    }
    static void BuildGallery()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Art Gallery Camera").AddComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var stations = new List<GameObject>();
        string[] stationNames = { "WayfarerTradeStation", "KeplerResearchStation", "AtlasIndustrialStation" };
        for (int i = 0; i < stationNames.Length; i++)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Stations + "/" + stationNames[i] + ".prefab"), scene);
            root.transform.position = new Vector3((i - 1) * 110f, 50f, 0f); stations.Add(root);
        }
        string[] shipNames = { "Courier", "Surveyor", "CargoTug", "Interceptor", "RescueShuttle" };
        for (int i = 0; i < shipNames.Length; i++)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Ships + "/" + shipNames[i] + ".prefab"), scene);
            root.transform.position = new Vector3((i - 2) * 6f, -10f, 0f);
        }
        for (int i = 1; i <= 7; i++)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Lander/Lander{i:00}.prefab"), scene);
            root.transform.position = new Vector3((i - 4) * 4f, -16f, 0f);
            var lander = root.GetComponent<LanderController>(); lander.isActive = false; lander.enabled = false;
            root.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            foreach (Transform child in root.transform) if (child.name.Contains("ThrustEffect")) child.gameObject.SetActive(false);
        }
        string[] characters = { "ServiceBot", "WorkBot", "FoxCourier", "BovineMechanic" };
        for (int i = 0; i < characters.Length; i++)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Expansion/Residents/Station" + characters[i] + ".prefab"), scene);
            root.transform.position = new Vector3((i - 1.5f) * 3f, -30f, 0f);
            root.GetComponent<StationResident>().enabled = false;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            var rig = root.transform.Find("VisualRoot/Facing/Rig");
            AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Characters/BipedIdle.anim").SampleAnimation(rig.gameObject, 0f);
            root.transform.position += Vector3.up * (-30f - root.GetComponentInChildren<CharacterVisual>().Bounds.min.y);
            var preview = root.AddComponent<CharacterAnimationPreview>();
            var previewData = new SerializedObject(preview);
            previewData.FindProperty("character").objectReferenceValue = root.GetComponentInChildren<CharacterVisual>();
            previewData.ApplyModifiedPropertiesWithoutUndo();
        }
        camera.transform.position = new Vector3(0f, 40f, -10f); camera.orthographicSize = 100f;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/SpaceArtGallery.unity");
        Render(camera, new Vector3(0f, 63f, -10f), 105f, 2000, 750, "Documentation/StationExpansion.png");
        for (int i = 0; i < stationNames.Length; i++) Render(camera, new Vector3((i - 1) * 110f, 56f + i * 4f, -10f), 22f + i * 5f, 1800, 1050, "Documentation/" + stationNames[i] + ".png");
        Render(camera, new Vector3(0f, -13f, -10f), 7.2f, 1800, 900, "Documentation/FleetExpansion.png");
        Render(camera, new Vector3(0f, -28.5f, -10f), 2.6f, 1600, 700, "Documentation/CharacterExpansion.png");
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var moon = UnityEngine.Object.FindFirstObjectByType<GravityManager2D>().transform;
        var moonCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
        Render(moonCamera, moon.position + new Vector3(0f, 0f, -10f), 28f, 1400, 1400, "Documentation/MoonSurface.png");
        Render(moonCamera, moon.position + new Vector3(0f, 22f, -10f), 9f, 1600, 900, "Documentation/MoonSurfaceClose.png");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var outpost = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Missions/AsteroidOutpost.prefab"));
        var capsule = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Missions/PassengerCapsule.prefab"));
        capsule.transform.position = outpost.GetComponent<AsteroidOutpost>().CargoSpawn.position;
        camera = new GameObject("Outpost Art Camera").AddComponent<Camera>(); camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        Render(camera, new Vector3(0f, 19f, -10f), 25f, 1800, 1000, "Documentation/CargoOutpost.png");
        Render(camera, new Vector3(0f, 0f, -10f), 195f, 1800, 1400, "Documentation/CargoAsteroidField.png");
        UnityEngine.Object.DestroyImmediate(outpost); UnityEngine.Object.DestroyImmediate(capsule);
        string[] rocks = { "Chunky", "Angular", "Long", "Notched", "Twin", "Rounded" };
        for (int i = 0; i < rocks.Length; i++)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/Asteroids/Asteroid" + rocks[i] + ".prefab"));
            root.transform.position = new Vector3((i % 3 - 1) * 22f, i < 3 ? 10f : -10f, 0f);
            root.transform.localScale = Vector3.one * 6f;
            foreach (var line in root.GetComponentsInChildren<LineRenderer>()) line.widthMultiplier *= 6f;
        }
        Render(camera, new Vector3(0f, 0f, -10f), 24f, 1800, 1100, "Documentation/AsteroidVariants.png");
        Debug.Log("Space art gallery and static previews exported. No Play Mode or gameplay tests were entered.");
    }
    static void Render(Camera camera, Vector3 position, float zoom, int width, int height, string path)
    {
        camera.transform.position = position; camera.orthographicSize = zoom; camera.aspect = width / (float)height;
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (text.TryGetComponent<LocalizedText>(out var localized)) text.text = Localization.Resolve(localized.Source);
        var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
        RenderTexture.active = target;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null; target.Release();
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
    }
}
