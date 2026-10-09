using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NPCPhysicsAuthoring
{
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (LayerMask.NameToLayer(NPCMotor2D.CharacterLayer) < 0 || LayerMask.NameToLayer(NPCMotor2D.GroundLayer) < 0)
            throw new InvalidOperationException("Configure NPC and NPCGround layers before authoring.");
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => AssetDatabase.GetDependencies(path, true).Length).ToArray();
        foreach (string path in paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var npc in root.GetComponentsInChildren<StationResident>(true)) { Configure(npc.gameObject); changed = true; }
                foreach (var npc in root.GetComponentsInChildren<CargoPassenger>(true)) { Configure(npc.gameObject); changed = true; }
                foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
                    if (Walkable(collider))
                    {
                        collider.gameObject.layer = LayerMask.NameToLayer(NPCMotor2D.GroundLayer);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(collider.gameObject); changed = true;
                    }
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        foreach (var npc in UnityEngine.Object.FindObjectsByType<StationResident>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Configure(npc.gameObject);
        foreach (var npc in UnityEngine.Object.FindObjectsByType<CargoPassenger>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Configure(npc.gameObject);
        foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Walkable(collider))
            {
                collider.gameObject.layer = LayerMask.NameToLayer(NPCMotor2D.GroundLayer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider.gameObject);
            }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        BuildPreview();
        Debug.Log("NPC physics authored: dynamic bodies, ground-only collision layers, updated residents and stairs/floors.");
    }
    static void BuildPreview()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        for (int lane = 0; lane < 2; lane++)
        {
            float y = lane * 7f;
            var stairs = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/Stairs/InteriorCompactRight.prefab"), scene);
            stairs.transform.position = new Vector3(0f, y, 0f);
            Vector3 upper = stairs.transform.Find("UpperConnection").position;
            Floor(new Vector3(-4f, y - 0.1f), new Vector2(8f, 0.2f));
            Floor(upper + new Vector3(2f, -0.1f), new Vector2(4f, 0.2f));
            var npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Station01/StationResident.prefab"), scene);
            npc.name = lane == 0 ? "MinMax Patrol" : "Waypoint Patrol";
            npc.transform.position = new Vector3(-3f, y + 2f);
            var data = new SerializedObject(npc.GetComponent<StationResident>());
            data.FindProperty("residentId").intValue = 101 + lane;
            data.FindProperty("minX").floatValue = -3f;
            data.FindProperty("maxX").floatValue = upper.x + 3f;
            data.FindProperty("floorY").floatValue = y;
            data.FindProperty("patrolMode").enumValueIndex = lane;
            data.FindProperty("entersStationOnApproach").boolValue = false;
            data.FindProperty("comments").arraySize = 0;
            if (lane == 1)
            {
                var route = new GameObject("Patrol Waypoints").transform;
                var points = new[] { new Vector3(-3f, y), new Vector3(0f, y), upper, upper + Vector3.right * 3f };
                var property = data.FindProperty("waypoints"); property.arraySize = points.Length;
                for (int i = 0; i < points.Length; i++)
                {
                    var point = new GameObject("Waypoint " + (i + 1)).transform;
                    point.SetParent(route); point.position = points[i];
                    property.GetArrayElementAtIndex(i).objectReferenceValue = point;
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        var camera = new GameObject("Preview Camera").AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 8f;
        camera.transform.position = new Vector3(2f, 5f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/NPCPatrolPreview.unity");
    }
    static void Floor(Vector3 position, Vector2 size)
    {
        var floor = new GameObject("Floor", typeof(BoxCollider2D), typeof(SpriteRenderer));
        floor.layer = LayerMask.NameToLayer(NPCMotor2D.GroundLayer);
        floor.transform.position = position;
        floor.GetComponent<BoxCollider2D>().size = size;
        var renderer = floor.GetComponent<SpriteRenderer>();
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        renderer.drawMode = SpriteDrawMode.Sliced; renderer.size = size;
    }
    public static bool Walkable(Collider2D collider)
    {
        if (collider.isTrigger || collider.GetComponentInParent<NPCMotor2D>() || collider.GetComponentInParent<LanderController>()) return false;
        string name = collider.name;
        return collider.CompareTag("Moon") || collider.CompareTag("Landscape") || collider.CompareTag("LandingPad")
            || collider.GetComponent<StationLandingPad>() || collider.GetComponent<PlatformEffector2D>()
            || name == "Floor" || name == "Walkway" || name == "OutpostDeck" || name == "LandingPlatform"
            || name.StartsWith("Walkway_L") || name.StartsWith("Ramp_L");
    }
    public static void Configure(GameObject root)
    {
        root.layer = LayerMask.NameToLayer(NPCMotor2D.CharacterLayer);
        var motor = root.GetComponent<NPCMotor2D>(); if (!motor) motor = root.AddComponent<NPCMotor2D>();
        var body = root.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic; body.gravityScale = 0f; body.mass = 1f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation; body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous; body.linearDamping = 0f;
        var shape = root.GetComponent<CapsuleCollider2D>(); shape.direction = CapsuleDirection2D.Vertical; shape.isTrigger = false;
        Vector3 min = new(float.PositiveInfinity, float.PositiveInfinity, 0f), max = new(float.NegativeInfinity, float.NegativeInfinity, 0f);
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (!renderer.enabled || !renderer.sprite) continue;
            var bounds = renderer.sprite.bounds;
            foreach (var point in new[] { bounds.min, bounds.max, new Vector3(bounds.min.x,bounds.max.y),new Vector3(bounds.max.x,bounds.min.y) })
            {
                Vector3 local = root.transform.InverseTransformPoint(renderer.transform.TransformPoint(point));
                min = Vector3.Min(min, local); max = Vector3.Max(max, local);
            }
        }
        if (!float.IsPositiveInfinity(min.y))
        {
            shape.size = new Vector2(Mathf.Max(0.2f, (max.x - min.x) * 0.4f), Mathf.Max(0.3f, max.y - min.y));
            shape.offset = new Vector2(0f, (min.y + max.y) * 0.5f);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        PrefabUtility.RecordPrefabInstancePropertyModifications(shape);
        PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
    }
}
