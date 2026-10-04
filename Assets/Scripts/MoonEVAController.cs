using System.Collections.Generic;
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
    [HideInInspector] public bool isOnMoonLanded;
    [HideInInspector] public GameObject astronaut;

    readonly HashSet<Collider2D> nearbyLanders = new();
    TMP_Text buttonText;
    LanderController lander => LanderController.Instance;

    void Awake() => Instance = this;

    public void Init()
    {
        buttonText = btnExit.GetComponentInChildren<TMP_Text>();
        btnExit.onClick.RemoveAllListeners();
        btnExit.onClick.AddListener(OnActionClicked);
        RefreshAction();
    }

    public void ResetRun()
    {
        if (astronaut) Destroy(astronaut);
        astronaut = null;
        nearbyLanders.Clear();
        isOnMoonLanded = false;
        RefreshAction();
    }

    void OnActionClicked()
    {
        if (astronaut) EnterLander(GetNearbyLander());
        else ExitLander();
    }

    public void RegisterLander(Collider2D collider)
    {
        if (!astronaut || !collider.GetComponentInParent<LanderController>()) return;
        nearbyLanders.Add(collider);
        RefreshAction();
    }

    public void UnregisterLander(Collider2D collider)
    {
        nearbyLanders.Remove(collider);
        RefreshAction();
    }

    LanderController GetNearbyLander()
    {
        if (!astronaut) return null;
        LanderController nearest = null;
        float bestDistance = float.PositiveInfinity;
        foreach (var collider in nearbyLanders)
        {
            if (!collider || !collider.enabled) continue;
            var candidate = collider.GetComponentInParent<LanderController>();
            if (!candidate || candidate.IsCrashed) continue;
            float distance = ((Vector2)(candidate.transform.position - astronaut.transform.position)).sqrMagnitude;
            if (distance >= bestDistance) continue;
            nearest = candidate;
            bestDistance = distance;
        }
        return nearest;
    }

    public void RefreshAction()
    {
        if (!btnExit) return;
        bool entering = astronaut != null;
        if (buttonText) buttonText.text = entering ? "Enter Lander" : "Exit Lander";
        bool canExit = lander && lander.landerState == LanderController.eLanderState.LandedMoon
            && lander.IsTouchingMoon && GameController.Instance.Phase != GameController.GamePhase.Countdown;
        btnExit.gameObject.SetActive(entering ? GetNearbyLander() != null : canExit);
    }

    public void ExitLander()
    {
        if (astronaut || !lander || lander.landerState != LanderController.eLanderState.LandedMoon || !lander.IsTouchingMoon) return;
        var renderer = lander.GetComponent<SpriteRenderer>();
        float halfWidth = renderer.sprite.bounds.extents.x * Mathf.Abs(lander.transform.lossyScale.x);
        float side = Random.value < 0.5f ? -1f : 1f;
        Vector3 spawn = lander.transform.position + lander.transform.right * side * (halfWidth * 1.5f + 0.5f);
        nearbyLanders.Clear();
        astronaut = Instantiate(astronautPrefab, spawn, lander.transform.rotation, astronautParent);
        lander.Park();
        GameController.Instance.SetPhase(GameController.GamePhase.EVA);
        GameController.Instance.SetControlledTarget(astronaut.transform);
        LanderUI.Instance.HideGameOver();
        LanderUI.Instance.SetPanelBottomCenter();
        RefreshAction();
    }

    public void EnterLander(LanderController newLander)
    {
        if (!astronaut || !newLander || newLander.IsCrashed) return;
        var previousAstronaut = astronaut;
        astronaut = null;
        nearbyLanders.Clear();
        Destroy(previousAstronaut);
        if (newLander != lander) LanderController.ChangeLander(newLander);
        newLander.ResumeFromMoon();
        LanderChooserManager.Instance.SelectDiscoveredLander(newLander);
        GameController.Instance.SetPhase(GameController.GamePhase.Flight);
        GameController.Instance.SetControlledTarget(newLander.transform, true);
        RefreshAction();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
