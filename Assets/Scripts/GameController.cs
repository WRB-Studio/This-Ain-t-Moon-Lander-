using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController Instance;
    public int level = 1;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        InitScripts();
        level = SaveLoadManager.Instance.Data.level;
        StartGame();
    }

    private void InitScripts()
    {
        StarField.Instance.Init();
        
        SaveLoadManager.Instance.Init();

        AudioManager.Instance.Init();

        ImpactFX.Instance.Init();
        ScoringController.Instance.Init();

        LanderController.Instance.Init();
        LanderChooserManager.Instance.Init();
        LanderUI.Instance.Init();

        CameraController.Instance.Init();
        GravityManager2D.Instance.Init();

        RandomLandscape.Instance.Init();
        LandingPadPlacer.Instance.Init();

        StoryTextController.Instance.Init();
    }

    public void StartGame()
    {
        AudioManager.Instance.PlayMusic(AudioManager.Instance.mainMusic, pitch: 1f);

        RandomLandscape.Instance.GenerateNewLevel();
        LandingPadPlacer.Instance.SetRandomPlaceForPad();
        LanderController.Instance.ResetLander();
        CameraController.Instance.SetInstantFocus();
        LanderUI.Instance.StartCountdown();
        StoryTextController.Instance.Restart();
    }

    public void NextLevel()
    {
        level++;
        SaveLoadManager.Instance.Data.level = level;
        SaveLoadManager.Instance.Save();
    }
}
