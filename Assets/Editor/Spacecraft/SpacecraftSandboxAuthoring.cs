using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class SpacecraftSandboxAuthoring
{
    const string Folder = "Assets/Prefabs/Spacecraft/AssistedFlight";
    const string Images = "Assets/Images/Spacecraft/AssistedFlight";
    const string ScenePath = "Assets/Scenes/SpacecraftFlightSandbox.unity";
    static readonly string[] Names = { "Courier", "Scout", "CargoTug", "Freighter", "Rescue", "Maintenance" };
    static readonly string[] Labels = { "Courier", "Scout", "Schlepper", "Frachter", "Rettungsschiff", "Wartungsschiff" };
    // Width, mass, speed, acceleration, braking, lateral thrust, turning, tank, fuel burn.
    static readonly float[][] Tuning = {
        new[] { 10f, 1f, 12f, 9f, 14f, 9f, 180f, 100f, .55f },
        new[] { 12f, .8f, 17f, 11f, 16f, 10f, 220f, 140f, .45f },
        new[] { 11f, 2.5f, 8f, 12f, 16f, 11f, 120f, 160f, .9f },
        new[] { 18f, 5f, 7f, 4.5f, 8f, 4f, 70f, 240f, 1.2f },
        new[] { 12f, 2f, 10f, 7f, 14f, 8f, 140f, 160f, .65f },
        new[] { 11f, 1.8f, 7f, 6f, 15f, 10f, 150f, 130f, .6f }
    };
    static Material lineMaterial;
    static Material blackMaterial;
    static Sprite square;
    static TMP_FontAsset font;

    [MenuItem("Tools/Spacecraft/Build Flight Sandbox")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolder(Folder); EnsureFolder(Images); EnsureFolder(Images + "/Sprites");
        EnsureFolder("Assets/Materials/Spacecraft");
        lineMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/World/OutlineWhite.mat");
        blackMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/World/OutlineBlack.mat");
        square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
        var outline = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Spacecraft/AssistedOutline.mat");
        if (!outline)
        {
            outline = new Material(Shader.Find("MoonLander/SpacecraftOutline"));
            AssetDatabase.CreateAsset(outline, "Assets/Materials/Spacecraft/AssistedOutline.mat");
        }
        for (int i = 0; i < Names.Length; i++) BuildShip(i, outline);
        BuildPad(); BuildPilot();
        BuildStation(); BuildButtonPrefab(); BuildScene(); AssetDatabase.SaveAssets();
        AddClassicShips();
        if(Names.All(name=>File.Exists("Assets/Images/Spacecraft/ApprovedFleet/"+name+".png"))) ApprovedFleetAuthoring.Build();
        Debug.Log("Spacecraft sandbox authored: 13 assisted-flight ships and 19 pads. No gameplay tests executed.");
    }
    [MenuItem("Tools/Spacecraft/Add Classic Ships To Sandbox")]
    public static void AddClassicShips()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        lineMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/World/OutlineWhite.mat");
        square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/DejaVuSansMono SDF.asset");
        for (int i=1;i<=7;i++) BuildClassicShip(i);
        ConfigureClassicFleet();
        var station=PrefabUtility.LoadPrefabContents(Folder+"/FlightTestStation.prefab");
        try
        {
            var bays=station.transform.Find("DockingBays");
            if (!bays.Find("Pad 19"))
            {
                for (int i=0;i<2;i++)
                {
                    var pad=bays.Find($"Pad {11+i:00}");
                    pad.name=$"Pad {18+i:00}"; pad.localPosition=new Vector3(299f+i*26f,0f);
                    pad.GetComponentInChildren<TextMeshPro>().text=$"PAD {18+i:00}";
                }
                for (int i=0;i<7;i++)
                {
                    float x=117f+i*26f;
                    var pad=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/DockingBay.prefab"));
                    pad.name=$"Pad {11+i:00}"; pad.transform.SetParent(bays,false); pad.transform.localPosition=new Vector3(x,0f);
                    WorldText(pad.transform,"Pad label",$"PAD {11+i:00}",new Vector3(0f,-1.9f),.7f);
                    Walkway(station.transform,new Vector3(x-13f,0f),4f);
                }
                for (int i=12;i<19;i++) Line(station.transform,"Bay support "+i,new[] {new Vector3(-143f+i*26f,-3f),new Vector3(-143f+i*26f,-7f)});
                var spine=station.transform.Find("Service spine").GetComponent<LineRenderer>();
                spine.SetPositions(new[] {new Vector3(-154f,-7f),new Vector3(336f,-7f),new Vector3(336f,-3f),new Vector3(-154f,-3f),new Vector3(-154f,-7f)});
                station.transform.Find("Station title").localPosition=new Vector3(91f,-10f);
                PrefabUtility.SaveAsPrefabAsset(station,Folder+"/FlightTestStation.prefab");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(station); }
        var scene=EditorSceneManager.OpenScene(ScenePath);
        var sandbox=UnityEngine.Object.FindFirstObjectByType<SpacecraftSandbox>();
        var stationRoot=scene.GetRootGameObjects().First(root=>root.name=="FlightTestStation");
        int newShipCount=UnityEngine.Object.FindObjectsByType<AssistedSpacecraft>(FindObjectsSortMode.None).Count(ship=>!ship.IsClassic);
        for(int i=1;i<=7;i++)
        {
            string name=$"{i+newShipCount:00} Classic Lander{i:00}";
            if(!UnityEngine.Object.FindObjectsByType<AssistedSpacecraft>(FindObjectsSortMode.None).Any(ship=>ship.IsClassic && ship.displayName==$"Lander {i:00}"))
            {
                var ship=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+$"/ClassicLander{i:00}.prefab"));
                ship.name=name;
                var controller=ship.GetComponent<AssistedSpacecraft>();
                controller.homePad=stationRoot.transform.Find($"DockingBays/Pad {i+10:00}").GetComponent<ShipDockingPad>();
                ship.transform.position=controller.homePad.dockPoint.position-Vector3.up*controller.footOffset+Vector3.up*.03f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller); PrefabUtility.RecordPrefabInstancePropertyModifications(ship.transform);
            }
            string label=$"Lander {i:00}";
            if(!sandbox.hudButtons.Find(label))
            {
                var button=Button(sandbox.hudButtons,label); button.transform.SetSiblingIndex(i+newShipCount-1);
                UnityEventTools.AddIntPersistentListener(button.onClick,sandbox.SelectShip,i+newShipCount-1);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        RenderPreview(sandbox.view);
        Debug.Log("Classic ships added: original graphics/colliders preserved in separate assisted-flight copies; 13 ships, 19 pads, 13 selection buttons.");
    }
    static void BuildClassicShip(int number)
    {
        string path=Folder+$"/ClassicLander{number:00}.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Lander/Lander{number:00}.prefab"));
        PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        root.name=$"ClassicLander{number:00}"; root.tag="Untagged";
        UnityEngine.Object.DestroyImmediate(root.GetComponent<LanderController>());
        foreach(Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        foreach(var collider in root.GetComponents<Collider2D>()) if(collider.isTrigger) UnityEngine.Object.DestroyImmediate(collider);
        var renderer=root.GetComponent<SpriteRenderer>(); renderer.sortingOrder=5;
        float scale=(5f+(number%3)*.5f)/renderer.sprite.bounds.size.x;
        root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); root.transform.localScale=Vector3.one*scale;
        var body=root.GetComponent<Rigidbody2D>(); body.gravityScale=0f; body.constraints=RigidbodyConstraints2D.None;
        var shape=root.GetComponent<PolygonCollider2D>(); shape.isTrigger=false;
        float bottom=float.PositiveInfinity;
        for(int p=0;p<shape.pathCount;p++) foreach(var point in shape.GetPath(p)) bottom=Mathf.Min(bottom,point.y+shape.offset.y);
        var ship=root.AddComponent<AssistedSpacecraft>(); ship.displayName=$"Lander {number:00}";
        ship.forwardAngle=90f; ship.footOffset=bottom*scale;
        ship.maxSpeed=8f+number; ship.acceleration=6f+number*.5f; ship.braking=10f+number;
        ship.maneuverAcceleration=6f+number*.4f; ship.turnSpeed=110f+number*12f;
        ship.fuelCapacity=80f+number*10f; ship.fuelBurnPerSecond=.4f+number*.05f;
        ship.thrustIndicators=new SpriteRenderer[4];
        Vector3 extents=renderer.sprite.bounds.extents;
        var positions=new[] {new Vector2(-extents.x-.15f,0f),new Vector2(extents.x+.15f,0f),new Vector2(0f,bottom-.15f),new Vector2(0f,extents.y+.15f)};
        for(int i=0;i<4;i++)
        {
            var effect=new GameObject("Thrust "+i,typeof(SpriteRenderer)); effect.transform.SetParent(root.transform,false); effect.transform.localPosition=positions[i];
            var sr=effect.GetComponent<SpriteRenderer>(); sr.sprite=square; sr.drawMode=SpriteDrawMode.Sliced;
            sr.size=i<2?new Vector2(.25f,.05f):new Vector2(.05f,.25f); sr.sortingOrder=6; sr.enabled=false;
            ship.thrustIndicators[i]=sr;
        }
        PrefabUtility.SaveAsPrefabAsset(root,path); UnityEngine.Object.DestroyImmediate(root);
    }
    [MenuItem("Tools/Spacecraft/Use Original Lander Controls")]
    public static void ConfigureClassicFleet()
    {
        for(int i=1;i<=7;i++)
        {
            string path=Folder+$"/ClassicLander{i:00}.prefab";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Lander/Lander{i:00}.prefab");
            var original=source.GetComponent<LanderController>();
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var flight=root.GetComponent<ClassicLanderFlight>(); if(!flight) flight=root.AddComponent<ClassicLanderFlight>();
                var controller=root.GetComponent<LanderController>(); if(!controller) controller=root.AddComponent<LanderController>();
                controller.sandboxControlled=true; controller.enabled=false; controller.isActive=false;
                root.transform.localScale=source.transform.localScale;
                flight.thrustForce=original.thrustForce; flight.rotationSpeed=original.rotationSpeed; flight.rotationSmooth=original.rotationSmooth;
                flight.steeringDeadzone=original.steeringDeadzone; flight.steeringRange=original.steeringRange; flight.maxSteer=original.maxSteer;
                flight.steerResponse=original.steerResponse; flight.maxFallSpeed=Mathf.Abs(original.maxFallSpeed);
                flight.gravityScale=source.GetComponent<Rigidbody2D>().gravityScale; flight.safeVerticalSpeed=original.safeVerticalSpeed;
                var ship=root.GetComponent<AssistedSpacecraft>(); ship.flightAssist=false;
                ship.fuelBurnPerSecond=original.fuelBurnPerSec; ship.safeLandingSpeed=original.safeSpeed; ship.safeLandingAngle=original.safeAngleDeg;
                float bottom=float.PositiveInfinity;
                var collider=root.GetComponent<PolygonCollider2D>();
                for(int p=0;p<collider.pathCount;p++) foreach(var point in collider.GetPath(p)) bottom=Mathf.Min(bottom,point.y+collider.offset.y);
                ship.footOffset=bottom*root.transform.localScale.y;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        var scene=EditorSceneManager.OpenScene(ScenePath);
        foreach(var ship in UnityEngine.Object.FindObjectsByType<AssistedSpacecraft>(FindObjectsSortMode.None))
            if(ship.IsClassic && ship.homePad)
            {
                ship.transform.position=ship.homePad.dockPoint.position-ship.homePad.transform.up*ship.footOffset+ship.homePad.transform.up*.03f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(ship.transform);
            }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Seven classic test ships configured with original thrust/steering equations and original prefab flight values; new spacecraft unchanged.");
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static Sprite ShipSprite(int index)
    {
        string path = Images + "/" + Names[index] + ".png";
        string source = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Documentation/AssistedSpacecraft/" + Names[index] + ".png");
        if (!File.Exists(path)) File.Copy(source, path);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default; importer.isReadable = true;
        importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048;
        importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var pixels = texture.GetPixels32();
        int minX = texture.width, minY = texture.height, maxX = 0, maxY = 0;
        for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
        {
            var pixel = pixels[y * texture.width + x];
            if (Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) < 90) continue;
            minX = Mathf.Min(x, minX); minY = Mathf.Min(y, minY); maxX = Mathf.Max(x, maxX); maxY = Mathf.Max(y, maxY);
        }
        if (minX >= maxX || minY >= maxY) throw new InvalidOperationException("Empty spacecraft art: " + path);
        minX = Mathf.Max(0, minX - 3); minY = Mathf.Max(0, minY - 3);
        maxX = Mathf.Min(texture.width - 1, maxX + 3); maxY = Mathf.Min(texture.height - 1, maxY + 3);
        var sprite = Sprite.Create(texture, new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1), Vector2.one * .5f,
            (maxX - minX + 1) / Tuning[index][0], 0, SpriteMeshType.FullRect);
        sprite.name = Names[index];
        string spritePath = Images + "/Sprites/" + Names[index] + ".asset";
        var previous = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (previous) { EditorUtility.CopySerialized(sprite, previous); UnityEngine.Object.DestroyImmediate(sprite); return previous; }
        AssetDatabase.CreateAsset(sprite, spritePath); return sprite;
    }
    static void BuildShip(int index, Material outline)
    {
        Sprite sprite = ShipSprite(index);
        float[] settings = Tuning[index]; float width = sprite.bounds.size.x, height = sprite.bounds.size.y;
        var root = new GameObject(Names[index], typeof(Rigidbody2D), typeof(PolygonCollider2D), typeof(AssistedSpacecraft));
        var ship = root.GetComponent<AssistedSpacecraft>();
        ship.displayName = Labels[index]; ship.footOffset = -height * .5f;
        ship.maxSpeed = settings[2]; ship.acceleration = settings[3]; ship.braking = settings[4];
        ship.maneuverAcceleration = settings[5]; ship.turnSpeed = settings[6]; ship.fuelCapacity = settings[7]; ship.fuelBurnPerSecond = settings[8];
        ship.approachSpeed = index == 3 ? 2f : 3f; ship.approachDescentSpeed = index == 3 ? .6f : .8f;
        var body = root.GetComponent<Rigidbody2D>(); body.mass = settings[1]; body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        var points = new[] { new Vector2(-width*.5f,-height*.24f), new Vector2(-width*.5f,height*.22f),
            new Vector2(-width*.32f,height*.38f),new Vector2(width*.23f,height*.38f),new Vector2(width*.5f,-height*.03f),
            new Vector2(width*.3f,-height*.30f),new Vector2(-width*.3f,-height*.30f) };
        root.GetComponent<PolygonCollider2D>().points = points;
        var mesh = new Mesh { name = Names[index] + " hull silhouette" };
        mesh.vertices = points.Select(point => (Vector3)point).ToArray();
        mesh.triangles = Enumerable.Range(1, points.Length - 2).SelectMany(i => new[] { 0, i + 1, i }).ToArray();
        mesh.RecalculateBounds();
        string meshPath = Images + "/Sprites/" + Names[index] + "Hull.asset";
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existingMesh) { EditorUtility.CopySerialized(mesh, existingMesh); UnityEngine.Object.DestroyImmediate(mesh); mesh = existingMesh; }
        else AssetDatabase.CreateAsset(mesh, meshPath);
        var fill = new GameObject("Black hull", typeof(MeshFilter), typeof(MeshRenderer)); fill.transform.SetParent(root.transform, false);
        fill.GetComponent<MeshFilter>().sharedMesh = mesh; fill.GetComponent<MeshRenderer>().sharedMaterial = blackMaterial;
        fill.GetComponent<MeshRenderer>().sortingOrder = 4;
        var art = new GameObject("Outline art", typeof(SpriteRenderer)); art.transform.SetParent(root.transform, false);
        var renderer = art.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = outline; renderer.sortingOrder = 5;
        float footX = width * (index == 1 ? .14f : .23f);
        for (int side = -1; side <= 1; side += 2)
        {
            var foot = new GameObject(side < 0 ? "Rear foot collider" : "Front foot collider", typeof(BoxCollider2D));
            foot.transform.SetParent(root.transform, false); foot.transform.localPosition = new Vector3(side * footX, ship.footOffset + .1f);
            foot.GetComponent<BoxCollider2D>().size = new Vector2(.55f, .2f);
        }
        ship.thrustIndicators = new SpriteRenderer[4];
        var positions = new[] { new Vector2(-width*.51f,0f),new Vector2(width*.51f,0f),new Vector2(0f,-height*.38f),new Vector2(0f,height*.42f) };
        for (int i = 0; i < 4; i++)
        {
            var effect = new GameObject("Thrust " + i, typeof(SpriteRenderer)); effect.transform.SetParent(root.transform, false); effect.transform.localPosition = positions[i];
            var sr = effect.GetComponent<SpriteRenderer>(); sr.sprite = square; sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = i < 2 ? new Vector2(.55f,.1f) : new Vector2(.1f,.5f); sr.sortingOrder = 6; sr.enabled = false;
            ship.thrustIndicators[i] = sr;
        }
        Marker(root.transform, "ExitHatch", new Vector3(0f, -height*.3f));
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + Names[index] + ".prefab"); UnityEngine.Object.DestroyImmediate(root);
    }
    static void BuildPad()
    {
        var root = new GameObject("DockingBay", typeof(BoxCollider2D), typeof(ShipDockingPad));
        root.layer = LayerMask.NameToLayer(NPCMotor2D.GroundLayer);
        var collider = root.GetComponent<BoxCollider2D>(); collider.size = new Vector2(22f,.4f); collider.offset = Vector2.down*.2f;
        var pad = root.GetComponent<ShipDockingPad>(); pad.dockPoint = Marker(root.transform, "DockPoint", Vector3.zero);
        pad.exitPoint = Marker(root.transform, "ExitPoint", new Vector3(9f,0f));
        Marker(root.transform,"LeftConnection",new Vector3(-11f,0f)); Marker(root.transform,"RightConnection",new Vector3(11f,0f));
        Line(root.transform, "Pad rim", new[] { new Vector3(-11f,0f), new Vector3(11f,0f),new Vector3(11f,-1.2f),new Vector3(-11f,-1.2f),new Vector3(-11f,0f) });
        Line(root.transform, "Left brace", new[] { new Vector3(-9f,-1.2f),new Vector3(-5f,-3f),new Vector3(-1f,-1.2f) });
        Line(root.transform, "Right brace", new[] { new Vector3(1f,-1.2f),new Vector3(5f,-3f),new Vector3(9f,-1.2f) });
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/DockingBay.prefab"); UnityEngine.Object.DestroyImmediate(root);
    }
    static void BuildPilot()
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Astronaut.prefab"));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        UnityEngine.Object.DestroyImmediate(root.GetComponent<AstronautMoonController>());
        foreach (var collider in root.GetComponentsInChildren<Collider2D>()) if (collider.isTrigger) UnityEngine.Object.DestroyImmediate(collider);
        root.name = "SandboxPilot"; root.layer = LayerMask.NameToLayer(NPCMotor2D.CharacterLayer);
        root.AddComponent<SpacecraftSandboxPilot>();
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/SandboxPilot.prefab"); UnityEngine.Object.DestroyImmediate(root);
    }
    static void BuildStation()
    {
        var station = new GameObject("FlightTestStation");
        var pads = new GameObject("DockingBays").transform; pads.SetParent(station.transform, false);
        for (int bay = 0; bay < 12; bay++)
        {
            var pad = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/DockingBay.prefab"));
            float x = (bay - 5.5f) * 26f;
            pad.name = $"Pad {bay + 1:00}"; pad.transform.SetParent(pads, false); pad.transform.localPosition = new Vector3(x,0f);
            if (bay < 9) Walkway(station.transform, new Vector3(x+13f,0f), 4f);
            if (bay >= 10)
            {
                pad.transform.localRotation = Quaternion.Euler(0f,0f,bay == 10 ? 90f : 180f);
                pad.GetComponent<ShipDockingPad>().allowExit = false;
            }
            WorldText(pad.transform, "Pad label", "PAD " + (bay+1).ToString("00"), new Vector3(0f,-1.9f), .7f);
            Line(station.transform,"Bay support " + bay,new[] {new Vector3(x,-3f),new Vector3(x,-7f)});
        }
        Decoration(station.transform,"FuelTanks",new Vector3(-91f,-5f),5f);
        Decoration(station.transform,"Airlock",new Vector3(-39f,-5f),6f);
        Decoration(station.transform,"Workshop",new Vector3(39f,-5f),8f);
        Decoration(station.transform,"Antenna",new Vector3(91f,-5f),4f);
        Line(station.transform,"Service spine",new[] {new Vector3(-154f,-7f),new Vector3(154f,-7f),new Vector3(154f,-3f),new Vector3(-154f,-3f),new Vector3(-154f,-7f)});
        WorldText(station.transform,"Station title","FLIGHT TEST STATION",new Vector3(0f,-10f),1.2f);
        PrefabUtility.SaveAsPrefabAsset(station, Folder + "/FlightTestStation.prefab"); UnityEngine.Object.DestroyImmediate(station);
    }
    static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var station = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/FlightTestStation.prefab"));
        var pads = station.GetComponentsInChildren<ShipDockingPad>().OrderBy(pad => pad.name).ToArray();
        for (int i = 0; i < Names.Length; i++)
        {
            int padIndex = i;
            var ship = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + Names[i] + ".prefab"));
            ship.name = $"{i+1:00} {Names[i]}";
            var controller = ship.GetComponent<AssistedSpacecraft>(); controller.homePad = pads[padIndex];
            ship.transform.position = pads[padIndex].dockPoint.position - pads[padIndex].transform.up * controller.footOffset + Vector3.up*.03f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ship.transform);
        }
        var pilot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SandboxPilot.prefab")); pilot.SetActive(false);
        var camera = new GameObject("Sandbox Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.orthographic = true; camera.orthographicSize = 26f; camera.transform.position = new Vector3(-143f,4f,-10f);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var sandbox = new GameObject("Spacecraft Sandbox").AddComponent<SpacecraftSandbox>(); sandbox.view = camera; sandbox.pilot = pilot.GetComponent<SpacecraftSandboxPilot>();
        BuildUI(sandbox);
        var random = new System.Random(9365);
        var stars = new GameObject("Stars").transform;
        for (int i = 0; i < 240; i++)
        {
            var star = new GameObject("Star",typeof(SpriteRenderer)); star.transform.SetParent(stars,false);
            star.transform.position = new Vector3((float)random.NextDouble()*420f-210f,(float)random.NextDouble()*150f-65f,0f);
            var renderer = star.GetComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sortingOrder=-50;
            star.transform.localScale = Vector3.one * (i%5==0 ? .035f:.018f);
        }
        EditorSceneManager.SaveScene(scene, ScenePath);
        RenderPreview(camera);
    }
    static void RenderPreview(Camera camera)
    {
        // Editor render only; does not enter Play Mode or simulate physics.
        Vector3 savedPosition = camera.transform.position; float savedSize = camera.orthographicSize;
        camera.transform.position = new Vector3(91f,-1f,-10f); camera.orthographicSize=155f;
        var target = new RenderTexture(1920,1080,24); camera.targetTexture = target;
        var canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1f;
        Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination = target });
        RenderTexture.active=target; var image = new Texture2D(1920,1080,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply();
        Directory.CreateDirectory("Documentation/AssistedSpacecraft"); File.WriteAllBytes("Documentation/AssistedSpacecraft/FlightSandbox.png",image.EncodeToPNG());
        camera.targetTexture=null; RenderTexture.active=null; UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
        canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null;
        camera.transform.position=savedPosition; camera.orthographicSize=savedSize;
    }
    static void BuildUI(SpacecraftSandbox sandbox)
    {
        var canvas = new GameObject("Sandbox HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720); scaler.matchWidthOrHeight=.5f;
        var panel=Rect(canvas.transform,"Top panel",new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(0f,-64f),new Vector2(0f,128f));
        sandbox.hudTop=panel;
        panel.gameObject.AddComponent<Image>().color=new Color(0f,0f,0f,.9f);
        sandbox.status=Text(panel,"Ship status","COURIER | GEDOCKT / TANKEN",new Vector2(0f,1f),new Vector2(1f,1f),new Vector2(0f,-43f),new Vector2(-32f,78f),20f);
        Text(panel,"Controls","WASD / Pfeile / Maus halten: Flug | Loslassen / Leertaste: Bremsen\nQ/E: Drehen | F: Ein-/Aussteigen | Tab: Schiffwechsel | R: Rücksetzen | Mausrad: Zoom",
            new Vector2(0f,0f),new Vector2(1f,0f),new Vector2(0f,24f),new Vector2(-32f,45f),15f);
        var bottom=Rect(canvas.transform,"Buttons",new Vector2(0f,0f),new Vector2(1f,0f),new Vector2(0f,64f),new Vector2(-24f,112f));
        bottom.pivot=new Vector2(.5f,0f); bottom.anchoredPosition=new Vector2(0f,12f);
        sandbox.hudButtons=bottom;
        var grid=bottom.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize=new Vector2(196f,46f); grid.spacing=new Vector2(8f,8f);
        grid.constraint=GridLayoutGroup.Constraint.Flexible; grid.childAlignment=TextAnchor.MiddleCenter;
        bottom.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        for (int i=0;i<6;i++) { int index=i; var button=Button(bottom,Labels[i]); UnityEventTools.AddIntPersistentListener(button.onClick,sandbox.SelectShip,index); }
        sandbox.interactionButton=Button(bottom,"Aussteigen (F)"); sandbox.interactionLabel=sandbox.interactionButton.GetComponentInChildren<TMP_Text>();
        UnityEventTools.AddPersistentListener(sandbox.interactionButton.onClick,sandbox.Interact);
        var reset=Button(bottom,"Rücksetzen (R)"); UnityEventTools.AddPersistentListener(reset.onClick,sandbox.ResetSelected);
        sandbox.assistButton=Button(bottom,"Flugassistenz: AN"); sandbox.assistLabel=sandbox.assistButton.GetComponentInChildren<TMP_Text>();
        UnityEventTools.AddPersistentListener(sandbox.assistButton.onClick,sandbox.ToggleAssist);
        new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
    }
    static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin=min; rect.anchorMax=max; rect.pivot=Vector2.one*.5f; rect.anchoredPosition=position; rect.sizeDelta=size; return rect;
    }
    static TMP_Text Text(Transform parent,string name,string content,Vector2 min,Vector2 max,Vector2 position,Vector2 size,float fontSize)
    {
        var rect=Rect(parent,name,min,max,position,size); var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font; text.text=content; text.fontSize=fontSize; text.color=Color.white; text.raycastTarget=false;
        text.alignment=TextAlignmentOptions.Left; return text;
    }
    static Button Button(Transform parent,string label)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/SandboxButton.prefab"));
        root.name=label; root.transform.SetParent(parent,false);
        root.GetComponentInChildren<TMP_Text>().text=label;
        return root.GetComponent<Button>();
    }
    static void BuildButtonPrefab()
    {
        var rect=Rect(null,"SandboxButton",Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(196f,46f));
        var image=rect.gameObject.AddComponent<Image>(); image.color=new Color(.12f,.12f,.12f,1f);
        var button=rect.gameObject.AddComponent<Button>(); button.targetGraphic=image;
        var text=Text(rect,"Label","Button",Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-12f,-6f),16f); text.alignment=TextAlignmentOptions.Center;
        PrefabUtility.SaveAsPrefabAsset(rect.gameObject,Folder+"/SandboxButton.prefab"); UnityEngine.Object.DestroyImmediate(rect.gameObject);
    }
    static Transform Marker(Transform parent,string name,Vector3 position)
    {
        var marker=new GameObject(name).transform; marker.SetParent(parent,false); marker.localPosition=position; return marker;
    }
    static void Line(Transform parent,string name,Vector3[] points)
    {
        var line=new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent,false);
        line.useWorldSpace=false; line.sharedMaterial=lineMaterial; line.startWidth=line.endWidth=.09f;
        line.positionCount=points.Length; line.SetPositions(points); line.sortingOrder=1;
    }
    static void Walkway(Transform parent,Vector3 position,float width)
    {
        var floor=new GameObject("Walkway",typeof(BoxCollider2D)); floor.transform.SetParent(parent,false); floor.transform.localPosition=position;
        floor.layer=LayerMask.NameToLayer(NPCMotor2D.GroundLayer); var box=floor.GetComponent<BoxCollider2D>(); box.size=new Vector2(width,.2f); box.offset=Vector2.down*.1f;
        Line(floor.transform,"Walkway rim",new[] {new Vector3(-width*.5f,0f),new Vector3(width*.5f,0f)});
    }
    static void WorldText(Transform parent,string name,string content,Vector3 position,float size)
    {
        var text=new GameObject(name).AddComponent<TextMeshPro>(); text.transform.SetParent(parent,false); text.transform.localPosition=position;
        text.font=font; text.text=content; text.fontSize=size*10f; text.alignment=TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta=new Vector2(30f,2f); text.GetComponent<MeshRenderer>().sortingOrder=2;
    }
    static void Decoration(Transform parent,string name,Vector3 position,float width)
    {
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/Modules/"+name+".prefab"));
        root.transform.SetParent(parent,false); root.transform.localPosition=position;
        var renderer=root.GetComponentInChildren<SpriteRenderer>();
        root.transform.localScale=Vector3.one*(width/renderer.sprite.bounds.size.x);
        renderer.sortingOrder=-2;
        foreach(var collider in root.GetComponentsInChildren<Collider2D>()) collider.enabled=false;
    }
}
