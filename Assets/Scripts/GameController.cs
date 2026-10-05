using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance;
    [Tooltip("Used for terrain previews in Edit Mode. In Play Mode the saved level replaces this value.")]
    [Min(1)] public int level = 1;

    public enum GamePhase { Countdown, Flight, Landed, EVA, Crashed }
    public GamePhase Phase { get; private set; }
    public bool HasResults { get; private set; }
    public bool IsExploring { get; private set; }
    public bool CanStartNextLevel => HasResults && Phase == GamePhase.Landed && !IsExploring;
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
        StartGame();
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
        StartArcadeRound();
    }

    void GenerateWorld()
    {
        RandomLandscape.Instance.GenerateNewLevel();
        LandingPadPlacer.Instance.SetRandomPlaceForPad();
    }

    public void RestartGame() => StartArcadeRound();

    void StartArcadeRound()
    {
        Phase = GamePhase.Countdown;
        HasResults = false;
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
    }

    public void NextLevel()
    {
        if (!CanStartNextLevel) return;
        level++;
        SaveLoadManager.Instance.Data.level = level;
        SaveLoadManager.Instance.Save();
        StartGame();
    }

    public void HandleLanding(Collision2D collision, bool moon)
    {
        Phase = GamePhase.Landed;
        if (moon) IsExploring = true;
        bool awarded = ScoringController.Instance.CalculateScore(collision);
        HasResults = awarded || !IsExploring;
        LanderController.Instance.controlsEnabled = !HasResults;
        if (HasResults) LanderUI.Instance.ShowResults(LanderController.Instance.landerState, awarded);
        MoonEVAController.Instance.RefreshAction();
        if (!moon) AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, 1.2f);
    }

    public void ContinueFlight()
    {
        if (Phase != GamePhase.Landed) return;
        IsExploring = true;
        HasResults = false;
        LanderUI.Instance.HideGameOver();
        LanderController.Instance.ResumeFlight();
        SetControlledTarget(LanderController.Instance.transform);
        MoonEVAController.Instance.RefreshAction();
    }

    public void BeginFlight()
    {
        if (Phase == GamePhase.Landed) Phase = GamePhase.Flight;
    }

    public void BeginEVA(Transform astronaut)
    {
        IsExploring = true;
        HasResults = false;
        Phase = GamePhase.EVA;
        LanderUI.Instance.HideGameOver();
        SetControlledTarget(astronaut);
    }

    public void BoardLander(LanderController lander)
    {
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
        Phase = GamePhase.Crashed;
        HasResults = true;
        MoonEVAController.Instance.RefreshAction();
        ImpactFX.Instance.PlayImpactEffect(state);
        LanderUI.Instance.ShowResults(state);
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, -0.8f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
