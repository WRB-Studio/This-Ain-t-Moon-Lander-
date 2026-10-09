using UnityEngine;

public class CargoMission : MonoBehaviour
{
    public enum Progress { Waiting, Briefed, CableInHand, Towing, Delivered, Released, WalkingToStation, Complete, Failed }
    [System.Serializable] public class Page
    {
        [TextArea(2, 8)] public string message;
        public string[] answers;
    }
    public static CargoMission Instance { get; private set; }
    [SerializeField] CargoCapsule capsule;
    [SerializeField] CargoPassenger passenger;
    [SerializeField] Collider2D deliverySurface;
    [SerializeField] Transform hallDestination;
    [SerializeField] Transform registrationDestination;
    [SerializeField] string passengerName = "[[cargo.passenger.name]]";
    [SerializeField] Page[] discoveryConversation;
    [SerializeField] Page[] releaseConversation;
    [SerializeField, Min(0.1f)] float interactionDistance = 3.5f;
    [SerializeField, Min(1f)] float minimumExpeditionFuel = 180f;
    public Progress Stage { get; private set; }
    public bool IsShowing { get; private set; }
    public Collider2D DeliverySurface => deliverySurface;
    public Transform SignalTarget => capsule.transform;
    public bool IsAvailable => SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null
        && SaveLoadManager.Instance.Data.GetFlag("story.registrationComplete");
    public bool HoldingCable => Stage == Progress.CableInHand;
    public bool CanBoard(LanderController ship) => !HoldingCable && (Stage != Progress.Towing || ship == towShip);
    public bool CanTalk => CanInteract && Stage <= Progress.CableInHand && NearCapsule;
    public bool CanTakeCable => CanInteract && Stage == Progress.Briefed && NearCapsule;
    public bool CanAttachCable => CanInteract && HoldingCable && NearbyShip;
    public bool CanOpen => CanInteract && Stage == Progress.Delivered && NearCapsule;
    bool NearCapsule => Vector2.Distance(GameController.Instance.ControlledTarget.position, capsule.transform.position) <= interactionDistance;
    bool CanInteract => IsAvailable && !IsShowing && !PauseMenu.IsPaused && GameController.Instance && GameController.Instance.IsPlaying
        && GameController.Instance.Phase == GameController.GamePhase.EVA && GameController.Instance.ControlledTarget
        && !StoryTextController.Instance.BlocksGameplayInput
        && !(StationConversation.Instance && StationConversation.Instance.IsShowing)
        && !(RadioController.Instance && RadioController.Instance.IsOpen)
        && !(StationInterior.Instance && StationInterior.Instance.IsTransitioning);
    LanderController NearbyShip => MoonEVAController.Instance ? MoonEVAController.Instance.NearbyLander : null;
    public string Status => Stage switch
    {
        Progress.Waiting => "[[cargo.status.find]]",
        Progress.Briefed => "[[cargo.status.connect]]",
        Progress.CableInHand => "[[cargo.status.attach]]",
        Progress.Towing => "[[cargo.status.tow]]",
        Progress.Delivered => "[[cargo.status.open]]",
        Progress.Released or Progress.WalkingToStation => "[[cargo.status.walk]]",
        Progress.Complete => "[[cargo.status.complete]]",
        _ => "[[cargo.status.failed]]"
    };
    LanderController towShip;
    StoryDialog dialog;
    CanvasGroup hud;
    Page[] pages;
    int page;
    int walkArea;
    bool releaseDialogue;
    float previousTimeScale, previousHudAlpha;
    bool previousInteractable, previousRaycasts;
    bool initialized;
    bool restoring;

