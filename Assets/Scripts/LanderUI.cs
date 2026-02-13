using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
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
    private LandingPadPlacer landingPad;
    private LanderController lander => LanderController.Instance;
    private bool hasHit;
    private RaycastHit2D hit;
    private bool showDeadZoneWarning;

    private bool isGameOver;

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
        scoring = ScoringController.Instance;
        landingPad = LandingPadPlacer.Instance;
        imgIndicatorPad = indicatorPad.GetComponent<Image>();
        imgIndicatorMoon = indicatorMoon.GetComponent<Image>();

        ShowHideDeadZoneWarning(false);
        txtGameOverTitle.gameObject.SetActive(false);
        txtScore.gameObject.SetActive(false);
        txtXPScore.gameObject.SetActive(false);

        btnRestart.gameObject.SetActive(false);
        btnRestart.onClick.AddListener(OnRestartClicked);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!txtLanderInfos) return;

        //show case in editor mode

        int half = blocks / 2;
        string bar = "";

        for (int i = 0; i < blocks; i++)
            bar += i < half ? fullBlock : emptyBlock;

        txtLanderFuel.text = $"FUEL  {bar}\n";

        txtLanderInfos.text =
                    $"SPD   3\n" +
                    $"ANG   8°\n" +
                    $"ALT   ---\n" +
                    $"STAT  OK";

        if (txtGameOverTitle && txtGameOverMessage && txtScore)
        {
            txtGameOverTitle.text =
                "LANDED\n\n";

            txtGameOverMessage.text =
                "Here is a little message for you!";

            txtScore.text =
                $"SUCCESS +999\n" +
                $"SPEED   +999\n" +
                $"ANGLE   +999\n" +
                $"CENTER  +999\n" +
                $"FUEL    +999\n" +
                $"TIME    +999\n" +
                "────────────\n" +
                $"SCORE   999\n" +
                $"\nBEST    999\n";

            txtGameOverTitle.gameObject.SetActive(true);
        }
    }
