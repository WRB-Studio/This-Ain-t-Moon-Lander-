using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance;
    [Tooltip("Used for terrain previews in Edit Mode. In Play Mode the saved level replaces this value.")]
    [Min(1)] public int level = 1;
    public LandingPadPlacer landingPadPrefab;
    [Min(1f)] public float autoSaveInterval = 10f;
    [Tooltip("Seconds needed to refill an empty tank.")]
    [Min(0.1f)] public float refillDuration = 5f;
    float refillContactGrace;
    float refillStartFuel;
    bool worldReady;
    float nextAutoSave;
    bool resultsHaveScore;

    public enum GamePhase { Countdown, Flight, Landed, EVA, Crashed }
    public GamePhase Phase { get; private set; }
    public bool HasResults { get; private set; }
    public bool IsExploring { get; private set; }
    public bool IsRefilling { get; private set; }
    public float RefillProgress => IsRefilling && LanderController.Instance
        ? Mathf.InverseLerp(refillStartFuel, LanderController.Instance.fuelMax, LanderController.Instance.currentFuel) : 0f;
    public bool CanRefill => Phase == GamePhase.Landed && LanderController.Instance
        && LanderController.Instance.landerState == LanderController.eLanderState.LandedPad
        && LanderController.Instance.IsTouchingPad;
    public bool CanStartNextLevel => HasResults && CanRefill;
    public bool CanChooseLander => HasResults && (!IsExploring || Phase == GamePhase.Crashed);
    public Transform ControlledTarget { get; private set; }
    public Rigidbody2D ControlledBody { get; private set; }
    public bool IsPlaying => !HasResults && (Phase == GamePhase.Flight || Phase == GamePhase.Landed || Phase == GamePhase.EVA);

    void Awake() => Instance = this;

    void Start()
    {
        SaveLoadManager.Instance.Init();
        level = SaveLoadManager.Instance.Data.level;
        AudioManager.Instance.Init();
        ScoringController.Instance.Init();
        LanderController.Instance.Init();
        LanderChooserManager.Instance.Init();
        LanderUI.Instance.Init();
        CameraController.Instance.Init();
        GravityManager2D.Instance.Init();
        StarField.Instance.Init();
        RandomLandscape.Instance.Init();
        LandingPadPlacer.Instance.Init();
        StoryTextController.Instance.Init();
        MoonEVAController.Instance.Init();
        var savedWorld = SaveLoadManager.Instance.Data.world;
        if (savedWorld == null || !RestoreWorld(savedWorld)) StartGame();
    }

    public void SetControlledTarget(Transform target, bool instantFocus = false)
    {
        ControlledTarget = target;
        ControlledBody = target ? target.GetComponent<Rigidbody2D>() : null;
        CameraController.Instance.SetTarget(target, instantFocus);
        StarField.Instance.SetTarget(target, instantFocus);
    }

    public void StartGame()
    {
        IsExploring = false;
        GenerateWorld();
        ScoringController.Instance.BeginLevel();
        StartArcadeRound();
        worldReady = true;
        SaveLoadManager.Instance.Save();
    }

    void GenerateWorld()
    {
        RandomLandscape.Instance.GenerateNewLevel();
        LandingPadPlacer.Instance.SetRandomPlaceForPad();
    }

    public void RestartGame()
    {
        StartArcadeRound();
        SaveLoadManager.Instance.Save();
    }

    void StartArcadeRound()
    {
        IsRefilling = false;
        Phase = GamePhase.Countdown;
        HasResults = false;
        resultsHaveScore = false;
        MoonEVAController.Instance.ResetRun();
        LanderUI.Instance.HideGameOver();
        ImpactFX.Instance.ResetEffect();

        LanderController.Instance.ResetLander();
        Physics2D.SyncTransforms();
        SetControlledTarget(LanderController.Instance.transform, true);
        StoryTextController.Instance.Restart();
        LanderUI.Instance.StartCountdown();
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic);
    }

    public void BeginRun()
    {
        if (Phase != GamePhase.Countdown) return;
        Phase = GamePhase.Flight;
        LanderController.Instance.StartLander();
        ScoringController.Instance.BeginRun();
        StoryTextController.Instance.Restart();
        SaveLoadManager.Instance.Save();
    }

    public void NextLevel()
    {
        if (!CanStartNextLevel) return;
        if (!LandingPadPlacer.Instance.CreateNextPad())
        {
            StoryTextController.Instance.Show("No room for another landing pad. Try flying instead.");
            return;
        }
        LanderChooserManager.Instance.SpawnSelectedLander();
        level++;
        SaveLoadManager.Instance.Data.level = level;
        IsExploring = false;
        ScoringController.Instance.BeginLevel();
        StartArcadeRound();
        SaveLoadManager.Instance.Save();
    }

    public void HandleLanding(Collision2D collision, bool moon)
    {
        Phase = GamePhase.Landed;
        if (moon) IsExploring = true;
        bool awarded = ScoringController.Instance.CalculateScore(collision);
        resultsHaveScore = awarded;
        HasResults = awarded || !moon;
        LanderController.Instance.controlsEnabled = !HasResults;
        if (HasResults) LanderUI.Instance.ShowResults(LanderController.Instance.landerState, awarded);
        MoonEVAController.Instance.RefreshAction();
        if (!moon) AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, 1.2f);
        SaveLoadManager.Instance.Save();
    }

    public void ContinueFlight()
    {
        if (Phase != GamePhase.Landed) return;
        IsRefilling = false;
        IsExploring = true;
        HasResults = false;
        LanderUI.Instance.HideGameOver();
        LanderController.Instance.ResumeFlight();
        SetControlledTarget(LanderController.Instance.transform);
        MoonEVAController.Instance.RefreshAction();
        SaveLoadManager.Instance.Save();
    }

    public void RefillTank()
    {
        if (!CanRefill || IsRefilling || LanderController.Instance.currentFuel >= LanderController.Instance.fuelMax) return;
        IsRefilling = true;
        refillStartFuel = LanderController.Instance.currentFuel;
        refillContactGrace = 0f;
        LanderUI.Instance.RefreshRefillButton();
        SaveLoadManager.Instance.Save();
    }

    public void BeginFlight()
    {
        IsRefilling = false;
        if (Phase == GamePhase.Landed) Phase = GamePhase.Flight;
    }

    public void BeginEVA(Transform astronaut)
    {
        IsRefilling = false;
        IsExploring = true;
        HasResults = false;
        Phase = GamePhase.EVA;
        LanderUI.Instance.HideGameOver();
        SetControlledTarget(astronaut);
        SaveLoadManager.Instance.Save();
    }

    public void BoardLander(LanderController lander)
    {
        IsRefilling = false;
        IsExploring = true;
        HasResults = false;
        Phase = GamePhase.Landed;
        LanderUI.Instance.HideGameOver();
        lander.landerState = LanderController.eLanderState.LandedMoon;
        lander.ResumeFlight();
        SetControlledTarget(lander.transform, true);
    }

    public void HandleCrash(LanderController.eLanderState state)
    {
        IsRefilling = false;
        Phase = GamePhase.Crashed;
        HasResults = true;
        resultsHaveScore = false;
        MoonEVAController.Instance.RefreshAction();
        ImpactFX.Instance.PlayImpactEffect(state);
        LanderUI.Instance.ShowResults(state);
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, -0.8f);
        SaveLoadManager.Instance.Save();
    }

    void Update()
    {
        if (IsRefilling)
        {
            if (CanRefill)
            {
                var ship = LanderController.Instance;
                ship.currentFuel = Mathf.MoveTowards(ship.currentFuel, ship.fuelMax,
                    ship.fuelMax / Mathf.Max(0.1f, refillDuration) * Time.deltaTime);
                if (ship.currentFuel >= ship.fuelMax)
                {
                    IsRefilling = false;
                    SaveLoadManager.Instance.Save();
                }
            }
            else if (Time.time >= refillContactGrace)
            {
                IsRefilling = false;
                SaveLoadManager.Instance.Save();
            }
        }
        if (!worldReady || Time.unscaledTime < nextAutoSave) return;
        SaveLoadManager.Instance.Save();
    }

    public void InvalidateWorldSave() => worldReady = false;

    public void CaptureWorld(SaveGame data)
    {
        if (!worldReady || !RandomLandscape.Instance.HasLayout) return;
        var pads = FindObjectsByType<LandingPadPlacer>(FindObjectsSortMode.None);
        var ships = FindObjectsByType<LanderController>(FindObjectsSortMode.None);
        var world = new WorldSave
        {
            pads = new Vector3[pads.Length], ships = new ShipSave[ships.Length],
            activePad = System.Array.IndexOf(pads, LandingPadPlacer.Instance),
            activeShip = System.Array.IndexOf(ships, LanderController.Instance),
            phase = Phase, exploring = IsExploring, hasResults = HasResults, showScore = resultsHaveScore, refilling = IsRefilling,
            refillStartFuel = IsRefilling ? refillStartFuel : 0f,
            scoring = ScoringController.Instance.CaptureState(pads)
        };
        RandomLandscape.Instance.CaptureWorld(world);
        StoryTextController.Instance.CaptureState(world);
        var moon = GravityManager2D.Instance.transform;
        world.moonPosition = moon.position; world.moonRotation = moon.eulerAngles.z; world.moonScale = moon.localScale;
        for (int i = 0; i < pads.Length; i++) world.pads[i] = pads[i].transform.position;
        for (int i = 0; i < ships.Length; i++) world.ships[i] = ships[i].CaptureState();
        var astronaut = MoonEVAController.Instance.astronaut;
        if (astronaut)
        {
            world.astronaut = new ActorSave();
            world.astronaut.Capture(astronaut.transform, astronaut.GetComponent<Rigidbody2D>());
        }
        if (world.IsValid()) data.world = world;
        nextAutoSave = Time.unscaledTime + Mathf.Max(1f, autoSaveInterval);
    }

    public bool RestoreWorld(WorldSave world)
    {
        if (!world.IsValid()) return false;
        foreach (var ship in world.ships)
            if (!LanderChooserManager.Instance.GetPrefab(ship.definitionId))
            {
                Debug.LogWarning("Saved world contains an unavailable ship. Starting a new world while preserving progress.");
                return false;
            }
        worldReady = false;
        level = SaveLoadManager.Instance.Data.level;
        MoonEVAController.Instance.ResetRun();
        LanderUI.Instance.HideGameOver();
        ImpactFX.Instance.ResetEffect();
        RandomLandscape.Instance.RestoreWorld(world);
        var moon = GravityManager2D.Instance.transform;
        moon.position = world.moonPosition; moon.rotation = Quaternion.Euler(0f, 0f, world.moonRotation); moon.localScale = world.moonScale;
        var oldPads = FindObjectsByType<LandingPadPlacer>(FindObjectsSortMode.None);
        var pads = new LandingPadPlacer[world.pads.Length];
        for (int i = 0; i < pads.Length; i++)
        {
            pads[i] = Instantiate(landingPadPrefab, oldPads[0].transform.parent);
            pads[i].name = "LandingPad " + (i + 1);
            pads[i].RestorePosition(world.pads[i]);
        }
        LandingPadPlacer.Instance = pads[world.activePad];
        foreach (var pad in oldPads) { pad.gameObject.SetActive(false); Destroy(pad.gameObject); }
        var oldShips = FindObjectsByType<LanderController>(FindObjectsSortMode.None);
        var parent = LanderController.Instance.transform.parent;
        foreach (var ship in oldShips) { ship.isActive = false; ship.gameObject.SetActive(false); Destroy(ship.gameObject); }
        var ships = new LanderController[world.ships.Length];
        for (int i = 0; i < ships.Length; i++)
        {
            LanderController.Instance = null;
            ships[i] = Instantiate(LanderChooserManager.Instance.GetPrefab(world.ships[i].definitionId), parent).GetComponent<LanderController>();
            ships[i].RestoreState(world.ships[i], i == world.activeShip);
        }
        LanderController.Instance = ships[world.activeShip];
        ScoringController.Instance.Init();
        ScoringController.Instance.RestoreState(world.scoring, pads);
        StoryTextController.Instance.RestoreState(world);
        Phase = world.phase; IsExploring = world.exploring; HasResults = world.hasResults; resultsHaveScore = world.showScore;
        IsRefilling = world.refilling && Phase == GamePhase.Landed
            && LanderController.Instance.landerState == LanderController.eLanderState.LandedPad;
        refillStartFuel = IsRefilling ? Mathf.Clamp(world.refillStartFuel, 0f, LanderController.Instance.currentFuel) : 0f;
        // Physics restores pad contacts on the next simulation step.
        refillContactGrace = Time.time + 0.5f;
        Physics2D.SyncTransforms();
        if (Phase == GamePhase.EVA)
        {
            MoonEVAController.Instance.RestoreAstronaut(world.astronaut);
            SetControlledTarget(MoonEVAController.Instance.astronaut.transform, true);
        }
        else
        {
            SetControlledTarget(LanderController.Instance.transform, true);
            if (Phase == GamePhase.Countdown) LanderUI.Instance.StartCountdown();
            else if (HasResults) LanderUI.Instance.ShowResults(LanderController.Instance.landerState, resultsHaveScore, true);
        }
        MoonEVAController.Instance.RefreshAction();
        if (!HasResults && LanderController.Instance.deadZoneTriggered) LanderUI.Instance.ShowHideDeadZoneWarning(true);
        worldReady = true;
        nextAutoSave = Time.unscaledTime + Mathf.Max(1f, autoSaveInterval);
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic);
        return true;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
