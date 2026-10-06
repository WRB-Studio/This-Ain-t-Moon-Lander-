using System.Collections;
using UnityEngine;

public class StationInterior : MonoBehaviour
{
    public enum Area { Outside, Hall, Registration }
    public static StationInterior Instance { get; private set; }
    [SerializeField] GameObject hall;
    [SerializeField] GameObject registration;
    [SerializeField] Transform hallArrival;
    [SerializeField] Transform hallReturn;
    [SerializeField] Transform registrationArrival;
    [SerializeField] Transform outsideArrival;
    [SerializeField] Transform nextSignalTarget;
    [SerializeField] Collider2D hallFloor;
    [SerializeField] Collider2D registrationFloor;
    CanvasGroup transitionCurtain;
    [SerializeField, Min(0.05f)] float fadeDuration = 0.2f;
    [SerializeField] AudioSource maintenanceSound;
    AudioClip maintenanceClip;
    float nextMaintenanceSound;
    public Area CurrentArea { get; private set; }
    public bool IsInside => CurrentArea != Area.Outside;
    public bool IsTransitioning { get; private set; }
    public Transform NextSignalTarget => nextSignalTarget;
    SpaceStation station;
    Coroutine transition;

    void Awake()
    {
        Instance = this;
        station = GetComponentInParent<SpaceStation>();
        RestoreArea(0);
    }
    void OnEnable()
    {
        if (!station) station = GetComponentInParent<SpaceStation>();
        station.EnterRequested += EnterHall;
    }
    void Start()
    {
        transitionCurtain = StationDoorPrompt.Instance.Curtain;
        if (maintenanceSound)
        {
            const int rate = 22050;
            var samples = new float[rate * 2];
            var noise = new System.Random(2406);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Sin(Mathf.PI * i / samples.Length);
                samples[i] = envelope * (0.1f * Mathf.Sin(t * 70f * Mathf.PI * 2f)
                    + 0.04f * Mathf.Sin(t * 114f * Mathf.PI * 2f) + ((float)noise.NextDouble() - 0.5f) * 0.025f);
            }
            maintenanceClip = AudioClip.Create("Station maintenance", samples.Length, 1, rate, false);
            maintenanceClip.SetData(samples, 0);
            maintenanceSound.clip = maintenanceClip;
        }
    }
    void OnDisable()
    {
        if (station) station.EnterRequested -= EnterHall;
        CancelTransition();
    }
    void Update()
    {
        if (IsInside && GameController.Instance && GameController.Instance.Phase != GameController.GamePhase.EVA)
        {
            CancelTransition();
            RestoreArea(0);
        }
        if (maintenanceSound)
        {
            if (SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null)
                maintenanceSound.volume = SaveLoadManager.Instance.Data.volSfx * 0.18f;
            if (CurrentArea != Area.Hall) maintenanceSound.Stop();
            else if (!maintenanceSound.isPlaying && Time.time >= nextMaintenanceSound && SaveLoadManager.Instance
                && SaveLoadManager.Instance.Data != null)
            {
                maintenanceSound.volume = SaveLoadManager.Instance.Data.volSfx * 0.18f;
                maintenanceSound.Play();
                nextMaintenanceSound = Time.time + Random.Range(6f, 14f);
            }
        }
    }

    public Vector2 GetGravity(Vector2 position)
    {
        if (!IsInside || !station.IsAvailable) return Vector2.zero;
        var room = CurrentArea == Area.Hall ? hall.transform : registration.transform;
        Vector3 local = room.InverseTransformPoint(position);
        float halfWidth = CurrentArea == Area.Hall ? 32f : 14f;
        return Mathf.Abs(local.x) <= halfWidth + 2f && local.y > -3f && local.y < 12f ? Vector2.down * station.GravityStrength : Vector2.zero;
    }

    public void EnterHall() => GoTo(Area.Hall, hallArrival);
    public void EnterRegistration() => GoTo(Area.Registration, registrationArrival);
    public void ReturnToHall() => GoTo(Area.Hall, hallReturn);
    public void ExitStation() => GoTo(Area.Outside, outsideArrival);
    void GoTo(Area area, Transform destination)
    {
        if (IsTransitioning || !destination || !station.IsAvailable || !GameController.Instance
            || GameController.Instance.Phase != GameController.GamePhase.EVA
            || (StationConversation.Instance && StationConversation.Instance.IsShowing)) return;
        IsTransitioning = true;
        AudioManager.Instance.PlaySound(AudioManager.Instance.sfxCountdownStart, 0.15f, 0.75f);
        StoryTextController.Instance.BlockInputUntilRelease();
        transition = StartCoroutine(ChangeRoom(area, destination));
    }
    IEnumerator ChangeRoom(Area area, Transform destination)
    {
        transitionCurtain.gameObject.SetActive(true);
        transitionCurtain.blocksRaycasts = true;
        yield return FadeTo(1f);
        RestoreArea((int)area);
        PlacePlayer(destination.position);
        SaveLoadManager.Instance.Save();
        yield return FadeTo(0f);
        transitionCurtain.blocksRaycasts = false;
        transitionCurtain.gameObject.SetActive(false);
        IsTransitioning = false;
        transition = null;
    }
    IEnumerator FadeTo(float target)
    {
        while (!Mathf.Approximately(transitionCurtain.alpha, target))
        {
            transitionCurtain.alpha = Mathf.MoveTowards(transitionCurtain.alpha, target, Time.unscaledDeltaTime / fadeDuration);
            yield return null;
        }
    }
    void PlacePlayer(Vector3 position)
    {
        var game = GameController.Instance;
        game.ControlledTarget.SetPositionAndRotation(position, Quaternion.identity);
        game.ControlledBody.position = position;
        game.ControlledBody.rotation = 0f;
        game.ControlledBody.linearVelocity = Vector2.zero;
        game.ControlledBody.angularVelocity = 0f;
        MoonEVAController.Instance.ClearNearbyLanders();
        Physics2D.SyncTransforms();
        var floor = CurrentArea == Area.Hall ? hallFloor : CurrentArea == Area.Registration ? registrationFloor : station.pads[0].Surface;
        var shape = game.ControlledTarget.GetComponent<Collider2D>();
        if (floor && shape)
        {
            float bottomOffset = shape.bounds.min.y - game.ControlledTarget.position.y;
            position.y = floor.bounds.max.y - bottomOffset + 0.02f;
            game.ControlledTarget.position = position;
            game.ControlledBody.position = position;
            Physics2D.SyncTransforms();
        }
        game.SetControlledTarget(game.ControlledTarget, true);
    }
    public void RestoreArea(int area)
    {
        CurrentArea = (Area)Mathf.Clamp(area, 0, 2);
        hall.SetActive(CurrentArea == Area.Hall);
        registration.SetActive(CurrentArea == Area.Registration);
    }
    void CancelTransition()
    {
        if (transition != null) StopCoroutine(transition);
        transition = null;
        IsTransitioning = false;
        if (transitionCurtain)
        {
            transitionCurtain.alpha = 0f;
            transitionCurtain.blocksRaycasts = false;
            transitionCurtain.gameObject.SetActive(false);
        }
    }
    public void ResetToOutside()
    {
        CancelTransition();
        RestoreArea(0);
    }

#if UNITY_EDITOR
    public void DebugJump(Area area)
    {
        CancelTransition();
        if (StationConversation.Instance) StationConversation.Instance.Close();
        StoryTextController.Instance.DebugJumpTo(StoryTextController.Discovery.StationLanding);
        LanderController.Instance.DebugDockAtStation(station.pads[0]);
        MoonEVAController.Instance.DebugSpawnAstronaut(outsideArrival.position);
        RestoreArea((int)area);
        PlacePlayer(area == Area.Registration ? registrationArrival.position : hallArrival.position);
        StoryTextController.Instance.BlockInputUntilRelease();
        SaveLoadManager.Instance.Save();
    }
#endif
    void OnDestroy()
    {
        if (maintenanceClip) Destroy(maintenanceClip);
        if (Instance == this) Instance = null;
    }
}
