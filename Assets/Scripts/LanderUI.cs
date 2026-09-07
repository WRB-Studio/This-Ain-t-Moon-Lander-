using System.Collections;
using System.Collections.Generic;
using TMPro;
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

    [Header("Navigation")]
    public Transform navigationGrp;
    public RectTransform indicatorPad;
    public RectTransform indicatorMoon;
    public float maxXRange = 10f;
    public float fadeDistance = 1.5f;
    public float maxOffset = 120f;

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
    public float statusCheckAltitude = 3f;
    public float warnMultiplier = 1.6f;

    [Header("Debug")]
    public bool showRay = true;
    public float gizmoRadius = 0.06f;

    ScoringController scoring;
    LandingPadPlacer landingPad;
    Image imgIndicatorPad;
    Image imgIndicatorMoon;
    bool hasHit;
    RaycastHit2D hit;
    bool showDeadZoneWarning;
    bool isGameOver;
    string currentDeadZoneWarningMessage;
    Coroutine endRoutine;
    Coroutine countdownRoutine;

    LanderController lander => LanderController.Active;

    readonly Dictionary<LanderController.eLanderState, string[]> stateMessages = new()
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

    void Awake() => Instance = this;

    public void Init()
    {
        scoring = ScoringController.Instance;
        landingPad = LandingPadPlacer.Instance;
        imgIndicatorPad = indicatorPad ? indicatorPad.GetComponent<Image>() : null;
        imgIndicatorMoon = indicatorMoon ? indicatorMoon.GetComponent<Image>() : null;

        ShowHideDeadZoneWarning(false);
        SetEndTextsVisible(false);

        btnRestart.gameObject.SetActive(false);
        btnRestart.onClick.RemoveAllListeners();
        btnRestart.onClick.AddListener(OnRestartClicked);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!txtLanderInfos || !txtLanderFuel) return;

        int half = blocks / 2;
        string bar = "";
        for (int i = 0; i < blocks; i++)
            bar += i < half ? fullBlock : emptyBlock;

        txtLanderFuel.text = $"FUEL  {bar}\n";
        txtLanderInfos.text = "SPD   3\nANG   8°\nALT   ---\nSTAT  OK";

        if (!txtGameOverTitle || !txtGameOverMessage || !txtScore) return;

        txtGameOverTitle.text = "LANDED\n\n";
        txtGameOverMessage.text = "Here is a little message for you!";
        txtScore.text =
            "SUCCESS +999\n" +
            "SPEED   +999\n" +
            "ANGLE   +999\n" +
            "CENTER  +999\n" +
            "FUEL    +999\n" +
            "TIME    +999\n" +
            "────────────\n" +
            "SCORE   999\n\nBEST    999\n";

        txtGameOverTitle.gameObject.SetActive(true);
    }