#endif

    void Update()
    {
        RefreshLanderFuel();
        RefreshLanderInfos();
        ShowRayEditorVisuals();
        UpdateNav(landingPad.transform, indicatorPad, imgIndicatorPad);
        UpdateNav(GravityManager2D.Instance.transform, indicatorMoon, imgIndicatorMoon);
        UpdateIndicatorScales();

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
        Vector2 g = Physics2D.gravity;
        if (g.sqrMagnitude < 0.0001f)
        {
            altitude = 0f;
            hit = default;
            return false;
        }

        Vector2 downDir = g.normalized;
        Vector2 origin = lander.rb.position; // exakt Schiffsmitte

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, downDir, maxCheckDistance);

        foreach (var h in hits)
        {
            if (!h.collider) continue;

            // Eigenes Schiff ignorieren
            if (h.collider.attachedRigidbody == lander.rb) continue;
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
        if (isGameOver) return;

        float fuelT = lander.currentFuel / lander.fuelMax;
        int filled = Mathf.RoundToInt(fuelT * blocks);

        string bar = "";
        for (int i = 0; i < blocks; i++)
            bar += i < filled ? fullBlock : emptyBlock;

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
        txtLanderFuel.text = $"FUEL  {bar}\n";
        txtLanderFuel.color = fuelColor;
    }

    private void RefreshLanderInfos()
    {
        if (isGameOver) return;

        // Speed
        int speedI = Mathf.RoundToInt(lander.currentSpeed);

        // Gravity state
        Vector2 g = Physics2D.gravity;
        bool hasGravity = g.sqrMagnitude > 0.001f;

        // Angle (nur sinnvoll mit Gravity)
        string angleStr = hasGravity
            ? $"ANG   {Mathf.RoundToInt(Mathf.Abs(Mathf.DeltaAngle(0f, lander.rb.rotation)))}°\n"
            : "";

        // Altitude
        hasHit = TryGetAltitude(out float altitude, out hit);
        string altText = hasHit ? Mathf.RoundToInt(altitude).ToString() : "---";

        // Grav display (optional nur wenn nicht baseGravity / oder nur wenn !hasGravity etc.)
        string gravStr = "";
        if (lander.transform.position.y > GravityManager2D.Instance.zeroGStartY)
        {
            // optional: magnitude statt nur y, weil Mondgravity nicht "y" ist
            gravStr = $"GRAV  {g.magnitude:F2}";
        }

        // Status (nur wenn wir wirklich Boden "unten" haben)
        string status = "---";
        if (hasGravity && hasHit && altitude <= statusCheckAltitude)
        {
            float speed = lander.rb.linearVelocity.magnitude;
            float angle = Mathf.Abs(Mathf.DeltaAngle(0f, lander.rb.rotation));

            bool ok = speed <= lander.safeSpeed && angle <= lander.safeAngleDeg;
            bool warn = speed <= lander.safeSpeed * warnMultiplier &&
                        angle <= lander.safeAngleDeg * warnMultiplier;

            status = ok ? "OK" : (warn ? "WARN" : "DANGER");
        }

        // Final text
        txtLanderInfos.text =
            $"SPD   {speedI}\n" +
            angleStr +
            $"ALT   {altText}\n" +
            gravStr;
    }


    private void ShowRayEditorVisuals()
    {
        if (showRay)
        {
            Vector2 origin = lander.rb.position + Vector2.down * rayOffset;
            Vector2 end = hasHit ? hit.point : (origin + Vector2.down * maxCheckDistance);
            Debug.DrawLine(origin, end, Color.green);
        }
    }

    void UpdateNav(Transform target, RectTransform indicator, Image imgIndicator)
    {
        if (!target || !lander) return;

        float dx = target.position.x - lander.transform.position.x;

        // Position
        float t = Mathf.Clamp(dx / maxXRange, -1f, 1f);
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
        float y = lander.transform.position.y;
        bool inSpace = y >= GravityManager2D.Instance.zeroGFullY;

        Vector3 padTarget = inSpace ? indicatorSmallScale : indicatorNormalScale;
        Vector3 moonTarget = inSpace ? indicatorNormalScale : indicatorSmallScale;

        float k = 1f - Mathf.Exp(-indicatorScaleSmooth * Time.deltaTime);

        indicatorPad.localScale = Vector3.Lerp(indicatorPad.localScale, padTarget, k);
        indicatorMoon.localScale = Vector3.Lerp(indicatorMoon.localScale, moonTarget, k);
    }

    public void ShowGameOver(LanderController.eLanderState state, bool isMoonLanded = false)
    {
        isGameOver = true;
        StartCoroutine(ShowEndRoutine(state, isMoonLanded));
    }

    private IEnumerator ShowEndRoutine(LanderController.eLanderState state, bool isMoon)
    {
        yield return new WaitForSeconds(1.5f);

        ShowHideDeadZoneWarning(false);

        txtLanderFuel.gameObject.SetActive(false);
        txtLanderInfos.gameObject.SetActive(false);
        navigationGrp.gameObject.SetActive(false);

        txtGameOverTitle.gameObject.SetActive(false);
        txtGameOverMessage.gameObject.SetActive(false);
        txtScore.gameObject.SetActive(false);

        txtXPScore.gameObject.SetActive(true);
        txtXPScore.text = "XP-SCORE " + ScoringController.Instance.CollectedScore;

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
                    $"SCORE   {scoring.LastScore}\n" +
                    $"\nBEST    {scoring.BestScore}\n";

                bool canShowChooserOnMoon = LanderChooserManager.Instance.IsSecretFound(5);
                LanderChooserManager.Instance.btnLanderChooser.gameObject.SetActive(canShowChooserOnMoon);
                LanderChooserManager.Instance.panelChooser.gameObject.SetActive(canShowChooserOnMoon);

                MoonEVAController.Instance.btnExit.gameObject.SetActive(true);
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
                    $"SCORE   {scoring.LastScore}\n" +
                    $"\nBEST    {scoring.BestScore}\n";
            }

            btnRestart.transform.GetChild(0).GetComponent<TMP_Text>().text = "Next Level";
            btnRestart.onClick.AddListener(OnNextClicked);
        }
        else
        {
            SetPanelCenter();

            txtGameOverTitle.gameObject.SetActive(true);
            txtGameOverMessage.gameObject.SetActive(true);

            btnRestart.transform.GetChild(0).GetComponent<TMP_Text>().text = "Try Again";
            btnRestart.onClick.AddListener(OnRestartClicked);
        }

        RefreshPanel();
    }

    public void SetPanelTopCenter()
    {
        RectTransform rt = panelGrp.GetComponent<RectTransform>();
        if (!rt) return;

        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);

        rt.anchoredPosition = new Vector2(0f, -300f);
    }

    public void SetPanelCenter()
    {
        RectTransform rt = panelGrp.GetComponent<RectTransform>();
        if (!rt) return;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.anchoredPosition = Vector2.zero;
    }

    public void SetPanelBottomCenter()
    {
        RectTransform rt = panelGrp.GetComponent<RectTransform>();
        if (!rt) return;

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);

        rt.anchoredPosition = new Vector2(0f, 300f);
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
        isGameOver = false;

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
    }

    void OnRestartClicked()
    {
        HideGameOver();
        lander.ResetLander();
        lander.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        StartCoroutine(StartCountdownRoutine());
    }

    void OnNextClicked()
    {
        HideGameOver();
        GameController.Instance.NextLevel();
        GameController.Instance.StartGame();
    }


    public void StartCountdown()
    {
        StartCoroutine(StartCountdownRoutine());
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

        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdown, 1f, 1f, false);
        txtGameOverMessage.text = "3";
        yield return new WaitForSeconds(1f);

        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdown, 1f, 1f, false);
        txtGameOverMessage.text = "2";
        yield return new WaitForSeconds(1f);

        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdown, 1f, 1f, false);
        txtGameOverMessage.text = "1";
        yield return new WaitForSeconds(1f);

        txtGameOverMessage.gameObject.SetActive(false);

        txtLanderFuel.gameObject.SetActive(true);
        txtLanderInfos.gameObject.SetActive(true);
        navigationGrp.gameObject.SetActive(true);

        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdownStart, 1f, 1f, false);
        txtGameOverTitle.text = "Land!";
        lander.StartLander();
        yield return new WaitForSeconds(1f);

        txtGameOverTitle.gameObject.SetActive(false);

    }


    public void RefreshPanel()
    {
        StartCoroutine(DelayedLayoutRebuild());
    }

    private IEnumerator DelayedLayoutRebuild()
    {
        yield return null;

        var layoutRoot = panelGrp.GetComponentInChildren<VerticalLayoutGroup>()?.transform as RectTransform;
        if (layoutRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
    }


    public bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        Vector2 screenPos;

        if (Input.touchCount > 0)
            screenPos = Input.GetTouch(0).position;
        else
            screenPos = Input.mousePosition;

        var ped = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        return results.Count > 0;
    }

    void OnDrawGizmos()
    {
        if (!showRay || !lander?.rb) return;

        //Draw ray in direction of gravity to visualize altitude check

        Vector2 g = Physics2D.gravity;
        if (g.sqrMagnitude < 0.0001f) return; // ZeroG → kein "unten"

        Vector2 downDir = g.normalized;

        Vector2 origin = lander.rb.position + downDir * rayOffset;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(origin, gizmoRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + downDir * maxCheckDistance);
    }

}
