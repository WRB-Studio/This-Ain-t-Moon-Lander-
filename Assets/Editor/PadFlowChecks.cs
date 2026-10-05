using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class PadFlowChecks
{
    static IEnumerator checks;
    static double deadline;
    static int passed;
    static readonly MethodInfo tickShip = typeof(LanderController).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
    static PadFlowChecks() { EditorApplication.playModeStateChanged += OnPlay; EditorApplication.update += Update; }
    public static void RunAll()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this scene check in a separate Unity batch process.");
        UIAuthoringChecks.RunAll(); GameChecks.RunAll(); TerrainChecks.RunAll();
        SessionState.SetBool("PadFlowChecks", true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (!SessionState.GetBool("PadFlowChecks", false) || state != PlayModeStateChange.EnteredPlayMode) return;
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (SaveLoadManager.Instance.Data != null) return;
            typeof(SaveLoadManager).GetProperty("StorageDirectory", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(SaveLoadManager.Instance, Path.Combine(Application.dataPath, "../Temp/TestSave"));
            SaveLoadManager.Instance.NewGame();
        };
        SceneManager.LoadScene("MainScene");
        checks = Run(); deadline = EditorApplication.timeSinceStartup + 90;
    }
    static void Update()
    {
        if (checks == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Pad checks timed out.");
            if (checks.MoveNext()) return;
            Debug.Log("Pad flow checks passed: " + passed); Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }
    static void Finish(int code) { checks = null; SessionState.SetBool("PadFlowChecks", false); EditorApplication.Exit(code); }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); passed++; }
    static void Tick(int count) { for (int i = 0; i < count; i++) { tickShip.Invoke(LanderController.Instance, null); Physics2D.Simulate(0.02f); } }
    static void Land(Collider2D pad)
    {
        var ship = LanderController.Instance;
        ship.StartLander(); ship.rb.rotation = 0; Physics2D.SyncTransforms();
        float bottom = ship.rb.position.y - ship.GetComponent<Collider2D>().bounds.min.y;
        ship.rb.position = new Vector2(pad.bounds.center.x, pad.bounds.max.y + bottom + 3);
        Physics2D.SyncTransforms(); Tick(1);
        ship.rb.position = new Vector2(pad.bounds.center.x, pad.bounds.max.y + bottom + 0.01f);
        ship.rb.linearVelocity = Vector2.down * 0.1f;
        Physics2D.SyncTransforms(); Tick(50);
        Require(GameController.Instance.Phase == GameController.GamePhase.Landed, "A safe pad landing must enter Landed.");
    }
    static IEnumerator Run()
    {
        while (!GameController.Instance || !GameController.Instance.ControlledTarget) yield return null;
        Require(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length
            == SessionState.GetInt("AuthoredButtonCount", -1), "Starting the game must preserve the authored button count without generating UI.");
        Physics2D.simulationMode = SimulationMode2D.Script;
        var game = GameController.Instance;
        var terrain = RandomLandscape.Instance;
        var score = ScoringController.Instance;
        var ui = LanderUI.Instance;
        var oldPad = LandingPadPlacer.Instance;
        var oldSurface = oldPad.GetComponentInChildren<Collider2D>();
        var line = terrain.GetComponent<LineRenderer>();
        var vertices = new Vector3[line.positionCount]; line.GetPositions(vertices);
        int seed = terrain.seed;
        game.BeginRun(); ui.HideGameOver();
        Land(oldSurface);
        int points = score.CollectedScore;
        Require(points > 0, "The first landing must award points.");
        var oldShip = LanderController.Instance;
        oldShip.currentFuel *= 0.4f;
        double until = EditorApplication.timeSinceStartup + 2;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        var refill = Array.Find(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None), button => button.name == "RefillTank");
        Require(refill && refill.gameObject.activeInHierarchy && refill.interactable, "A landed pad must show an enabled refill button for a partial tank.");
        Require(ui.txtLanderFuel.gameObject.activeInHierarchy, "Results must keep the fuel display visible.");
        game.refillDuration = 1f;
        float initialFuel = oldShip.currentFuel;
        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(refill.gameObject);
        refill.onClick.Invoke();
        Require(oldShip.currentFuel == initialFuel && game.IsRefilling && !refill.interactable, "Refill must start gradually and disable its button while running.");
        Require(game.RefillProgress == 0f, "Button fill must start at zero even with a partially filled tank.");
        Require(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != refill.gameObject
            && refill.targetGraphic.canvasRenderer.GetColor().a == 1f && !ui.refillProgressFill.sprite,
            "Starting refill must clear selection while preserving the frame and using a solid rectangular fill.");
        until = EditorApplication.timeSinceStartup + 0.2;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(oldShip.currentFuel > initialFuel && oldShip.currentFuel < oldShip.fuelMax && score.CollectedScore == points,
            "Refill must increase fuel progressively without awarding points.");
        Require(ui.refillProgressFill.gameObject.activeInHierarchy && Mathf.Abs(ui.refillProgressFill.rectTransform.anchorMax.x - game.RefillProgress) < 0.02f
            && ui.btnRefill.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("%"), "Refill feedback must show the current tank level and percentage.");
        Require(AudioManager.Instance.refillSource.volume > 0f && AudioManager.Instance.refillSource.loop,
            "Refilling must fade in its dedicated sound loop.");
        until = EditorApplication.timeSinceStartup + 2;
        while (game.IsRefilling && EditorApplication.timeSinceStartup < until) yield return null;
        Require(oldShip.currentFuel == oldShip.fuelMax && !game.IsRefilling && !refill.interactable, "Refill must finish at capacity and remain disabled when full.");
        until = EditorApplication.timeSinceStartup + 0.3;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Require(!ui.refillProgressFill.gameObject.activeSelf && AudioManager.Instance.refillSource.volume == 0f,
            "Completing refill must hide its progress and silence its sound.");
        game.ContinueFlight(); game.BeginFlight();
        score.scoreResetDistance = 30;
        oldShip.rb.position = (Vector2)oldPad.transform.position + Vector2.up * 10;
        Physics2D.SyncTransforms();
        yield return null;
        Land(oldSurface);
        Require(score.CollectedScore == points && game.HasResults && game.CanRefill, "A short hop must not award points but must still offer pad services.");
        game.ContinueFlight(); game.BeginFlight();
        oldShip.rb.position = (Vector2)oldPad.transform.position + Vector2.up * 40;
        Physics2D.SyncTransforms();
        yield return null; yield return null;
        Land(oldSurface);
        Require(score.CollectedScore > points, "Flying beyond the distance threshold must enable a new landing score.");
        int beforeNextScore = score.CollectedScore;
        var shipPosition = oldShip.transform.position;
        var padPosition = oldPad.transform.position;
        int countBefore = UnityEngine.Object.FindObjectsByType<LandingPadPlacer>(FindObjectsSortMode.None).Length;
        int shipCountBefore = UnityEngine.Object.FindObjectsByType<LanderController>(FindObjectsSortMode.None).Length;
        int level = game.level;
        game.NextLevel();
        yield return null;
        Require(game.level == level + 1 && game.Phase == GameController.GamePhase.Countdown, "Next Level must advance to a new countdown.");
        Require(oldShip && oldShip.transform.position == shipPosition && !oldShip.isActive && oldShip.rb.bodyType == RigidbodyType2D.Static, "The landed ship must remain parked in place.");
        Require(oldPad && oldPad.transform.position == padPosition && LandingPadPlacer.Instance != oldPad, "The previous pad must remain in place and the new pad become the target.");
        Require(UnityEngine.Object.FindObjectsByType<LandingPadPlacer>(FindObjectsSortMode.None).Length == countBefore + 1, "Next Level must add exactly one pad.");
        Require(UnityEngine.Object.FindObjectsByType<LanderController>(FindObjectsSortMode.None).Length == shipCountBefore + 1, "Next Level must add exactly one ship.");
        Require(terrain.seed == seed && line.positionCount == vertices.Length, "Next Level must preserve terrain identity.");
        for (int i = 0; i < vertices.Length; i++) if (vertices[i] != line.GetPosition(i)) throw new Exception("Next Level changed terrain geometry.");
        passed++;
        Require(Mathf.Abs(LandingPadPlacer.Instance.transform.position.x - padPosition.x) >= oldPad.minimumPadSpacing, "Added pads must respect spacing.");
        var ship = LanderController.Instance;
        game.BeginRun(); ui.HideGameOver();
        Land(LandingPadPlacer.Instance.GetComponentInChildren<Collider2D>());
        Require(score.CollectedScore > beforeNextScore, "Next Level must enable a new landing score.");
        var camera = CameraController.Instance;
        float minimumZoom = (float)typeof(CameraController).GetField("minZoom", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(camera);
        float targetZoom = (float)typeof(CameraController).GetMethod("CalcTargetZoom", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(camera, null);
        Require(Mathf.Abs(targetZoom - minimumZoom) < 0.01f, "Camera must zoom in at a newly added pad.");
        game.ContinueFlight(); game.BeginFlight();
        Land(oldSurface);
        targetZoom = (float)typeof(CameraController).GetMethod("CalcTargetZoom", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(camera, null);
        Require(Mathf.Abs(targetZoom - minimumZoom) < 0.01f && game.CanRefill, "Old pads must still support landing zoom and refilling.");
        game.NextLevel(); yield return null;
        Require(game.level == level + 2 && oldShip && oldPad && UnityEngine.Object.FindObjectsByType<LandingPadPlacer>(FindObjectsSortMode.None).Length == countBefore + 2,
            "Pads and landed ships must accumulate across multiple levels.");
        game.BeginRun(); ui.HideGameOver();
        var activePad = LandingPadPlacer.Instance;
        Land(activePad.GetComponentInChildren<Collider2D>());
        int beforeFailureLevel = game.level;
        activePad.minimumPadSpacing = terrain.width * 2;
        game.NextLevel(); yield return null;
        Require(game.level == beforeFailureLevel && LandingPadPlacer.Instance == activePad && game.Phase == GameController.GamePhase.Landed,
            "A full map must leave the current landing intact without advancing the level.");
        game.ContinueFlight(); game.BeginFlight();
        LanderController.Instance.currentFuel = 0;
        game.RefillTank();
        Require(LanderController.Instance.currentFuel == 0, "Refill must be unavailable during flight.");
        var saveChecks = WorldSaveChecks.Run();
        while (saveChecks.MoveNext()) yield return saveChecks.Current;
    }
}
