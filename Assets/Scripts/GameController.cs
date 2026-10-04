using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance;
    public int level = 1;

    public enum GamePhase { Countdown, Flight, Results, EVA }
    public GamePhase Phase { get; private set; }
    public Transform ControlledTarget { get; private set; }
    public Rigidbody2D ControlledBody { get; private set; }
    public bool IsPlaying => Phase == GamePhase.Flight || Phase == GamePhase.EVA;

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

    public void SetPhase(GamePhase phase) => Phase = phase;

    public void SetControlledTarget(Transform target, bool instantFocus = false)
    {
        ControlledTarget = target;
        ControlledBody = target ? target.GetComponent<Rigidbody2D>() : null;
        CameraController.Instance.SetTarget(target, instantFocus);
        StarField.Instance.SetTarget(target, instantFocus);
    }

    public void StartGame() => PrepareRun(true);
    public void RestartGame() => PrepareRun(false);

    void PrepareRun(bool generateLevel)
    {
        Phase = GamePhase.Countdown;
        MoonEVAController.Instance.ResetRun();
        LanderUI.Instance.HideGameOver();
        ImpactFX.Instance.ResetEffect();
        if (generateLevel)
        {
            RandomLandscape.Instance.GenerateNewLevel();
            LandingPadPlacer.Instance.SetRandomPlaceForPad();
        }

        foreach (var lander in FindObjectsByType<LanderController>(FindObjectsSortMode.None))
        {
            if (lander == LanderController.Instance) continue;
            if (lander.isSecretLander && !LanderChooserManager.Instance.IsSecretFound(lander.landerIndex)) continue;
            Destroy(lander.gameObject);
        }

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
        level++;
        SaveLoadManager.Instance.Data.level = level;
        SaveLoadManager.Instance.Save();
        StartGame();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
