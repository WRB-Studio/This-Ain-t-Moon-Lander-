using UnityEngine;
using UnityEngine.UI;

public class StationDoorPrompt : MonoBehaviour
{
    public static StationDoorPrompt Instance { get; private set; }
    [SerializeField] Button enterButton;
    [SerializeField] CanvasGroup transitionCurtain;
    public CanvasGroup Curtain => transitionCurtain;
    StationInteraction[] interactions;
    StationInteraction selected;
    TMPro.TMP_Text label;
    void Awake()
    {
        Instance = this;
        enterButton.onClick.AddListener(OnEnterClicked);
        label = enterButton.GetComponentInChildren<TMPro.TMP_Text>();
    }
    void Start() => interactions = FindObjectsByType<StationInteraction>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    void OnEnterClicked() { if (selected) selected.Use(); }
    void LateUpdate()
    {
        var game = GameController.Instance;
        var station = SpaceStation.Instance;
        selected = null;
        bool allowed = station && station.IsAvailable && game && game.ControlledTarget && game.IsPlaying
            && game.Phase == GameController.GamePhase.EVA && interactions != null
            && (!StationConversation.Instance || !StationConversation.Instance.IsShowing)
            && (!StationInterior.Instance || !StationInterior.Instance.IsTransitioning)
            && !StoryTextController.Instance.BlocksGameplayInput;
        if (allowed)
            foreach (var interaction in interactions)
                if (interaction && interaction.IsNear(game.ControlledTarget.position)
                    && (!selected || Vector2.Distance(game.ControlledTarget.position, interaction.transform.position)
                        < Vector2.Distance(game.ControlledTarget.position, selected.transform.position))) selected = interaction;
        bool visible = selected;
        enterButton.gameObject.SetActive(visible);
        if (!visible) return;
        enterButton.interactable = selected.CanUse;
        label.text = selected.buttonLabel;
    }
    void OnDestroy()
    {
        if (enterButton) enterButton.onClick.RemoveListener(OnEnterClicked);
        if (Instance == this) Instance = null;
    }
}
