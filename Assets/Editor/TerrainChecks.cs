using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TerrainChecks
{
    [MenuItem("Tools/Game/Run Terrain Checks")]
    public static void RunAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run checks in Edit Mode.");
        var previousTerrain = RandomLandscape.Instance;
        var previousPad = LandingPadPlacer.Instance;
        var previousRandom = UnityEngine.Random.state;
        var terrainObject = new GameObject("TerrainCheck") { hideFlags = HideFlags.HideAndDontSave };
        var padObject = new GameObject("PadCheck") { hideFlags = HideFlags.HideAndDontSave };
        var shipObject = new GameObject("SpawnCheck") { hideFlags = HideFlags.HideAndDontSave };
        int checks = 0;
        try
        {
            terrainObject.transform.position = new Vector3(12f, 3f, 0f);
            terrainObject.transform.localScale = new Vector3(1.3f, 0.8f, 1f);
            var terrain = terrainObject.AddComponent<RandomLandscape>();
            terrain.livePreviewInEditor = false;
            terrain.points = 1000;
            terrain.width = 2500f;
            terrain.amplitude = 5f;
            terrain.featureHeight = 9.6f;
            terrain.featureWidth = 10f;
            terrain.terrainFrequency = 0.09f;
            terrain.landingZoneHalfWidth = 5.18f;
            RandomLandscape.Instance = terrain;
            var box = padObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(4f, 1f);
            box.offset = new Vector2(0.4f, 0.25f);
            var pad = padObject.AddComponent<LandingPadPlacer>();
            LandingPadPlacer.Instance = pad;
            pad.scaleLegs = false;
            pad.Init();
            var ship = shipObject.AddComponent<LanderController>();
            var polygon = ship.GetComponent<PolygonCollider2D>();
            polygon.points = new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0f, 0.5f) };
            ship.rb = ship.GetComponent<Rigidbody2D>();
            typeof(LanderController).GetField("hull", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ship, polygon);
            ship.maxDistanceXToPad = 20f;
            ship.clearance = 2f;
            var line = terrain.GetComponent<LineRenderer>();
            var edge = terrain.GetComponent<EdgeCollider2D>();

            foreach (var type in new[] { RandomLandscape.TerrainType.Hills, RandomLandscape.TerrainType.Craters, RandomLandscape.TerrainType.MountainRanges })
            foreach (int level in new[] { 1, 15, 30, 500 })
            foreach (int seed in new[] { 1, 733, int.MinValue })
            {
                terrain.terrainType = type;
                var state = UnityEngine.Random.state;
                terrain.GenerateFromSeed(seed, level);
                Require(UnityEngine.Random.state.Equals(state), "Terrain must not consume global random state.");
                Vector2 zone = terrain.LandingZone;
                var original = new Vector3[line.positionCount];
                line.GetPositions(original);
                Vector2[] collider = edge.points;
                for (int i = 0; i < original.Length; i++)
                {
                    Require(!float.IsNaN(original[i].y) && !float.IsInfinity(original[i].y), "Terrain must contain finite heights.");
                    Require(((Vector2)terrain.transform.TransformPoint(collider[i]) - (Vector2)original[i]).sqrMagnitude < 0.0001f,
                        "Collider and rendered terrain must match after transforms.");
                    if (Mathf.Abs(original[i].x - zone.x) <= terrain.LandingHalfWidth)
                        Require(Mathf.Abs(original[i].y - zone.y) < 0.0001f, "Landing zone must be completely flat.");
                }
                terrain.GenerateFromSeed(seed, level);
                Require(terrain.LandingZone == zone, "Seed replay must retain the landing zone.");
                for (int i = 0; i < original.Length; i++) Require(line.GetPosition(i) == original[i], "Seed replay must retain every terrain point.");
                pad.SetRandomPlaceForPad();
                Require(Mathf.Abs(pad.transform.position.x - zone.x) < 0.001f && box.bounds.min.y > zone.y,
                    "Pad must sit above its planned flat ground.");
                ship.SetRandomPosition();
                float highest = terrain.GetHighestGround(Mathf.Min(ship.transform.position.x, zone.x) - 0.5f,
                    Mathf.Max(ship.transform.position.x, zone.x) + 0.5f);
                Require(ship.transform.position.y >= highest + ship.clearance + 0.49f,
                    "Spawn must leave a clear route above the terrain to the pad.");
                checks++;
            }

            terrain.terrainType = RandomLandscape.TerrainType.Automatic;
            terrain.GenerateFromSeed(733, 1);
            pad.SetRandomPlaceForPad();
            UnityEngine.Random.InitState(937);
            bool left = false, right = false;
            float firstX = float.NaN;
            bool varied = false;
            for (int i = 0; i < 60; i++)
            {
                ship.SetRandomPosition();
                float offset = ship.transform.position.x - pad.transform.position.x;
                Require(Mathf.Abs(offset) >= ship.minDistanceXToPad - 0.001f,
                    "Spawns must have a meaningful sideways offset.");
                left |= offset < 0; right |= offset > 0;
                if (i == 0) firstX = offset;
                else varied |= Mathf.Abs(offset - firstX) > 0.5f;
            }
            Require(left && right && varied, "Repeated spawns in the same world must vary and use both sides of the pad.");
            Require(terrain.GeneratedType == RandomLandscape.TerrainType.Hills, "First levels must start with hills.");
            terrain.GenerateFromSeed(733, 4);
            Require(terrain.GeneratedType == RandomLandscape.TerrainType.Craters, "Later levels must introduce craters.");
            terrain.GenerateFromSeed(733, 7);
            Require(terrain.GeneratedType == RandomLandscape.TerrainType.MountainRanges, "Later levels must introduce mountain ranges.");
            terrain.lockSeed = true;
            terrain.GenerateNewLevel();
            Vector2 lockedZone = terrain.LandingZone;
            Vector3 lockedPoint = line.GetPosition(0);
            terrain.GenerateNewLevel();
            Require(terrain.seed == 733 && terrain.LandingZone == lockedZone && line.GetPosition(0) == lockedPoint,
                "Locked seed must reproduce the layout when starting a new level.");
            terrain.points = 4;
            terrain.width = 1f;
            pad.padSpawnRange = -10f;
            terrain.GenerateFromSeed(1, 1);
            Require(line.positionCount == 4 && terrain.HasLayout, "Tiny landscapes and invalid spawn ranges must remain safe.");
            Debug.Log("Terrain checks passed: " + checks + " layouts, seed replay, collider alignment, planned pads and safe spawns.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(shipObject);
            UnityEngine.Object.DestroyImmediate(padObject);
            UnityEngine.Object.DestroyImmediate(terrainObject);
            RandomLandscape.Instance = previousTerrain;
            LandingPadPlacer.Instance = previousPad;
            UnityEngine.Random.state = previousRandom;
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
