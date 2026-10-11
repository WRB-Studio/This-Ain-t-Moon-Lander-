using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrainingBriefingPanel : MonoBehaviour
{
    [SerializeField] TMP_Text speakerText;
    [SerializeField] TMP_Text messageText;
    [SerializeField] Button continueButton;
    [SerializeField] Image storyPortrait;
    [SerializeField] StoryCharacter pilot;
    [SerializeField] StoryLineAudio audioPlayer;
    public event Action ContinueClicked;

    void Awake() => continueButton.onClick.AddListener(Continue);
    void Continue() => ContinueClicked?.Invoke();

    public void Show(string message, bool canContinue = true)
    {
        var node = StoryLibrary.Project ? StoryLibrary.Project.Match(message, StoryLibrary.Flag) : null;
        var speaker = node?.speaker ? node.speaker : pilot;
        speakerText.SetLocalizedText(speaker ? "[[" + speaker.nameKey + "]]" : "[[story.training.speaker]]");
        if (storyPortrait)
        {
            storyPortrait.sprite = speaker ? speaker.portrait : null;
            storyPortrait.gameObject.SetActive(storyPortrait.sprite);
        }
        messageText.SetLocalizedText(node == null ? message : "[[" + node.textKey + "]]");
        gameObject.SetActive(true);
        SetCanContinue(canContinue);
        if (audioPlayer) audioPlayer.Play(node);
    }

    public void SetCanContinue(bool enabled) => continueButton.interactable = enabled;
    public void Hide() { if (audioPlayer) audioPlayer.Stop(); gameObject.SetActive(false); }
    void OnDisable() { if (audioPlayer) audioPlayer.Stop(); }
    void OnDestroy() { if (continueButton) continueButton.onClick.RemoveListener(Continue); }
}
