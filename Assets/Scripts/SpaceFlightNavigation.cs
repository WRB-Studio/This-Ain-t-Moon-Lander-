using TMPro;
using UnityEngine;

public class SpaceFlightNavigation : MonoBehaviour
{
    public static SpaceFlightNavigation Instance { get; private set; }
    [SerializeField] TMP_Text asideText;
    [SerializeField, Min(1)] float asideDuration = 4;
    GameObject asidePanel;
    float asideUntil;

    void Awake()
    {
        Instance = this;
        if (asideText)
        {
            asidePanel = asideText.transform.parent ? asideText.transform.parent.gameObject : asideText.gameObject;
            asidePanel.SetActive(false);
        }
    }

    public string FormatDistance(float distance) => DirectionMarkerSystem.FormatDistance(distance);

    public void ShowAside(string text)
    {
        if (string.IsNullOrEmpty(text) || !asideText || !asidePanel) return;
        asideText.SetLocalizedText(text);
        asideUntil = Time.unscaledTime + asideDuration;
        asidePanel.SetActive(true);
    }

    void LateUpdate()
    {
        if (!asidePanel) return;
        var game = GameController.Instance;
        bool playing = game && game.IsPlaying && !PauseMenu.IsPaused
            && !(RadioController.Instance && RadioController.Instance.IsOpen)
            && !(StoryTextController.Instance && StoryTextController.Instance.IsShowingDialogue);
        asidePanel.SetActive(playing && Time.unscaledTime < asideUntil
            && !(CargoMission.Instance && CargoMission.Instance.Stage == CargoMission.Progress.Failed));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
