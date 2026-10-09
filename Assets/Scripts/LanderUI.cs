using System.Collections;
using System.Collections.Generic;
using TMPro;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LanderUI : MonoBehaviour
{
    public static LanderUI Instance;

    public int startCountdown = 3;

    [Header("Panels")]
    public StartPanel startPanel;
    public LandingPanel landingPanel;
    public CrashPanel crashPanel;

    [Header("HUD")]
    public TMP_Text txtLanderFuel;
    public TMP_Text txtLanderInfos;
    public TMP_Text warningTitle;
    public TMP_Text warningMessage;
    public Button btnRefill => landingPanel.refillButton;
    public Image refillProgressFill => landingPanel.refillProgressFill;
    public TMP_Text txtGameOverMessage => GameController.Instance.Phase == GameController.GamePhase.Crashed
        ? crashPanel.message : landingPanel.message;
    TMP_Text refillLabel;
    string refillIdleLabel;
    Selectable.Transition refillTransition;
    bool showingRefill;
    [Header("Navigation")]
    public Transform navigationGrp;
    public RectTransform indicatorPad;
    public RectTransform indicatorMoon;

    public float maxXRange = 10f;         // ab wann ganz links/rechts
    public float fadeDistance = 1.5f;     // unterhalb wird ausgeblendet
    public float maxOffset = 120f;        // UI-Pixel nach links/rechts
    Image imgIndicatorPad;
    Image imgIndicatorMoon;

    [Header("Indicator Scale")]
    public Vector3 indicatorNormalScale = Vector3.one;
    public Vector3 indicatorSmallScale = Vector3.one * 0.6f;
    public float indicatorScaleSmooth = 10f;

    [Header("Fuel Bar")]
    public int blocks = 10;
    public char fullBlock = '▮';
    public char emptyBlock = '▯';

    [Header("Altitude")]
    public float rayOffset = 0.2f;
    public float maxCheckDistance = 50f;

    [Header("Live Landing Status")]
    public float statusCheckAltitude = 3.0f;
    public float warnMultiplier = 1.6f;

    [Header("Debug")]
    public bool showRay = true;
    public float gizmoRadius = 0.06f;

    private ScoringController scoring;
    private LandingPadPlacer landingPad => LandingPadPlacer.Instance;
    private LanderController lander => LanderController.Instance;
    Rigidbody2D hudBody => GameController.Instance.ControlledBody;
    Vector2 hudGravity => GameController.Instance.Phase == GameController.GamePhase.EVA
        ? GravityManager2D.Instance.GetGravity(hudBody.position) : lander.Gravity;
    private bool hasHit;
    private RaycastHit2D hit;
    private bool showDeadZoneWarning;

    private bool isGameOver;
    Coroutine flow;
    Coroutine layout;
    readonly List<RaycastHit2D> altitudeHits = new();
    readonly List<RaycastResult> uiHits = new();
    readonly StringBuilder fuelText = new(96);
    PointerEventData pointerData;
    EventSystem pointerEventSystem;
    bool initialized;
    float nextHudUpdate;

    Dictionary<LanderController.eLanderState, string[]> stateMessages =
    new Dictionary<LanderController.eLanderState, string[]>
    {
        {
            LanderController.eLanderState.LandedPad, new[]
            {
                "[[game.landing.confirmed]]",
                "[[game.touchdown.achieved]]",
                "[[game.surface.contact.stable]]",
                "[[game.landing.successful]]",
                "[[game.descent.nominal]]",
                "[[game.contact.within.limits]]",
                "[[game.landing.sequence.complete]]",
                "[[game.surface.secured]]",
                "[[game.all.systems.stable]]",
                "[[game.mission.step.completed]]"
            }
        },
        {
            LanderController.eLanderState.CrashedLandscape, new[]
            {
                "[[game.terrain.resistance.exceeded]]",
                "[[game.surface.integrity.lost]]",
                "[[game.that.mountain.won]]",
                "[[game.structural.failure.on.contact]]",
                "[[game.impact.outside.tolerance]]",
                "[[game.terrain.interaction.unsuccessful]]",
                "[[game.hull.met.geology]]",
                "[[game.descent.ended.abruptly]]",
                "[[game.surface.was.not.negotiable]]",
                "[[game.topography.prevailed]]"
            }
        },
        {
            LanderController.eLanderState.CrashedPad, new[]
            {
                "[[game.pad.alignment.failed]]",
                "[[game.close.too.close]]",
                "[[game.docking.attempt.rejected]]",
                "[[game.landing.protocol.violated]]",
                "[[game.pad.contact.unstable]]",
                "[[game.approach.vector.incorrect]]",
                "[[game.clearance.insufficient]]",
                "[[game.landing.pad.disagreed]]",
                "[[game.almost.counted]]",
                "[[game.precision.required]]"
            }
        },
        {
            LanderController.eLanderState.OutOfFuel, new[]
            {
                "[[game.fuel.depleted]]",
                "[[game.engines.silent]]",
                "[[game.momentum.only]]",
                "[[game.that.was.the.last.drop]]",
                "[[game.no.propellant.remaining]]",
                "[[game.thrust.unavailable]]",
                "[[game.fuel.reserves.exhausted]]",
                "[[game.power.without.control]]",
                "[[game.burn.sequence.incomplete]]",
                "[[game.nothing.left.to.burn]]"
            }
        },
        {
            LanderController.eLanderState.DeadZone, new[]
            {
                "[[game.navigation.boundary.exceeded]]",
                "[[game.signal.lost]]",
                "[[game.you.went.too.far]]",
                "[[game.that.space.was.not.for.you]]",
                "[[game.operational.area.left]]",
                "[[game.tracking.terminated]]",
                "[[game.return.vector.invalid]]",
                "[[game.out.of.bounds]]",
                "[[game.no.recovery.possible]]",
                "[[game.mission.envelope.breached]]"
            }
        },
        {
            LanderController.eLanderState.LandedMoon, new[]
            {
                "[[game.impressive.trajectory.incorrect.destination]]",
                "[[game.you.have.achieved.an.unintended.milestone]]",
                "[[game.this.maneuver.was.not.in.the.flight.manual]]",
                "[[game.congratulations.wrong.target.successfully.reached]]",
                "[[game.you.missed.the.pad.by.targetdistance.units.the.moon.was.not.the.b]]",
                "[[game.you.were.not.supposed.to.land.here]]",
                "[[game.unplanned.landing.succeeded]]",
                "[[game.this.should.not.have.worked]]",
                "[[game.edge.case.resolved]]",
                "[[game.this.was.not.the.objective.you.were.targetdistance.units.away.but]]"
            }
        },
        {
            LanderController.eLanderState.CrashedMoon, new[]
            {
                "[[game.and.that.s.why.the.moon.was.not.the.mission]]",
                "[[game.the.moon.was.never.in.the.briefing]]",
                "[[game.unplanned.lunar.crash.predictable]]",
                "[[game.congratulations.you.crashed.on.the.wrong.objective]]",
                "[[game.the.pad.is.still.down.there.not.on.the.moon]]",
                "[[game.you.went.off.script.hard]]",
                "[[game.next.time.land.where.you.re.supposed.to]]",
                "[[game.lunar.impact.confirmed]]",
                "[[game.foreign.gravity.misjudged]]",
                "[[game.moonfall.aborted]]",
                "[[game.lunar.approach.ended]]",
                "[[game.this.was.not.the.objective.and.you.did.not.make.it]]",
                "[[game.this.was.not.the.objective.you.were.targetdistance.units.away.and]]"
            }
        }
    };

    readonly string[] deadZoneWarnings =
    {
        "[[game.danger.turn.back]]",
        "[[game.critical.return.now]]",
        "[[game.danger.exit.immediately]]",
        "[[game.warning.leaving.safe.zone]]",
        "[[game.danger.nav.limit]]"
    };

    private string currentDeadZoneWarningMessage;

    void Awake()
    {
        Instance = this;
    }

    public void Init()
    {
        if (initialized) return;
        initialized = true;
        scoring = ScoringController.Instance;
        imgIndicatorPad = indicatorPad.GetComponent<Image>();
        imgIndicatorMoon = indicatorMoon.GetComponent<Image>();

        ShowHideDeadZoneWarning(false);
        HidePanels();
        crashPanel.retryButton.onClick.AddListener(OnRestartClicked);
        landingPanel.nextLevelButton.onClick.AddListener(OnNextClicked);
        landingPanel.continueButton.onClick.AddListener(() => GameController.Instance.ContinueFlight());
        btnRefill.onClick.AddListener(() => GameController.Instance.RefillTank());
        refillLabel = btnRefill.GetComponentInChildren<TMP_Text>();
        refillIdleLabel = refillLabel.LocalizationSource();
        refillTransition = btnRefill.transition;
        refillProgressFill.gameObject.SetActive(false);
    }

    void OnValidate()
    {
        blocks = Mathf.Clamp(blocks, 1, 64);
        startCountdown = Mathf.Max(0, startCountdown);
    }
    void Update()
    {
        if (!initialized || !lander || !GameController.Instance.ControlledTarget) return;
        if (StationInterior.Instance && GameController.Instance.Phase == GameController.GamePhase.EVA)
        {
            bool inside = StationInterior.Instance.IsInside;
            txtLanderFuel.gameObject.SetActive(!inside);
            txtLanderInfos.gameObject.SetActive(!inside);
            navigationGrp.gameObject.SetActive(!inside);
        }
        if (Time.unscaledTime >= nextHudUpdate)
        {
            RefreshLanderFuel();
            RefreshLanderInfos();
            nextHudUpdate = Time.unscaledTime + 0.1f;
        }
        ShowRayEditorVisuals();
        UpdateNav(landingPad.transform, indicatorPad, imgIndicatorPad);
        UpdateNav(GravityManager2D.Instance.transform, indicatorMoon, imgIndicatorMoon);
        UpdateIndicatorScales();
        RefreshRefillButton();

        if (showDeadZoneWarning)
        {
            warningTitle.SetLocalizedText(currentDeadZoneWarningMessage);
            warningMessage.SetLocalizedText(Mathf.CeilToInt(lander.deadZoneTimer).ToString());
            if (lander.deadZoneTimer <= 0)
                ShowHideDeadZoneWarning(false);
        }
    }

    public void ShowHideDeadZoneWarning(bool show)
    {
        if (show)
        {
            showDeadZoneWarning = true;
            currentDeadZoneWarningMessage = GetRandomDeadZoneWarning() + "\n\n";
            warningTitle.color = Color.red;
            warningTitle.gameObject.SetActive(true);
            warningMessage.color = Color.red;
            warningMessage.gameObject.SetActive(true);
        }
        else
        {
            showDeadZoneWarning = false;
            warningTitle.color = Color.white;
            warningTitle.gameObject.SetActive(false);
            warningMessage.color = Color.white;
            warningMessage.gameObject.SetActive(false);
        }
    }

    bool TryGetAltitude(out float altitude, out RaycastHit2D hit)
    {
        Vector2 g = hudGravity;
        if (g.sqrMagnitude < 0.0001f)
        {
            altitude = 0f;
            hit = default;
            return false;
        }

        Vector2 downDir = g.normalized;
        Vector2 origin = hudBody.position;

        Physics2D.Raycast(origin, downDir, new ContactFilter2D { useTriggers = false }, altitudeHits, maxCheckDistance);

        foreach (var h in altitudeHits)
        {
            if (!h.collider) continue;

            // Eigenes Schiff ignorieren
            if (h.collider.attachedRigidbody == hudBody) continue;
            if (h.collider.isTrigger) continue;

            hit = h;
            altitude = h.distance;
            return true;
        }

        altitude = 0f;
        hit = default;
        return false;
    }

    private void RefreshLanderFuel()
    {
        float fuelT = lander.FuelFraction;
        int filled = Mathf.RoundToInt(fuelT * blocks);

        fuelText.Clear();
        fuelText.Append("[[hud.fuel]]  ");
        for (int i = 0; i < blocks; i++) fuelText.Append(i < filled ? fullBlock : emptyBlock);
        fuelText.Append('\n');

        // Farbe bestimmen
        Color fuelColor = Color.white;

        if (fuelT < 0.25f)
        {
            // sanfter Puls Richtung Rot
            float pulse = 0.5f + Mathf.Sin(Time.time * 4f) * 0.5f;
            fuelColor = Color.Lerp(Color.yellow, Color.red, pulse);
        }
        else if (fuelT < 0.5f)
        {
            fuelColor = Color.white;
        }

        // Text setzen
        txtLanderFuel.SetLocalizedText(fuelText.ToString());
        txtLanderFuel.color = fuelColor;
    }

    private void RefreshLanderInfos()
    {
        if (isGameOver) return;

        // Speed
        int speedI = Mathf.RoundToInt(hudBody.linearVelocity.magnitude);

        // Gravity state
        Vector2 g = hudGravity;
        bool hasGravity = g.sqrMagnitude > 0.001f;

        // Angle (nur sinnvoll mit Gravity)
        string angleStr = hasGravity
            ? $"[[hud.angle]]   {Mathf.RoundToInt(Vector2.Angle(hudBody.transform.up, -g))}°\n"
            : "";

        // Altitude
        hasHit = TryGetAltitude(out float altitude, out hit);
        string altText = hasHit ? Mathf.RoundToInt(altitude).ToString() : "---";
        string gravStr = "";
        if (hudBody.position.y > GravityManager2D.Instance.zeroGStartY)
        {
            gravStr = $"[[hud.gravity]]  {g.magnitude:F2}";
        }

        // Status (nur wenn wir wirklich Boden "unten" haben)
        string status = "---";
        if (GameController.Instance.Phase == GameController.GamePhase.Flight && hasGravity && hasHit && altitude <= statusCheckAltitude)
        {
            float speed = lander.rb.linearVelocity.magnitude;
            float angle = Mathf.Abs(Mathf.DeltaAngle(0f, lander.rb.rotation));

            bool moon = hit.collider.CompareTag("Moon");
            float vertical = Mathf.Abs(Vector2.Dot(lander.rb.linearVelocity, g.normalized));
            bool ok = lander.IsSafeLanding(speed, vertical, angle, moon);
            bool warn = lander.IsSafeLanding(speed, vertical, angle, moon, warnMultiplier);

            status = ok ? "[[hud.ok]]" : (warn ? "[[hud.warn]]" : "[[hud.danger]]");
        }

        // Final text
        txtLanderInfos.SetLocalizedText($"[[hud.speed]]   {speedI}\n" +
            angleStr +
            $"[[hud.altitude]]   {altText}\n" +
            $"[[hud.status]]  {status}\n" + gravStr);
    }

    private void ShowRayEditorVisuals()
    {
        if (showRay)
        {
            Vector2 down = hudGravity.normalized;
            Vector2 origin = hudBody.position;
            Vector2 end = hasHit ? hit.point : (origin + down * maxCheckDistance);
            Debug.DrawLine(origin, end, Color.green);
        }
    }

    void UpdateNav(Transform target, RectTransform indicator, Image imgIndicator)
    {
        if (!target || !lander) return;

        float dx = target.position.x - GameController.Instance.ControlledTarget.position.x;

        // Position
        float t = Mathf.Clamp(dx / Mathf.Max(0.0001f, maxXRange), -1f, 1f);
        indicator.anchoredPosition =
            new Vector2(t * maxOffset, indicator.anchoredPosition.y);

        // Fade (nah = unsichtbar)
        float a = Mathf.InverseLerp(0f, fadeDistance, Mathf.Abs(dx));
        var c = imgIndicator.color;
        c.a = a;
        imgIndicator.color = c;
    }

    void UpdateIndicatorScales()
    {
        float y = GameController.Instance.ControlledTarget.position.y;
        bool inSpace = y >= GravityManager2D.Instance.zeroGFullY;

        Vector3 padTarget = inSpace ? indicatorSmallScale : indicatorNormalScale;
        Vector3 moonTarget = inSpace ? indicatorNormalScale : indicatorSmallScale;

        float k = 1f - Mathf.Exp(-indicatorScaleSmooth * Time.deltaTime);

        indicatorPad.localScale = Vector3.Lerp(indicatorPad.localScale, padTarget, k);
        indicatorMoon.localScale = Vector3.Lerp(indicatorMoon.localScale, moonTarget, k);
    }

    public void ShowResults(LanderController.eLanderState state, bool showScore = false, bool immediate = false)
    {
        CancelFlow();
        isGameOver = true;
        HidePanels();
        LanderChooserManager.Instance.SetChooserButtonsVisible(false);
        flow = StartCoroutine(ShowEndRoutine(state, showScore, immediate));
    }

    private IEnumerator ShowEndRoutine(LanderController.eLanderState state, bool showScore, bool immediate)
    {
        bool isMoon = state == LanderController.eLanderState.LandedMoon;
        bool landed = isMoon || state == LanderController.eLanderState.LandedPad;
        if (!immediate) yield return new WaitForSeconds(1.5f);
        ShowHideDeadZoneWarning(false);
        txtLanderFuel.gameObject.SetActive(true);
        txtLanderInfos.gameObject.SetActive(false);
        navigationGrp.gameObject.SetActive(false);
        HidePanels();
        string message = string.IsNullOrEmpty(GameController.Instance.ResultStoryMessage)
            ? GetRandomGameOverMessage(state) : GameController.Instance.ResultStoryMessage;
        string xp = "[[score.xp]] " + scoring.CollectedScore;
        if (landed)
        {
            bool station = lander.IsOnStation;
            landingPanel.title.SetLocalizedText(lander.IsOnOutpost ? "[[cargo.outpost.title]]" : station ? "[[game.station.landing]]" : isMoon ? "[[game.moon.landing]]" : "[[game.landed.title]]");
            landingPanel.message.SetLocalizedText(message);
            landingPanel.totalScore.SetLocalizedText(xp);
            landingPanel.score.gameObject.SetActive(showScore);
            landingPanel.score.SetLocalizedText(isMoon
                ? $"[[score.success]] +{scoring.LastBaseScore}\n[[score.speed]]   +{scoring.LastSpeedScore}\n[[score.fuel]]    +{scoring.LastFuelScore}\n[[score.time]]    +{scoring.LastTimeScore}\n★[[score.moon]]★  +{scoring.LastMoonScore}\n────────────\n[[score.score]]   {scoring.LastScore}\n\n[[score.best]]    {scoring.BestScore}\n"
                : $"[[score.success]] +{scoring.LastBaseScore}\n[[score.speed]]   +{scoring.LastSpeedScore}\n[[score.angle]]   +{scoring.LastAngleScore}\n[[score.center]]  +{scoring.LastCenterScore}\n[[score.fuel]]    +{scoring.LastFuelScore}\n[[score.time]]    +{scoring.LastTimeScore}\n────────────\n[[score.score]]   {scoring.LastScore}\n\n[[score.best]]    {scoring.BestScore}\n");
            landingPanel.nextLevelButton.gameObject.SetActive(GameController.Instance.CanStartNextLevel);
            landingPanel.continueButton.gameObject.SetActive(!station || SaveLoadManager.Instance.Data.GetFlag("story.registrationComplete"));
            btnRefill.gameObject.SetActive(GameController.Instance.CanRefill);
            landingPanel.gameObject.SetActive(true);
            MoonEVAController.Instance.RefreshAction();
        }
        else
        {
            crashPanel.title.SetLocalizedText(state switch
            {
                LanderController.eLanderState.OutOfFuel => "[[game.out.of.fuel]]",
                LanderController.eLanderState.DeadZone => "[[game.signal.lost.title]]",
                _ => "[[game.crashed]]"
            });
            crashPanel.message.SetLocalizedText(message);
            crashPanel.totalScore.SetLocalizedText(xp);
            crashPanel.gameObject.SetActive(true);
        }
        LanderChooserManager.Instance.SetChooserButtonsVisible(GameController.Instance.CanChooseLander);
        LanderChooserManager.Instance.panelChooser.gameObject.SetActive(false);
        RefreshPanel();
    }
    public void ShowResultMessage(string message)
    {
        txtGameOverMessage.SetLocalizedText(message);
        txtGameOverMessage.gameObject.SetActive(true);
        RefreshPanel();
    }

    string GetRandomGameOverMessage(LanderController.eLanderState state)
    {
        if (!stateMessages.ContainsKey(state)) return "";
        var arr = stateMessages[state];
        var msg = arr[Random.Range(0, arr.Length)];
        if (Localization.Resolve(msg).Contains("{0}"))
        {
            int distI = Mathf.RoundToInt(Vector2.Distance(lander.transform.position, landingPad.transform.position));
            msg = Localization.FormatReference(msg, distI);
        }
        return msg;
    }

    string GetRandomDeadZoneWarning()
    {
        return deadZoneWarnings[Random.Range(0, deadZoneWarnings.Length)];
    }

    void HidePanels()
    {
        startPanel.gameObject.SetActive(false);
        landingPanel.gameObject.SetActive(false);
        crashPanel.gameObject.SetActive(false);
    }

    public void HideGameOver()
    {
        CancelFlow();
        HidePanels();
        ShowHideDeadZoneWarning(false);
        isGameOver = false;
        nextHudUpdate = 0f;
        txtLanderFuel.gameObject.SetActive(true);
        txtLanderInfos.gameObject.SetActive(true);
        navigationGrp.gameObject.SetActive(true);
        LanderChooserManager.Instance.SetChooserButtonsVisible(false);
        LanderChooserManager.Instance.panelChooser.gameObject.SetActive(false);
        refillProgressFill.gameObject.SetActive(false);
    }
    void OnRestartClicked()
    {
        if (GameController.Instance.Phase == GameController.GamePhase.Crashed)
            GameController.Instance.RestartGame();
    }

    public void RefreshRefillButton()
    {
        if (!btnRefill) return;
        if (GameController.Instance.HasResults && GameController.Instance.Phase == GameController.GamePhase.Landed)
            landingPanel.nextLevelButton.gameObject.SetActive(GameController.Instance.CanStartNextLevel);
        btnRefill.interactable = GameController.Instance.CanRefill && !GameController.Instance.IsRefilling && lander.currentFuel < lander.fuelMax;
        bool refilling = GameController.Instance.IsRefilling;
        if (showingRefill != refilling)
        {
            showingRefill = refilling;
            btnRefill.transition = refilling ? Selectable.Transition.None : refillTransition;
            if (refilling && EventSystem.current && EventSystem.current.currentSelectedGameObject == btnRefill.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            var colors = btnRefill.colors;
            var tint = btnRefill.interactable ? colors.normalColor : colors.disabledColor;
            btnRefill.targetGraphic.CrossFadeColor((refilling ? colors.normalColor : tint) * colors.colorMultiplier, 0f, true, true);
        }
        if (GameController.Instance.IsRefilling)
            refillLabel.SetLocalizedText(Localization.FormatReference("[[hud.refilling]]", Mathf.RoundToInt(GameController.Instance.RefillProgress * 100f)));
        else refillLabel.SetLocalizedText(refillIdleLabel);
        refillProgressFill.gameObject.SetActive(GameController.Instance.IsRefilling);
        refillProgressFill.rectTransform.anchorMax = new Vector2(GameController.Instance.RefillProgress, 1f);
    }

    void OnNextClicked()
    {
        if (GameController.Instance.CanStartNextLevel)
            GameController.Instance.NextLevel();
    }

    void CancelFlow()
    {
        if (flow != null) StopCoroutine(flow);
        flow = null;
    }

    public void StartCountdown()
    {
        CancelFlow();
        HidePanels();
        flow = StartCoroutine(StartCountdownRoutine());
    }
    private IEnumerator StartCountdownRoutine()
    {
        startPanel.gameObject.SetActive(true);
        startPanel.gameTitle.gameObject.SetActive(true);
        startPanel.title.gameObject.SetActive(false);
        startPanel.message.gameObject.SetActive(false);
        txtLanderFuel.gameObject.SetActive(false);
        txtLanderInfos.gameObject.SetActive(false);
        navigationGrp.gameObject.SetActive(false);

        yield return new WaitForSeconds(2f);
        startPanel.gameTitle.gameObject.SetActive(false);

        startPanel.title.gameObject.SetActive(true);
        startPanel.message.gameObject.SetActive(true);
        RefreshPanel();

        startPanel.message.SetLocalizedText("[[game.start.in]]");
        startPanel.title.SetLocalizedText("[[hud.level]] " + GameController.Instance.level + "\n\n ");

        yield return new WaitForSeconds(1.5f);

        for (int count = Mathf.Max(0, startCountdown); count > 0; count--)
        {
            AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdown);
            startPanel.message.SetLocalizedText(count.ToString());
            yield return new WaitForSeconds(1f);
        }
        startPanel.message.gameObject.SetActive(false);

        txtLanderFuel.gameObject.SetActive(true);
        txtLanderInfos.gameObject.SetActive(true);
        navigationGrp.gameObject.SetActive(true);

        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdownStart, 1f, 1f, false);
        startPanel.title.SetLocalizedText("[[game.land]]");
        GameController.Instance.BeginRun();
        yield return PauseMenu.WaitUnpaused(1f);

        startPanel.gameObject.SetActive(false);

    }

    public void RefreshPanel()
    {
        if (layout != null) return;
        layout = StartCoroutine(DelayedLayoutRebuild());
    }

    private IEnumerator DelayedLayoutRebuild()
    {
        yield return null;
        layout = null;

        foreach (var panel in new[] { startPanel.gameObject, landingPanel.gameObject, crashPanel.gameObject })
            if (panel.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform);
    }

    public bool TryGetGameplayPointer(out Vector2 screenPosition)
    {
        screenPosition = default;
        if (!GameController.Instance || !GameController.Instance.IsPlaying) return false;
        if ((StationInterior.Instance && StationInterior.Instance.IsTransitioning)
            || (StationConversation.Instance && StationConversation.Instance.IsShowing)) return false;
        if (StoryTextController.Instance && StoryTextController.Instance.BlocksGameplayInput) return false;
        if (CargoMission.Instance && CargoMission.Instance.IsShowing) return false;
        if (RadioController.Instance && RadioController.Instance.IsOpen) return false;
        if (PauseMenu.IsPaused) return false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            var touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
            if (IsPointerOverUI(touch.position)) continue;
            screenPosition = touch.position;
            return true;
        }
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButton(0) && !IsPointerOverUI(Input.mousePosition))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif
        screenPosition = default;
        return false;
    }

    public bool IsPointerOverUI()
        => IsPointerOverUI(Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition);

    bool IsPointerOverUI(Vector2 screenPosition)
    {
        var eventSystem = EventSystem.current;
        if (!eventSystem) return false;
        if (pointerEventSystem != eventSystem)
        {
            pointerEventSystem = eventSystem;
            pointerData = new PointerEventData(eventSystem);
        }
        pointerData.Reset();
        pointerData.position = screenPosition;
        uiHits.Clear();
        eventSystem.RaycastAll(pointerData, uiHits);
        foreach (var result in uiHits)
        {
            var selectable = result.gameObject.GetComponentInParent<Selectable>();
            if (selectable && selectable.isActiveAndEnabled) return true;
            var chooser = LanderChooserManager.Instance.panelChooser;
            if (chooser.gameObject.activeInHierarchy && result.gameObject.transform.IsChildOf(chooser)) return true;
        }
        return false;
    }

    void OnDisable()
    {
        CancelFlow();
        if (layout != null) StopCoroutine(layout);
        layout = null;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
    void OnDrawGizmos()
    {
        if (!showRay || !lander?.rb) return;

        //Draw ray in direction of gravity to visualize altitude check

        Vector2 g = lander.Gravity;
        if (g.sqrMagnitude < 0.0001f) return; // ZeroG → kein "unten"

        Vector2 downDir = g.normalized;

        Vector2 origin = lander.rb.position + downDir * rayOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(origin, gizmoRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + downDir * maxCheckDistance);
    }

}
