using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldSaveChecks
{
    static int passed;
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
    }

    static void Tick(int count)
    {
        var fixedUpdate = typeof(LanderController).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < count; i++) { fixedUpdate.Invoke(LanderController.Instance, null); Physics2D.Simulate(0.02f); }
    }

    static void CheckWorld(WorldSave expected)
    {
        var data = new SaveGame();
        GameController.Instance.CaptureWorld(data);
        var actual = data.world;
        Require(actual != null && actual.IsValid(), "Restored world must remain serializable.");
        Require(actual.seed == expected.seed && actual.terrainLevel == expected.terrainLevel && actual.terrain.Length == expected.terrain.Length,
            "Load must preserve terrain identity rather than regenerate for the current level.");
        for (int i = 0; i < actual.terrain.Length; i++)
            if (actual.terrain[i] != expected.terrain[i]) throw new Exception("Loaded terrain geometry changed.");
        passed++;
        Require(actual.pads.Length == expected.pads.Length && actual.ships.Length == expected.ships.Length, "Load must preserve accumulated pad and ship counts without duplicates.");
        foreach (var parked in expected.ships)
            Require(Array.Exists(actual.ships, candidate => candidate.definitionId == parked.definitionId
                && Vector3.Distance(candidate.position, parked.position) < 0.0001f
                && Vector3.Distance(candidate.scale, parked.scale) < 0.0001f
                && Mathf.Abs(candidate.fuel - parked.fuel) < 0.0001f), "Each saved ship must preserve type, position, size and fuel.");
        foreach (var position in expected.pads)
            Require(Array.Exists(actual.pads, candidate => Vector3.Distance(position, candidate) < 0.0001f), "Every saved pad must remain at the same position.");
        var active = actual.ships[actual.activeShip];
        var savedActive = expected.ships[expected.activeShip];
        Require(active.definitionId == savedActive.definitionId && Vector3.Distance(active.position, savedActive.position) < 0.0001f,
            "The same ship instance and position must be controlled after load.");
        Require(Mathf.Abs(active.fuel - savedActive.fuel) < 0.0001f && active.fuelMax == savedActive.fuelMax,
            "Load must preserve remaining fuel and capacity without refilling.");
        Require(actual.phase == expected.phase && actual.hasResults == expected.hasResults && actual.refilling == expected.refilling
            && actual.refillStartFuel == expected.refillStartFuel,
            "Load must restore the current phase, result visibility and refill state.");
        Require(actual.scoring.padAwarded == expected.scoring.padAwarded && actual.scoring.moonAwarded == expected.scoring.moonAwarded,
            "Loading must not grant another landing score.");
        foreach (var ship in UnityEngine.Object.FindObjectsByType<LanderController>(FindObjectsSortMode.None))
            if (ship.IsParkedOnPad)
                Require(ship.GetComponent<Collider2D>().enabled && !ship.GetComponent<Collider2D>().isTrigger,
                    "Loading must keep parked pad ships solid.");
    }

    public static IEnumerator Run()
    {
        passed = 0;
        var game = GameController.Instance;
        game.autoSaveInterval = 3600;
        var manager = SaveLoadManager.Instance;
        var ship = LanderController.Instance;
        ship.landerState = LanderController.eLanderState.Flying;
        ship.rb.position = (Vector2)LandingPadPlacer.Instance.transform.position + Vector2.up * 20;
        ship.rb.rotation = 12;
        ship.rb.linearVelocity = new Vector2(1, 2);
        ship.rb.angularVelocity = 3;
        ship.currentFuel = ship.fuelMax * 0.37f;
        Physics2D.SyncTransforms();
        var scaledParent = new GameObject("ScaledShipParent").transform;
        scaledParent.localScale = new Vector3(2, 3, 1);
        var parkedShip = Array.Find(UnityEngine.Object.FindObjectsByType<LanderController>(FindObjectsSortMode.None), candidate => !candidate.isActive);
        parkedShip.transform.SetParent(scaledParent, true);
        manager.Save();
        var saved = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data));
        Require(saved.version == 3 && saved.world != null && saved.world.IsValid(), "The new schema must round-trip all world data through JSON.");
        var snapshot = saved.world;
        int score = saved.CollectedScore;
        manager.Load();
        Require(game.RestoreWorld(manager.Data.world), "A valid world snapshot must restore.");
        CheckWorld(snapshot);
        UnityEngine.Object.Destroy(scaledParent.gameObject);
        Require((LanderController.Instance.rb.linearVelocity - snapshot.ships[snapshot.activeShip].velocity).sqrMagnitude < 0.00001f,
            "Flight momentum must survive load.");
        manager.Save();
        snapshot = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data)).world;
        SceneManager.LoadScene("MainScene");
        yield return null;
        game = GameController.Instance;
        game.autoSaveInterval = 3600;
        Physics2D.simulationMode = SimulationMode2D.Script;
        CheckWorld(snapshot);
        Require(ScoringController.Instance.CollectedScore == score && game.Phase == GameController.GamePhase.Flight,
            "A fresh scene must resume the saved flight and points without countdown or a new map.");
        ship = LanderController.Instance;
        var pad = LandingPadPlacer.Instance.GetComponentInChildren<Collider2D>();
        game.RestartGame(); game.BeginRun(); LanderUI.Instance.HideGameOver();
        ship.rb.rotation = 0; Physics2D.SyncTransforms();
        float bottom = ship.rb.position.y - ship.GetComponent<Collider2D>().bounds.min.y;
        ship.rb.position = new Vector2(pad.bounds.center.x, pad.bounds.max.y + bottom + 3);
        Physics2D.SyncTransforms(); Tick(1);
        ship.rb.position = new Vector2(pad.bounds.center.x, pad.bounds.max.y + bottom + 0.01f);
        ship.rb.linearVelocity = Vector2.down * 0.1f;
        Physics2D.SyncTransforms(); Tick(50);
        manager.Save();
        snapshot = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data)).world;
        Require(snapshot.phase == GameController.GamePhase.Landed, "Test setup must produce a landed save.");
        Require(game.RestoreWorld(snapshot), "A pad landing must restore.");
        CheckWorld(snapshot);
        Tick(5);
        Require(game.CanRefill && game.CanStartNextLevel, "Loaded pad contact must allow refill and Next Level.");
        game.refillDuration = 1f;
        LanderController.Instance.currentFuel = LanderController.Instance.fuelMax * 0.25f;
        game.RefillTank();
        double until = UnityEditor.EditorApplication.timeSinceStartup + 0.15;
        while (UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
        manager.Save();
        snapshot = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data)).world;
        Require(snapshot.refilling && snapshot.ships[snapshot.activeShip].fuel < snapshot.ships[snapshot.activeShip].fuelMax,
            "Saving during refill must preserve the partial tank and running refill.");
        Require(game.RestoreWorld(snapshot), "A refill save must restore.");
        CheckWorld(snapshot);
        Tick(5);
        float partialFuel = LanderController.Instance.currentFuel;
        until = UnityEditor.EditorApplication.timeSinceStartup + 0.15;
        while (UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
        Require(game.IsRefilling && LanderController.Instance.currentFuel > partialFuel,
            "Loading a partial refill must resume gradual filling.");
        Require(LanderUI.Instance.refillProgressFill.gameObject.activeInHierarchy && AudioManager.Instance.refillSource.volume > 0f,
            "Loading a partial refill must resume visual feedback and sound.");
        game.ContinueFlight(); game.BeginFlight();
        partialFuel = LanderController.Instance.currentFuel;
        until = UnityEditor.EditorApplication.timeSinceStartup + 0.3;
        while (UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
        Require(!game.IsRefilling && LanderController.Instance.currentFuel == partialFuel,
            "Continuing flight must stop refill and preserve the partial tank.");
        Require(!LanderUI.Instance.refillProgressFill.gameObject.activeSelf && AudioManager.Instance.refillSource.volume == 0f,
            "Continuing flight must hide refill feedback and silence its loop.");
        ship = LanderController.Instance;
        ship.landerState = LanderController.eLanderState.Flying;
        typeof(LanderController).GetMethod("Crash", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(ship, new object[] { LanderController.eLanderState.OutOfFuel });
        snapshot = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data)).world;
        Require(game.RestoreWorld(snapshot) && game.Phase == GameController.GamePhase.Crashed && LanderController.Instance.IsCrashed,
            "A crashed save must retain the failure state and retry flow.");
        Require(!LanderController.Instance.GetComponent<Collider2D>().enabled, "A loaded crashed ship must remain non-colliding.");
        game.RestartGame(); manager.Save();
        snapshot = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data)).world;
        Require(game.RestoreWorld(snapshot) && game.Phase == GameController.GamePhase.Countdown,
            "Countdown saves must resume a countdown.");
        game.BeginRun(); LanderUI.Instance.HideGameOver();
        Require(!LanderController.Instance.GetComponent<Collider2D>().isTrigger, "Loaded countdown ships must have a solid hull when flight starts.");
        ship = LanderController.Instance;
        ship.landerState = LanderController.eLanderState.LandedMoon;
        ship.rb.position = GravityManager2D.Instance.transform.position + Vector3.up * 25;
        ship.Park();
        var actor = new ActorSave
        {
            position = GravityManager2D.Instance.transform.position + Vector3.up * 24,
            scale = Vector3.one, velocity = Vector2.right * 0.5f
        };
        MoonEVAController.Instance.RestoreAstronaut(actor);
        game.BeginEVA(MoonEVAController.Instance.astronaut.transform);
        snapshot = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data)).world;
        Require(game.RestoreWorld(snapshot), "An EVA save must restore.");
        Require(game.Phase == GameController.GamePhase.EVA && MoonEVAController.Instance.astronaut
            && game.ControlledTarget == MoonEVAController.Instance.astronaut.transform, "Loaded EVA must control the astronaut rather than the parked ship.");
        Require(Vector3.Distance(game.ControlledTarget.position, actor.position) < 0.0001f
            && !LanderController.Instance.controlsEnabled, "Loaded EVA must preserve position and keep the ship parked.");
        typeof(SaveLoadManager).GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, new object[] { true });
        manager.Load();
        Require(manager.Data.world.phase == GameController.GamePhase.EVA && manager.Data.world.astronaut != null,
            "Application pause must capture EVA into the on-disk save.");
        SceneManager.LoadScene("MainScene");
        yield return null;
        game = GameController.Instance;
        game.autoSaveInterval = 3600;
        Physics2D.simulationMode = SimulationMode2D.Script;
        Require(game.Phase == GameController.GamePhase.EVA && MoonEVAController.Instance.astronaut
            && game.ControlledTarget == MoonEVAController.Instance.astronaut.transform,
            "A fresh scene must restore EVA directly.");
        MoonEVAController.Instance.EnterLander(LanderController.Instance);
        Require(game.Phase == GameController.GamePhase.Landed && !MoonEVAController.Instance.astronaut,
            "Boarding must work after restoring EVA.");
        var previousShip = LanderController.Instance;
        var visitedShip = Array.Find(UnityEngine.Object.FindObjectsByType<LanderController>(FindObjectsSortMode.None), candidate => !candidate.isActive && candidate.CaptureState().fuelInitialized);
        Require(visitedShip != null, "The world must contain a previously used ship.");
        float remainingFuel = visitedShip.currentFuel;
        float capacity = visitedShip.fuelMax;
        LanderController.ChangeLander(visitedShip);
        Require(visitedShip.currentFuel == remainingFuel && visitedShip.fuelMax == capacity,
            "Boarding an already used ship after load must preserve its saved tank state.");
        LanderController.ChangeLander(previousShip);
        var legacy = JsonUtility.FromJson<SaveGame>("{\"version\":2,\"level\":8,\"CollectedScore\":1234,\"selectedLanderId\":2}");
        legacy.Normalize();
        Require(legacy.version == 3 && legacy.world == null && legacy.level == 8 && legacy.CollectedScore == 1234 && legacy.selectedLanderId == 2,
            "Version 2 saves must retain progress while starting their first persistent world.");
        var broken = JsonUtility.FromJson<SaveGame>(JsonUtility.ToJson(manager.Data));
        broken.world.activeShip = -1; broken.Normalize();
        Require(broken.world == null && broken.CollectedScore == manager.Data.CollectedScore, "Invalid world data must not destroy score progress.");
        Debug.Log("World save checks passed: " + passed);
    }
}
