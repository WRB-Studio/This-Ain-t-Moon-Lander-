using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CargoMissionUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text status;
    [SerializeField] Button talkButton;
    [SerializeField] Button connectButton;
    [SerializeField] Button attachButton;
    [SerializeField] Button openButton;
    [SerializeField] Button retryButton;
    void Awake()
    {
        talkButton.onClick.AddListener(() => CargoMission.Instance.Talk());
        connectButton.onClick.AddListener(() => CargoMission.Instance.TakeCable());
        attachButton.onClick.AddListener(() => CargoMission.Instance.AttachCable());
        openButton.onClick.AddListener(() => CargoMission.Instance.OpenCapsule());
        retryButton.onClick.AddListener(() => CargoMission.Instance.Retry());
    }
    void LateUpdate()
    {
        var mission = CargoMission.Instance;
        var game = GameController.Instance;
        bool visible = mission && mission.IsAvailable && game && game.IsPlaying && !PauseMenu.IsPaused
            && !(RadioController.Instance && RadioController.Instance.IsOpen) && !mission.IsShowing
            && !(StationConversation.Instance && StationConversation.Instance.IsShowing)
            && !StoryTextController.Instance.BlocksGameplayInput;
        panel.SetActive(visible);
        if (!visible) return;
        status.SetLocalizedText(mission.Status);
        talkButton.gameObject.SetActive(mission.CanTalk);
        connectButton.gameObject.SetActive(mission.CanTakeCable);
        attachButton.gameObject.SetActive(mission.CanAttachCable);
        openButton.gameObject.SetActive(mission.CanOpen);
        retryButton.gameObject.SetActive(mission.Stage == CargoMission.Progress.Failed);
    }
}
