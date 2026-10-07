using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StoryTextController : MonoBehaviour
{
    public static StoryTextController Instance;
    public enum eStoryTextType { AtmosphereExit, BackToPlanet, NearToMoon }
    public enum Discovery { ZeroG, Moon, MoonLanding, EVA, AbandonedShip, UFO, Station, StationLanding }
    public enum DiscoveryPresentation { SlowMotion, Overlay, LandingResults, Immediate, Paused }

    [Serializable]
    public class DiscoveryMessage
    {
        public Discovery discovery;
        [TextArea] public string text;
        public DiscoveryPresentation presentation;
        public DiscoveryMessage(Discovery discovery, string text, DiscoveryPresentation presentation = DiscoveryPresentation.SlowMotion)
        { this.discovery = discovery; this.text = text; this.presentation = presentation; }
    }

    [Serializable]
    public class LevelMessages
    {
        [Min(1)] public int fromLevel;
        [TextArea] public string[] messages;
        public LevelMessages(int fromLevel, params string[] messages) { this.fromLevel = fromLevel; this.messages = messages; }
    }

    [Serializable]
    public class EarthTransmission
    {
        [Min(1)] public int level;
        [TextArea(4, 10)] public string text;
        public EarthTransmission(int level, string text) { this.level = level; this.text = text; }
    }

    [Header("UI")]
    [SerializeField] TMP_Text txtInfo;
    [SerializeField] StoryDialog dialog;
    [SerializeField] CanvasGroup gameplayPanelVisibility;
    public StoryDialog Dialog => dialog;
    public CanvasGroup GameplayVisibility => gameplayPanelVisibility;
    public void BlockInputUntilRelease() => waitForPointerRelease = true;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] float visibleTime = 3f;
    [SerializeField, Min(0f)] float triggerDelayFromRunStart = 0.5f;
    [SerializeField, Min(0f)] float typeCooldown = 2f;
    [SerializeField, Range(0.01f, 1f)] float discoveryTimeScale = 0.05f;
    [SerializeField, Min(0f)] float discoveryDelay = 2.5f;
    [Tooltip("Seconds to slow down and resume for the first Earth gravity exit and Moon atmosphere contact.")]
    [SerializeField, Min(0.05f)] float gravityTransitionDuration = 1.5f;
    [SerializeField, Min(0.05f)] float timeTransitionDuration = 0.65f;

    [Header("First Discoveries")]
    [SerializeField] DiscoveryMessage[] discoveryMessages = {
        new(Discovery.ZeroG, "Oh. You're actually trying something else.\n\nAnd gravity has stopped participating.\nI hadn't planned for that."),
        new(Discovery.Moon, "Oh.\nSo THAT is the Moon.\nMinor naming issue."),
        new(Discovery.MoonLanding, "You actually landed on the Moon.\nI hadn't planned that far ahead.", DiscoveryPresentation.LandingResults),
        new(Discovery.EVA, "You can get out?\nApparently this spacecraft came with legs.", DiscoveryPresentation.Overlay),
        new(Discovery.AbandonedShip, "Finally. The company lander.\n\nParked on the Moon with a full tank.\nI'll have a word with accounting.\n\nBring it back to Earth in one piece.\nInsurance doesn't cover\n'forgotten on the Moon'.", DiscoveryPresentation.Immediate),
        new(Discovery.UFO, "INCOMING TRANSMISSION\n\nYou're late.\n\n...Wait. You're not our pilot.\nWhere did you get that ship?", DiscoveryPresentation.Immediate),
        new(Discovery.Station, "There you are. Welcome to the station.\nWe were expecting that lander.\n\nUse either landing pad.\nKeep it upright, please.\nOur maintenance crew has had\na very long day.", DiscoveryPresentation.Paused),
        new(Discovery.StationLanding, "Docking confirmed.\nYou can step outside.", DiscoveryPresentation.LandingResults)
    };
    [SerializeField, TextArea] string zeroGAfterDepartureMessage = "Wait. Where are you now?\n\nI know. I said I was leaving.\nBut gravity has stopped participating.\nThat wasn't part of my little landing game.";
    [SerializeField, Range(0f, 1f)] float flightCommentChance = 0.25f;
    [SerializeField, TextArea(4, 10)] string earthReturnMessage = "Back in one piece. With the company lander.\nExcellent work.\n\nBy 'show some initiative', I did not mean\nan unauthorized Moon trip on company fuel.\n\nStill. The ship is back.\nI'll keep the report suitably vague.";

    [Header("Earth Launches")]
    [SerializeField, TextArea] string moonKnownEarthMessage = "You know where the real Moon is.\nBack for fuel, or just fond of this place?";
    [SerializeField] LevelMessages[] earthMessages = {
        new(1, "Land safely on the marked pad.", "Slow your descent before touching down.", "Keep the spacecraft upright. Aim for the pad."),
        new(2, "Another pad. Same procedure.", "Bring it down. Preferably in one piece.", "Try landing near the middle this time."),
        new(4, "Yes. Another landing. How adventurous.", "Another parking spot awaits your grand entrance.", "You seem very committed to the obvious task."),
        new(5, "You have really made landing your whole thing.", "I briefly hoped you would surprise me.", "The scenery changes. Your ambition does not."),
        new(7, "I am no longer counting enthusiastically.", "You could explore. Or polish your parking record.", "I specifically told you that you can fly up."),
        new(8, "You are remarkably good at missing the point.", "I would sigh, but this is text.", "Land if you must. I am past negotiating.")
    };

    readonly Dictionary<eStoryTextType, string[]> stateMessages = new() {
        { eStoryTextType.AtmosphereExit, new[] { "There you go. Beyond the parking spots.", "Still exploring. Now I'm curious.", "I wonder how far this goes." } },
        { eStoryTextType.BackToPlanet, new[] { "Ah. Parking spots again.", "A familiar view. Try not to get too comfortable.", "Back for fuel? That sounds like a plan." } },
        { eStoryTextType.NearToMoon, new[] { "Back to the Moon. You know more about it than I do.", "I wonder what else is up there.", "Still there. This wasn't in my little landing game." } }
    };

    [Header("Earth Transmissions")]
    [SerializeField] EarthTransmission[] earthTransmissions = {
        new(3, "Good. Third level.\nYou understand how a landing pad works.\n\nI was hoping for a little more\ninitiative after the introduction.\nBut fine. Another pad.\n\nDon't let me interrupt your parking."),
        new(6, "Sixth level.\nYou are seriously collecting\nparking spots.\n\nFine. Let me be very clear:\nFill your tank. Fly up.\nAnd don't stop just because\nthe landing pad disappears from view.\n\nThe edge of the screen is not a wall.\nI really thought you would\ntry that yourself eventually."),
        new(9, "NINE LEVELS!\nI gave you a sky!\nA spacecraft! Thrusters!\nAnd you keep opening\none parking spot after another!\n\nFULL TANK. FLY UP. KEEP GOING.\nI don't know how to make\nthis any clearer!\n\nYou know what? Do whatever you want.\nLand another hundred times.\nGive the pads names. I'm out.")
    };
    [Header("After Level Nine")]
    [SerializeField, Range(0f, 1f)] float earthAsideChance = 0.25f;
    [SerializeField, TextArea(1, 5)] string[] earthAsides = {
        "...", "Uh-huh.", "Sure.", "Of course.", "How surprising.", "Another one.",
        "You've got this.", "I'm not here.", "That was not an invitation to talk.", "No. I am not commenting on this.",
        "'Is that pilot still landing?'\n'Yes. Don't ask.'\nOh. The microphone is on.",
        "'Coffee?'\n'Please. It's been nothing but parking all day.'\nWait. Is this still broadcasting?",
        "'Has anyone told them they can leave?'\n'I did. Several times.'\nOh. You can hear us.",
        "'Any progress?'\n'The parking situation is excellent.'\nRight. Microphone off."
    };

    readonly Queue<(string text, string acknowledgement, int transmissionLevel, Discovery? discovery)> queue = new();
    readonly HashSet<Discovery> queuedDiscoveries = new();
    readonly Dictionary<eStoryTextType, float> nextAllowed = new();
    readonly Dictionary<Discovery, float> discoveryReadyAt = new();
    Coroutine runner;
    string lastQueued;
    bool shownAtmosphereExit, shownBackToPlanet, shownNearMoon;
    bool flightCommentUsed;
    bool dialogueVisible;
    bool operatorDialogue;
    bool waitForPointerRelease;
    bool hadResults;
    float runStartTime, previousTimeScale;
    float previousFixedDeltaTime;
    bool ownsTimeScale, continueRequested;
    public bool IsTransitioning { get; private set; }
    string activeAcknowledgement;
    int activeTransmissionLevel;
    GravityManager2D gravityManager;
    SaveGame data => SaveLoadManager.Instance ? SaveLoadManager.Instance.Data : null;
    public bool IsShowingDiscovery => activeAcknowledgement != null && activeTransmissionLevel == 0;
    public bool IsShowingTransmission => activeTransmissionLevel > 0;
    public bool IsShowingDialogue => activeAcknowledgement != null;
    public bool BlocksGameplayInput => dialogueVisible || waitForPointerRelease;
    public bool HasPendingDialogue
    {
        get
        {
            if (IsShowingDialogue || IsTransitioning) return true;
            foreach (var entry in queue) if (entry.acknowledgement != null) return true;
            return false;
        }
    }

    void Awake()
    {
        Instance = this;
        if (txtInfo) txtInfo.gameObject.SetActive(false);
        if (dialog)
        {
            dialog.Hide();
            dialog.ContinueClicked += ContinueDiscovery;
        }
    }

    public void Init()
    {
        gravityManager = GravityManager2D.Instance;
        // Existing saves already remember the secret ship unlock.
        if (LanderChooserManager.Instance.HasFoundSecret) Discover(Discovery.AbandonedShip);
    }

    public bool HasDiscovered(Discovery discovery) => data != null && data.GetFlag("discovery." + discovery);

    public void Discover(Discovery discovery)
    {
#if UNITY_EDITOR
        if (!DebugIsEnabled(discovery)) return;
#endif
        if (data == null || HasDiscovered(discovery)) return;
        if ((IsGravityDiscovery(discovery) || discovery == Discovery.AbandonedShip || discovery == Discovery.UFO || discovery == Discovery.Station)
            && runner != null && !IsShowingDialogue) ClearPresentation();
        data.SetFlag("discovery." + discovery, true);
        discoveryReadyAt[discovery] = Time.unscaledTime + GetDiscoveryDelay(discovery);
        SaveLoadManager.Instance.Save();
    }

    public void Restart()
    {
        ClearPresentation();
        shownAtmosphereExit = shownBackToPlanet = shownNearMoon = false;
        flightCommentUsed = false;
        runStartTime = Time.time;
        nextAllowed.Clear();
        discoveryReadyAt.Clear();
    }

    void ClearPresentation()
    {
        if (runner != null) StopCoroutine(runner);
        runner = null;
        EndDiscovery();
        if (txtInfo) txtInfo.gameObject.SetActive(false);
        queue.Clear(); queuedDiscoveries.Clear(); lastQueued = null;
        queuedTransmissions.Clear();
    }

    public void ShowEarthBriefing()
    {
        if (GameController.Instance.IsExploring) return;
        int level = GameController.Instance.level;
        if (level >= 10)
        {
            if (data.earthAsideLevel >= level) return;
            data.earthAsideLevel = level;
            if (UnityEngine.Random.value >= earthAsideChance) return;
            string aside = PickEarthAside();
            if (!string.IsNullOrEmpty(aside)) Show(aside);
            return;
        }
        if (QueueEarthTransmission(level)) return;
        string[] messages = GetEarthMessages(level);
        if (messages.Length == 0) return;
        string message = messages[UnityEngine.Random.Range(0, messages.Length)];
        if (HasDiscovered(Discovery.Moon)) message = moonKnownEarthMessage;
        Show(message);
    }

    public string[] GetEarthMessages(int level)
    {
        if (level >= 10) return Array.Empty<string>();
        foreach (var transmission in earthTransmissions)
            if (transmission.level == level) return Array.Empty<string>();
        LevelMessages selected = null;
        foreach (var group in earthMessages)
            if (group.fromLevel <= level && (selected == null || group.fromLevel > selected.fromLevel)) selected = group;
        return selected?.messages ?? Array.Empty<string>();
    }

    public string PickEarthAside()
    {
        if (earthAsides.Length == 0) return "";
        int previous = Array.IndexOf(earthAsides, data.lastEarthAside);
        int index = UnityEngine.Random.Range(0, earthAsides.Length - (previous >= 0 && earthAsides.Length > 1 ? 1 : 0));
        if (previous >= 0 && earthAsides.Length > 1 && index >= previous) index++;
        data.lastEarthAside = earthAsides[index];
        return data.lastEarthAside;
    }

    bool QueueEarthTransmission(int level)
    {
#if UNITY_EDITOR
        if (debugDisabledTransmissions.Contains(level)) return true;
#endif
        foreach (var transmission in earthTransmissions)
        {
            if (transmission.level != level) continue;
            string key = "story.earth." + level;
            if (!HasDiscovered(Discovery.ZeroG) && !data.GetFlag(key)
                && activeAcknowledgement != key && !queuedTransmissions.Contains(level))
            {
                queuedTransmissions.Add(level);
                Enqueue(transmission.text, key, level);
            }
            return true;
        }
        return false;
    }

    readonly HashSet<int> queuedTransmissions = new();

    void Update()
    {
        if (waitForPointerRelease && Input.touchCount == 0 && !Input.GetMouseButton(0)) waitForPointerRelease = false;
        if (RadioController.Instance && RadioController.Instance.IsOpen) return;
        if (StationConversation.Instance && StationConversation.Instance.IsShowing) return;
        var game = GameController.Instance;
        if (!game || (!game.IsPlaying && game.Phase != GameController.GamePhase.Landed
            && !(operatorDialogue && game.HasResults)))
        {
            if (runner != null || IsShowingDialogue) ClearPresentation();
            return;
        }
        if (operatorDialogue && !game.HasResults) ClearPresentation();
        if (game.HasResults && !hadResults) ClearPresentation();
        hadResults = game.HasResults;
        if (game.HasResults && game.Phase == GameController.GamePhase.Landed
            && LanderController.Instance && LanderController.Instance.landerState == LanderController.eLanderState.LandedPad
            && !LanderController.Instance.IsOnStation
#if UNITY_EDITOR
            && DebugReturnEnabled
#endif
            && data.GetFlag("story.companyLanderReturned") && !data.GetFlag("story.ack.CompanyReturn")
            && !IsShowingDialogue)
            ShowOperatorDialogue(earthReturnMessage, "story.ack.CompanyReturn");
        if (!game.IsExploring) QueueEarthTransmission(game.level);
        if (!gravityManager || !game.ControlledTarget) return;
        var target = game.ControlledTarget;
        float distance = Vector2.Distance(target.position, gravityManager.transform.position);
        if (game.Phase == GameController.GamePhase.EVA) Discover(Discovery.EVA);
        if ((game.Phase == GameController.GamePhase.EVA && !LanderController.Instance.IsOnStation) || (LanderController.Instance
            && LanderController.Instance.landerState == LanderController.eLanderState.LandedMoon))
        {
            Discover(Discovery.Moon);
            Discover(Discovery.MoonLanding);
        }
        bool inZeroG = target.position.y > gravityManager.zeroGFullY && distance > gravityManager.moonEnterRadius;
        bool firstZeroG = !HasDiscovered(Discovery.ZeroG);
        if (inZeroG) Discover(Discovery.ZeroG);
        if (!shownAtmosphereExit && inZeroG)
        {
            shownBackToPlanet = false; shownAtmosphereExit = true;
            if (!firstZeroG && data.GetFlag("story.ack.ZeroG")) Show(eStoryTextType.AtmosphereExit);
        }
        if (shownAtmosphereExit && !shownBackToPlanet && target.position.y < gravityManager.zeroGStartY)
        {
            shownAtmosphereExit = false; shownBackToPlanet = true;
            Show(eStoryTextType.BackToPlanet);
        }
        bool firstMoon = !HasDiscovered(Discovery.Moon);
        if (distance <= gravityManager.moonEnterRadius) Discover(Discovery.Moon);
        if (!shownNearMoon && distance <= gravityManager.moonFullRadius)
        {
            shownNearMoon = true;
            if (!firstMoon && data.GetFlag("story.ack.Moon")) Show(eStoryTextType.NearToMoon);
        }
        else if (distance > gravityManager.moonFullRadius) shownNearMoon = false;

        // Acknowledgements are separate from discoveries so interrupted notices return after loading.
        foreach (var entry in discoveryMessages)
            if (entry.presentation != DiscoveryPresentation.LandingResults
#if UNITY_EDITOR
                && DebugIsEnabled(entry.discovery)
#endif
                && HasDiscovered(entry.discovery) && !data.GetFlag("story.ack." + entry.discovery)
                && DiscoveryIsReady(entry) && queuedDiscoveries.Add(entry.discovery))
                Enqueue(GetDiscoveryText(entry.discovery), "story.ack." + entry.discovery, 0, entry.discovery);
    }

    float GetDiscoveryDelay(Discovery discovery)
        => IsGravityDiscovery(discovery) || discovery == Discovery.AbandonedShip || discovery == Discovery.UFO
            || discovery == Discovery.Station ? 0f : discoveryDelay;

    static bool IsGravityDiscovery(Discovery discovery) => discovery == Discovery.ZeroG || discovery == Discovery.Moon;

    bool DiscoveryIsReady(DiscoveryMessage entry)
    {
        if (entry.presentation == DiscoveryPresentation.Overlay) return true;
        if (!discoveryReadyAt.TryGetValue(entry.discovery, out float readyAt))
        {
            readyAt = Time.unscaledTime + GetDiscoveryDelay(entry.discovery);
            discoveryReadyAt[entry.discovery] = readyAt;
        }
        return Time.unscaledTime >= readyAt && CanShowDiscovery(entry.discovery);
    }

    bool CanShowDiscovery(Discovery discovery)
    {
        var game = GameController.Instance;
        if (!game.IsPlaying || !game.ControlledTarget) return false;
        if (discovery == Discovery.AbandonedShip) return game.Phase != GameController.GamePhase.EVA;
        if (discovery == Discovery.UFO) return UFOEncounter.Instance && UFOEncounter.Instance.IsHolding;
        if (game.Phase != GameController.GamePhase.Flight) return false;
        if (discovery == Discovery.Station) return SpaceStation.Instance && SpaceStation.Instance.IsAvailable;
        float distance = Vector2.Distance(game.ControlledTarget.position, gravityManager.transform.position);
        if (discovery == Discovery.ZeroG)
            return game.ControlledTarget.position.y > gravityManager.zeroGFullY && distance > gravityManager.moonEnterRadius;
        if (discovery != Discovery.Moon) return true;
        return distance <= gravityManager.moonEnterRadius;
    }

    public string GetDiscoveryText(Discovery discovery)
    {
        if (discovery == Discovery.ZeroG && data != null && data.GetFlag("story.earth.9"))
            return zeroGAfterDepartureMessage;
        foreach (var entry in discoveryMessages) if (entry.discovery == discovery) return entry.text;
        return "";
    }

    DiscoveryPresentation GetDiscoveryPresentation(Discovery discovery)
    {
        foreach (var entry in discoveryMessages) if (entry.discovery == discovery) return entry.presentation;
        return DiscoveryPresentation.SlowMotion;
    }

    public string TakeMoonLandingMessage()
    {
#if UNITY_EDITOR
        if (!DebugIsEnabled(Discovery.MoonLanding)) return null;
#endif
        if (data.GetFlag("story.ack.MoonLanding")) return null;
        data.SetFlag("story.ack.MoonLanding", true);
        data.SetFlag("story.ack.Moon", true);
        return GetDiscoveryText(Discovery.MoonLanding);
    }

    public string TakeEarthReturnMessage()
    {
#if UNITY_EDITOR
        if (!DebugReturnEnabled) return null;
#endif
        var ship = LanderController.Instance;
        if (!ship || !ship.isSecretLander || !HasDiscovered(Discovery.AbandonedShip)
            || data.GetFlag("story.companyLanderReturned")) return null;
        data.SetFlag("story.companyLanderReturned", true);
        LanderChooserManager.Instance.RefreshChooser();
        return earthReturnMessage;
    }

    public string TakeStationLandingMessage()
    {
        data.SetFlag("story.ack.StationLanding", true);
        return GetDiscoveryText(Discovery.StationLanding);
    }

    public void Show(string message) => Enqueue(message, null);

    public void ShowOperatorDialogue(string message, string acknowledgement = null)
    {
        if (RadioController.Instance && RadioController.Instance.IsOwned)
        {
            RadioController.Instance.Receive(new RadioMessage
            {
                id = acknowledgement ?? "operator." + message, sender = "Company", title = "Company transmission",
                body = message, portrait = StoryDialog.Expression.Neutral
            });
            if (acknowledgement != null) data.SetFlag(acknowledgement, true);
            SaveLoadManager.Instance.Save();
            return;
        }
        ClearPresentation();
        hadResults = GameController.Instance.HasResults;
        activeAcknowledgement = acknowledgement ?? "operator";
        operatorDialogue = true;
        continueRequested = false;
        DisplayDialogue(message, 3, false, true);
    }

    void DisplayDialogue(string message, int portraitLevel, bool surprised, bool canContinue)
    {
        dialog.Show(message, StoryDialog.Expression.None, canContinue);
        dialogueVisible = true;
        SetGameplayPanelVisible(false);
    }
    public void Show(eStoryTextType type)
    {
        if (Time.time - runStartTime < triggerDelayFromRunStart) return;
        // His departure ends routine commentary; new discoveries can still bring him back.
        if (data.GetFlag("story.earth.9") || flightCommentUsed || IsShowingDialogue || queue.Count > 0) return;
        if (nextAllowed.TryGetValue(type, out float allowedAt) && Time.time < allowedAt) return;
        nextAllowed[type] = Time.time + typeCooldown;
        if (UnityEngine.Random.value >= flightCommentChance) return;
        if (!stateMessages.TryGetValue(type, out var messages) || messages.Length == 0) return;
        string message = messages[UnityEngine.Random.Range(0, messages.Length)];
        if (GameController.Instance.level < 3 && type == eStoryTextType.BackToPlanet)
            message = "Back on Earth. You can refuel on a free landing pad.";
        flightCommentUsed = true;
        Show(message);
    }

    void Enqueue(string text, string acknowledgement, int transmissionLevel = 0, Discovery? discovery = null)
    {
        if (string.IsNullOrEmpty(text) || (acknowledgement == null && text == lastQueued)) return;
        queue.Enqueue((text, acknowledgement, transmissionLevel, discovery)); lastQueued = text;
        if (runner == null) runner = StartCoroutine(RunQueue());
    }

    IEnumerator RunQueue()
    {
        // Assign runner before any immediate cancellation or completion.
        yield return null;
        while (queue.Count > 0)
        {
            var entry = queue.Dequeue();
            bool overlay = entry.discovery.HasValue && GetDiscoveryPresentation(entry.discovery.Value) == DiscoveryPresentation.Overlay;
            if (entry.acknowledgement != null && !overlay)
            {
                if (entry.transmissionLevel > 0 && HasDiscovered(Discovery.ZeroG)) continue;
                if (entry.discovery.HasValue && !CanShowDiscovery(entry.discovery.Value))
                {
                    queuedDiscoveries.Remove(entry.discovery.Value);
                    continue;
                }
                bool communication = entry.transmissionLevel > 0 || entry.discovery == Discovery.Station
                    || entry.discovery == Discovery.UFO || entry.discovery == Discovery.AbandonedShip;
                if (communication && RadioController.Instance && RadioController.Instance.IsOwned)
                {
                    bool station = entry.discovery == Discovery.Station;
                    bool unknown = entry.discovery == Discovery.UFO;
                    var portrait = station ? StoryDialog.Expression.StationCrew : unknown ? StoryDialog.Expression.None
                        : entry.transmissionLevel == 6 ? StoryDialog.Expression.Annoyed
                        : entry.transmissionLevel == 9 ? StoryDialog.Expression.Angry : StoryDialog.Expression.Neutral;
                    RadioController.Instance.Receive(new RadioMessage
                    {
                        id = entry.acknowledgement, sender = station ? "Station" : unknown ? "Unknown sender" : "Company",
                        title = unknown ? "Unidentified transmission" : "Incoming transmission", body = entry.text, portrait = portrait
                    });
                    data.SetFlag(entry.acknowledgement, true);
                    if (entry.discovery.HasValue) queuedDiscoveries.Remove(entry.discovery.Value);
                    SaveLoadManager.Instance.Save();
                    continue;
                }
                activeAcknowledgement = entry.acknowledgement;
                activeTransmissionLevel = entry.transmissionLevel;
                bool paused = entry.discovery.HasValue
                    && GetDiscoveryPresentation(entry.discovery.Value) == DiscoveryPresentation.Paused;
                bool immediate = paused || (entry.discovery.HasValue
                    && GetDiscoveryPresentation(entry.discovery.Value) == DiscoveryPresentation.Immediate);
                if (immediate)
                {
                    continueRequested = false;
                    if (paused)
                    {
                        previousTimeScale = Time.timeScale;
                        previousFixedDeltaTime = Time.fixedDeltaTime;
                        ownsTimeScale = true;
                        Time.timeScale = 0f;
                    }
                    DisplayDialogue(entry.text, entry.discovery == Discovery.Station ? 12
                        : entry.discovery == Discovery.AbandonedShip ? 3 : 0, false, true);
                    while (!continueRequested) yield return null;
                    EndDiscovery();
                    continue;
                }
                previousTimeScale = Time.timeScale;
                previousFixedDeltaTime = Time.fixedDeltaTime;
                ownsTimeScale = true;
                continueRequested = false;
                bool gravityDiscovery = entry.discovery.HasValue && IsGravityDiscovery(entry.discovery.Value);
                float transitionDuration = gravityDiscovery ? gravityTransitionDuration : timeTransitionDuration;
                yield return ChangeTimeScale(entry.transmissionLevel > 0 ? 0f : previousTimeScale * discoveryTimeScale, transitionDuration, () =>
                {
                    DisplayDialogue(entry.text, entry.transmissionLevel,
                        entry.discovery == Discovery.ZeroG || entry.discovery == Discovery.Moon, false);
                });
                if (entry.discovery.HasValue && !gravityDiscovery && !CanShowDiscovery(entry.discovery.Value))
                {
                    queuedDiscoveries.Remove(entry.discovery.Value);
                    HideDialogue();
                    yield return ChangeTimeScale(previousTimeScale, transitionDuration);
                    EndDiscovery();
                    continue;
                }
                dialog.SetCanContinue(true);
                while (!continueRequested) yield return null;
                yield return ChangeTimeScale(previousTimeScale, transitionDuration);
                EndDiscovery();
            }
            else
            {
                if (!GameController.Instance.IsPlaying) continue;
                txtInfo.text = entry.text;
                txtInfo.gameObject.SetActive(true);
                if (entry.acknowledgement != null)
                {
                    data.SetFlag(entry.acknowledgement, true);
                    SaveLoadManager.Instance.Save();
                }
                yield return new WaitForSecondsRealtime(Mathf.Max(visibleTime, entry.text.Length * 0.055f));
                txtInfo.gameObject.SetActive(false);
            }
        }
        runner = null; lastQueued = null;
    }

    public void ContinueDiscovery()
    {
        if (activeAcknowledgement == null || IsTransitioning || continueRequested) return;
        if (activeAcknowledgement != "operator") data.SetFlag(activeAcknowledgement, true);
        continueRequested = true;
        waitForPointerRelease = true;
        HideDialogue();
        SaveLoadManager.Instance.Save();
        if (operatorDialogue) EndDiscovery();
    }

    IEnumerator ChangeTimeScale(float target, float duration, Action onHalfway = null)
    {
        IsTransitioning = true;
        float start = Time.timeScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            Time.fixedDeltaTime = previousFixedDeltaTime * Mathf.Max(0.01f, Time.timeScale / Mathf.Max(0.001f, previousTimeScale));
            if (elapsed >= duration * 0.5f && onHalfway != null)
            {
                onHalfway();
                onHalfway = null;
            }
            yield return null;
        }
        Time.timeScale = target;
        IsTransitioning = false;
    }

    void SetGameplayPanelVisible(bool visible)
    {
        if (!gameplayPanelVisibility) return;
        gameplayPanelVisibility.alpha = visible ? 1f : 0f;
        gameplayPanelVisibility.interactable = visible;
        gameplayPanelVisibility.blocksRaycasts = visible;
    }

    void HideDialogue()
    {
        dialogueVisible = false;
        if (dialog) dialog.Hide();
        SetGameplayPanelVisible(true);
    }

    void EndDiscovery()
    {
        if (ownsTimeScale)
        {
            Time.timeScale = previousTimeScale;
            Time.fixedDeltaTime = previousFixedDeltaTime;
            waitForPointerRelease = true;
        }
        ownsTimeScale = false;
        IsTransitioning = false;
        activeAcknowledgement = null;
        activeTransmissionLevel = 0;
        operatorDialogue = false;
        HideDialogue();
    }

    public void CaptureState(WorldSave world)
    {
        world.atmosphereExit = shownAtmosphereExit; world.backToPlanet = shownBackToPlanet; world.nearMoon = shownNearMoon;
        world.storyElapsed = Mathf.Max(0f, Time.time - runStartTime);
    }
    public void RestoreState(WorldSave world)
    {
        Restart();
        shownAtmosphereExit = world.atmosphereExit; shownBackToPlanet = world.backToPlanet; shownNearMoon = world.nearMoon;
        runStartTime = Time.time - Mathf.Max(0f, world.storyElapsed);
    }
