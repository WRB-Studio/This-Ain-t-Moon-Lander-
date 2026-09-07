using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoonEVAController : MonoBehaviour
{
    public static MoonEVAController Instance;

    [Header("Refs")]
    public GameObject astronautPrefab;
    public Transform astronautParent;

    [Header("UI")]
    public Button btnExit;
    [SerializeField] float exitStableDelay = 0.4f;

    [HideInInspector] public bool isOnMoonLanded;
    [HideInInspector] public GameObject astronaut;

    LanderController enterTarget;
    float exitStableTimer;

    void Awake() => Instance = this;

    public void Init()
    {
        HideAction();
        exitStableTimer = 0f;
    }

    void Update()
    {
        if (astronaut) return;

        LanderController lander = LanderController.Active;
        if (!lander || lander.landerState != LanderController.eLanderState.LandedMoon ||
            !lander.IsMoonContact || lander.IsThrusting)
        {
            exitStableTimer = 0f;
            HideAction();
            return;
        }

        exitStableTimer += Time.deltaTime;
        if (exitStableTimer >= exitStableDelay)
            ShowExitAction();
    }

    public void ResetRunState()
    {
        if (astronaut)
            Destroy(astronaut);

        astronaut = null;
        enterTarget = null;
        isOnMoonLanded = false;
        exitStableTimer = 0f;
        HideAction();
    }

    public void ExitLander()
    {
        LanderController lander = LanderController.Active;
        if (!lander || lander.landerState != LanderController.eLanderState.LandedMoon) return;

        Renderer renderer = lander.GetComponentInChildren<Renderer>();
        float halfWidth = renderer ? renderer.bounds.extents.x : 0.5f;
        float side = Random.value < 0.5f ? -1f : 1f;
        float extra = 0.5f;

        Vector3 spawnPos = lander.transform.position +
                           lander.transform.right * side * (halfWidth * 1.5f + extra);

        astronaut = Instantiate(astronautPrefab, spawnPos, Quaternion.identity, astronautParent);

        lander.controlsEnabled = false;
        lander.rb.bodyType = RigidbodyType2D.Static;

        Collider2D collider = lander.GetComponent<Collider2D>();
        if (collider) collider.isTrigger = true;

        HideAction();
        LanderUI.Instance.HideGameOver();
        GameController.Instance.SetControlledTarget(astronaut.transform, GameController.GamePhase.EVA, true);
    }

    public void EnterLander(LanderController newLander)
    {
        if (!astronaut || !newLander) return;

        Destroy(astronaut);
        astronaut = null;
        enterTarget = null;

        if (newLander != LanderController.Active)
        {
            LanderController.ChangeLander(newLander);
        }
        else
        {
            newLander.controlsEnabled = true;
            newLander.rb.bodyType = RigidbodyType2D.Dynamic;

            Collider2D collider = newLander.GetComponent<Collider2D>();
            if (collider) collider.isTrigger = false;
        }

        HideAction();
        exitStableTimer = 0f;
        GameController.Instance.SetControlledTarget(LanderController.Active.transform, GameController.GamePhase.SpaceFlight, true);

        if (newLander.isSecretLander && !LanderChooserManager.Instance.IsSecretFound(newLander.landerIndex))
        {
            LanderChooserManager.Instance.UnlockSecret(newLander.landerIndex);

            if (LanderController.Active.landerState == LanderController.eLanderState.LandedMoon)
            {
                StoryTextController.Instance.Show("Guess the moon landing was real after all.");
                StoryTextController.Instance.Show("Okay… maybe this is a Moon Lander game. 😄");
            }
        }
    }

    public void ShowEnterLander(LanderController target)
    {
        if (!astronaut || !target || !btnExit) return;

        enterTarget = target;
        TMP_Text text = btnExit.GetComponentInChildren<TMP_Text>();
        if (text) text.text = "Enter Lander";

        btnExit.onClick.RemoveAllListeners();
        btnExit.onClick.AddListener(() => EnterLander(target));
        btnExit.gameObject.SetActive(true);
        LanderUI.Instance.SetPanelBottomCenter();
    }

    public void HideEnterLander(LanderController target)
    {
        if (enterTarget != target) return;

        enterTarget = null;
        HideAction();
    }

    void ShowExitAction()
    {
        if (!btnExit || btnExit.gameObject.activeSelf && enterTarget == null) return;

        enterTarget = null;
        TMP_Text text = btnExit.GetComponentInChildren<TMP_Text>();
        if (text) text.text = "Exit Lander";

        btnExit.onClick.RemoveAllListeners();
        btnExit.onClick.AddListener(ExitLander);
        btnExit.gameObject.SetActive(true);
    }

    public void HideAction()
    {
        if (!btnExit) return;

        btnExit.onClick.RemoveAllListeners();
        btnExit.gameObject.SetActive(false);
    }
}
