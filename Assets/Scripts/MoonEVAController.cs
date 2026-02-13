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

    [HideInInspector] public bool isOnMoonLanded = false;
    LanderController lander;
    [HideInInspector] public GameObject astronaut;

    void Awake() => Instance = this;

    public void Init()
    {
        lander = LanderController.Instance;

        btnExit.onClick.RemoveAllListeners();
        btnExit.onClick.AddListener(ExitLander);
        btnExit.gameObject.SetActive(false);
    }

    public void ExitLander()
    {
        if (lander.landerState != LanderController.eLanderState.LandedMoon) return;

        // Renderer Breite vom Lander holen
        var rend = lander.GetComponentInChildren<Renderer>();
        float halfWidth = rend.bounds.extents.x;

        // Zufällig links oder rechts
        float side = Random.value < 0.5f ? -1f : 1f;

        // Kleine Extra-Luft
        float extra = 0.5f;

        // Spawnposition relativ zum Lander
        Vector3 spawnPos = lander.transform.position +
                           lander.transform.right * side * (halfWidth + halfWidth * 0.5f + extra);

        astronaut = Instantiate(astronautPrefab, spawnPos, Quaternion.identity, astronautParent);

        // Lander "parken"
        lander.controlsEnabled = false;
        lander.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        lander.GetComponent<Collider2D>().isTrigger = true;

        CameraController.Instance.SetTarget(astronaut.transform);
        StarField.Instance.SetTarget(astronaut.transform);

        btnExit.gameObject.SetActive(false);
        LanderUI.Instance.HideGameOver();
    }

    public void EnterLander(LanderController newLander)
    {
        if (astronaut == null) return;

        Destroy(astronaut);
        astronaut = null;

        if (newLander != lander)
        {
            LanderController.ChangeLander(newLander);
            lander = newLander;
        }
        else
        {
            lander.controlsEnabled = true;
            lander.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
            lander.GetComponent<Collider2D>().isTrigger = false;

            CameraController.Instance.SetTarget(lander.transform);
        }

        StarField.Instance.SetTarget(lander.transform);

        btnExit.GetComponentInChildren<TMP_Text>().text = "Exit Lander";
        btnExit.gameObject.SetActive(true);

        if (newLander.isSecretLander && !LanderChooserManager.Instance.IsSecretFound(newLander.landerIndex))
        {
            LanderChooserManager.Instance.UnlockSecret(newLander.landerIndex);
            if (LanderController.Instance.landerState == LanderController.eLanderState.LandedMoon)
            {
                StoryTextController.Instance.Show("Guess the moon landing was real after all.");
                StoryTextController.Instance.Show("Okay… maybe this is a Moon Lander game. 😄");
            }
        }
    }


}
