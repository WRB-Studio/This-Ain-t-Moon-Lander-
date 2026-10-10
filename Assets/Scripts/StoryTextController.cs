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
        new(Discovery.ZeroG, "[[game.oh.you.re.actually.trying.something.else.and.gravity.has.stopped]]"),
        new(Discovery.Moon, "[[game.oh.so.that.is.the.moon.minor.naming.issue]]"),
        new(Discovery.MoonLanding, "[[game.you.actually.landed.on.the.moon.i.hadn.t.planned.that.far.ahead]]", DiscoveryPresentation.LandingResults),
        new(Discovery.EVA, "[[game.you.can.get.out.apparently.this.spacecraft.came.with.legs]]", DiscoveryPresentation.Overlay),
        new(Discovery.AbandonedShip, "[[game.finally.the.company.lander.parked.on.the.moon.with.a.full.tank.i]]", DiscoveryPresentation.Immediate),
        new(Discovery.UFO, "[[game.incoming.transmission.you.re.late.wait.you.re.not.our.pilot.where]]", DiscoveryPresentation.Immediate),
        new(Discovery.Station, "[[game.there.you.are.welcome.to.the.station.we.were.expecting.that.lande]]", DiscoveryPresentation.Paused),
        new(Discovery.StationLanding, "[[game.docking.confirmed.you.can.step.outside]]", DiscoveryPresentation.LandingResults)
    };
    [SerializeField, TextArea] string zeroGAfterDepartureMessage = "[[game.wait.where.are.you.now.i.know.i.said.i.was.leaving.but.gravity.ha]]";
    [SerializeField, Range(0f, 1f)] float flightCommentChance = 0.25f;
    [SerializeField, TextArea(4, 10)] string earthReturnMessage = "[[game.back.in.one.piece.with.the.company.lander.excellent.work.by.show]]";

    [Header("Earth Launches")]
    [SerializeField, TextArea] string moonKnownEarthMessage = "[[game.you.know.where.the.real.moon.is.back.for.fuel.or.just.fond.of.thi]]";
    [SerializeField] LevelMessages[] earthMessages = {
        new(1, "[[game.land.safely.on.the.marked.pad]]", "[[game.slow.your.descent.before.touching.down]]", "[[game.keep.the.spacecraft.upright.aim.for.the.pad]]"),
        new(2, "[[game.another.pad.same.procedure]]", "[[game.bring.it.down.preferably.in.one.piece]]", "[[game.try.landing.near.the.middle.this.time]]"),
        new(4, "[[game.yes.another.landing.how.adventurous]]", "[[game.another.parking.spot.awaits.your.grand.entrance]]", "[[game.you.seem.very.committed.to.the.obvious.task]]"),
        new(5, "[[game.you.have.really.made.landing.your.whole.thing]]", "[[game.i.briefly.hoped.you.would.surprise.me]]", "[[game.the.scenery.changes.your.ambition.does.not]]"),
        new(7, "[[game.i.am.no.longer.counting.enthusiastically]]", "[[game.you.could.explore.or.polish.your.parking.record]]", "[[game.i.specifically.told.you.that.you.can.fly.up]]"),
        new(8, "[[game.you.are.remarkably.good.at.missing.the.point]]", "[[game.i.would.sigh.but.this.is.text]]", "[[game.land.if.you.must.i.am.past.negotiating]]")
    };

    readonly Dictionary<eStoryTextType, string[]> stateMessages = new() {
        { eStoryTextType.AtmosphereExit, new[] { "[[game.there.you.go.beyond.the.parking.spots]]", "[[game.still.exploring.now.i.m.curious]]", "[[game.i.wonder.how.far.this.goes]]" } },
        { eStoryTextType.BackToPlanet, new[] { "[[game.ah.parking.spots.again]]", "[[game.a.familiar.view.try.not.to.get.too.comfortable]]", "[[game.back.for.fuel.that.sounds.like.a.plan]]" } },
        { eStoryTextType.NearToMoon, new[] { "[[game.back.to.the.moon.you.know.more.about.it.than.i.do]]", "[[game.i.wonder.what.else.is.up.there]]", "[[game.still.there.this.wasn.t.in.my.little.landing.game]]" } }
    };

    [Header("Earth Transmissions")]
    [SerializeField] EarthTransmission[] earthTransmissions = {
        new(3, "[[game.good.third.level.you.understand.how.a.landing.pad.works.i.was.hop]]"),
        new(6, "[[game.sixth.level.you.are.seriously.collecting.parking.spots.fine.let.m]]"),
        new(9, "[[game.nine.levels.i.gave.you.a.sky.a.spacecraft.thrusters.and.you.keep]]")
    };
    [Header("After Level Nine")]
    [SerializeField, Range(0f, 1f)] float earthAsideChance = 0.25f;
    [SerializeField, TextArea(1, 5)] string[] earthAsides = {
        "...", "[[game.uh.huh]]", "[[game.sure]]", "[[game.of.course]]", "[[game.how.surprising]]", "[[game.another.one]]",
        "[[game.you.ve.got.this]]", "[[game.i.m.not.here]]", "[[game.that.was.not.an.invitation.to.talk]]", "[[game.no.i.am.not.commenting.on.this]]",
        "[[game.is.that.pilot.still.landing.yes.don.t.ask.oh.the.microphone.is.on]]",
        "[[game.coffee.please.it.s.been.nothing.but.parking.all.day.wait.is.this]]",
        "[[game.has.anyone.told.them.they.can.leave.i.did.several.times.oh.you.ca]]",
        "[[game.any.progress.the.parking.situation.is.excellent.right.microphone]]"
    };

    readonly Queue<(string text, string acknowledgement, int transmissionLevel, Discovery? discovery)> queue = new();
    readonly HashSet<Discovery> queuedDiscoveries = new();
    readonly Dictionary<eStoryTextType, float> nextAllowed = new();
    readonly Dictionary<Discovery, float> discoveryReadyAt = new();
    Coroutine runner;
    StoryLineAudio lineAudio;
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
        if (lineAudio) lineAudio.Stop();
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
        if (HasDiscovered(Discovery.Moon)) { Show(EventText("moonKnownEarthMessage", moonKnownEarthMessage)); return; }
        string[] messages = GetEarthMessages(level);
        if (messages.Length == 0) return;
        string message = messages[UnityEngine.Random.Range(0, messages.Length)];
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
        var fallback = selected?.messages ?? Array.Empty<string>();
        return StoryLibrary.Project ? StoryLibrary.Project.TrainingTexts(level, fallback, StoryLibrary.Flag) : fallback;
    }

    public string PickEarthAside()
    {
        var messages = StoryLibrary.Project ? StoryLibrary.Project.EventTexts("earth-asides", earthAsides) : earthAsides;
        if (messages.Length == 0) return "";
        int previous = Array.IndexOf(messages, Localization.Reference(data.lastEarthAside));
        int index = UnityEngine.Random.Range(0, messages.Length - (previous >= 0 && messages.Length > 1 ? 1 : 0));
        if (previous >= 0 && messages.Length > 1 && index >= previous) index++;
        data.lastEarthAside = messages[index];
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
                Enqueue(EventText("transmission-" + level, transmission.text), key, level);
            }
            return true;
        }
        return false;
    }

    readonly HashSet<int> queuedTransmissions = new();

    void Update()
    {
        if (waitForPointerRelease && Input.touchCount == 0 && !Input.GetMouseButton(0)) waitForPointerRelease = false;
        if (PauseMenu.IsPaused) return;
        if (RadioController.Instance && RadioController.Instance.IsOpen) return;
        if (StationConversation.Instance && StationConversation.Instance.IsShowing) return;
        if (CargoMission.Instance && CargoMission.Instance.IsShowing) return;
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
            && !LanderController.Instance.IsOnOutpost
#if UNITY_EDITOR
            && DebugReturnEnabled
#endif
            && data.GetFlag("story.companyLanderReturned") && !data.GetFlag("story.ack.CompanyReturn")
            && !IsShowingDialogue)
            ShowOperatorDialogue(EventText("earthReturnMessage", earthReturnMessage), "story.ack.CompanyReturn");
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
            return EventText("zeroGAfterDepartureMessage", zeroGAfterDepartureMessage);
        foreach (var entry in discoveryMessages) if (entry.discovery == discovery) return EventText("discovery-" + discovery, entry.text);
        return "";
    }

    string EventText(string pool, string fallback)
    {
        var texts = StoryLibrary.Project ? StoryLibrary.Project.EventTexts(pool, new[] { fallback }) : new[] { fallback };
        return texts.Length > 0 ? texts[0] : "";
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
        return EventText("earthReturnMessage", earthReturnMessage);
    }

    public string TakeStationLandingMessage()
    {
        data.SetFlag("story.ack.StationLanding", true);
        return GetDiscoveryText(Discovery.StationLanding);
    }

    public void Show(string message) => Enqueue(message, null);

    public void ShowOperatorDialogue(string message, string acknowledgement = null)
    {
        if (string.IsNullOrEmpty(message)) return;
        if (RadioController.Instance && RadioController.Instance.IsOwned)
        {
            RadioController.Instance.Receive(new RadioMessage
            {
                id = acknowledgement ?? "operator." + message, sender = "[[game.company]]", title = "[[game.company.transmission]]",
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
        if (StoryLibrary.Project) messages = StoryLibrary.Project.EventTexts("flight-" + type, messages);
        if (messages.Length == 0) return;
        string message = messages[UnityEngine.Random.Range(0, messages.Length)];
        if (GameController.Instance.level < 3 && type == eStoryTextType.BackToPlanet)
            message = EventText("early-earth-return", "[[game.back.on.earth.you.can.refuel.on.a.free.landing.pad]]");
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
            while (PauseMenu.IsPaused) yield return null;
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
                        id = entry.acknowledgement, sender = station ? "[[game.station]]" : unknown ? "[[game.unknown.sender]]" : "[[game.company]]",
                        title = unknown ? "[[game.unidentified.transmission]]" : "[[game.incoming.transmission]]", body = entry.text, portrait = portrait
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
                var node = StoryLibrary.Project ? StoryLibrary.Project.Match(entry.text, StoryLibrary.Flag) : null;
                txtInfo.SetLocalizedText(node == null ? entry.text : "[[" + node.textKey + "]]");
                txtInfo.gameObject.SetActive(true);
                if (!lineAudio) lineAudio = txtInfo.gameObject.AddComponent<StoryLineAudio>();
                lineAudio.Play(node);
                if (entry.acknowledgement != null)
                {
                    data.SetFlag(entry.acknowledgement, true);
                    SaveLoadManager.Instance.Save();
                }
                yield return PauseMenu.WaitUnpaused(Mathf.Max(visibleTime, Localization.Resolve(entry.text).Length * 0.055f));
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
            while (PauseMenu.IsPaused) yield return null;
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
