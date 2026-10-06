using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class StoryDialog : MonoBehaviour
{
    public enum Expression { None, Neutral, Annoyed, Angry, Surprised }

    [SerializeField] TMP_Text messageText;
    [SerializeField] Button continueButton;
    [SerializeField] GameObject neutralPortrait;
    [SerializeField] GameObject annoyedPortrait;
    [SerializeField] GameObject angryPortrait;
    [SerializeField] GameObject surprisedPortrait;

    public event Action ContinueClicked;

    void Awake() => continueButton.onClick.AddListener(OnContinueClicked);
    void OnContinueClicked() => ContinueClicked?.Invoke();

    public void Show(string message, Expression expression = Expression.None, bool canContinue = true)
    {
        messageText.text = message;
        if (neutralPortrait) neutralPortrait.SetActive(expression == Expression.Neutral);
        if (annoyedPortrait) annoyedPortrait.SetActive(expression == Expression.Annoyed);
        if (angryPortrait) angryPortrait.SetActive(expression == Expression.Angry);
        if (surprisedPortrait) surprisedPortrait.SetActive(expression == Expression.Surprised);
        gameObject.SetActive(true);
        SetCanContinue(canContinue);
    }

    public void SetCanContinue(bool canContinue)
    {
        continueButton.interactable = canContinue;
        if (canContinue && EventSystem.current)
            EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
    }

    public void Hide()
    {
        if (continueButton && EventSystem.current && EventSystem.current.currentSelectedGameObject == continueButton.gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (continueButton) continueButton.onClick.RemoveListener(OnContinueClicked);
    }
}
