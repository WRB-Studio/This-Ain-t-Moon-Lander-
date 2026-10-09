using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RadioController : MonoBehaviour
{
    [Serializable] public class Portrait
    {
        public StoryDialog.Expression expression;
        public GameObject visual;
    }

    public static RadioController Instance { get; private set; }
    [SerializeField] Button openButton;
    [SerializeField] TMP_Text openLabel;
    [SerializeField] GameObject unreadLight;
    [SerializeField] GameObject device;
    [SerializeField] Button closeButton, previousButton, nextButton, trackButton;
    [SerializeField] Button previousSignalButton, nextSignalButton;
    [SerializeField] TMP_Text signalNameText;
    [SerializeField] TMP_Text senderText, titleText, bodyText, pageText, signalText, trackLabel;
    [SerializeField] ScrollRect messageScroll;
    [SerializeField] Button[] replyButtons;
    [SerializeField] Portrait[] portraits;
    [SerializeField] GameObject voiceOnly;
    [SerializeField] RectTransform navigationIcon;
    [SerializeField] Graphic navigationGraphic;
    [SerializeField] TMP_Text navigationDistance;
    [SerializeField] float orbitRadius = 150f;
    [SerializeField] RadioMessage pickupMessage = new()
    {
        id = "station.pickup", sender = "[[game.registration]]", title = "[[game.last.confirmed.pickup]]",
        body = "[[game.your.radio.is.registered.equipment.issued.acceptance.recorded.att]]",
        portrait = StoryDialog.Expression.StationCrew, signal = "pickup",
        replies = new[]
        {
            new RadioReply { text = "[[game.received.i.ll.take.a.look]]", reaction = "[[game.excellent.a.confirmed.receipt.my.favourite.kind.of.adventure]]" },
            new RadioReply { text = "[[game.i.m.not.your.courier]]", reaction = "[[game.noted.i.ll.put.that.under.courier.comments]]" }
        }
    };
    int selected;
    int selectedSignal;
    readonly List<RadioMessage> signals = new();
    readonly RadioMessage stationSignal = new() { id = "station.beacon", title = "[[game.station]]", signal = "station" };
    float previousTimeScale;
    bool initialized;
    TMP_Text[] replyLabels;
    SaveGame Data => SaveLoadManager.Instance ? SaveLoadManager.Instance.Data : null;
    public bool IsOwned => Data != null && Data.GetFlag("story.radioOwned");
    public bool IsOpen => device && device.activeSelf;
    public bool HasTrackedSignal => IsOwned && TrackedMessage != null;
    RadioMessage TrackedMessage => Data?.radioTrackedMessageId == stationSignal.id
        ? SpaceStation.Instance && SpaceStation.Instance.IsAvailable ? stationSignal : null
        : Data?.radioMessages?.Find(m => m.id == Data.radioTrackedMessageId && !string.IsNullOrEmpty(m.signal));
    RadioMessage SelectedSignal => signals.Count > 0 ? signals[selectedSignal] : null;

    void Awake()
    {
        Instance = this;
        device.SetActive(false);
        openButton.gameObject.SetActive(false);
        navigationIcon.gameObject.SetActive(false);
        openButton.onClick.AddListener(Open);
        closeButton.onClick.AddListener(Close);
        previousButton.onClick.AddListener(() => Select(selected - 1));
        nextButton.onClick.AddListener(() => Select(selected + 1));
        previousSignalButton.onClick.AddListener(() => SelectSignal(selectedSignal - 1));
        nextSignalButton.onClick.AddListener(() => SelectSignal(selectedSignal + 1));
        trackButton.onClick.AddListener(TrackSelected);
        replyLabels = new TMP_Text[replyButtons.Length];
        for (int i = 0; i < replyButtons.Length; i++)
        {
            int index = i;
            replyLabels[i] = replyButtons[i].GetComponentInChildren<TMP_Text>(true);
            replyButtons[i].onClick.AddListener(() => Reply(index));
        }
    }

    void Update()
    {
        if (Data == null || !GameController.Instance || PauseMenu.IsPaused) return;
        if (!initialized)
        {
            initialized = true;
            if (IsOwned && Data.GetFlag("story.registrationComplete")) ReceivePickup();
            selected = Mathf.Max(0, Data.radioMessages.Count - 1);
        }
        bool available = IsOwned && (GameController.Instance.IsPlaying
            || GameController.Instance.Phase == GameController.GamePhase.Landed)
            && !(StationConversation.Instance && StationConversation.Instance.IsShowing)
            && !(CargoMission.Instance && CargoMission.Instance.IsShowing)
            && !(StoryTextController.Instance && ((StoryTextController.Instance.BlocksGameplayInput && !IsOpen)
                || StoryTextController.Instance.HasPendingDialogue))
            && !(StationInterior.Instance && StationInterior.Instance.IsTransitioning);
        openButton.gameObject.SetActive(available && !IsOpen);
        if (IsOpen && !available) Close();
        int unread = Data.radioMessages.Count(m => !m.read);
        openLabel.SetLocalizedText(unread > 0 ? $"[[game.radio]] ({unread})" : "[[game.radio]]");
        unreadLight.SetActive(unread > 0 && Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.8f);
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetKeyDown(KeyCode.R)) { if (IsOpen) Close(); else if (available) Open(); }
        if (IsOpen && !PauseMenu.HandledEscape && Input.GetKeyDown(KeyCode.Escape)) Close();
#endif
        if (IsOpen) UpdateSignalText();
    }

    public void Acquire()
    {
        if (Data == null) return;
        Data.SetFlag("story.radioOwned", true);
    }

    public void ReceivePickup()
    {
        var message = JsonUtility.FromJson<RadioMessage>(JsonUtility.ToJson(pickupMessage));
        message.coordinates = CargoMission.Instance ? CargoMission.Instance.SignalTarget.position
            : StationInterior.Instance && StationInterior.Instance.NextSignalTarget
            ? StationInterior.Instance.NextSignalTarget.position : Vector3.zero;
        Receive(message);
    }

    public void Receive(RadioMessage message)
    {
        if (!IsOwned || message == null || string.IsNullOrEmpty(message.id)
            || Data.radioMessages.Exists(m => m.id == message.id)) return;
        Data.radioMessages.Add(message);
        if (IsOpen)
        {
            RefreshSignals();
            Select(Data.radioMessages.Count - 1);
        }
        if (AudioManager.Instance) AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdownStart, 0.25f, 1.4f);
        SaveLoadManager.Instance.Save();
    }

    public void Open()
    {
        if (CargoMission.Instance && CargoMission.Instance.IsShowing) return;
        if (PauseMenu.IsPaused || !IsOwned || IsOpen || !GameController.Instance
            || !(GameController.Instance.IsPlaying || GameController.Instance.Phase == GameController.GamePhase.Landed)
            || (StoryTextController.Instance && (StoryTextController.Instance.BlocksGameplayInput
                || StoryTextController.Instance.HasPendingDialogue))
            || (StationConversation.Instance && StationConversation.Instance.IsShowing)
            || (StationInterior.Instance && StationInterior.Instance.IsTransitioning)) return;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        device.SetActive(true);
        openButton.gameObject.SetActive(false);
        RefreshSignals();
        Select(selected);
    }

    public void Close()
    {
        if (!IsOpen) return;
        device.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        if (StoryTextController.Instance) StoryTextController.Instance.BlockInputUntilRelease();
    }

    void Select(int index)
    {
        selected = Mathf.Clamp(index, 0, Mathf.Max(0, Data.radioMessages.Count - 1));
        if (Data.radioMessages.Count == 0)
        {
            senderText.SetLocalizedText("[[game.no.transmissions]]");
            titleText.SetLocalizedText("[[game.receiver.ready]]");
            bodyText.SetLocalizedText("[[game.new.messages.will.be.stored.here]]");
            pageText.SetLocalizedText("0 / 0");
            ShowPortrait(StoryDialog.Expression.None);
            previousButton.interactable = nextButton.interactable = false;
            foreach (var button in replyButtons) button.gameObject.SetActive(false);
            UpdateSignalText();
            return;
        }
        var message = Data.radioMessages[selected];
        bool unread = !message.read;
        message.read = true;
        senderText.SetLocalizedText(message.sender);
        titleText.SetLocalizedText(message.title);
        bodyText.SetLocalizedText(Localization.Reference(message.body));
        if (message.chosenReply >= 0 && message.replies != null && message.chosenReply < message.replies.Length)
        {
            var reply = message.replies[message.chosenReply];
            bodyText.SetLocalizedText(Localization.Reference(message.body) + $"\n\n[[radio.you]]: {Localization.Reference(reply.text)}\n\n{Localization.Reference(message.sender)}: {Localization.Reference(reply.reaction)}");
        }
        pageText.SetLocalizedText($"{selected + 1} / {Data.radioMessages.Count}");
        previousButton.interactable = selected > 0;
        nextButton.interactable = selected < Data.radioMessages.Count - 1;
        ShowPortrait(message.portrait);
        for (int i = 0; i < replyButtons.Length; i++)
        {
            bool visible = message.chosenReply < 0 && message.replies != null && i < message.replies.Length;
            replyButtons[i].gameObject.SetActive(visible);
            if (visible) replyLabels[i].SetLocalizedText(message.replies[i].text);
        }
        Canvas.ForceUpdateCanvases();
        messageScroll.verticalNormalizedPosition = 1f;
        UpdateSignalText();
        if (unread) SaveLoadManager.Instance.Save();
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    void ShowPortrait(StoryDialog.Expression expression)
    {
        bool visible = false;
        foreach (var portrait in portraits)
        {
            bool show = expression != StoryDialog.Expression.None && portrait.expression == expression;
            portrait.visual.SetActive(show);
            visible |= show;
        }
        voiceOnly.SetActive(!visible);
    }

    void Reply(int index)
    {
        if (!IsOpen || Data.radioMessages.Count == 0) return;
        var message = Data.radioMessages[selected];
        if (message.chosenReply >= 0 || message.replies == null || index < 0 || index >= message.replies.Length) return;
        message.chosenReply = index;
        Select(selected);
        SaveLoadManager.Instance.Save();
    }

    void TrackSelected()
    {
        if (!IsOpen || SelectedSignal == null) return;
        var signal = SelectedSignal;
        Data.radioTrackedMessageId = Data.radioTrackedMessageId == signal.id ? null : signal.id;
        UpdateSignalText();
        SaveLoadManager.Instance.Save();
    }

    void RefreshSignals()
    {
        string currentId = SelectedSignal?.id ?? Data.radioTrackedMessageId;
        signals.Clear();
        if (SpaceStation.Instance && SpaceStation.Instance.IsAvailable) signals.Add(stationSignal);
        foreach (var message in Data.radioMessages)
            if (!string.IsNullOrEmpty(message.signal) && !signals.Exists(s => s.signal == message.signal)) signals.Add(message);
        selectedSignal = Mathf.Max(0, signals.FindIndex(s => s.id == currentId));
    }

    void SelectSignal(int index)
    {
        if (!IsOpen || signals.Count == 0) return;
        selectedSignal = (index % signals.Count + signals.Count) % signals.Count;
        UpdateSignalText();
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }

    Vector3 Destination(RadioMessage message)
    {
        if (message.signal == "pickup" && CargoMission.Instance) return CargoMission.Instance.SignalTarget.position;
        if (message.signal == "station.delivery" && CargoMission.Instance) return CargoMission.Instance.DeliverySurface.bounds.center;
        if (message.signal == "station" && SpaceStation.Instance) return SpaceStation.Instance.transform.position;
        if (message.signal == "pickup" && StationInterior.Instance && StationInterior.Instance.NextSignalTarget)
            return StationInterior.Instance.NextSignalTarget.position;
        return message.coordinates;
    }

    void UpdateSignalText()
    {
        var message = SelectedSignal;
        previousSignalButton.interactable = nextSignalButton.interactable = signals.Count > 1;
        trackButton.gameObject.SetActive(message != null);
        if (message == null)
        {
            signalNameText.SetLocalizedText("[[game.no.known.signals]]");
            signalText.SetLocalizedText("[[game.coordinates.appear.here.when.a.signal.is.received]]");
            return;
        }
        signalNameText.SetLocalizedText($"{Localization.Reference(message.title)}  ({selectedSignal + 1}/{signals.Count})");
        trackLabel.SetLocalizedText(Data.radioTrackedMessageId == message.id ? "[[game.stop.tracking]]" : "[[game.track.signal]]");
        var position = Destination(message);
        float distance = GameController.Instance.ControlledTarget
            ? Vector2.Distance(GameController.Instance.ControlledTarget.position, position) : 0f;
        string status = Data.radioTrackedMessageId == message.id ? "[[radio.active]]" : "[[radio.selected]]";
        signalText.SetLocalizedText($"{status}  {position.x:0}, {position.y:0}\n[[radio.distance]]  {distance:0} [[radio.units]]");
    }

    void LateUpdate()
    {
        var message = TrackedMessage;
        var game = GameController.Instance;
        var camera = Camera.main;
        bool visible = IsOwned && message != null && game && game.ControlledTarget && camera && !IsOpen
            && game.IsPlaying && (game.Phase == GameController.GamePhase.Flight
                || game.Phase == GameController.GamePhase.EVA && !(StationInterior.Instance && StationInterior.Instance.IsInside));
        navigationIcon.gameObject.SetActive(visible);
        if (!visible) return;
        Vector3 destination = Destination(message);
        Vector2 sourceScreen = camera.WorldToScreenPoint(game.ControlledTarget.position);
        Vector2 direction = ((Vector2)camera.WorldToScreenPoint(destination) - sourceScreen).normalized;
        var canvas = navigationIcon.GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)navigationIcon.parent, sourceScreen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var localPosition);
        navigationIcon.anchoredPosition = localPosition + direction * orbitRadius;
        navigationIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        navigationDistance.transform.rotation = Quaternion.identity;
        float distance = Vector2.Distance(game.ControlledTarget.position, destination);
        navigationDistance.SetLocalizedText(Localization.Reference(message.title) + "\n" + (distance < 10f ? "[[game.signal.nearby]]" : $"{distance:0} u"));
        navigationGraphic.color = Color.white;
    }

    void OnDisable() => Close();
    void OnDestroy() { Close(); if (Instance == this) Instance = null; }
}
