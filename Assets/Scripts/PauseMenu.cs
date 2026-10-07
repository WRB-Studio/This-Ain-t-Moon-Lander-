using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }
    [SerializeField] GameObject panel, settings;
    [SerializeField] Button openButton, resumeButton, settingsButton, exitButton, backButton;
    [SerializeField] TMP_Dropdown language;
    [SerializeField] Slider music, effects;
    float previousTimeScale;
    bool previousAudioPause;
    bool pauseRequested;
    int handledEscapeFrame = -1;
    public static bool IsPaused => Instance && Instance.panel.activeSelf;
    public static bool HandledEscape => Instance && Instance.handledEscapeFrame == Time.frameCount;

    void Awake()
    {
        Instance = this;
        panel.SetActive(false);
        settings.SetActive(false);
        openButton.onClick.AddListener(Open);
        resumeButton.onClick.AddListener(Close);
        settingsButton.onClick.AddListener(() => ShowSettings(true));
        exitButton.onClick.AddListener(ExitGame);
        backButton.onClick.AddListener(() => { ShowSettings(false); SaveLoadManager.Instance.Save(); });
        language.ClearOptions();
        foreach (var item in Localization.Languages) language.options.Add(new TMP_Dropdown.OptionData(item.nativeName));
        language.RefreshShownValue();
        language.onValueChanged.AddListener(index => Localization.SetLanguage(Localization.Languages[index].code));
        music.onValueChanged.AddListener(value =>
        {
            if (!AudioManager.Instance || !SaveLoadManager.Instance || SaveLoadManager.Instance.Data == null) return;
            AudioManager.Instance.SetMusicVolume(value, false);
            SaveLoadManager.Instance.Data.volMusic = value;
        });
        effects.onValueChanged.AddListener(value =>
        {
            if (!AudioManager.Instance || !SaveLoadManager.Instance || SaveLoadManager.Instance.Data == null) return;
            AudioManager.Instance.SetSfxVolume(value, false);
            SaveLoadManager.Instance.Data.volSfx = value;
        });
    }

    void Update()
    {
        if (pauseRequested && CanOpen()) { pauseRequested = false; Open(); }
        openButton.gameObject.SetActive(!IsPaused);
        openButton.interactable = CanOpen();
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (IsPaused)
        {
            handledEscapeFrame = Time.frameCount;
            // Let an expanded language dropdown consume Escape first.
            if (language.IsExpanded) return;
            if (settings.activeSelf) ShowSettings(false);
            else Close();
        }
        else if (!(RadioController.Instance && RadioController.Instance.IsOpen)) Open();
    }

    bool CanOpen() => GameController.Instance && SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null
        && !(StoryTextController.Instance && StoryTextController.Instance.IsTransitioning)
        && !(StationInterior.Instance && StationInterior.Instance.IsTransitioning);

    public void Open()
    {
        if (IsPaused || !CanOpen()) return;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        panel.SetActive(true);
        ShowSettings(false);
        music.SetValueWithoutNotify(AudioManager.Instance.GetMusicVolume());
        effects.SetValueWithoutNotify(AudioManager.Instance.GetSfxVolume());
        for (int i = 0; i < Localization.Languages.Count; i++)
            if (Localization.Languages[i].code == Localization.CurrentLanguage) language.SetValueWithoutNotify(i);
        SaveLoadManager.Instance.Save();
    }

    void ShowSettings(bool visible)
    {
        settings.SetActive(visible);
        resumeButton.interactable = settingsButton.interactable = exitButton.interactable = !visible;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(visible ? backButton.gameObject : resumeButton.gameObject);
    }

    public void Close()
    {
        if (!IsPaused) return;
        language.Hide();
        panel.SetActive(false);
        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        if (StoryTextController.Instance) StoryTextController.Instance.BlockInputUntilRelease();
        if (SaveLoadManager.Instance) SaveLoadManager.Instance.Save();
    }

    void ExitGame()
    {
        if (SaveLoadManager.Instance) SaveLoadManager.Instance.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnApplicationPause(bool paused) { if (paused && !IsPaused) pauseRequested = true; }
    public static IEnumerator WaitUnpaused(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            if (!IsPaused) elapsed += Time.unscaledDeltaTime;
        }
    }
    void OnDisable() => Close();
    void OnDestroy() { Close(); if (Instance == this) Instance = null; }
}
