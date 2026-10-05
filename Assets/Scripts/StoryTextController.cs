using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StoryTextController : MonoBehaviour
{
    public static StoryTextController Instance;
    public enum eStoryTextType { AtmosphereExit, BackToPlanet, NearToMoon }
    public enum Discovery { ZeroG, Moon, MoonLanding, EVA, AbandonedShip }
    public enum DiscoveryPresentation { SlowMotion, Overlay, LandingResults }

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
    [SerializeField] GameObject discoveryPanel;
    [SerializeField] TMP_Text discoveryText;
    [SerializeField] Button continueButton;
    [SerializeField] GameObject[] transmissionPortraits = Array.Empty<GameObject>();
    [SerializeField] GameObject zeroGPortrait;
    [SerializeField] CanvasGroup gameplayPanelVisibility;

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
        new(Discovery.AbandonedShip, "Finally. The company lander.\n\nParked on the Moon with a full tank.\nI'll have a word with accounting.\n\nBring it back to Earth in one piece.\nInsurance doesn't cover\n'forgotten on the Moon'.")
    };
    [SerializeField, TextArea] string zeroGAfterDepartureMessage = "Wait. Where are you now?\n\nI know. I said I was leaving.\nBut gravity has stopped participating.\nThat wasn't part of my little landing game.";
    [SerializeField, Range(0f, 1f)] float flightCommentChance = 0.25f;

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

    void Awake()
    {
        Instance = this;
        if (txtInfo) txtInfo.gameObject.SetActive(false);
        if (discoveryPanel) discoveryPanel.SetActive(false);
        SetTransmissionPortrait(0);
        if (continueButton) continueButton.onClick.AddListener(ContinueDiscovery);
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
        if (data == null || HasDiscovered(discovery)) return;
        if (IsGravityDiscovery(discovery) && runner != null && !IsShowingDialogue) ClearPresentation();
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
        var game = GameController.Instance;
        if (!game || (!game.IsPlaying && game.Phase != GameController.GamePhase.Landed))
        {
            if (runner != null) ClearPresentation();
            return;
        }
        if (game.HasResults && !hadResults) ClearPresentation();
        hadResults = game.HasResults;
        if (!game.IsExploring) QueueEarthTransmission(game.level);
        if (!gravityManager || !game.ControlledTarget) return;
        var target = game.ControlledTarget;
        float distance = Vector2.Distance(target.position, gravityManager.transform.position);
        if (game.Phase == GameController.GamePhase.EVA) Discover(Discovery.EVA);
        if (game.Phase == GameController.GamePhase.EVA || (LanderController.Instance
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
                && HasDiscovered(entry.discovery) && !data.GetFlag("story.ack." + entry.discovery)
                && DiscoveryIsReady(entry) && queuedDiscoveries.Add(entry.discovery))
                Enqueue(GetDiscoveryText(entry.discovery), "story.ack." + entry.discovery, 0, entry.discovery);
    }

    float GetDiscoveryDelay(Discovery discovery)
        => IsGravityDiscovery(discovery) ? 0f : discoveryDelay;

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
        if (game.Phase != GameController.GamePhase.Flight) return false;
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
        if (data.GetFlag("story.ack.MoonLanding")) return null;
        data.SetFlag("story.ack.MoonLanding", true);
        data.SetFlag("story.ack.Moon", true);
        return GetDiscoveryText(Discovery.MoonLanding);
    }

    public void Show(string message) => Enqueue(message, null);
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
                activeAcknowledgement = entry.acknowledgement;
                activeTransmissionLevel = entry.transmissionLevel;
                previousTimeScale = Time.timeScale;
                previousFixedDeltaTime = Time.fixedDeltaTime;
                ownsTimeScale = true;
                continueRequested = false;
                bool gravityDiscovery = entry.discovery.HasValue && IsGravityDiscovery(entry.discovery.Value);
                float transitionDuration = gravityDiscovery ? gravityTransitionDuration : timeTransitionDuration;
                yield return ChangeTimeScale(entry.transmissionLevel > 0 ? 0f : previousTimeScale * discoveryTimeScale, transitionDuration, () =>
                {
                    SetTransmissionPortrait(entry.discovery == Discovery.AbandonedShip ? 3 : entry.transmissionLevel);
                    if (zeroGPortrait) zeroGPortrait.SetActive(entry.discovery == Discovery.ZeroG || entry.discovery == Discovery.Moon);
                    discoveryText.text = entry.text;
                    discoveryPanel.SetActive(true);
                    dialogueVisible = true;
                    continueButton.interactable = false;
                    SetGameplayPanelVisible(false);
                });
                if (entry.discovery.HasValue && !gravityDiscovery && !CanShowDiscovery(entry.discovery.Value))
                {
                    queuedDiscoveries.Remove(entry.discovery.Value);
                    HideDialogue();
                    yield return ChangeTimeScale(previousTimeScale, transitionDuration);
                    EndDiscovery();
                    continue;
                }
                continueButton.interactable = true;
                if (UnityEngine.EventSystems.EventSystem.current)
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
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
        data.SetFlag(activeAcknowledgement, true);
        continueRequested = true;
        waitForPointerRelease = true;
        HideDialogue();
        SaveLoadManager.Instance.Save();
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
        SetTransmissionPortrait(0);
        if (discoveryPanel) discoveryPanel.SetActive(false);
        SetGameplayPanelVisible(true);
        if (continueButton && UnityEngine.EventSystems.EventSystem.current
            && UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject == continueButton.gameObject)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
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
        HideDialogue();
    }

    void SetTransmissionPortrait(int level)
    {
        if (zeroGPortrait) zeroGPortrait.SetActive(false);
        for (int i = 0; i < transmissionPortraits.Length; i++)
            if (transmissionPortraits[i]) transmissionPortraits[i].SetActive(level == (i + 1) * 3);
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
    void OnDisable() => Restart();
    void OnDestroy()
    {
        if (continueButton) continueButton.onClick.RemoveListener(ContinueDiscovery);
        if (Instance == this) Instance = null;
    }
}
