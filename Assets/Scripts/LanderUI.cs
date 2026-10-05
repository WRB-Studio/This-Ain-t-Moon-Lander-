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

    [Header("Refs")]
    public Transform panelGrp;
    public TMP_Text txtGameTitle;
    public TMP_Text txtLanderFuel;
    public TMP_Text txtLanderInfos;
    public TMP_Text txtGameOverTitle;
    public TMP_Text txtGameOverMessage;
    public TMP_Text txtScore;
    public TMP_Text txtXPScore;
    public Button btnRestart;
    public Button btnContinue;
    public Button btnRefill;
    TMP_Text refillLabel;
    string refillIdleLabel;
    Selectable.Transition refillTransition;
    bool showingRefill;
    public GameObject flightActions;
    [Header("Refill Progress")]
    public Image refillProgressFill;
    [Header("Panel Positions")]
    public RectTransform panelTopPosition;
    public RectTransform panelCenterPosition;
    public RectTransform panelBottomPosition;

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
                "Landing confirmed.",
                "Touchdown achieved.",
                "Surface contact stable.",
                "Landing successful.",
                "Descent nominal.",
                "Contact within limits.",
                "Landing sequence complete.",
                "Surface secured.",
                "All systems stable.",
                "Mission step completed."
            }
        },
        {
            LanderController.eLanderState.CrashedLandscape, new[]
            {
                "Terrain resistance exceeded.",
                "Surface integrity lost.",
                "That mountain won.",
                "Structural failure on contact.",
                "Impact outside tolerance.",
                "Terrain interaction unsuccessful.",
                "Hull met geology.",
                "Descent ended abruptly.",
                "Surface was not negotiable.",
                "Topography prevailed."
            }
        },
        {
            LanderController.eLanderState.CrashedPad, new[]
            {
                "Pad alignment failed.",
                "Close. Too close.",
                "Docking attempt rejected.",
                "Landing protocol violated.",
                "Pad contact unstable.",
                "Approach vector incorrect.",
                "Clearance insufficient.",
                "Landing pad disagreed.",
                "Almost counted.",
                "Precision required."
            }
        },
        {
            LanderController.eLanderState.OutOfFuel, new[]
            {
                "Fuel depleted.",
                "Engines silent.",
                "Momentum only.",
                "That was the last drop.",
                "No propellant remaining.",
                "Thrust unavailable.",
                "Fuel reserves exhausted.",
                "Power without control.",
                "Burn sequence incomplete.",
                "Nothing left to burn."
            }
        },
        {
            LanderController.eLanderState.DeadZone, new[]
            {
                "Navigation boundary exceeded.",
                "Signal lost.",
                "You went too far.",
                "That space was not for you.",
                "Operational area left.",
                "Tracking terminated.",
                "Return vector invalid.",
                "Out of bounds.",
                "No recovery possible.",
                "Mission envelope breached."
            }
        },
        {
            LanderController.eLanderState.LandedMoon, new[]
            {
                "Impressive trajectory. Incorrect destination.",
                "You have achieved an unintended milestone.",
                "This maneuver was not in the flight manual.",
                "Congratulations. Wrong target successfully reached.",
                "You missed the pad by {TargetDistance} units. The moon was not the backup plan.",
                "You were not supposed to land here.",
                "Unplanned landing succeeded.",
                "This should not have worked.",
                "Edge case resolved.",
                "This was not the objective. You were {TargetDistance} units away, but you landed."
            }
        },
        {
            LanderController.eLanderState.CrashedMoon, new[]
            {
                "And that's why the moon was not the mission.",
                "The Moon was never in the briefing.",
                "Unplanned lunar crash. Predictable.",
                "Congratulations. You crashed on the wrong objective.",
                "The pad is still down there. Not on the Moon.",
                "You went off-script. Hard.",
                "Next time: land where you're supposed to.",
                "Lunar impact confirmed.",
                "Foreign gravity misjudged.",
                "Moonfall aborted.",
                "Lunar approach ended.",
                "This was not the objective, and you did not make it.",
                "This was not the objective. You were {TargetDistance} units away, and you failed."
            }
        }
    };

    readonly string[] deadZoneWarnings =
    {
        "DANGER: TURN BACK",
        "CRITICAL: RETURN NOW",
        "DANGER: EXIT IMMEDIATELY",
        "WARNING: LEAVING SAFE ZONE",
        "DANGER: NAV LIMIT"
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
        txtGameOverTitle.gameObject.SetActive(false);
        txtScore.gameObject.SetActive(false);
        txtXPScore.gameObject.SetActive(false);

        btnRestart.gameObject.SetActive(false);
        btnRestart.onClick.AddListener(OnRestartClicked);
        btnContinue.onClick.AddListener(() => GameController.Instance.ContinueFlight());
        btnRefill.onClick.AddListener(() => GameController.Instance.RefillTank());
        refillLabel = btnRefill.GetComponentInChildren<TMP_Text>();
        refillIdleLabel = refillLabel.text;
        refillTransition = btnRefill.transition;
        flightActions.SetActive(false);
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
            txtGameOverTitle.text = currentDeadZoneWarningMessage;
            txtGameOverMessage.text = Mathf.CeilToInt(lander.deadZoneTimer).ToString();
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
            txtGameOverTitle.color = Color.red;
            txtGameOverTitle.gameObject.SetActive(true);
            txtGameOverMessage.color = Color.red;
            txtGameOverMessage.gameObject.SetActive(true);
        }
        else
        {
            showDeadZoneWarning = false;
            txtGameOverTitle.color = Color.white;
            txtGameOverTitle.gameObject.SetActive(false);
            txtGameOverMessage.color = Color.white;
            txtGameOverMessage.gameObject.SetActive(false);
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
        fuelText.Append("FUEL  ");
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
        txtLanderFuel.SetText(fuelText);
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
            ? $"ANG   {Mathf.RoundToInt(Vector2.Angle(hudBody.transform.up, -g))}°\n"
            : "";

        // Altitude
        hasHit = TryGetAltitude(out float altitude, out hit);
        string altText = hasHit ? Mathf.RoundToInt(altitude).ToString() : "---";
        string gravStr = "";
        if (hudBody.position.y > GravityManager2D.Instance.zeroGStartY)
        {
            gravStr = $"GRAV  {g.magnitude:F2}";
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

            status = ok ? "OK" : (warn ? "WARN" : "DANGER");
        }

        // Final text
        txtLanderInfos.text =
            $"SPD   {speedI}\n" +
            angleStr +
            $"ALT   {altText}\n" +
            $"STAT  {status}\n" + gravStr;
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

        txtGameOverTitle.gameObject.SetActive(false);
        txtGameOverMessage.gameObject.SetActive(false);
        txtScore.gameObject.SetActive(false);

        txtXPScore.gameObject.SetActive(true);
        txtXPScore.text = "XP-SCORE " + ScoringController.Instance.CollectedScore;

        txtGameOverMessage.text = string.IsNullOrEmpty(GameController.Instance.ResultStoryMessage)
            ? GetRandomGameOverMessage(state) : GameController.Instance.ResultStoryMessage;
        txtGameOverTitle.text = state switch
        {
            LanderController.eLanderState.LandedPad => "LANDED\n\n",
            LanderController.eLanderState.LandedMoon => "MOON LANDING\n\n",
            LanderController.eLanderState.OutOfFuel => "OUT OF FUEL\n\n",
            LanderController.eLanderState.DeadZone => "SIGNAL LOST\n\n",
            _ => "CRASHED\n\n"
        };

        btnRestart.onClick.RemoveAllListeners();
        btnRestart.gameObject.SetActive(true);

        LanderChooserManager.Instance.btnLanderChooser.gameObject.SetActive(GameController.Instance.CanChooseLander);
        LanderChooserManager.Instance.panelChooser.gameObject.SetActive(false);

        SetPanelTopCenter();

        if (landed)
        {
            txtGameOverTitle.gameObject.SetActive(true);
            txtGameOverMessage.gameObject.SetActive(true);
            txtScore.gameObject.SetActive(showScore);

            if (isMoon)
            {
                txtScore.text =
                    $"SUCCESS +{scoring.LastBaseScore}\n" +
                    $"SPEED   +{scoring.LastSpeedScore}\n" +
                    $"FUEL    +{scoring.LastFuelScore}\n" +
                    $"TIME    +{scoring.LastTimeScore}\n" +
                    $"★MOON★  +{scoring.LastMoonScore}\n" +
                    "────────────\n" +
                    $"SCORE   {scoring.LastScore}\n" +
                    $"\nBEST    {scoring.BestScore}\n";

                MoonEVAController.Instance.RefreshAction();
            }
            else
            {
                txtScore.text =
                    $"SUCCESS +{scoring.LastBaseScore}\n" +
                    $"SPEED   +{scoring.LastSpeedScore}\n" +
                    $"ANGLE   +{scoring.LastAngleScore}\n" +
                    $"CENTER  +{scoring.LastCenterScore}\n" +
                    $"FUEL    +{scoring.LastFuelScore}\n" +
                    $"TIME    +{scoring.LastTimeScore}\n" +
                    "────────────\n" +
                    $"SCORE   {scoring.LastScore}\n" +
                    $"\nBEST    {scoring.BestScore}\n";
            }

            btnRestart.transform.GetChild(0).GetComponent<TMP_Text>().text = "Next Level";
            btnRestart.onClick.AddListener(OnNextClicked);
            btnRestart.gameObject.SetActive(GameController.Instance.CanStartNextLevel);
        }
        else
        {
            SetPanelCenter();

            txtGameOverTitle.gameObject.SetActive(true);
            txtGameOverMessage.gameObject.SetActive(true);

            btnRestart.transform.GetChild(0).GetComponent<TMP_Text>().text = "Try Again";
            btnRestart.onClick.AddListener(OnRestartClicked);
        }

        btnContinue.gameObject.SetActive(landed);
        btnRefill.gameObject.SetActive(state == LanderController.eLanderState.LandedPad);
        flightActions.SetActive(true);
        RefreshPanel();
    }

    public void ShowResultMessage(string message)
    {
        txtGameOverMessage.text = message;
        txtGameOverMessage.gameObject.SetActive(true);
        RefreshPanel();
    }

    public void SetPanelTopCenter() => SetPanelPosition(panelTopPosition);
    public void SetPanelCenter() => SetPanelPosition(panelCenterPosition);
    public void SetPanelBottomCenter() => SetPanelPosition(panelBottomPosition);

    void SetPanelPosition(RectTransform position)
    {
        var rect = panelGrp as RectTransform;
        if (!rect || !position) return;
        rect.anchorMin = position.anchorMin;
        rect.anchorMax = position.anchorMax;
        rect.pivot = position.pivot;
        rect.anchoredPosition = position.anchoredPosition;
    }
    string GetRandomGameOverMessage(LanderController.eLanderState state)
    {
        if (!stateMessages.ContainsKey(state)) return "";
        var arr = stateMessages[state];
        var msg = arr[Random.Range(0, arr.Length)];
        if (msg.Contains("{TargetDistance}"))
        {
            int distI = Mathf.RoundToInt(Vector2.Distance(lander.transform.position, landingPad.transform.position));
            msg = msg.Replace("{TargetDistance}", distI.ToString());
        }
        return msg;
    }

    string GetRandomDeadZoneWarning()
    {
        return deadZoneWarnings[Random.Range(0, deadZoneWarnings.Length)];
    }

    public void HideGameOver()
    {
        CancelFlow();
        ShowHideDeadZoneWarning(false);
        isGameOver = false;
        nextHudUpdate = 0f;

        txtLanderFuel.gameObject.SetActive(true);
        txtLanderInfos.gameObject.SetActive(true);
        navigationGrp.gameObject.SetActive(true);

        txtGameOverTitle.gameObject.SetActive(false);
        txtGameOverMessage.gameObject.SetActive(false);
        txtScore.gameObject.SetActive(false);
        txtXPScore.gameObject.SetActive(false);

        LanderChooserManager.Instance.btnLanderChooser.gameObject.SetActive(false);
        LanderChooserManager.Instance.panelChooser.gameObject.SetActive(false);
        btnRestart.gameObject.SetActive(false);
        btnContinue.gameObject.SetActive(false);
        btnRefill.gameObject.SetActive(false);
        flightActions.SetActive(false);
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
            btnRestart.gameObject.SetActive(GameController.Instance.CanStartNextLevel);
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
            refillLabel.SetText("Refilling... {0}%", Mathf.RoundToInt(GameController.Instance.RefillProgress * 100f));
        else refillLabel.text = refillIdleLabel;
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
        SetPanelCenter();
        flow = StartCoroutine(StartCountdownRoutine());
    }
    private IEnumerator StartCountdownRoutine()
    {
        txtLanderFuel.gameObject.SetActive(false);
        txtLanderInfos.gameObject.SetActive(false);
        navigationGrp.gameObject.SetActive(false);

        yield return new WaitForSeconds(2f);
        txtGameTitle.gameObject.SetActive(false);

        txtGameOverTitle.gameObject.SetActive(true);
        txtGameOverMessage.gameObject.SetActive(true);
        RefreshPanel();

        txtGameOverMessage.text = "Start in...";
        txtGameOverTitle.text = "LVL " + GameController.Instance.level + "\n\n ";

        yield return new WaitForSeconds(1.5f);

        for (int count = Mathf.Max(0, startCountdown); count > 0; count--)
        {
            AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdown);
            txtGameOverMessage.text = count.ToString();
            yield return new WaitForSeconds(1f);
        }
        txtGameOverMessage.gameObject.SetActive(false);

        txtLanderFuel.gameObject.SetActive(true);
        txtLanderInfos.gameObject.SetActive(true);
        navigationGrp.gameObject.SetActive(true);

        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdownStart, 1f, 1f, false);
        txtGameOverTitle.text = "Land!";
        GameController.Instance.BeginRun();
        yield return new WaitForSecondsRealtime(1f);

        txtGameOverTitle.gameObject.SetActive(false);

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

        var layoutRoot = panelGrp.GetComponentInChildren<VerticalLayoutGroup>()?.transform as RectTransform;
        if (layoutRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
    }

    public bool TryGetGameplayPointer(out Vector2 screenPosition)
    {
        screenPosition = default;
        if (!GameController.Instance || !GameController.Instance.IsPlaying) return false;
        if (StoryTextController.Instance && StoryTextController.Instance.BlocksGameplayInput) return false;
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