#if UNITY_EDITOR
    readonly HashSet<Discovery> debugDisabledDiscoveries = new();
    readonly HashSet<int> debugDisabledTransmissions = new();
    public bool DebugReturnEnabled { get; private set; } = true;
    public bool DebugIsEnabled(Discovery discovery) => !debugDisabledDiscoveries.Contains(discovery);
    public bool DebugTransmissionEnabled(int level) => !debugDisabledTransmissions.Contains(level);

    public void DebugSetDiscovery(Discovery discovery, bool enabled, bool discovered, bool acknowledged)
    {
        Restart();
        if (UFOEncounter.Instance) UFOEncounter.Instance.DebugReset();
        if (enabled) debugDisabledDiscoveries.Remove(discovery);
        else debugDisabledDiscoveries.Add(discovery);
        data.SetFlag("discovery." + discovery, discovered);
        data.SetFlag("story.ack." + discovery, discovered && acknowledged);
        if (discovery == Discovery.UFO)
        {
            data.SetFlag("story.ufoDeparted", false);
            data.stationSignalStartDistance = 0f;
        }
        if (discovery == Discovery.AbandonedShip) LanderChooserManager.Instance.DebugSetSecretFound(discovered);
    }

    public void DebugSetTransmission(int level, bool enabled, bool acknowledged)
    {
        Restart();
        if (enabled) debugDisabledTransmissions.Remove(level);
        else debugDisabledTransmissions.Add(level);
        data.SetFlag("story.earth." + level, acknowledged);
    }

    public void DebugSetReturn(bool enabled, bool returned, bool acknowledged)
    {
        Restart();
        DebugReturnEnabled = enabled;
        data.SetFlag("story.companyLanderReturned", returned);
        data.SetFlag("story.ack.CompanyReturn", returned && acknowledged);
        LanderChooserManager.Instance.RefreshChooser();
    }

    public void DebugJumpTo(Discovery destination)
    {
        Restart();
        if (UFOEncounter.Instance) UFOEncounter.Instance.DebugReset();
        debugDisabledDiscoveries.Remove(destination);
        foreach (Discovery discovery in Enum.GetValues(typeof(Discovery)))
        {
            bool completed = (int)discovery < (int)destination;
            data.SetFlag("discovery." + discovery, completed);
            data.SetFlag("story.ack." + discovery, completed);
        }
        foreach (int level in new[] { 3, 6, 9 }) data.SetFlag("story.earth." + level, false);
        data.SetFlag("story.ufoDeparted", (int)destination >= (int)Discovery.Station);
        data.stationSignalStartDistance = 0f;
        data.SetFlag("story.companyLanderReturned", false);
        data.SetFlag("story.ack.CompanyReturn", false);
        bool secret = (int)destination >= (int)Discovery.AbandonedShip;
        var chooser = LanderChooserManager.Instance;
        chooser.DebugSetSecretFound((int)destination >= (int)Discovery.UFO);
        chooser.DebugSelectLander(secret);
        var ship = LanderController.Instance;
        Vector3 position = LandingPadPlacer.Instance.transform.position;
        bool landedMoon = destination == Discovery.EVA || destination == Discovery.AbandonedShip;
        if (destination == Discovery.ZeroG) position.y = gravityManager.zeroGFullY + 5f;
        else if ((int)destination >= (int)Discovery.Station && SpaceStation.Instance)
            position = destination == Discovery.Station ? SpaceStation.Instance.transform.position + Vector3.up * 25f
                : SpaceStation.Instance.pads[0].transform.position + Vector3.up * 3f;
        else if (destination == Discovery.UFO)
            position = new Vector3(gravityManager.transform.position.x,
                UFOEncounter.Instance ? UFOEncounter.Instance.DebugTriggerHeight + 5f
                    : gravityManager.transform.position.y + gravityManager.moonEnterRadius + 55f, 0f);
        else
        {
            var surface = gravityManager.GetComponent<Collider2D>().bounds;
            float shipHalfHeight = ship.GetComponent<Collider2D>().bounds.extents.y;
            position = new Vector3(surface.center.x, surface.max.y + shipHalfHeight + 0.1f, 0f);
            if (destination == Discovery.Moon) position.y = gravityManager.transform.position.y + gravityManager.moonEnterRadius - 1f;
            else if (destination == Discovery.MoonLanding) position.y += 1f;
        }
        GameController.Instance.DebugStartFlight(position, landedMoon);
        if (destination == Discovery.EVA) MoonEVAController.Instance.ExitLander();
        if (destination == Discovery.AbandonedShip) chooser.SelectDiscoveredLander(ship);
    }

    public void DebugReturnToEarth()
    {
        DebugJumpTo(Discovery.UFO);
        data.SetFlag("discovery.UFO", false);
        data.SetFlag("story.ack.UFO", false);
        DebugReturnEnabled = true;
        var ship = LanderController.Instance;
        Vector3 position = DebugEarthPad().transform.position
            + Vector3.up * (ship.GetComponent<Collider2D>().bounds.extents.y + 0.5f);
        GameController.Instance.DebugStartFlight(position, false);
    }

    public void DebugStartEarth(int level)
    {
        Restart();
        if (UFOEncounter.Instance) UFOEncounter.Instance.DebugReset();
        foreach (Discovery discovery in Enum.GetValues(typeof(Discovery)))
        {
            data.SetFlag("discovery." + discovery, false);
            data.SetFlag("story.ack." + discovery, false);
        }
        foreach (int milestone in new[] { 3, 6, 9 }) data.SetFlag("story.earth." + milestone, false);
        data.SetFlag("story.ufoDeparted", false);
        data.stationSignalStartDistance = 0f;
        data.SetFlag("story.companyLanderReturned", false);
        data.SetFlag("story.ack.CompanyReturn", false);
        data.earthAsideLevel = 0;
        data.lastEarthAside = null;
        LanderChooserManager.Instance.DebugSetSecretFound(false);
        LanderChooserManager.Instance.DebugSelectLander(false);
        var game = GameController.Instance;
        game.level = data.level = Mathf.Max(1, level);
        game.DebugStartFlight(DebugEarthPad().transform.position + new Vector3(10f, 15f, 0f), false, false);
        ShowEarthBriefing();
    }

    LandingPadPlacer DebugEarthPad()
    {
        foreach (var pad in FindObjectsByType<LandingPadPlacer>(FindObjectsSortMode.None))
            if (!pad.HasParkedShip(LanderController.Instance)) return pad;
        var current = LandingPadPlacer.Instance;
        return current.CreateNextPad(current.transform.position) ?? current;
    }
#endif

    void OnDisable() => Restart();
    void OnDestroy()
    {
        if (dialog) dialog.ContinueClicked -= ContinueDiscovery;
        if (Instance == this) Instance = null;
    }
}
