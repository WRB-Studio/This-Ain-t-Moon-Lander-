using System;
using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance;

    public enum GamePhase
    {
        LandingRun,
        SpaceFlight,
        EVA
    }

    public int level = 1;
    public GamePhase Phase { get; private set; } = GamePhase.LandingRun;
    public event Action<GamePhase> PhaseChanged;

    void Awake() => Instance = this;

    void Start()
    {
        LanderController.ActiveChanged += OnActiveLanderChanged;

        InitScripts();
        level = SaveLoadManager.Instance.Data.level;
        StartLandingRun();
    }

    void OnDestroy()
    {
        LanderController.ActiveChanged -= OnActiveLanderChanged;
    }

    void InitScripts()
    {
        SaveLoadManager.Instance.Init();
        AudioManager.Instance.Init();
        ImpactFX.Instance.Init();
        ScoringController.Instance.Init();

        GravityManager2D.Instance.Init();
        LanderController.Active.Init();
        LanderChooserManager.Instance.Init();

        CameraController.Instance.Init();
        StarField.Instance.Init();
        LanderUI.Instance.Init();

        LandingPadPlacer.Instance.Init();
        RandomLandscape.Instance.Init();
        StoryTextController.Instance.Init();
        MoonEVAController.Instance.Init();
    }

    public void StartGame() => StartLandingRun();

    public void StartLandingRun()
    {
        SetPhase(GamePhase.LandingRun);
        MoonEVAController.Instance.ResetRunState();

        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, pitch: 1f);

        RandomLandscape.Instance.GenerateNewLevel();
        LandingPadPlacer.Instance.SetRandomPlaceForPad();
        LanderController.Active.ResetLander();

        SetControlledTarget(LanderController.Active.transform, GamePhase.LandingRun, true);

        LanderUI.Instance.StartCountdown();
        StoryTextController.Instance.Restart();
        CleanupInactiveSceneLanders();
    }

    public void SetControlledTarget(Transform target, GamePhase phase, bool instantFocus = false)
    {
        if (!target) return;

        SetPhase(phase);

        if (CameraController.Instance)
            CameraController.Instance.SetTarget(target, phase, instantFocus);

        if (StarField.Instance)
            StarField.Instance.SetTarget(target, instantFocus);
    }

    public void SetPhase(GamePhase phase)
    {
        if (Phase == phase) return;
        Phase = phase;
        PhaseChanged?.Invoke(phase);
    }

    void OnActiveLanderChanged(LanderController lander)
    {
        if (!lander || Phase == GamePhase.EVA) return;
        SetControlledTarget(lander.transform, Phase, true);
    }

    void CleanupInactiveSceneLanders()
    {
        LanderController[] landers = FindObjectsByType<LanderController>(FindObjectsSortMode.None);

        foreach (LanderController lander in landers)
        {
            if (!lander || lander == LanderController.Active) continue;
            if (lander.isSecretLander && !LanderChooserManager.Instance.IsSecretFound(lander)) continue;

            Destroy(lander.gameObject);
        }
    }

    public void NextLevel()
    {
        level++;
        SaveLoadManager.Instance.Data.level = level;
        SaveLoadManager.Instance.Save();
    }
}
