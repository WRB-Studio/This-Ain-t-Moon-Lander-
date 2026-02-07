using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class StoryTextController : MonoBehaviour
{
    public static StoryTextController Instance;

    public enum eStoryTextType
    {
        AtmosphereExit,
        BackToPlanet,
        NearToMoon
    }

    [Header("UI")]
    [SerializeField] TMP_Text txtInfo;

    [Header("Timing")]
    [SerializeField] float visibleTime = 1.5f;

    [Tooltip("How many seconds must pass after run start before triggers can fire (avoid countdown spam).")]
    [SerializeField] float triggerDelayFromRunStart = 0.5f;

    readonly Dictionary<eStoryTextType, string[]> stateMessages =
        new Dictionary<eStoryTextType, string[]>
        {
            {
                eStoryTextType.AtmosphereExit, new[]
                {
                    "This is not a space game.",
                    "Your target is below, not above.",
                    "Where do you think you're going?",
                    "Wrong direction.",
                    "Gravity exists for a reason in this game.",
                    "That's not the objective.",
                    "Up there is not the goal.",
                    "This wasn't the plan.",
                    "You’re leaving the mission area.",
                    "The landing pad is not in space."
                }
            },
            {
                eStoryTextType.BackToPlanet, new[]
                {
                    "I knew you'd come back.",
                    "Good. Focus restored.",
                    "Back to the actual mission.",
                    "That makes more sense.",
                    "Gravity feels familiar, right?",
                    "Course correction accepted.",
                    "Much better.",
                    "The pad missed you.",
                    "Mission back on track.",
                    "Let’s do this properly."
                }
            },
            {
                eStoryTextType.NearToMoon, new[]
                {
                    "This is not a lunar lander game.",
                    "The moon was not the objective.",
                    "The moon was never part of the plan.",
                    "The moon is the wrong celestial body.",
                    "The landing pad is {TargetDistance} units away.",
                    "The moon wasn't briefed.",
                    "The mission does not include the moon."
                }
            }
        };

    // --- run flags ---
    bool shownAtmosphereExit;
    bool shownBackToPlanet;
    bool shownNearMoon;

    float runStartTime;

    private LanderController lander;
    private GravityManager2D gravityManager;

    void Awake()
    {
        Instance = this;
        txtInfo.gameObject.SetActive(false);
    }

    public void Init()
    {
        lander = LanderController.Instance;
        gravityManager = GravityManager2D.Instance;
    }

    /// <summary>Call this on "GO" / run start / next level.</summary>
    public void Restart()
    {
        txtInfo.gameObject.SetActive(false);

        shownAtmosphereExit = false;
        shownBackToPlanet = false;
        shownNearMoon = false;

        runStartTime = Time.time;
    }

    void Update()
    {
        // wait a bit after run start
        if (Time.time - runStartTime < triggerDelayFromRunStart) return;




        // --- 1) AtmosphereExit / BackToPlanet ---
        if (!shownAtmosphereExit && lander.transform.position.y > gravityManager.zeroGFullY)
        {
            shownBackToPlanet = false;
            shownAtmosphereExit = true;
            Show(eStoryTextType.AtmosphereExit);
        }

        if (shownAtmosphereExit && !shownBackToPlanet && lander.transform.position.y < gravityManager.zeroGFullY)
        {
            shownAtmosphereExit = false;
            shownBackToPlanet = true;
            Show(eStoryTextType.BackToPlanet);
        }

        // --- 2) NearToMoon via distance to Moon transform (if available) ---
        // Assumption: GravityManager2D has a public Transform moon (or similar). Adjust line below if your field name differs.
        float distanceToMoon = Vector2.Distance(lander.transform.position, gravityManager.transform.position);
        if (!shownNearMoon && distanceToMoon <= gravityManager.moonFullRadius)
        {
            shownNearMoon = true;
            Show(eStoryTextType.NearToMoon);
        }
        else if (distanceToMoon > gravityManager.moonFullRadius)
        {
            shownNearMoon = false;
        }
    }

    public void Show(eStoryTextType type)
    {
        if (!txtInfo) return;

        string msg = GetRandomMessage(type);
        if (string.IsNullOrEmpty(msg)) return;

        StopAllCoroutines();
        txtInfo.text = msg;
        txtInfo.gameObject.SetActive(true);
        StartCoroutine(HideAfterDelay());
    }

    string GetRandomMessage(eStoryTextType type)
    {
        if (!stateMessages.TryGetValue(type, out var arr) || arr == null || arr.Length == 0)
            return "";

        var msg = arr[Random.Range(0, arr.Length)];

        if (msg.Contains("{TargetDistance}"))
        {
            int distI = Mathf.RoundToInt(Vector2.Distance(
                lander.transform.position,
                LandingPadPlacer.Instance.transform.position));

            msg = msg.Replace("{TargetDistance}", distI.ToString());
        }

        return msg;
    }

    System.Collections.IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(visibleTime);
        if (txtInfo) txtInfo.gameObject.SetActive(false);
    }
}
