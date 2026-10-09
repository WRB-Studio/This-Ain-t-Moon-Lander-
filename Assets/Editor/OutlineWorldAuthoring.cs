using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OutlineWorldAuthoring
{
    const string Asteroids = "Assets/Prefabs/World/Asteroids";
    const string Geometry = "Assets/Images/World/Geometry";
    static Material Black => Material("OutlineBlack", Color.black);
    static Material White => Material("OutlineWhite", Color.white);

    static Material Material(string name, Color color)
    {
        string path = "Assets/Materials/World/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material, path); }
        material.mainTexture = null; material.color = color; EditorUtility.SetDirty(material);
        return material;
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
    static Transform Child(Transform parent, string name)
    {
        var root = new GameObject(name); root.transform.SetParent(parent, false); return root.transform;
    }
    static void Body(Transform parent, string name, Vector2[] points, int order)
    {
        var mesh = new Mesh { name = name };
        var vertices = new Vector3[points.Length + 1]; var triangles = new int[points.Length * 3];
        for (int i = 0; i < points.Length; i++)
        {
            vertices[i + 1] = points[i];
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % points.Length + 1;
        }
        mesh.vertices = vertices; mesh.triangles = triangles;
        mesh.colors = Enumerable.Repeat(Color.white, vertices.Length).ToArray(); mesh.RecalculateBounds();
        string path = Geometry + "/" + name + ".asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (saved) { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); mesh = saved; }
        else AssetDatabase.CreateAsset(mesh, path);
        var root = Child(parent, "BlackBody");
        root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = Black; renderer.sortingOrder = order;
    }
    static void Stroke(Transform parent, string name, Vector2[] points, float width, int order, bool loop = true)
    {
        var line = Child(parent, name).gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = White; line.useWorldSpace = false; line.loop = loop;
        line.positionCount = points.Length; line.SetPositions(points.Select(point => (Vector3)point).ToArray());
        line.startWidth = line.endWidth = width; line.numCornerVertices = 3; line.numCapVertices = 3; line.sortingOrder = order;
    }
    static void Crater(Transform parent, string name, Vector2 center, Vector2 radii, float width, int order)
    {
        var points = Enumerable.Range(0, 96).Select(i =>
        {
            float angle = i * Mathf.PI * 2f / 96f;
            return center + new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
        }).ToArray();
        Stroke(parent, name, points, width, order);
    }
    static GameObject Rock(string name, Vector2[] points)
    {
        var root = new GameObject(name, typeof(PolygonCollider2D));
        root.GetComponent<PolygonCollider2D>().points = points;
        Body(root.transform, name, points, -6); Stroke(root.transform, "Contour", points, 0.025f, -5);
        Crater(root.transform, "CraterA", new Vector2(-0.3f, 0.18f), Vector2.one * 0.18f, 0.017f, -5);
        Crater(root.transform, "CraterB", new Vector2(0.4f, -0.25f), Vector2.one * 0.12f, 0.017f, -5);
        var asset = PrefabUtility.SaveAsPrefabAsset(root, Asteroids + "/" + name + ".prefab");
        Object.DestroyImmediate(root); return asset;
    }
    public static void BuildAsteroidField(Transform field)
    {
        Folder(Asteroids); Folder(Geometry);
        var variants = new[]
        {
            Rock("AsteroidChunky", new Vector2[] { new(-1,0),new(-.75f,.65f),new(-.3f,.98f),new(.25f,.92f),new(.85f,.6f),new(1,.05f),new(.67f,-.75f),new(-.1f,-1),new(-.7f,-.7f) }),
            Rock("AsteroidAngular", new Vector2[] { new(-1,-.3f),new(-.9f,.5f),new(-.35f,.88f),new(.2f,1),new(.95f,.35f),new(.73f,-.48f),new(.1f,-.84f),new(-.68f,-.9f) }),
            Rock("AsteroidLong", new Vector2[] { new(-1.4f,-.28f),new(-1.22f,.24f),new(-.8f,.52f),new(-.25f,.61f),new(.43f,.48f),new(1.35f,.14f),new(1.1f,-.39f),new(.52f,-.55f),new(-.38f,-.62f),new(-1.1f,-.5f) }),
            Rock("AsteroidNotched", new Vector2[] { new(-1,.1f),new(-.78f,.67f),new(-.22f,1),new(.3f,.83f),new(.25f,.23f),new(.8f,.52f),new(1,.14f),new(.83f,-.58f),new(.15f,-.88f),new(-.6f,-.65f) }),
            Rock("AsteroidTwin", new Vector2[] { new(-1.3f,.15f),new(-1.12f,.72f),new(-.55f,1),new(-.08f,.41f),new(.41f,.84f),new(.94f,.67f),new(1.2f,.18f),new(.9f,-.51f),new(.35f,-.8f),new(-.1f,-.4f),new(-.71f,-.78f),new(-1.24f,-.48f) }),
            Rock("AsteroidRounded", Enumerable.Range(0, 32).Select(i =>
            {
                float a = -i * Mathf.PI * 2f / 32f;
                float r = 1f + 0.07f * Mathf.Cos(a * 3f) + 0.04f * Mathf.Sin(a * 7f);
                return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }).ToArray())
        };
        var main = new GameObject("OutpostAsteroid", typeof(PolygonCollider2D));
        Vector2[] outline = { new(-37,-2),new(-33,7),new(-23,12),new(-12,17),new(-2,19),new(10,16),new(25,12),new(33,7),new(38,-4),new(31,-14),new(13,-21),new(-3,-23),new(-24,-17),new(-35,-10) };
        main.GetComponent<PolygonCollider2D>().points = outline;
        Body(main.transform, "OutpostAsteroid", outline, -6); Stroke(main.transform, "Contour", outline, 0.1f, -5);
        Crater(main.transform, "CraterA", new Vector2(-15f, 0f), new Vector2(6f, 4.5f), 0.07f, -5);
        Crater(main.transform, "CraterB", new Vector2(17f, -8f), new Vector2(4.5f, 4f), 0.07f, -5);
        Crater(main.transform, "CraterC", new Vector2(0f, -12f), Vector2.one * 2.7f, 0.07f, -5);
        var mainAsset = PrefabUtility.SaveAsPrefabAsset(main, Asteroids + "/OutpostAsteroid.prefab"); Object.DestroyImmediate(main);
        foreach (Transform old in field.Cast<Transform>().ToArray()) Object.DestroyImmediate(old.gameObject);
        main = (GameObject)PrefabUtility.InstantiatePrefab(mainAsset, field); main.transform.localPosition = new Vector3(0f, -3f);
        Vector2[] positions = { new(-95,18),new(-128,95),new(-70,125),new(12,112),new(92,84),new(127,15),new(94,-75),new(10,-113),new(-95,-86),new(-166,-30),new(167,126),new(42,174) };
        for (int i = 0; i < positions.Length; i++)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(variants[i % variants.Length], field);
            root.name = "SmallAsteroid" + (i + 1);
            root.transform.localPosition = positions[i]; root.transform.localScale = Vector3.one * (5.5f + i % 4 * 1.4f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, i * 37f);
            foreach (var line in root.GetComponentsInChildren<LineRenderer>())
            {
                line.widthMultiplier *= root.transform.localScale.x;
                PrefabUtility.RecordPrefabInstancePropertyModifications(line);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(main.transform);
        Debug.Log("Outline asteroids authored: six individual contours and one broad outpost rock; all instance scales uniform.");
    }
    public static void BuildMoon(GameObject moon)
    {
        Folder(Geometry);
        var old = moon.transform.Find("DetailedSurface"); if (old) Object.DestroyImmediate(old.gameObject);
        var root = Child(moon.transform, "DetailedSurface");
        var outline = moon.GetComponent<PolygonCollider2D>().GetPath(0);
        Body(root, "OutlineMoonBody", outline, -12); Stroke(root, "SharpWalkableRim", outline, 0.09f, -9);
        Crater(root, "CraterNorthWest", new Vector2(-3.1f, 3.3f), new Vector2(1.9f, 1.7f), .045f, -10);
        Crater(root, "CraterEastUpper", new Vector2(3.4f, 2.3f), new Vector2(1.4f, 1.3f), .045f, -10);
        Crater(root, "CraterEastLower", new Vector2(3.35f, -.45f), new Vector2(1.35f, 1.55f), .045f, -10);
        Crater(root, "CraterSouthWest", new Vector2(-3.8f, -3.5f), Vector2.one * .85f, .04f, -10);
        Crater(root, "CraterSouth", new Vector2(.2f, -4.3f), new Vector2(1.15f, .9f), .045f, -10);
        Crater(root, "CraterCentre", new Vector2(-.4f, -.6f), Vector2.one * .55f, .035f, -10);
        moon.GetComponent<SpriteRenderer>().enabled = false;
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Prefabs/World/MoonSurfaceArt.prefab");
        Debug.Log("Outline moon authored entirely as black mesh and white geometry; no surface bitmap or resolution limit.");
    }
    public static void RestoreMissionStyle()
    {
        const string passengerPath = "Assets/Prefabs/Characters/Rhekk.prefab";
        var passenger = PrefabUtility.LoadPrefabContents(passengerPath);
        foreach (var renderer in passenger.GetComponentsInChildren<SpriteRenderer>(true))
            renderer.color = renderer.name.Contains("Inset") ? Color.black : Color.white;
        PrefabUtility.SaveAsPrefabAsset(passenger, passengerPath); PrefabUtility.UnloadPrefabContents(passenger);
        const string capsulePath = "Assets/Prefabs/Missions/PassengerCapsule.prefab";
        var capsule = PrefabUtility.LoadPrefabContents(capsulePath);
        foreach (string part in new[] { "Closed", "Open" })
        {
            var parent = capsule.transform.Find(part);
            foreach (var renderer in parent.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
            var old = parent.Find("OutlineFrame"); if (old) Object.DestroyImmediate(old.gameObject);
            var frame = Child(parent, "OutlineFrame");
            Vector2[] contour = { new(-1.7f,-.8f),new(-1.7f,.8f),new(-1.45f,1.1f),new(1.45f,1.1f),new(1.7f,.8f),new(1.7f,-.8f),new(1.45f,-1.1f),new(-1.45f,-1.1f) };
            Body(frame, "Capsule" + part, contour, 4); Stroke(frame, "Contour", contour, .05f, 5);
            if (part == "Closed")
            {
                Stroke(frame, "BraceA", new[] { new Vector2(-1.45f,-.8f),new Vector2(1.45f,.8f) }, .03f, 5, false);
                Stroke(frame, "BraceB", new[] { new Vector2(-1.45f,.8f),new Vector2(1.45f,-.8f) }, .03f, 5, false);
            }
            else Stroke(frame, "OpenHatch", new[] { new Vector2(-1f,-.7f),new Vector2(-1f,.7f),new Vector2(1f,.7f),new Vector2(1f,-.7f) }, .04f, 5);
        }
        PrefabUtility.SaveAsPrefabAsset(capsule, capsulePath); PrefabUtility.UnloadPrefabContents(capsule);
    }
}
