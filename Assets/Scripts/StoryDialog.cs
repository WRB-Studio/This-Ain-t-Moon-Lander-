using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StoryDialog : MonoBehaviour
{
    public enum Expression { None, Neutral, Annoyed, Angry, Surprised, StationCrew }

    [SerializeField] TMP_Text messageText;
    [SerializeField] Button continueButton;
    [SerializeField] GameObject conversationContent;
    [SerializeField] TMP_Text speakerText;
    [SerializeField] TMP_Text conversationText;
    [SerializeField] Button[] answerButtons;
    [SerializeField] Image storyPortrait;
    TMP_Text[] answerLabels;
    StorySession storySession;
    bool drawingStory;
    bool legacyStory;
    string legacyPrefix;
    Action storyCompleted;
    StoryLineAudio storyAudio;
    public string LastStoryReactionReference { get; private set; }

    public event Action ContinueClicked;
    public event Action<int> AnswerClicked;

    void Awake()
    {
        continueButton.onClick.AddListener(OnContinueClicked);
        answerLabels = new TMP_Text[answerButtons.Length];
        for (int i = 0; i < answerButtons.Length; i++)
        {
            int index = i;
            answerLabels[i] = answerButtons[i].GetComponentInChildren<TMP_Text>(true);
            answerButtons[i].onClick.AddListener(() => SelectStoryAnswer(index));
        }
    }
    void OnContinueClicked() => ContinueClicked?.Invoke();

    public void Show(string message, Expression expression = Expression.None, bool canContinue = true)
    {
        if (!drawingStory) { storySession = null; storyCompleted = null; LastStoryReactionReference = null; StopStoryAudio(); }
        if (storyPortrait) storyPortrait.gameObject.SetActive(false);
        conversationContent.SetActive(false);
        foreach (var button in answerButtons) button.gameObject.SetActive(false);
        messageText.gameObject.SetActive(true);
        continueButton.gameObject.SetActive(true);
        var authored = !drawingStory && StoryLibrary.Project ? StoryLibrary.Project.Match(message, StoryLibrary.Flag) : null;
        messageText.SetLocalizedText(authored == null ? message : "[[" + authored.textKey + "]]");
        gameObject.SetActive(true);
        SetCanContinue(canContinue);
        if (authored != null) PlayStoryAudio(authored);
    }

    public void ShowConversation(string speaker, string message, string[] answers)
    {
        if (!drawingStory && StoryLibrary.Project)
        {
            var node = StoryLibrary.Project.Match(message, StoryLibrary.Flag);
            if (node != null && node.choices.Count > 0)
            {
                LastStoryReactionReference = null;
                legacyPrefix = message.Substring(0, message.Length - node.sourceReference.Length);
                storySession = StoryLibrary.Session(); legacyStory = true;
                if (storySession.Begin(node)) { DrawStory(); return; }
            }
        }
        Show(string.Empty, Expression.StationCrew, false);
        messageText.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);
        conversationContent.SetActive(true);
        speakerText.SetLocalizedText(Localization.Reference(speaker) + ":");
        conversationText.SetLocalizedText(message);
        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool visible = i < answers.Length;
            answerButtons[i].gameObject.SetActive(visible);
            if (visible) answerLabels[i].SetLocalizedText(answers[i]);
        }
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    public bool ShowStory(string nodeId, Action completed = null)
    {
        if (!StoryLibrary.Project) return false;
        var session = StoryLibrary.Session();
        if (!session.Begin(StoryLibrary.Project.Find(nodeId))) return false;
        storySession = session; legacyStory = false; legacyPrefix = null; storyCompleted = completed;
        DrawStory(); return true;
    }
    void DrawStory()
    {
        var node = storySession.Node;
        var choices = storySession.Choices;
        if (choices.Count > answerButtons.Length) Debug.LogError("Story has more answers than the authored dialog buttons: " + node.id);
        drawingStory = true;
        var text = string.IsNullOrEmpty(storySession.Reaction) ? "[[" + node.textKey + "]]"
            : "[[" + storySession.Reaction + "]]\n\n[[" + node.textKey + "]]";
        if (!string.IsNullOrEmpty(legacyPrefix)) text = legacyPrefix + text;
        ShowConversation(node.speaker ? "[[" + node.speaker.nameKey + "]]" : "", text,
            choices.Select(c => "[[" + c.textKey + "]]").ToArray());
        drawingStory = false;
        if (storyPortrait)
        {
            storyPortrait.sprite = node.speaker ? node.speaker.portrait : null;
            storyPortrait.gameObject.SetActive(node.channel != StoryChannel.Radio && storyPortrait.sprite);
        }
        PlayStoryAudio(node);
        StoryLibrary.Save();
    }
    void SelectStoryAnswer(int index)
    {
        if (storySession == null) { AnswerClicked?.Invoke(index); return; }
        if (!storySession.Choose(index, legacyStory)) return;
        legacyPrefix = null;
        if (storySession.Node != null) { DrawStory(); return; }
        int original = storySession.OriginalAnswer;
        LastStoryReactionReference = string.IsNullOrEmpty(storySession.Reaction) ? null : "[[" + storySession.Reaction + "]]";
        storySession = null; StopStoryAudio(); StoryLibrary.Save();
        if (legacyStory) AnswerClicked?.Invoke(original);
        else { var completed = storyCompleted; storyCompleted = null; Hide(); completed?.Invoke(); }
    }
    void PlayStoryAudio(StoryNode node)
    {
        if (!storyAudio) storyAudio = gameObject.AddComponent<StoryLineAudio>();
        storyAudio.Play(node);
    }
    void StopStoryAudio() { if (storyAudio) storyAudio.Stop(); }

    public void SetCanContinue(bool canContinue)
    {
        continueButton.interactable = canContinue;
        if (canContinue && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
    }

    public void Hide()
    {
        storySession = null; storyCompleted = null; StopStoryAudio();
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(null);
        gameObject.SetActive(false);
    }

    void OnEnable() => Localization.Changed += RefreshStoryLanguage;
    void OnDisable() { Localization.Changed -= RefreshStoryLanguage; StopStoryAudio(); }
    void RefreshStoryLanguage() { if (storySession?.Node != null) DrawStory(); }

    void OnDestroy()
    {
        if (continueButton) continueButton.onClick.RemoveListener(OnContinueClicked);
    }
}
