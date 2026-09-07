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
        NearToMoon
    }

    [Header("UI")]
    [SerializeField] TMP_Text txtInfo;

    [Header("Timing")]
    [SerializeField] float visibleTime = 1.5f;
    [SerializeField] float triggerDelayFromRunStart = 0.5f;
    [SerializeField] float typeCooldown = 2f;

    readonly Dictionary<eStoryTextType, string[]> stateMessages = new()
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

    readonly Queue<string> queue = new();
    readonly Dictionary<eStoryTextType, float> nextAllowed = new();

    GravityManager2D gravityManager;
    Coroutine runner;
    string lastQueued;
    float runStartTime;
    bool shownAtmosphereExit;
    bool shownBackToPlanet;
    bool shownNearMoon;

    LanderController lander => LanderController.Active;

    void Awake()
    {
        Instance = this;
        if (txtInfo) txtInfo.gameObject.SetActive(false);
    }

    public void Init() => gravityManager = GravityManager2D.Instance;

    public void Restart()
    {
        shownAtmosphereExit = false;
        shownBackToPlanet = false;
        shownNearMoon = false;
        runStartTime = Time.time;

        queue.Clear();
        nextAllowed.Clear();
        lastQueued = null;

        if (runner != null) StopCoroutine(runner);
        runner = null;

        if (txtInfo) txtInfo.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Time.time - runStartTime < triggerDelayFromRunStart) return;
        if (!lander || !gravityManager) return;

        float zeroT = gravityManager.GetZeroGravityBlendAt(lander.transform.position);
        if (!shownAtmosphereExit && zeroT >= 0.999f)
        {
            shownAtmosphereExit = true;
            shownBackToPlanet = false;
            Enqueue(eStoryTextType.AtmosphereExit);
        }
        else if (shownAtmosphereExit && !shownBackToPlanet && zeroT < 0.999f)
        {
            shownAtmosphereExit = false;
            shownBackToPlanet = true;
            Enqueue(eStoryTextType.BackToPlanet);
        }

        float moonT = gravityManager.GetMoonBlendAt(lander.transform.position);
        if (!shownNearMoon && moonT >= 0.999f)
        {
            shownNearMoon = true;
            Enqueue(eStoryTextType.NearToMoon);
        }
        else if (moonT < 0.999f)
        {
            shownNearMoon = false;
        }
    }

    public void Show(string message) => Enqueue(message);
    public void Show(eStoryTextType type) => Enqueue(type);

    void Enqueue(eStoryTextType type)
    {
        nextAllowed.TryGetValue(type, out float allowedAt);
        if (Time.time < allowedAt) return;

        nextAllowed[type] = Time.time + typeCooldown;
        Enqueue(GetRandomMessage(type));
    }

    void Enqueue(string message)
    {
        if (string.IsNullOrEmpty(message) || message == lastQueued) return;

        queue.Enqueue(message);
        lastQueued = message;

        if (runner == null)
            runner = StartCoroutine(RunQueue());
    }

    IEnumerator RunQueue()
    {
        while (queue.Count > 0)
        {
            if (!txtInfo) yield break;

            txtInfo.text = queue.Dequeue();
            txtInfo.gameObject.SetActive(true);
            yield return new WaitForSeconds(visibleTime);
            txtInfo.gameObject.SetActive(false);
        }

        runner = null;
        lastQueued = null;
    }

    string GetRandomMessage(eStoryTextType type)
    {
        if (!stateMessages.TryGetValue(type, out string[] messages) || messages == null || messages.Length == 0)
            return "";

        string message = messages[Random.Range(0, messages.Length)];
        if (message.Contains("{TargetDistance}") && LandingPadPlacer.Instance)
        {
            int distance = Mathf.RoundToInt(Vector2.Distance(
                lander.transform.position,
                LandingPadPlacer.Instance.transform.position));

            message = message.Replace("{TargetDistance}", distance.ToString());
        }

        return message;
    }
}