#endif

    void Update()
    {
        if (!lander) return;

        if (!isGameOver && GameController.Instance && GameController.Instance.Phase != GameController.GamePhase.EVA)
        {
            RefreshLanderFuel();
            RefreshLanderInfos();
            ShowRayEditorVisuals();

            if (landingPad) UpdateNav(landingPad.transform, indicatorPad, imgIndicatorPad);
            if (GravityManager2D.Instance) UpdateNav(GravityManager2D.Instance.transform, indicatorMoon, imgIndicatorMoon);
            UpdateIndicatorScales();
        }

        if (!showDeadZoneWarning) return;

        txtGameOverTitle.text = currentDeadZoneWarningMessage;
        txtGameOverMessage.text = Mathf.CeilToInt(lander.deadZoneTimer).ToString();
        if (lander.deadZoneTimer <= 0f)
            ShowHideDeadZoneWarning(false);
    }

    public void ShowHideDeadZoneWarning(bool show)
    {
        showDeadZoneWarning = show;

        if (show)
        {
            currentDeadZoneWarningMessage = GetRandomDeadZoneWarning() + "\n\n";
            txtGameOverTitle.color = Color.red;
            txtGameOverMessage.color = Color.red;
            txtGameOverTitle.gameObject.SetActive(true);
            txtGameOverMessage.gameObject.SetActive(true);
        }
        else
        {
            txtGameOverTitle.color = Color.white;
            txtGameOverMessage.color = Color.white;
            txtGameOverTitle.gameObject.SetActive(false);
            txtGameOverMessage.gameObject.SetActive(false);
        }
    }

    bool TryGetAltitude(out float altitude, out RaycastHit2D result)
    {
        Vector2 gravity = lander.CurrentGravity;
        if (gravity.sqrMagnitude < 0.0001f)
        {
            altitude = 0f;
            result = default;
            return false;
        }

        Vector2 downDir = gravity.normalized;
        Vector2 origin = lander.rb.position;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, downDir, maxCheckDistance);

        foreach (RaycastHit2D candidate in hits)
        {
            if (!candidate.collider) continue;
            if (candidate.collider.attachedRigidbody == lander.rb) continue;
            if (candidate.collider.isTrigger) continue;

            result = candidate;
            altitude = candidate.distance;
            return true;
        }

        altitude = 0f;
        result = default;
        return false;
    }

    void RefreshLanderFuel()
    {
        float fuelT = lander.Fuel01;
        int filled = Mathf.RoundToInt(fuelT * blocks);

        string bar = "";
        for (int i = 0; i < blocks; i++)
            bar += i < filled ? fullBlock : emptyBlock;

        Color fuelColor = Color.white;
        if (fuelT < 0.25f)
        {
            float pulse = 0.5f + Mathf.Sin(Time.time * 4f) * 0.5f;
            fuelColor = Color.Lerp(Color.yellow, Color.red, pulse);
        }

        txtLanderFuel.text = $"FUEL  {bar}\n";
        txtLanderFuel.color = fuelColor;
    }

    void RefreshLanderInfos()
    {
        Vector2 gravity = lander.CurrentGravity;
        bool hasGravity = gravity.sqrMagnitude > 0.001f;
        int speed = Mathf.RoundToInt(lander.currentSpeed);

        string angleStr = "";
        float angle = 0f;
        if (hasGravity)
        {
            angle = GetGravityRelativeAngle(gravity);
            angleStr = $"ANG   {Mathf.RoundToInt(angle)}°\n";
        }

        hasHit = TryGetAltitude(out float altitude, out hit);
        string altitudeText = hasHit ? Mathf.RoundToInt(altitude).ToString() : "---";

        string gravityText = "";
        GravityManager2D gm = GravityManager2D.Instance;
        if (gm && (gm.GetZeroGravityBlendAt(lander.transform.position) > 0.001f || gm.IsMoonGravityActiveAt(lander.transform.position)))
            gravityText = $"GRAV  {gravity.magnitude:F2}";

        string status = "---";
        if (hasGravity && hasHit && altitude <= statusCheckAltitude)
        {
            float currentSpeed = lander.rb.linearVelocity.magnitude;
            bool ok = currentSpeed <= lander.safeSpeed && angle <= lander.safeAngleDeg;
            bool warn = currentSpeed <= lander.safeSpeed * warnMultiplier &&
                        angle <= lander.safeAngleDeg * warnMultiplier;

            status = ok ? "OK" : warn ? "WARN" : "DANGER";
        }

        txtLanderInfos.text =
            $"SPD   {speed}\n" +
            angleStr +
            $"ALT   {altitudeText}\n" +
            (string.IsNullOrEmpty(gravityText) ? "" : gravityText + "\n") +
            $"STAT  {status}";
    }

    float GetGravityRelativeAngle(Vector2 gravity)
    {
        Vector2 up = -gravity.normalized;
        float desired = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f;
        return Mathf.Abs(Mathf.DeltaAngle(desired, lander.rb.rotation));
    }

    void ShowRayEditorVisuals()
    {
        if (!showRay || lander.CurrentGravity.sqrMagnitude < 0.0001f) return;

        Vector2 downDir = lander.CurrentGravity.normalized;
        Vector2 origin = lander.rb.position + downDir * rayOffset;
        Vector2 end = hasHit ? hit.point : origin + downDir * maxCheckDistance;
        Debug.DrawLine(origin, end, Color.green);
    }

    void UpdateNav(Transform navTarget, RectTransform indicator, Image image)
    {
        if (!navTarget || !indicator || !image || !lander) return;

        float dx = navTarget.position.x - lander.transform.position.x;
        float t = Mathf.Clamp(dx / maxXRange, -1f, 1f);
        indicator.anchoredPosition = new Vector2(t * maxOffset, indicator.anchoredPosition.y);

        float alpha = Mathf.InverseLerp(0f, fadeDistance, Mathf.Abs(dx));
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    void UpdateIndicatorScales()
    {
        if (!indicatorPad || !indicatorMoon || !GravityManager2D.Instance) return;

        bool inSpace = GravityManager2D.Instance.GetZeroGravityBlendAt(lander.transform.position) >= 0.99f &&
                       !GravityManager2D.Instance.IsMoonGravityActiveAt(lander.transform.position);

        Vector3 padTarget = inSpace ? indicatorSmallScale : indicatorNormalScale;
        Vector3 moonTarget = inSpace ? indicatorNormalScale : indicatorSmallScale;
        float k = 1f - Mathf.Exp(-indicatorScaleSmooth * Time.deltaTime);

        indicatorPad.localScale = Vector3.Lerp(indicatorPad.localScale, padTarget, k);
        indicatorMoon.localScale = Vector3.Lerp(indicatorMoon.localScale, moonTarget, k);
    }

    public void ShowGameOver(LanderController.eLanderState state, bool isMoonLanded = false)
    {
        isGameOver = true;

        if (endRoutine != null) StopCoroutine(endRoutine);
        endRoutine = StartCoroutine(ShowEndRoutine(state, isMoonLanded));
    }

    IEnumerator ShowEndRoutine(LanderController.eLanderState state, bool isMoon)
    {
        yield return new WaitForSeconds(1.5f);
        endRoutine = null;

        if (!isGameOver) yield break;

        ShowHideDeadZoneWarning(false);
        SetGameplayHudVisible(false);
        SetEndTextsVisible(false);

        txtXPScore.gameObject.SetActive(true);
        txtXPScore.text = "XP-SCORE " + scoring.CollectedScore;
        txtGameOverMessage.text = GetRandomGameOverMessage(state);

        btnRestart.onClick.RemoveAllListeners();
        btnRestart.gameObject.SetActive(true);

        LanderChooserManager.Instance.btnLanderChooser.gameObject.SetActive(true);
        LanderChooserManager.Instance.panelChooser.gameObject.SetActive(false);

        SetPanelTopCenter();

        if (isMoon || state == LanderController.eLanderState.LandedPad)
        {
            txtGameOverTitle.gameObject.SetActive(true);
            txtGameOverMessage.gameObject.SetActive(true);
            txtScore.gameObject.SetActive(true);

            if (isMoon)
            {
                txtScore.text =
                    $"SUCCESS +{scoring.baseLandingScore}\n" +
                    $"SPEED   +{scoring.LastSpeedScore}\n" +
                    $"FUEL    +{scoring.LastFuelScore}\n" +
                    $"TIME    +{scoring.LastTimeScore}\n" +
                    $"★MOON★  +{scoring.LastMoonScore}\n" +
                    "────────────\n" +
                    $"SCORE   {scoring.LastScore}\n\nBEST    {scoring.BestScore}\n";

                bool canShowChooser = LanderChooserManager.Instance.HasAnyUnlockedSecret();
                LanderChooserManager.Instance.btnLanderChooser.gameObject.SetActive(canShowChooser);
                LanderChooserManager.Instance.panelChooser.gameObject.SetActive(false);
            }
            else
            {
                txtScore.text =
                    $"SUCCESS +{scoring.baseLandingScore}\n" +
                    $"SPEED   +{scoring.LastSpeedScore}\n" +
                    $"ANGLE   +{scoring.LastAngleScore}\n" +
                    $"CENTER  +{scoring.LastCenterScore}\n" +
                    $"FUEL    +{scoring.LastFuelScore}\n" +
                    $"TIME    +{scoring.LastTimeScore}\n" +
                    "────────────\n" +
                    $"SCORE   {scoring.LastScore}\n\nBEST    {scoring.BestScore}\n";
            }

            SetRestartLabel("Next Level");
            btnRestart.onClick.AddListener(OnNextClicked);
        }
        else
        {
            SetPanelCenter();
            txtGameOverTitle.gameObject.SetActive(true);
            txtGameOverMessage.gameObject.SetActive(true);

            SetRestartLabel("Try Again");
            btnRestart.onClick.AddListener(OnRestartClicked);
        }

        RefreshPanel();
    }

    void SetRestartLabel(string label)
    {
        TMP_Text text = btnRestart.GetComponentInChildren<TMP_Text>();
        if (text) text.text = label;
    }

    void SetEndTextsVisible(bool visible)
    {
        txtGameOverTitle.gameObject.SetActive(visible);
        txtGameOverMessage.gameObject.SetActive(visible);
        txtScore.gameObject.SetActive(visible);
        txtXPScore.gameObject.SetActive(visible);
    }

    void SetGameplayHudVisible(bool visible)
    {
        txtLanderFuel.gameObject.SetActive(visible);
        txtLanderInfos.gameObject.SetActive(visible);
        navigationGrp.gameObject.SetActive(visible);
    }

    public void SetPanelTopCenter()
    {
        SetPanelAnchor(new Vector2(0.5f, 1f), new Vector2(0f, -300f));
    }

    public void SetPanelCenter()
    {
        SetPanelAnchor(new Vector2(0.5f, 0.5f), Vector2.zero);
    }

    public void SetPanelBottomCenter()
    {
        SetPanelAnchor(new Vector2(0.5f, 0f), new Vector2(0f, 300f));
    }

    void SetPanelAnchor(Vector2 anchor, Vector2 position)
    {
        RectTransform rect = panelGrp ? panelGrp.GetComponent<RectTransform>() : null;
        if (!rect) return;

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
    }

    string GetRandomGameOverMessage(LanderController.eLanderState state)
    {
        if (!stateMessages.TryGetValue(state, out string[] messages) || messages.Length == 0) return "";

        string message = messages[Random.Range(0, messages.Length)];
        if (message.Contains("{TargetDistance}") && landingPad && lander)
        {
            int distance = Mathf.RoundToInt(Vector2.Distance(lander.transform.position, landingPad.transform.position));
            message = message.Replace("{TargetDistance}", distance.ToString());
        }

        return message;
    }

    string GetRandomDeadZoneWarning()
        => deadZoneWarnings[Random.Range(0, deadZoneWarnings.Length)];

    public void HideGameOver()
    {
        isGameOver = false;

        if (endRoutine != null)
        {
            StopCoroutine(endRoutine);
            endRoutine = null;
        }

        bool showHud = !GameController.Instance || GameController.Instance.Phase != GameController.GamePhase.EVA;
        SetGameplayHudVisible(showHud);
        SetEndTextsVisible(false);

        LanderChooserManager.Instance.btnLanderChooser.gameObject.SetActive(false);
        LanderChooserManager.Instance.panelChooser.gameObject.SetActive(false);
        btnRestart.gameObject.SetActive(false);
    }

    void OnRestartClicked()
    {
        HideGameOver();
        lander.ResetLander();
        lander.rb.bodyType = RigidbodyType2D.Static;
        StartCountdown();
    }

    void OnNextClicked()
    {
        HideGameOver();
        GameController.Instance.NextLevel();
        GameController.Instance.StartLandingRun();
    }

    public void StartCountdown()
    {
        if (countdownRoutine != null) StopCoroutine(countdownRoutine);
        countdownRoutine = StartCoroutine(StartCountdownRoutine());
    }

    IEnumerator StartCountdownRoutine()
    {
        SetGameplayHudVisible(false);

        yield return new WaitForSeconds(2f);
        txtGameTitle.gameObject.SetActive(false);

        txtGameOverTitle.gameObject.SetActive(true);
        txtGameOverMessage.gameObject.SetActive(true);
        RefreshPanel();

        txtGameOverMessage.text = "Start in...";
        txtGameOverTitle.text = "LVL " + GameController.Instance.level + "\n\n ";

        yield return new WaitForSeconds(1.5f);

        for (int i = Mathf.Max(0, startCountdown); i > 0; i--)
        {
            AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdown, 1f, 1f, false);
            txtGameOverMessage.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }

        txtGameOverMessage.gameObject.SetActive(false);
        SetGameplayHudVisible(true);

        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdownStart, 1f, 1f, false);
        txtGameOverTitle.text = "Land!";
        lander.StartLander();

        yield return new WaitForSeconds(1f);
        txtGameOverTitle.gameObject.SetActive(false);
        countdownRoutine = null;
    }

    public void RefreshPanel()
    {
        StartCoroutine(DelayedLayoutRebuild());
    }

    IEnumerator DelayedLayoutRebuild()
    {
        yield return null;

        RectTransform layoutRoot = panelGrp?.GetComponentInChildren<VerticalLayoutGroup>()?.transform as RectTransform;
        if (layoutRoot)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
    }

    public bool IsPointerOverUI()
    {
        if (!EventSystem.current) return false;

        Vector2 screenPos = Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        PointerEventData pointer = new(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(pointer, results);
        return results.Count > 0;
    }

    void OnDrawGizmos()
    {
        if (!showRay || !lander || !lander.rb) return;

        Vector2 gravity = lander.CurrentGravity;
        if (gravity.sqrMagnitude < 0.0001f) return;

        Vector2 downDir = gravity.normalized;
        Vector2 origin = lander.rb.position + downDir * rayOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(origin, gizmoRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + downDir * maxCheckDistance);
    }
}
