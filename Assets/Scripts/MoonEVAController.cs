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
    public LandingPanel landingPanel;
    public Button scoreExitButton => landingPanel.exitLanderButton;
    [HideInInspector] public GameObject astronaut;
    [SerializeField, Min(0.1f)] float stationBoardingDistance = 1.25f;
    LanderController[] stationLanders;

    readonly HashSet<Collider2D> nearbyLanders = new();
    TMP_Text buttonText;
    LanderController lander => LanderController.Instance;

    void Awake() => Instance = this;

    public void Init()
    {
        buttonText = btnExit.GetComponentInChildren<TMP_Text>();
        btnExit.onClick.RemoveAllListeners();
        btnExit.onClick.AddListener(OnActionClicked);
        if (scoreExitButton)
        {
            scoreExitButton.onClick.RemoveAllListeners();
            scoreExitButton.onClick.AddListener(ExitLander);
        }
        RefreshAction();
    }

    public void ResetRun()
    {
        if (astronaut) Destroy(astronaut);
        astronaut = null;
        nearbyLanders.Clear();
        stationLanders = null;
        RefreshAction();
    }
    public void ClearNearbyLanders()
    {
        nearbyLanders.Clear();
        RefreshAction();
    }

#if UNITY_EDITOR
    public void DebugSpawnAstronaut(Vector3 position)
    {
        ResetRun();
        lander.Park();
        astronaut = Instantiate(astronautPrefab, position, Quaternion.identity, astronautParent);
        GameController.Instance.BeginEVA(astronaut.transform);
        RefreshAction();
    }
#endif

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
            if (CargoMission.Instance && !CargoMission.Instance.CanBoard(candidate) && !CargoMission.Instance.HoldingCable) continue;
            float distance = ((Vector2)(candidate.transform.position - astronaut.transform.position)).sqrMagnitude;
            if (distance >= bestDistance) continue;
            nearest = candidate;
            bestDistance = distance;
        }
        // Station hulls remain solid for ships, but EVA ignores them and uses proximity to board.
        stationLanders ??= FindObjectsByType<LanderController>(FindObjectsSortMode.None);
        foreach (var candidate in stationLanders)
        {
            if (!candidate || !candidate.IsOnServicePad || candidate.IsCrashed) continue;
            if (CargoMission.Instance && !CargoMission.Instance.CanBoard(candidate) && !CargoMission.Instance.HoldingCable) continue;
            var hull = candidate.GetComponent<Collider2D>();
            if (Vector2.Distance(astronaut.transform.position, hull.ClosestPoint(astronaut.transform.position)) > stationBoardingDistance) continue;
            float distance = ((Vector2)(candidate.transform.position - astronaut.transform.position)).sqrMagnitude;
            if (distance >= bestDistance) continue;
            nearest = candidate;
            bestDistance = distance;
        }
        return nearest;
    }
    public LanderController NearbyLander => GetNearbyLander();

    void LateUpdate()
    {
        if (astronaut) RefreshAction();
    }

    public void RefreshAction()
    {
        if (!btnExit) return;
        bool entering = astronaut != null;
        if (buttonText) buttonText.SetLocalizedText(entering ? "[[game.enter.lander]]" : "[[game.exit.lander]]");
        bool canExit = lander && ((lander.landerState == LanderController.eLanderState.LandedMoon && lander.IsTouchingMoon)
            || (lander.IsOnServicePad && lander.IsTouchingPad)) && GameController.Instance.Phase == GameController.GamePhase.Landed;
        bool holdingCable = CargoMission.Instance && CargoMission.Instance.HoldingCable;
        btnExit.gameObject.SetActive(!GameController.Instance.HasResults
            && (entering ? !holdingCable && GetNearbyLander() != null : canExit));
        if (scoreExitButton) scoreExitButton.gameObject.SetActive(GameController.Instance.HasResults && canExit && !entering);
    }

    public void ExitLander()
    {
        if ((GameController.Instance.HasResults && (!scoreExitButton || !scoreExitButton.gameObject.activeInHierarchy))
            || astronaut || !lander || GameController.Instance.Phase != GameController.GamePhase.Landed
            || !((lander.landerState == LanderController.eLanderState.LandedMoon && lander.IsTouchingMoon)
                || (lander.IsOnServicePad && lander.IsTouchingPad))) return;
        var renderer = lander.GetComponent<SpriteRenderer>();
        float halfWidth = renderer.sprite.bounds.extents.x * Mathf.Abs(lander.transform.lossyScale.x);
        float side = Random.value < 0.5f ? -1f : 1f;
        if (lander.IsOnStation && SpaceStation.Instance.Entrance)
        {
            side = Mathf.Sign(SpaceStation.Instance.Entrance.position.x - lander.transform.position.x);
            if (side == 0f) side = 1f;
        }
        Vector3 spawn = lander.transform.position + lander.transform.right * side * (halfWidth * 1.5f + 0.5f);
        if (lander.IsOnOutpost && AsteroidOutpost.Instance.CargoSpawn)
        {
            side = Mathf.Sign(AsteroidOutpost.Instance.CargoSpawn.position.x - lander.transform.position.x);
            spawn = lander.transform.position + lander.transform.right * side * (halfWidth * 1.5f + 0.5f);
        }
        nearbyLanders.Clear();
        stationLanders = null;
        astronaut = Instantiate(astronautPrefab, spawn, lander.transform.rotation, astronautParent);
        lander.Park();
        GameController.Instance.BeginEVA(astronaut.transform);
        RefreshAction();
    }

    public void EnterLander(LanderController newLander)
    {
        if (!astronaut || !newLander || newLander.IsCrashed
            || (CargoMission.Instance && !CargoMission.Instance.CanBoard(newLander))) return;
        var previousAstronaut = astronaut;
        astronaut = null;
        nearbyLanders.Clear();
        Destroy(previousAstronaut);
        if (newLander != lander) LanderController.ChangeLander(newLander);
        GameController.Instance.BoardLander(newLander);
        LanderChooserManager.Instance.SelectDiscoveredLander(newLander);
        RefreshAction();
    }

    public void RestoreAstronaut(ActorSave state)
    {
        ResetRun();
        astronaut = Instantiate(astronautPrefab, state.position, Quaternion.Euler(0f, 0f, state.rotation), astronautParent);
        state.Restore(astronaut.transform, astronaut.GetComponent<Rigidbody2D>());
        RefreshAction();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
