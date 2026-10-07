using System;
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
    TMP_Text[] answerLabels;

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
            answerButtons[i].onClick.AddListener(() => AnswerClicked?.Invoke(index));
        }
    }
    void OnContinueClicked() => ContinueClicked?.Invoke();

    public void Show(string message, Expression expression = Expression.None, bool canContinue = true)
    {
        conversationContent.SetActive(false);
        foreach (var button in answerButtons) button.gameObject.SetActive(false);
        messageText.gameObject.SetActive(true);
        continueButton.gameObject.SetActive(true);
        messageText.text = message;
        gameObject.SetActive(true);
        SetCanContinue(canContinue);
    }

    public void ShowConversation(string speaker, string message, string[] answers)
    {
        Show(string.Empty, Expression.StationCrew, false);
        messageText.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);
        conversationContent.SetActive(true);
        speakerText.text = speaker + ":";
        conversationText.text = message;
        for (int i = 0; i < answerButtons.Length; i++)
        {
            bool visible = i < answers.Length;
            answerButtons[i].gameObject.SetActive(visible);
            if (visible) answerLabels[i].text = answers[i];
        }
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    public void SetCanContinue(bool canContinue)
    {
        continueButton.interactable = canContinue;
        if (canContinue && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
    }

    public void Hide()
    {
        if (EventSystem.current && EventSystem.current.currentSelectedGameObject
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(null);
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (continueButton) continueButton.onClick.RemoveListener(OnContinueClicked);
    }
}
