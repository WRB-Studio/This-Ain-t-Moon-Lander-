using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GameChecks
{
    static int passed;

    [MenuItem("Tools/Game/Run Refactor Checks")]
    public static void RunAll()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run checks in Edit Mode.");
        passed = 0;
        CheckSaveMigration();
        CheckDictionary();
        CheckGravityAndLanding();
        CheckFallSpeedTransition();
        CheckSaveRecovery();
        Debug.Log("Game checks passed: " + passed);
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed++;
    }

    static void CheckSaveMigration()
    {
        var data = JsonUtility.FromJson<SaveGame>("{\"version\":1,\"level\":6,\"selectedLanderIndex\":4,\"CollectedScore\":3210}");
        data.Normalize();
        Assert(data.version == SaveGame.CurrentVersion && data.selectedLanderId == -1, "Legacy selection must be migrated by the prefab catalogue.");
        Assert(data.level == 6 && data.CollectedScore == 3210 && data.selectedLanderIndex == 4, "Migration must preserve progress.");

        data.level = -4;
        data.volMusic = float.NaN;
        data.volSfx = 4f;
        data.flags = null;
        data.Normalize();
        Assert(data.level == 1 && data.volMusic == 0.8f && data.volSfx == 1f && data.flags != null, "Invalid saved values must be normalized.");
        data.SetFlag("LANDER_SECRET_FOUND_5", true);
        var restored = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(data));
        Assert(restored.GetFlag("LANDER_SECRET_FOUND_5"), "Unlock flags must survive JSON serialization.");

        data.version = SaveGame.CurrentVersion + 1;
        data.Normalize();
        Assert(data.version > SaveGame.CurrentVersion, "Future versions must not be silently downgraded.");
    }

    static void CheckDictionary()
    {
        var dictionary = new SerializableDictionary<string, bool>();
        var type = dictionary.GetType();
        var keys = type.GetField("keys", BindingFlags.Instance | BindingFlags.NonPublic);
        var values = type.GetField("values", BindingFlags.Instance | BindingFlags.NonPublic);
        keys.SetValue(dictionary, new List<string> { "one", "one", null, "unpaired" });
        values.SetValue(dictionary, new List<bool> { false, true, true });
        dictionary.OnAfterDeserialize();
        Assert(dictionary.Count == 1 && dictionary["one"], "Duplicate, null and unpaired keys must deserialize safely.");
        keys.SetValue(dictionary, null);
        values.SetValue(dictionary, null);
        dictionary.OnAfterDeserialize();
        dictionary["fresh"] = true;
        dictionary.OnBeforeSerialize();
        dictionary.OnAfterDeserialize();
        Assert(dictionary.Count == 1 && dictionary["fresh"], "Null serialized lists must recover.");
    }

    static void CheckGravityAndLanding()
    {
        var moonObject = new GameObject("GravityCheck") { hideFlags = HideFlags.HideAndDontSave };
        var landerObject = new GameObject("LandingCheck") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var gravity = moonObject.AddComponent<GravityManager2D>();
            gravity.transform.position = new Vector3(0f, 500f);
            gravity.zeroGStartY = 80f;
            gravity.zeroGFullY = 120f;
            gravity.moonEnterRadius = 100f;
            gravity.moonFullRadius = 60f;
            gravity.moonGravityStrength = 5f;
            Assert((gravity.GetGravity(Vector2.zero) - gravity.baseGravity).sqrMagnitude < 0.0001f, "Ground gravity must retain the existing base value.");
            Assert(gravity.GetGravity(new Vector2(200f, 300f)).sqrMagnitude < 0.0001f, "Deep space must have zero gravity.");
            Vector2 right = gravity.GetGravity(new Vector2(50f, 500f));
            Vector2 left = gravity.GetGravity(new Vector2(-50f, 500f));
            Assert(right.x < 0f && left.x > 0f && Mathf.Abs(right.y) < 0.001f, "Moon gravity must point toward the centre at each body position.");
            Assert(gravity.GetGravity(Vector2.zero) == gravity.baseGravity, "Queries must not depend on a previously queried ship position.");

            var lander = landerObject.AddComponent<LanderController>();
            lander.safeSpeed = 2f;
            lander.safeVerticalSpeed = 1.5f;
            lander.safeAngleDeg = 20f;
            Assert(lander.IsSafeLanding(2f, 1.5f, 20f, false), "Pad thresholds are inclusive.");
            Assert(!lander.IsSafeLanding(2.1f, 1f, 0f, false)
                && !lander.IsSafeLanding(1f, 1.6f, 0f, false)
                && !lander.IsSafeLanding(1f, 1f, 21f, false), "Every pad landing threshold must be enforced.");
            Assert(lander.IsSafeLanding(2f, 50f, 180f, true), "The existing speed-only moon landing rule must be preserved.");
            Assert(!lander.IsSafeLanding(2.1f, 0f, 0f, true), "Unsafe moon impacts must still crash.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(landerObject);
            UnityEngine.Object.DestroyImmediate(moonObject);
        }
    }

    static void CheckFallSpeedTransition()
    {
        var limiter = typeof(LanderController).GetMethod("LimitFallVelocity", BindingFlags.Static | BindingFlags.NonPublic);
        Vector2 incoming = new Vector2(3f, 12f);
        Vector2 weakGravity = new Vector2(0f, 0.02f);
        var result = (Vector2)limiter.Invoke(null, new object[] { incoming, weakGravity, 4f, 0.02f });
        Assert(Mathf.Abs(result.y - 11.9996f) < 0.00001f && result.x == incoming.x,
            "Entering weak moon gravity must retain incoming and sideways momentum.");
        var inSpace = (Vector2)limiter.Invoke(null, new object[] { incoming, Vector2.zero, 4f, 0.02f });
        Assert(inSpace == incoming, "Zero gravity must not limit drift speed.");
        var normalFall = new Vector2(3f, -2f);
        var unchanged = (Vector2)limiter.Invoke(null, new object[] { normalFall, new Vector2(0f, -8.5f), 4f, 0.02f });
        Assert(unchanged == normalFall, "Falls below the limit must not be braked.");
        var capped = (Vector2)limiter.Invoke(null, new object[] { new Vector2(3f, -4.1f), new Vector2(0f, -8.5f), 4f, 0.02f });
        Assert(Mathf.Abs(capped.y + 4f) < 0.00001f && capped.x == 3f,
            "Normal downward acceleration must still be capped without affecting lateral speed.");
        var oneStep = (Vector2)limiter.Invoke(null, new object[] { incoming, new Vector2(0f, 5f), 4f, 0.02f });
        var halfStep = (Vector2)limiter.Invoke(null, new object[] { incoming, new Vector2(0f, 5f), 4f, 0.01f });
        var twoSteps = (Vector2)limiter.Invoke(null, new object[] { halfStep, new Vector2(0f, 5f), 4f, 0.01f });
        Assert((oneStep - twoSteps).sqrMagnitude < 0.0000001f,
            "Fall speed correction must scale with the physics timestep.");
    }

    static void CheckSaveRecovery()
    {
        string directory = Path.Combine(Path.GetTempPath(), "MoonLanderChecks-" + Guid.NewGuid().ToString("N"));
        var gameObject = new GameObject("SaveCheck") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var manager = gameObject.AddComponent<SaveLoadManager>();
            typeof(SaveLoadManager).GetProperty("StorageDirectory", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, directory);
            manager.Init();
            manager.Data.level = 6;
            manager.Save();
            manager.Data.level = 7;
            manager.Save();
            string path = Path.Combine(directory, "savegame.json");
            File.WriteAllText(path, "{");
            manager.Load();
            Assert(manager.Data.level == 6, "A damaged primary save must recover the last complete backup.");
            manager.Data.level = 8;
            manager.Save();
            manager.Load();
            Assert(manager.Data.level == 8 && File.Exists(path + ".bak"), "Progress must save again after backup recovery.");

            File.WriteAllText(path, "{\"version\":99,\"level\":12}");
            manager.Load();
            string future = File.ReadAllText(path);
            manager.Save();
            Assert(File.ReadAllText(path) == future, "A future-format save must not be overwritten.");
            manager.Delete();
            Assert(!File.Exists(path) && !File.Exists(path + ".bak"), "Reset must remove both primary save and backup.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
