using UnityEngine;

public class StationConversation : MonoBehaviour
{
    public static StationConversation Instance { get; private set; }
    [System.Serializable]
    public class Answer
    {
        public string text;
        [TextArea(2, 6)] public string reaction;
    }
    [System.Serializable]
    public class ConversationStep
    {
        [TextArea(2, 8)] public string message;
        public Answer[] answers;
    }
    [SerializeField] string speaker = "Registration";
    [SerializeField] ConversationStep[] steps;
    [SerializeField, TextArea] string repeatMessage = "The pickup coordinates are still on your radio.\n\nInvestigate, go home, or enjoy the corridor.\nI have plenty of forms to keep me company.";
    [SerializeField] string goodbyeAnswer = "Thanks. I'll take a look.";
    public bool IsShowing { get; private set; }
    StoryDialog dialog;
    CanvasGroup hud;
    float previousTimeScale;
    float previousHudAlpha;
    bool previousInteractable, previousRaycasts;
    bool repeat;
    void Awake() => Instance = this;
    void Start()
    {
        dialog = StoryTextController.Instance.Dialog;
        hud = StoryTextController.Instance.GameplayVisibility;
        dialog.AnswerClicked += SelectAnswer;
    }
    public void BeginConversation()
    {
#if UNITY_EDITOR
        if (!DebugEnabled) return;
#endif
        if (IsShowing || !dialog || steps == null || steps.Length == 0 || !GameController.Instance.IsPlaying || StoryTextController.Instance.BlocksGameplayInput
            || !StationInterior.Instance || StationInterior.Instance.CurrentArea != StationInterior.Area.Registration) return;
        StoryTextController.Instance.Restart();
        repeat = SaveLoadManager.Instance.Data.GetFlag("story.registrationComplete");
        IsShowing = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        previousHudAlpha = hud.alpha;
        previousInteractable = hud.interactable;
        previousRaycasts = hud.blocksRaycasts;
        hud.alpha = 0f;
        hud.interactable = hud.blocksRaycasts = false;
        ShowPage();
    }
    void ShowPage()
    {
        var data = SaveLoadManager.Instance.Data;
        if (repeat)
        {
            dialog.ShowConversation(speaker, repeatMessage, new[] { goodbyeAnswer });
            return;
        }
        int page = Mathf.Clamp(data.stationConversationPage, 0, steps.Length - 1);
        data.stationConversationPage = page;
        var step = steps[page];
        string[] answers = System.Array.ConvertAll(step.answers, answer => answer.text);
        string reaction = data.stationConversationReaction;
        string message = string.IsNullOrEmpty(reaction) ? step.message : reaction + "\n\n" + step.message;
        dialog.ShowConversation(speaker, message, answers);
    }
    void SelectAnswer(int index)
    {
        if (!IsShowing) return;
        var data = SaveLoadManager.Instance.Data;
        if (repeat) { Close(); return; }
        var step = steps[data.stationConversationPage];
        if (index < 0 || index >= step.answers.Length) return;
        data.stationConversationReaction = step.answers[index].reaction;
        data.stationConversationPage++;
        if (data.stationConversationPage >= steps.Length)
        {
            data.SetFlag("story.registrationComplete", true);
            data.nextSignalStartDistance = 0f;
            var ship = LanderController.Instance;
            if (ship && ship.StationPad)
            {
                var specification = LanderChooserManager.Instance.GetPrefab(ship.landerIndex).GetComponent<LanderController>();
                if (ship.isSecretLander) ship.fuelMax = Mathf.Max(ship.fuelMax, specification.fuelMax);
                ship.currentFuel = ship.fuelMax;
            }
            Close();
        }
        else ShowPage();
        SaveLoadManager.Instance.Save();
    }
    public void Close()
    {
        if (!IsShowing) return;
        IsShowing = false;
        if (dialog) dialog.Hide();
        Time.timeScale = previousTimeScale;
        if (hud)
        {
            hud.alpha = previousHudAlpha;
            hud.interactable = previousInteractable;
            hud.blocksRaycasts = previousRaycasts;
        }
        if (StoryTextController.Instance) StoryTextController.Instance.BlockInputUntilRelease();
    }
#if UNITY_EDITOR
    public bool DebugEnabled { get; private set; } = true;
    public void DebugSetProgress(bool enabled, bool completed)
    {
        Close();
        DebugEnabled = enabled;
        var data = SaveLoadManager.Instance.Data;
        data.SetFlag("story.registrationComplete", completed);
        data.stationConversationPage = completed ? steps.Length : 0;
        data.stationConversationReaction = null;
        data.nextSignalStartDistance = 0f;
        SaveLoadManager.Instance.Save();
    }
    public void DebugReset()
    {
        Close();
        DebugEnabled = true;
        var data = SaveLoadManager.Instance.Data;
        data.stationConversationPage = 0;
        data.stationConversationReaction = null;
        data.nextSignalStartDistance = 0f;
        data.SetFlag("story.registrationComplete", false);
        data.SetFlag("story.stationGuideEntered", false);
        foreach (var resident in FindObjectsByType<StationResident>(FindObjectsInactive.Include, FindObjectsSortMode.None)) resident.DebugReset();
        SaveLoadManager.Instance.Save();
    }
#endif
    void OnDisable() => Close();
    void OnDestroy()
    {
        if (dialog) dialog.AnswerClicked -= SelectAnswer;
        if (Instance == this) Instance = null;
    }
}
