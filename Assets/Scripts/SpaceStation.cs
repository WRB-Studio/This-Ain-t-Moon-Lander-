using UnityEngine;
using System;

public class SpaceStation : MonoBehaviour
{
    public const int LayoutVersion = 2;
    public static SpaceStation Instance { get; private set; }
    [SerializeField] GameObject geometry;
    [SerializeField] Vector2 offsetFromMoon = new(120f, 400f);
    [SerializeField, Min(1f)] float greetingDistance = 28f;
    [SerializeField, Min(1f)] float approachDistance = 80f;
    [SerializeField, Min(1f)] float gravityRange = 42f;
    [SerializeField, Min(0f)] float gravityStrength = 3.5f;
    [SerializeField] SpriteRenderer gravityField;
    [SerializeField, Min(1f)] float approachZoom = 42f;
    [SerializeField, Min(1f)] float stationHalfWidth = 26f;
    [SerializeField] SpriteRenderer upperFacade;
    [SerializeField, Range(0f, 1f)] float evaFacadeBrightness = 0.4f;
    [SerializeField, Min(0.05f)] float facadeFadeDuration = 0.6f;
    [SerializeField] Transform entrance;
    [SerializeField, Min(0.1f)] float entranceDistance = 2f;
    public event Action EnterRequested;
    public Transform Entrance => entrance;
    public bool HasEntryAction => EnterRequested != null;
    public bool IsAtEntrance => IsAvailable && entrance && GameController.Instance
        && GameController.Instance.Phase == GameController.GamePhase.EVA && GameController.Instance.ControlledTarget
        && Vector2.Distance(GameController.Instance.ControlledTarget.position, entrance.position) <= entranceDistance;
    public void RequestEntry() { if (IsAtEntrance) EnterRequested?.Invoke(); }
    public StationLandingPad[] pads;
    public bool IsAvailable => SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null
        && SaveLoadManager.Instance.Data.GetFlag("story.ufoDeparted");
    public float GreetingDistance => greetingDistance;
    public float ApproachDistance => approachDistance;
    public float GravityStrength => gravityStrength;
    public float GravityRange => gravityRange;
    public bool IsInView(Camera camera, float margin = 0f)
    {
        if (!IsAvailable || !upperFacade || !upperFacade.gameObject.activeInHierarchy) return false;
        var bounds = upperFacade.bounds;
        foreach (var pad in pads) bounds.Encapsulate(pad.Surface.bounds);
        Vector3 min = camera.WorldToViewportPoint(bounds.min);
        Vector3 max = camera.WorldToViewportPoint(bounds.max);
        return max.z > 0f && max.x >= -margin && min.x <= 1f + margin && max.y >= -margin && min.y <= 1f + margin;
    }
    public Vector2 OffsetFromMoon => offsetFromMoon;
    public void RestoreOffset(Vector2 offset)
    {
        offsetFromMoon = offset;
        transform.position = GravityManager2D.Instance.transform.position + (Vector3)offsetFromMoon;
        geometry.SetActive(IsAvailable);
    }

    void Awake()
    {
        Instance = this;
        geometry.SetActive(IsAvailable);
    }
    void Update()
    {
        if (!GravityManager2D.Instance || !GameController.Instance || !StoryTextController.Instance) return;
        transform.position = GravityManager2D.Instance.transform.position + (Vector3)offsetFromMoon;
        geometry.SetActive(IsAvailable);
        if (gravityField)
        {
            // Keep the visible field aligned with its physical range when adjusted in the Inspector.
            gravityField.transform.localScale = Vector3.one * (gravityRange * 2f / gravityField.sprite.bounds.size.x);
            gravityField.enabled = !StationInterior.Instance || !StationInterior.Instance.IsInside;
        }
        var game = GameController.Instance;
        if (upperFacade)
        {
            bool walkingOnStation = game.Phase == GameController.GamePhase.EVA && game.ControlledTarget
                && GetGravity(game.ControlledTarget.position).sqrMagnitude > 0f;
            float brightness = Mathf.MoveTowards(upperFacade.color.r, walkingOnStation ? evaFacadeBrightness : 1f,
                Time.unscaledDeltaTime * Mathf.Max(0.01f, 1f - evaFacadeBrightness) / facadeFadeDuration);
            upperFacade.color = new Color(brightness, brightness, brightness, 1f);
        }
        if (IsAvailable && game.IsPlaying && game.Phase == GameController.GamePhase.Flight
            && game.ControlledTarget && Vector2.Distance(game.ControlledTarget.position, transform.position) <= greetingDistance)
            StoryTextController.Instance.Discover(StoryTextController.Discovery.Station);
    }
    public Vector2 GetGravity(Vector2 position)
    {
        if (!IsAvailable) return Vector2.zero;
        var interiorGravity = StationInterior.Instance ? StationInterior.Instance.GetGravity(position) : Vector2.zero;
        if (interiorGravity.sqrMagnitude > 0f) return interiorGravity;
        float distance = Vector2.Distance(position, transform.position);
        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(gravityRange, gravityRange - 6f, distance));
        return Vector2.down * gravityStrength * blend;
    }
    public float GetApproachZoom(Vector2 position, float spaceZoom)
    {
        float proximity = 1f - Mathf.InverseLerp(gravityRange, approachDistance, Vector2.Distance(position, transform.position));
        float fullViewZoom = Camera.main ? Mathf.Max(approachZoom, stationHalfWidth / Camera.main.aspect) : approachZoom;
        return Mathf.Lerp(spaceZoom, fullViewZoom, Mathf.SmoothStep(0f, 1f, proximity));
    }
    public StationLandingPad GetPad(int index) => index >= 0 && index < pads.Length ? pads[index] : null;
    void OnDestroy() { if (Instance == this) Instance = null; }
}
