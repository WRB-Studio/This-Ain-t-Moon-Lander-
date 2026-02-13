using System.Collections;
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
        NearToMoon,
    }

    [Header("UI")]
    [SerializeField] TMP_Text txtInfo;

    [Header("Timing")]
    [SerializeField] float visibleTime = 1.5f;

    [Tooltip("How many seconds must pass after run start before triggers can fire (avoid countdown spam).")]
    [SerializeField] float triggerDelayFromRunStart = 0.5f;

    [Tooltip("Cooldown per story type to avoid spamming the queue.")]
    [SerializeField] float typeCooldown = 2.0f;

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

    LanderController lander => LanderController.Instance;
    GravityManager2D gravityManager;

    // --- queue ---
    readonly Queue<string> queue = new Queue<string>();
    Coroutine runner;
    string lastQueued;

    // --- anti spam per type ---
    readonly Dictionary<eStoryTextType, float> nextAllowed = new Dictionary<eStoryTextType, float>();

    void Awake()
    {
        Instance = this;
        if (txtInfo) txtInfo.gameObject.SetActive(false);
    }

    public void Init()
    {
        gravityManager = GravityManager2D.Instance;
    }

    /// <summary>Call this on "GO" / run start / next level.</summary>
    public void Restart()
    {
        if (txtInfo) txtInfo.gameObject.SetActive(false);

        shownAtmosphereExit = false;
        shownBackToPlanet = false;
        shownNearMoon = false;

        runStartTime = Time.time;

        queue.Clear();
        lastQueued = null;
        nextAllowed.Clear();

        if (runner != null) StopCoroutine(runner);
        runner = null;
    }

    void Update()
    {
        if (Time.time - runStartTime < triggerDelayFromRunStart) return;
        if (!lander || !gravityManager) return;

        // --- 1) AtmosphereExit / BackToPlanet ---
        if (!shownAtmosphereExit && lander.transform.position.y > gravityManager.zeroGFullY)
        {
            shownBackToPlanet = false;
            shownAtmosphereExit = true;
            Enqueue(eStoryTextType.AtmosphereExit);
        }

        if (shownAtmosphereExit && !shownBackToPlanet && lander.transform.position.y < gravityManager.zeroGFullY)
        {
            shownAtmosphereExit = false;
            shownBackToPlanet = true;
            Enqueue(eStoryTextType.BackToPlanet);
        }

        // --- 2) NearToMoon ---
        float distanceToMoon = Vector2.Distance(lander.transform.position, gravityManager.transform.position);
        if (!shownNearMoon && distanceToMoon <= gravityManager.moonFullRadius)
        {
            shownNearMoon = true;
            Enqueue(eStoryTextType.NearToMoon);
        }
        else if (distanceToMoon > gravityManager.moonFullRadius)
        {
            shownNearMoon = false;
        }
    }

    // --- Public API ---
    public void Show(string msg) => Enqueue(msg);
    public void Show(eStoryTextType type) => Enqueue(type);

    // --- Queue entrypoints ---
    void Enqueue(eStoryTextType type)
    {
        // cooldown per type
        float allowedAt = 0f;
        nextAllowed.TryGetValue(type, out allowedAt);
        if (Time.time < allowedAt) return;

        nextAllowed[type] = Time.time + typeCooldown;

        Enqueue(GetRandomMessage(type));
    }

    void Enqueue(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;

        // optional: same message not twice in a row
        if (msg == lastQueued) return;

        queue.Enqueue(msg);
        lastQueued = msg;

        if (runner == null)
            runner = StartCoroutine(RunQueue());
    }

    IEnumerator RunQueue()
    {
        while (queue.Count > 0)
        {
            string msg = queue.Dequeue();

            if (!txtInfo) yield break;

            txtInfo.text = msg;
            txtInfo.gameObject.SetActive(true);

            yield return new WaitForSeconds(visibleTime);

            if (txtInfo) txtInfo.gameObject.SetActive(false);

            // tiny gap (optional)
            // yield return new WaitForSeconds(0.05f);
        }

        runner = null;
        lastQueued = null;
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
}
