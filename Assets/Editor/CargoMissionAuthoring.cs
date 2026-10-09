using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class CargoMissionAuthoring
{
    const string Folder = "Assets/Prefabs/Missions";
    const string OutpostPath = Folder + "/AsteroidOutpost.prefab";
    const string CapsulePath = Folder + "/PassengerCapsule.prefab";
    const string PassengerPath = "Assets/Prefabs/Characters/Rhekk.prefab";
    const string UIPath = "Assets/Prefabs/UI/CargoMissionHUD.prefab";
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
    static Sprite Square => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Missions/Panel.png");

    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        if (UnityEngine.Object.FindFirstObjectByType<CargoMission>(FindObjectsInactive.Include))
            throw new InvalidOperationException("Cargo mission already exists. Edit its prefabs and scene references directly.");
        EnsureFolder(Folder);
        EnsureFolder("Assets/Images/Missions");
        EnsureFolder("Assets/Audio/Missions");
        MakePanelSprite();
        MakeKnocking();
        var passenger = MakePassenger();
        var capsule = MakeCapsule();
        var outpost = MakeOutpost();
        var hud = MakeHUD();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var outpostInstance = (GameObject)PrefabUtility.InstantiatePrefab(outpost, scene);
        var missionRoot = new GameObject("Cargo Rescue Mission", typeof(CargoMission));
        var capsuleInstance = (GameObject)PrefabUtility.InstantiatePrefab(capsule, missionRoot.transform);
        var passengerInstance = (GameObject)PrefabUtility.InstantiatePrefab(passenger, missionRoot.transform);
        var station = UnityEngine.Object.FindFirstObjectByType<SpaceStation>(FindObjectsInactive.Include);
        var interior = UnityEngine.Object.FindFirstObjectByType<StationInterior>(FindObjectsInactive.Include);
        var stationGeometry = station.transform.Find("StationGeometry");
        var delivery = MakeDelivery(stationGeometry);
        var interiorData = new SerializedObject(interior);
        var registration = ((GameObject)interiorData.FindProperty("registration").objectReferenceValue).transform;
        var hall = ((GameObject)interiorData.FindProperty("hall").objectReferenceValue).transform;
        var registrationDoor = interior.GetComponentsInChildren<StationInteraction>(true)
            .First(interaction => interaction.action == StationInteraction.Action.RegistrationEntrance);
        var hallGoal = Child(hall, "RhekkHallDestination", new Vector3(hall.InverseTransformPoint(registrationDoor.transform.position).x, 0.03f));
        var registrationGoal = Child(registration, "RhekkRegistrationDestination", new Vector3(6.5f, 0.03f));
        var mission = new SerializedObject(missionRoot.GetComponent<CargoMission>());
        Set(mission, "capsule", capsuleInstance.GetComponent<CargoCapsule>());
        Set(mission, "passenger", passengerInstance.GetComponent<CargoPassenger>());
        Set(mission, "deliverySurface", delivery);
        Set(mission, "hallDestination", hallGoal);
        Set(mission, "registrationDestination", registrationGoal);
        SetConversation(mission, "discoveryConversation", new[]
        {
            ("cargo.dialog.discovery.1", new[] { "cargo.answer.who", "cargo.answer.freight" }),
            ("cargo.dialog.discovery.2", new[] { "cargo.answer.pilot" }),
            ("cargo.dialog.discovery.3", new[] { "cargo.answer.ship" }),
            ("cargo.dialog.discovery.4", new[] { "cargo.answer.transport" }),
            ("cargo.dialog.discovery.5", new[] { "cargo.answer.help", "cargo.answer.no.promises" })
        });
        SetConversation(mission, "releaseConversation", new[]
        {
            ("cargo.dialog.release.1", new[] { "cargo.answer.welcome", "cargo.answer.complaint" }),
            ("cargo.dialog.release.2", new[] { "cargo.answer.what.now" }),
            ("cargo.dialog.release.3", new[] { "cargo.answer.see.you" })
        });
        mission.ApplyModifiedPropertiesWithoutUndo();
        outpostInstance.transform.position = station.transform.position + new Vector3(650f, 480f);
        var spawn = outpostInstance.GetComponent<AsteroidOutpost>().CargoSpawn;
        capsuleInstance.transform.position = spawn.position;
        passengerInstance.SetActive(false);
        PrefabUtility.InstantiatePrefab(hud, scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Cargo mission authored: asteroid field, abandoned outpost, capsule, Rhekk, station delivery area and HUD.");
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }
    static void MakePanelSprite()
    {
        string path = "Assets/Images/Missions/Panel.png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(8, 8);
            texture.SetPixels(Enumerable.Repeat(Color.white, 64).ToArray());
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 8f;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
    static void MakeKnocking()
    {
        string path = "Assets/Audio/Missions/CapsuleKnock.wav";
        if (File.Exists(path)) return;
        const int rate = 22050;
        const int count = rate * 2;
        var noise = new System.Random(1947);
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float value = 0f;
                foreach (float start in new[] { 0.05f, 0.3f, 0.62f })
                {
                    float dt = t - start;
                    if (dt >= 0f && dt < 0.22f)
                        value += Mathf.Exp(-dt * 24f) * (Mathf.Sin(dt * 2f * Mathf.PI * 135f) * 0.4f
                            + Mathf.Sin(dt * 2f * Mathf.PI * 410f) * 0.14f + ((float)noise.NextDouble() - 0.5f) * 0.22f);
                }
                if (t > 1f) value += Mathf.Sin(t * 2f * Mathf.PI * 77f) * Mathf.Exp(-(t - 1f) * 5f) * 0.04f;
                writer.Write((short)(Mathf.Clamp(value, -1f, 1f) * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path);
    }
    static GameObject MakePassenger()
    {
        var root = new GameObject("Rhekk", typeof(CargoPassenger));
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Station/StationReptilianVisual.prefab"), root.transform);
        visual.name = "RhekkVisual";
        // Shared rig, individual ochre travel suit, teal skin and a copper identification badge.
        foreach (var renderer in visual.GetComponentsInChildren<SpriteRenderer>(true))
        {
            bool skin = renderer.transform.IsChildOf(visual.transform.Find("Facing/Rig/Hips/Spine/Head"))
                || renderer.name.Contains("Hand");
            renderer.color = skin ? new Color(0.58f, 0.93f, 0.84f) : new Color(1f, 0.78f, 0.46f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        var spine = visual.transform.Find("Facing/Rig/Hips/Spine");
        Shape(spine, "PersonalBadge", new Vector3(0.16f, 0.12f), new Vector2(0.13f, 0.18f), new Color(0.9f, 0.4f, 0.18f), 20);
        Shape(spine, "BadgeInset", new Vector3(0.16f, 0.12f), new Vector2(0.09f, 0.14f), Color.black, 21);
        Shape(spine, "TravelScarf", new Vector3(0.05f, 0.4f), new Vector2(0.42f, 0.09f), new Color(0.3f, 0.8f, 0.75f), 21);
        Shape(spine, "ScarfInset", new Vector3(0.05f, 0.4f), new Vector2(0.37f, 0.045f), Color.black, 22);
        var data = new SerializedObject(root.GetComponent<CargoPassenger>());
        Set(data, "visual", visual.GetComponent<CharacterVisual>());
        data.ApplyModifiedPropertiesWithoutUndo();
        var asset = PrefabUtility.SaveAsPrefabAsset(root, PassengerPath);
        UnityEngine.Object.DestroyImmediate(root);
        return asset;
    }
    static GameObject MakeCapsule()
    {
        var root = new GameObject("PassengerCapsule", typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(DistanceJoint2D), typeof(CargoCapsule));
        var body = root.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        body.mass = 0.18f;
        body.gravityScale = 0f;
        body.linearDamping = 0.08f;
        body.angularDamping = 0.3f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var collider = root.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(3.5f, 2.3f);
        var closed = Child(root.transform, "Closed", Vector3.zero);
        Art(closed, "ReinforcedContainer", Sprite("Assets/Images/StationProps/CargoCrate.png"), Vector3.zero, 3.6f, 4);
        Shape(closed, "InspectionWindow", new Vector3(0f, 0.05f), new Vector2(0.72f, 0.7f), new Color(0.2f, 0.45f, 0.42f), 5);
        Text(closed, "PassengerLabel", "[[cargo.capsule.label]]", new Vector3(0f, -0.7f), 1.8f, 0.19f);
        var opened = Child(root.transform, "Open", Vector3.zero);
        Art(opened, "Container", Sprite("Assets/Images/StationProps/CargoCrate.png"), Vector3.zero, 3.6f, 4);
        Shape(opened, "OpenHatch", Vector3.zero, new Vector2(2f, 1.5f), new Color(0.03f, 0.04f, 0.05f), 5);
        opened.gameObject.SetActive(false);
        var wreck = Child(root.transform, "Wreck", Vector3.zero);
        for (int i = 0; i < 5; i++)
        {
            var fragment = Shape(wreck, "Fragment" + i, new Vector3((i - 2f) * 0.6f, -0.8f + i % 2 * 0.15f),
                new Vector2(0.7f, 0.25f), new Color(0.45f, 0.42f, 0.4f), 4);
            fragment.transform.localRotation = Quaternion.Euler(0, 0, i * 24f);
        }
        wreck.gameObject.SetActive(false);
        var anchor = Child(root.transform, "TowEye", new Vector3(0f, 1.25f));
        Shape(anchor, "Eye", Vector3.zero, new Vector2(0.3f, 0.12f), Color.white, 5);
        var rope = Child(root.transform, "SteelCable", Vector3.zero).gameObject.AddComponent<LineRenderer>();
        rope.sharedMaterial = CableMaterial();
        rope.positionCount = 2;
        rope.useWorldSpace = true;
        rope.startWidth = rope.endWidth = 0.045f;
        rope.startColor = rope.endColor = new Color(0.85f, 0.87f, 0.9f);
        rope.sortingOrder = 6;
        rope.enabled = false;
        var sound = root.AddComponent<AudioSource>();
        sound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Missions/CapsuleKnock.wav");
        sound.playOnAwake = false;
        var data = new SerializedObject(root.GetComponent<CargoCapsule>());
        Set(data, "cableAnchor", anchor); Set(data, "cable", rope); Set(data, "knocking", sound);
        Set(data, "closedVisual", closed.gameObject); Set(data, "openVisual", opened.gameObject); Set(data, "wreckVisual", wreck.gameObject);
        data.ApplyModifiedPropertiesWithoutUndo();
        var asset = PrefabUtility.SaveAsPrefabAsset(root, CapsulePath);
        UnityEngine.Object.DestroyImmediate(root);
        return asset;
    }
    static Material CableMaterial()
    {
        string path = "Assets/Materials/CargoCable.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            EnsureFolder("Assets/Materials");
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }
    static GameObject MakeOutpost()
    {
        var root = new GameObject("AsteroidOutpost", typeof(AsteroidOutpost));
        var geometry = Child(root.transform, "Geometry", Vector3.zero);
        var rocks = Child(geometry, "AsteroidField", Vector3.zero);
        Rock(rocks, "OutpostAsteroid", new Vector2(0f, -3f), new Vector2(76f, 40f), 0f);
        Vector2[] positions = { new(-95, 18), new(-128, 95), new(-70, 125), new(12, 112), new(92, 84), new(127, 15),
            new(94, -75), new(10, -113), new(-95, -86), new(-166, -30), new(167, 126), new(42, 174) };
        for (int i = 0; i < positions.Length; i++)
            Rock(rocks, "SmallAsteroid" + (i + 1), positions[i], new Vector2(11f + i % 4 * 3f, 8f + i % 3 * 3f), i * 37f);
        const float deckY = 17.5f;
        var deck = Shape(geometry, "OutpostDeck", new Vector3(0f, deckY), new Vector2(67f, 0.7f), new Color(0.45f, 0.5f, 0.52f), 1);
        var floor = deck.gameObject.AddComponent<BoxCollider2D>();
        floor.size = Vector2.one;
        for (int i = 0; i < 6; i++)
            Shape(geometry, "DeckSupport" + i, new Vector3(-30f + i * 12f, 14f), new Vector2(0.5f, 7f), new Color(0.35f, 0.4f, 0.43f), 0);
        var padVisual = Shape(geometry, "LandingPlatform", new Vector3(-22f, deckY + 0.12f), new Vector2(16f, 0.55f), Color.white, 2);
        padVisual.gameObject.tag = "LandingPad";
        var padCollider = padVisual.gameObject.AddComponent<BoxCollider2D>();
        padCollider.size = Vector2.one;
        var pad = padVisual.gameObject.AddComponent<StationLandingPad>();
        pad.index = 2;
        pad.isOutpost = true;
        Text(geometry, "PadMark", "[[cargo.outpost.pad]]", new Vector3(-22f, deckY - 1.1f), 12f, 0.65f);
        var buildings = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Lander/Buildings.png").OfType<Sprite>().ToArray();
        for (int i = 0; i < 3; i++)
        {
            var building = Art(geometry, new[] { "AbandonedControl", "SealedHabitat", "OldWorkshop" }[i],
                buildings.First(s => s.name == "Buildings_" + (i + 1)), new Vector3(-6f + i * 13f, deckY + 0.4f), 11f, -3);
            Vector3 position = building.transform.localPosition;
            position.y = deckY + 0.35f - VisibleBottom(building.sprite) * building.transform.localScale.y;
            building.transform.localPosition = position;
            building.color = new Color(0.52f, 0.57f, 0.59f);
        }
        Text(geometry, "OutpostName", "[[cargo.outpost.sign]]", new Vector3(9f, deckY + 9f), 32f, 0.6f);
        Text(geometry, "ClosedSign", "[[cargo.outpost.closed]]", new Vector3(7f, deckY + 1f), 7f, 0.35f);
        var tanks = Child(geometry, "FuelTanks", new Vector3(-11f, deckY + 0.35f));
        for (int i = 0; i < 2; i++)
        {
            Art(tanks, "Tank" + i, Sprite("Assets/Images/StationProps/SupplyCanister.png"), new Vector3(i * 2f, 1.5f), 1.7f, 0);
            Shape(tanks, "SupplyPipe" + i, new Vector3(-3f + i, 0.1f), new Vector2(6f, 0.1f), new Color(0.65f, 0.7f, 0.7f), 2);
        }
        Text(tanks, "FuelSign", "[[cargo.outpost.fuel]]", new Vector3(0.8f, 3.7f), 5f, 0.36f);
        var canopy = Child(geometry, "OpenCargoShelter", new Vector3(10f, deckY + 0.35f));
        Shape(canopy, "Roof", new Vector3(0, 4.6f), new Vector2(8f, 0.25f), new Color(0.55f, 0.6f, 0.62f), -2);
        Shape(canopy, "LeftPost", new Vector3(-3.8f, 2.3f), new Vector2(0.14f, 4.6f), new Color(0.55f, 0.6f, 0.62f), -2);
        Shape(canopy, "RightPost", new Vector3(3.8f, 2.3f), new Vector2(0.14f, 4.6f), new Color(0.55f, 0.6f, 0.62f), -2);
        var spawn = Child(root.transform, "CapsuleSpawn", new Vector3(10f, deckY + 0.35f + 1.16f));
        var debris = Child(geometry, "AbandonedEquipment", Vector3.zero);
        Art(debris, "EmptyCanister", Sprite("Assets/Images/StationProps/SupplyCanister.png"), new Vector3(29f, deckY + 0.8f), 1.5f, 0)
            .transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
        for (int i = 0; i < 4; i++)
            Shape(debris, "LoosePanel" + i, new Vector3(-2f + i * 8f, deckY + 0.5f), new Vector2(0.8f, 0.15f), Color.gray, 1);
        var data = new SerializedObject(root.GetComponent<AsteroidOutpost>());
        Set(data, "geometry", geometry); Set(data, "cargoSpawn", spawn); Set(data, "landingPad", pad);
        data.ApplyModifiedPropertiesWithoutUndo();
        var asset = PrefabUtility.SaveAsPrefabAsset(root, OutpostPath);
        UnityEngine.Object.DestroyImmediate(root);
        return asset;
    }
    static void Rock(Transform parent, string name, Vector2 position, Vector2 size, float rotation)
    {
        var sprite = Sprite("Assets/Images/Moon2.png");
        var renderer = Art(parent, name, sprite, position, size.x, -5);
        renderer.transform.localScale = new Vector3(size.x / sprite.bounds.size.x, size.y / sprite.bounds.size.y, 1f);
        renderer.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        renderer.color = new Color(0.6f, 0.65f, 0.68f);
        var collider = renderer.gameObject.AddComponent<PolygonCollider2D>();
        // Explicit outer hulls keep thin contour art and crater details out of the physics shape.
        collider.points = Enumerable.Range(0, 16).Select(i =>
        {
            float angle = i * Mathf.PI * 2f / 16f;
            float radius = i % 3 == 0 ? 0.94f : 1f;
            return new Vector2(Mathf.Cos(angle) * sprite.bounds.extents.x, Mathf.Sin(angle) * sprite.bounds.extents.y) * radius;
        }).ToArray();
        collider.offset = sprite.bounds.center;
    }
    static Collider2D MakeDelivery(Transform parent)
    {
        var zone = Shape(parent, "PassengerDeliveryPad", new Vector3(6.7f, 0.37f), new Vector2(6.4f, 0.42f), new Color(0.5f, 0.9f, 0.8f), 2);
        var collider = zone.gameObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        Text(parent, "PassengerDeliverySign", "[[cargo.delivery.sign]]", new Vector3(6.7f, -0.6f), 6.4f, 0.35f);
        for (int i = 0; i < 2; i++)
            Shape(parent, "DeliveryBeacon" + i, new Vector3(3.3f + i * 6.8f, 0.7f), new Vector2(0.12f, 0.3f), new Color(0.5f, 0.9f, 0.8f), 2);
        return collider;
    }
    static GameObject MakeHUD()
    {
        var root = new GameObject("CargoMissionHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CargoMissionUI));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 15;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        var panel = Rect(root.transform, "MissionActions", Vector2.zero, Vector2.zero);
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one;
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        var status = Rect(panel, "MissionStatus", new Vector2(0, -230f), new Vector2(950f, 115f));
        status.anchorMin = status.anchorMax = new Vector2(0.5f, 1f);
        var text = status.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font; text.fontSize = 26f; text.enableAutoSizing = true;
        text.fontSizeMin = 21f; text.fontSizeMax = 26f;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        text.text = "[[cargo.status.find]]";
        var data = new SerializedObject(root.GetComponent<CargoMissionUI>());
        Set(data, "panel", panel.gameObject); Set(data, "status", text);
        Set(data, "talkButton", Button(panel, "Talk", "[[cargo.action.talk]]", new Vector2(-205, 270)));
        Set(data, "connectButton", Button(panel, "TakeCable", "[[cargo.action.connect]]", new Vector2(205, 270)));
        Set(data, "attachButton", Button(panel, "AttachToShip", "[[cargo.action.attach]]", new Vector2(0, 270)));
        Set(data, "openButton", Button(panel, "OpenCapsule", "[[cargo.action.open]]", new Vector2(0, 270)));
        Set(data, "retryButton", Button(panel, "RetryMission", "[[cargo.action.retry]]", new Vector2(0, 270)));
        data.ApplyModifiedPropertiesWithoutUndo();
        var asset = PrefabUtility.SaveAsPrefabAsset(root, UIPath);
        UnityEngine.Object.DestroyImmediate(root);
        return asset;
    }
    static Button Button(Transform parent, string name, string label, Vector2 position)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ActionButton.prefab"), parent);
        root.name = name;
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(380, 100); rect.localScale = Vector3.one;
        var text = root.GetComponentInChildren<TMP_Text>(true);
        text.font = Font; text.text = label; text.enableAutoSizing = true;
        text.fontSizeMin = 22f; text.fontSizeMax = 30f; text.raycastTarget = false;
        var localized = text.GetComponent<LocalizedText>();
        if (localized)
        {
            var data = new SerializedObject(localized);
            data.FindProperty("source").stringValue = label;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var button = root.GetComponent<Button>();
        var serialized = new SerializedObject(button);
        serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").ClearArray();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }
    static void SetConversation(SerializedObject data, string field, (string message, string[] answers)[] pages)
    {
        var list = data.FindProperty(field);
        list.arraySize = pages.Length;
        for (int i = 0; i < pages.Length; i++)
        {
            var page = list.GetArrayElementAtIndex(i);
            page.FindPropertyRelative("message").stringValue = "[[" + pages[i].message + "]]";
            var answers = page.FindPropertyRelative("answers");
            answers.arraySize = pages[i].answers.Length;
            for (int j = 0; j < answers.arraySize; j++) answers.GetArrayElementAtIndex(j).stringValue = "[[" + pages[i].answers[j] + "]]";
        }
    }
    static Transform Child(Transform parent, string name, Vector3 position)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false); root.transform.localPosition = position;
        return root.transform;
    }
    static Sprite Sprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    static float VisibleBottom(Sprite sprite)
    {
        // The old building atlas has generous empty margins inside its sprite rectangles.
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        var pixels = texture.GetPixels32();
        var rect = sprite.rect;
        float result = sprite.bounds.min.y;
        for (int y = (int)rect.yMin; y < rect.yMax; y++)
        {
            bool found = false;
            for (int x = (int)rect.xMin; x < rect.xMax; x++)
            {
                var color = pixels[y * texture.width + x];
                if (color.a > 64 && Mathf.Max(color.r, Mathf.Max(color.g, color.b)) > 64) { found = true; break; }
            }
            if (!found) continue;
            result = (y - rect.yMin - sprite.pivot.y) / sprite.pixelsPerUnit;
            break;
        }
        UnityEngine.Object.DestroyImmediate(texture);
        return result;
    }
    static SpriteRenderer Art(Transform parent, string name, Sprite sprite, Vector3 position, float width, int order)
    {
        var root = Child(parent, name, position);
        var renderer = root.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite; renderer.sortingOrder = order;
        root.localScale = Vector3.one * (width / sprite.bounds.size.x);
        root.localScale = new Vector3(root.localScale.x, root.localScale.y, 1f);
        root.localPosition -= Vector3.Scale(sprite.bounds.center, root.localScale);
        return renderer;
    }
    static SpriteRenderer Shape(Transform parent, string name, Vector3 position, Vector2 size, Color color, int order)
    {
        var renderer = Art(parent, name, Square, position, size.x, order);
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        renderer.color = color;
        return renderer;
    }
    static void Text(Transform parent, string name, string source, Vector3 position, float width, float height)
    {
        var root = Child(parent, name, position);
        var text = root.gameObject.AddComponent<TextMeshPro>();
        text.font = Font; text.fontSize = height * 10f; text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(width, height * 2.5f);
        text.text = source; text.color = new Color(0.65f, 0.7f, 0.72f);
        text.GetComponent<MeshRenderer>().sortingOrder = 6;
        var localized = text.gameObject.AddComponent<LocalizedText>();
        var data = new SerializedObject(localized); data.FindProperty("source").stringValue = source;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var root = new GameObject(name, typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    static void Set(SerializedObject data, string field, UnityEngine.Object value) => data.FindProperty(field).objectReferenceValue = value;

    public static void ExportPreview()
    {
        // Render authored assets in Edit Mode; this never enters Play Mode or advances a savegame.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var outpost = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OutpostPath), scene);
        var capsule = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CapsulePath), scene);
        capsule.transform.position = outpost.GetComponent<AsteroidOutpost>().CargoSpawn.position;
        var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.015f, 0.022f, 0.032f);
        LocalizePreview();
        Render(camera, new Vector3(0, 0, -10), 195f, 1600, 1200, "Documentation/CargoAsteroidField.png");
        Render(camera, new Vector3(0, 15, -10), 26f, 1600, 1000, "Documentation/CargoOutpost.png");
        UnityEngine.Object.DestroyImmediate(outpost); UnityEngine.Object.DestroyImmediate(capsule);
        var passenger = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PassengerPath), scene);
        var bounds = passenger.GetComponentInChildren<CharacterVisual>().Bounds;
        Render(camera, bounds.center + new Vector3(0, 0, -10), bounds.extents.y + 0.25f, 700, 1000, "Documentation/Rhekk.png");
        UnityEngine.Object.DestroyImmediate(passenger);
        var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(UIPath), scene);
        var canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10f;
        var data = new SerializedObject(hud.GetComponent<CargoMissionUI>());
        foreach (string field in new[] { "attachButton", "openButton", "retryButton" })
            ((Button)data.FindProperty(field).objectReferenceValue).gameObject.SetActive(false);
        ((TMP_Text)data.FindProperty("status").objectReferenceValue).text = "[[cargo.status.connect]]";
        LocalizePreview();
        Render(camera, new Vector3(0, 0, -10), 5f, 1080, 1920, "Documentation/CargoHUD-Portrait.png");
        Render(camera, new Vector3(0, 0, -10), 5f, 1920, 1080, "Documentation/CargoHUD-Landscape.png");
        Debug.Log("Cargo mission Edit Mode previews exported.");
    }
    static void LocalizePreview()
    {
        var catalog = JsonUtility.FromJson<Localization.Language>(File.ReadAllText("Assets/Resources/Localization/de.json"))
            .entries.ToDictionary(entry => entry.key, entry => entry.text);
        foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string source = text.TryGetComponent<LocalizedText>(out var localized) ? localized.Source : text.text;
            text.text = System.Text.RegularExpressions.Regex.Replace(source, @"\[\[([^\]]+)\]\]",
                match => catalog.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
            text.ForceMeshUpdate(true);
        }
    }
    static void Render(Camera camera, Vector3 position, float zoom, int width, int height, string path)
    {
        var target = new RenderTexture(width, height, 24);
        camera.transform.position = position; camera.orthographicSize = zoom; camera.aspect = width / (float)height;
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        foreach (var scaler in UnityEngine.Object.FindObjectsByType<CanvasScaler>(FindObjectsSortMode.None)) scaler.SendMessage("Handle");
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
    }

    public static void FinalizeAssetsAndPreview()
    {
        MakePassenger(); MakeCapsule(); MakeOutpost(); MakeHUD();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        var mission = UnityEngine.Object.FindFirstObjectByType<CargoMission>(FindObjectsInactive.Include);
        var data = new SerializedObject(mission);
        foreach (string field in new[] { "capsule", "passenger", "deliverySurface", "hallDestination", "registrationDestination" })
            if (!data.FindProperty(field).objectReferenceValue) throw new InvalidOperationException("Missing authored mission reference: " + field);
        Debug.Log("Cargo mission scene references resolved after prefab authoring.");
        var interior = UnityEngine.Object.FindFirstObjectByType<StationInterior>(FindObjectsInactive.Include);
        var door = interior.GetComponentsInChildren<StationInteraction>(true)
            .First(interaction => interaction.action == StationInteraction.Action.RegistrationEntrance);
        var interiorData = new SerializedObject(interior);
        var hallFloor = (Collider2D)interiorData.FindProperty("hallFloor").objectReferenceValue;
        var registrationFloor = (Collider2D)interiorData.FindProperty("registrationFloor").objectReferenceValue;
        var hallGoal = (Transform)data.FindProperty("hallDestination").objectReferenceValue;
        hallGoal.position = new Vector3(door.transform.position.x, hallFloor.bounds.max.y, 0f);
        var registrationGoal = (Transform)data.FindProperty("registrationDestination").objectReferenceValue;
        Vector3 local = registrationGoal.localPosition;
        local.x = 6.5f; registrationGoal.localPosition = local;
        registrationGoal.position = new Vector3(registrationGoal.position.x, registrationFloor.bounds.max.y, 0f);
        EditorSceneManager.MarkSceneDirty(mission.gameObject.scene);
        EditorSceneManager.SaveScene(mission.gameObject.scene);
        ExportPreview();
    }
}