    void Awake() => Instance = this;
    public void Initialize(CargoMissionSave saved, LanderController[] ships = null)
    {
        CloseConversation();
        restoring = true;
        if (!dialog)
        {
            dialog = StoryTextController.Instance.Dialog;
            hud = StoryTextController.Instance.GameplayVisibility;
            dialog.AnswerClicked += SelectAnswer;
        }
        AsteroidOutpost.Instance.SyncLayout();
        capsule.Disconnect();
        passenger.transform.SetParent(transform, true);
        Stage = saved == null ? SaveLoadManager.Instance.Data.GetFlag("story.cargoRescued") ? Progress.Complete : Progress.Waiting : saved.stage;
        walkArea = saved == null ? 0 : saved.walkArea;
        towShip = null;
        capsule.SetAppearance(Stage);
        if (saved == null)
        {
            Vector3 position = Stage == Progress.Complete
                ? new Vector3(deliverySurface.bounds.center.x, deliverySurface.bounds.max.y + 1.16f, 0f)
                : AsteroidOutpost.Instance.CargoSpawn.position;
            capsule.transform.SetPositionAndRotation(position, Quaternion.identity);
            capsule.Body.position = capsule.transform.position;
            capsule.Body.rotation = 0f;
        }
        else
        {
            saved.capsule.Restore(capsule.transform, capsule.Body);
            passenger.Motor.Teleport(saved.passengerPosition);
            if (Stage == Progress.Towing && ships != null && saved.towShip >= 0 && saved.towShip < ships.Length)
            {
                towShip = ships[saved.towShip];
                capsule.Attach(towShip, saved.cableLength);
            }
            else if (Stage == Progress.Towing) { Stage = Progress.Briefed; capsule.SetAppearance(Stage); }
            if (HoldingCable)
            {
                var astronaut = MoonEVAController.Instance.astronaut;
                if (astronaut) capsule.HoldCable(astronaut.transform);
                else Stage = Progress.Briefed;
            }
        }
        if (Stage == Progress.Complete) PlaceAtRegistration();
        else passenger.gameObject.SetActive(Stage == Progress.Released || Stage == Progress.WalkingToStation);
        initialized = true;
        restoring = false;
        if (IsAvailable && LanderController.Instance.IsOnStation
            && !SaveLoadManager.Instance.Data.GetFlag("story.cargoExpeditionFueled")) PrepareExpedition(LanderController.Instance);
    }
    public void OnRunRestart()
    {
        CloseConversation();
        if (Stage == Progress.Towing) Fail();
        if (HoldingCable)
        {
            Stage = Progress.Briefed;
            capsule.Disconnect();
        }
    }
    void Update()
    {
        if (!initialized || !GameController.Instance || !SaveLoadManager.Instance) return;
        capsule.gameObject.SetActive(AsteroidOutpost.Instance.IsAvailable);
        if (Stage == Progress.Towing && (!towShip || towShip.IsCrashed)) Fail();
        if (HoldingCable && !MoonEVAController.Instance.astronaut)
        {
            Stage = Progress.Briefed;
            capsule.Disconnect();
        }
        if (IsShowing || PauseMenu.IsPaused) return;
        if (Stage == Progress.Released && CanInteract)
            BeginConversation(true, releaseConversation);
        if (Stage == Progress.WalkingToStation) WalkToRegistration();
    }
    public void PrepareExpedition(LanderController ship)
    {
        if (!ship || !ship.isSecretLander) return;
        ship.fuelMax = Mathf.Max(ship.fuelMax, minimumExpeditionFuel);
        ship.currentFuel = ship.fuelMax;
        SaveLoadManager.Instance.Data.SetFlag("story.cargoExpeditionFueled", true);
    }
    public void Talk()
    {
        if (!CanTalk) return;
        BeginConversation(false, Stage == Progress.Waiting ? discoveryConversation : new[]
        {
            new Page { message = "[[cargo.dialog.repeat]]", answers = new[] { "[[cargo.answer.back]]" } }
        });
    }
    void BeginConversation(bool releasing, Page[] conversation)
    {
        if (!dialog || conversation == null || conversation.Length == 0) return;
        StoryTextController.Instance.Restart();
        pages = conversation;
        page = 0;
        releaseDialogue = releasing;
        previousTimeScale = Time.timeScale;
        previousHudAlpha = hud.alpha;
        previousInteractable = hud.interactable;
        previousRaycasts = hud.blocksRaycasts;
        hud.alpha = 0f;
        hud.interactable = hud.blocksRaycasts = false;
        IsShowing = true;
        Time.timeScale = 0f;
        ShowPage();
    }
    void ShowPage() => dialog.ShowConversation(passengerName, pages[page].message, pages[page].answers);
    void SelectAnswer(int index)
    {
        if (!IsShowing || PauseMenu.IsPaused || index < 0 || index >= pages[page].answers.Length) return;
        page++;
        if (page < pages.Length) { ShowPage(); return; }
        if (releaseDialogue) Stage = Progress.WalkingToStation;
        else if (Stage == Progress.Waiting) Stage = Progress.Briefed;
        CloseConversation();
        capsule.SetAppearance(Stage);
        Save();
    }
    public void CloseConversation()
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
    public void TakeCable()
    {
        if (!CanTakeCable) return;
        Stage = Progress.CableInHand;
        capsule.HoldCable(GameController.Instance.ControlledTarget);
        MoonEVAController.Instance.RefreshAction();
        Save();
    }
    public void AttachCable()
    {
        if (!CanAttachCable) return;
        towShip = NearbyShip;
        if (towShip != LanderController.Instance || towShip.IsCrashed) return;
        Stage = Progress.Towing;
        capsule.SetAppearance(Stage);
        capsule.Attach(towShip);
        MoonEVAController.Instance.RefreshAction();
        Receive("cargo.tow", "[[cargo.radio.tow.title]]", "[[cargo.radio.tow.body]]", "station.delivery");
        Save();
    }
    public void Deliver()
    {
        if (Stage != Progress.Towing) return;
        Stage = Progress.Delivered;
        capsule.Disconnect();
        capsule.SetAppearance(Stage);
        towShip = null;
        Receive("cargo.delivered", "[[cargo.radio.delivered.title]]", "[[cargo.radio.delivered.body]]");
        Save();
    }
    public void OpenCapsule()
    {
        if (!CanOpen) return;
        Stage = Progress.Released;
        capsule.SetAppearance(Stage);
        passenger.transform.SetParent(transform, true);
        PlaceFeet(capsule.transform.position.x + 2.5f, deliverySurface.bounds.max.y);
        passenger.gameObject.SetActive(true);
        passenger.Stand();
        walkArea = 0;
        BeginConversation(true, releaseConversation);
        Save();
    }
    void WalkToRegistration()
    {
        var interior = StationInterior.Instance;
        Vector3 destination = walkArea == 0 ? SpaceStation.Instance.Entrance.position
            : walkArea == 1 ? hallDestination.position : registrationDestination.position;
        destination.y = passenger.transform.position.y;
        passenger.gameObject.SetActive(walkArea == 0 ? !interior.IsInside : (int)interior.CurrentArea == walkArea);
        if (passenger.gameObject.activeInHierarchy) passenger.WalkTowards(destination);
        else passenger.AdvanceHiddenTransit(destination);
        if (Mathf.Abs(passenger.transform.position.x - destination.x) > 0.2f) return;
        walkArea++;
        if (walkArea == 1) PlaceFeet(interior.HallArrival.position.x, hallDestination.position.y);
        else if (walkArea == 2) PlaceFeet(interior.RegistrationArrival.position.x, registrationDestination.position.y);
        else
        {
            Stage = Progress.Complete;
            PlaceAtRegistration();
            SaveLoadManager.Instance.Data.SetFlag("story.cargoRescued", true);
            Receive("cargo.complete", "[[cargo.radio.complete.title]]", "[[cargo.radio.complete.body]]");
        }
        Save();
    }
    void PlaceFeet(float x, float floorY)
    {
        float offset = passenger.FootOffset;
        passenger.Motor.Teleport(new Vector3(x, floorY - offset + 0.02f, 0f));
    }
    void PlaceAtRegistration()
    {
        passenger.transform.SetParent(registrationDestination.parent, true);
        PlaceFeet(registrationDestination.position.x, registrationDestination.position.y);
        passenger.gameObject.SetActive(true);
        passenger.Stand();
    }
    public void Fail()
    {
        if (Stage != Progress.Towing) return;
        Stage = Progress.Failed;
        capsule.Disconnect();
        capsule.SetAppearance(Stage);
        towShip = null;
        if (AudioManager.Instance) AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCrash, 0.7f);
        Receive("cargo.failed", "[[cargo.radio.failed.title]]", "[[cargo.radio.failed.body]]");
        Save();
    }
    public void Retry()
    {
        if (Stage != Progress.Failed || PauseMenu.IsPaused || !IsAvailable || !GameController.Instance.IsPlaying) return;
        capsule.Disconnect();
        Stage = Progress.Waiting;
        capsule.SetAppearance(Stage);
        capsule.transform.SetPositionAndRotation(AsteroidOutpost.Instance.CargoSpawn.position, Quaternion.identity);
        capsule.Body.position = capsule.transform.position;
        capsule.Body.rotation = 0f;
        passenger.gameObject.SetActive(false);
        var data = SaveLoadManager.Instance.Data;
        data.radioMessages.RemoveAll(message => message.id.StartsWith("cargo."));
        data.radioTrackedMessageId = "station.pickup";
        Save();
    }
    void Receive(string id, string title, string body, string signal = null)
    {
        if (!RadioController.Instance) return;
        RadioController.Instance.Receive(new RadioMessage
        {
            id = id, sender = "[[cargo.passenger.name]]", title = title, body = body,
            signal = signal, coordinates = signal == null ? Vector3.zero : deliverySurface.bounds.center
        });
        if (signal != null) SaveLoadManager.Instance.Data.radioTrackedMessageId = id;
    }
    void Save() { if (!restoring) SaveLoadManager.Instance.Save(); }
    public CargoMissionSave Capture(LanderController[] ships)
    {
        var state = new CargoMissionSave
        {
            stage = Stage, capsule = new ActorSave(), towShip = System.Array.IndexOf(ships, towShip),
            walkArea = walkArea, passengerPosition = passenger.transform.position, cableLength = capsule.CableLength
        };
        state.capsule.Capture(capsule.transform, capsule.Body);
        return state;
    }
    void OnDestroy()
    {
        CloseConversation();
        if (dialog) dialog.AnswerClicked -= SelectAnswer;
        if (Instance == this) Instance = null;
    }
}
