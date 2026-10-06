using UnityEngine;

public class UFOEncounter : MonoBehaviour
{
    public static UFOEncounter Instance { get; private set; }
    [SerializeField] GameObject model;
    [SerializeField] LineRenderer tractorBeam;
    [Tooltip("Height above the Moon's outer gravity zone where the first UFO contact starts.")]
    [SerializeField, Min(1f)] float distanceBeyondMoon = 50f;
    [SerializeField, Min(0.1f)] float approachDuration = 2.5f;
    [SerializeField, Min(0f)] float beamContactDelay = 1f;
    [SerializeField] Vector2 contactOffset = new(5f, 4f);
    [SerializeField] Vector2 departureDirection = new(1f, 1f);
    [SerializeField, Min(0.1f)] float departureSpeed = 12f;
    [SerializeField, Min(0f)] float departureSwayAmplitude = 0.75f;
    [SerializeField, Min(0f)] float departureSwayFrequency = 1.5f;

    LanderController heldShip;
    Vector3 approachStartOffset;
    float approachElapsed;
    float beamElapsed;
    float departureElapsed;
    bool approaching;
    Renderer[] modelRenderers;
    readonly Plane[] cameraPlanes = new Plane[6];
    public bool IsHolding => heldShip && heldShip.IsTractorHeld;

    void Awake() => modelRenderers = model.GetComponentsInChildren<Renderer>(true);

    void Update()
    {
        var game = GameController.Instance;
        var story = StoryTextController.Instance;
        var gravity = GravityManager2D.Instance;
        var saves = SaveLoadManager.Instance;
        if (!game || !story || !gravity || !saves || !model || !tractorBeam) return;
#if UNITY_EDITOR
        if (!story.DebugIsEnabled(StoryTextController.Discovery.UFO))
        {
            DebugReset();
            return;
        }
#endif

        bool found = story.HasDiscovered(StoryTextController.Discovery.UFO);
        bool flying = game.IsPlaying && game.Phase == GameController.GamePhase.Flight && game.ControlledTarget;
        model.SetActive(found || approaching || IsHolding);
        if (saves.Data.GetFlag("story.ack.UFO"))
        {
            approaching = false;
            ReleaseShip();
            if (saves.Data.GetFlag("story.ufoDeparted"))
            {
                model.SetActive(false);
                return;
            }
            model.SetActive(true);
            Vector2 direction = departureDirection.sqrMagnitude > 0.001f ? departureDirection.normalized : Vector2.up;
            Vector2 sideways = new(-direction.y, direction.x);
            float frequency = departureSwayFrequency * Mathf.PI * 2f;
            float previousSway = Mathf.Sin(departureElapsed * frequency) * departureSwayAmplitude;
            departureElapsed += Time.deltaTime;
            float sway = Mathf.Sin(departureElapsed * frequency) * departureSwayAmplitude;
            transform.position = saves.Data.ufoContactPosition
                + (Vector3)(direction * departureSpeed * Time.deltaTime + sideways * (sway - previousSway));
            saves.Data.ufoContactPosition = transform.position;
            var camera = Camera.main;
            if (camera)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, cameraPlanes);
                bool visible = false;
                foreach (var renderer in modelRenderers)
                    if (renderer && GeometryUtility.TestPlanesAABB(cameraPlanes, renderer.bounds)) { visible = true; break; }
                if (!visible)
                {
                    model.SetActive(false);
                    saves.Data.SetFlag("story.ufoDeparted", true);
                    saves.Save();
                }
            }
            return;
        }
        if (!flying)
        {
            approaching = false;
            ReleaseShip();
            model.SetActive(found);
            if (found) transform.position = saves.Data.ufoContactPosition;
            return;
        }

        bool beyondMoon = game.ControlledTarget.position.y
            > gravity.transform.position.y + gravity.moonEnterRadius + distanceBeyondMoon;
        if (!approaching && !IsHolding && beyondMoon && LanderChooserManager.Instance.HasFoundSecret
            && !story.IsShowingDialogue)
        {
            if (found)
            {
                // Resume an interrupted contact without persisting a control lock in the save.
                transform.position = game.ControlledTarget.position + (Vector3)contactOffset;
                saves.Data.ufoContactPosition = transform.position;
                CaptureShip();
            }
            else
            {
                var camera = Camera.main;
                float halfWidth = camera ? camera.orthographicSize * camera.aspect : 15f;
                approachStartOffset = new Vector3(halfWidth + 8f, contactOffset.y + 5f, 0f);
                approachElapsed = 0f;
                approaching = true;
                transform.position = game.ControlledTarget.position + approachStartOffset;
                model.SetActive(true);
            }
        }

        if (approaching)
        {
            approachElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(approachElapsed / Mathf.Max(0.1f, approachDuration));
            transform.position = game.ControlledTarget.position
                + Vector3.Lerp(approachStartOffset, (Vector3)contactOffset, Mathf.SmoothStep(0f, 1f, progress));
            if (progress >= 1f)
            {
                approaching = false;
                CaptureShip();
                saves.Data.ufoContactPosition = transform.position;
            }
        }
        tractorBeam.gameObject.SetActive(IsHolding);
        if (IsHolding)
        {
            tractorBeam.SetPosition(0, transform.position + Vector3.down * 0.8f);
            tractorBeam.SetPosition(1, heldShip.transform.position);
            beamElapsed += Time.deltaTime;
            if (!found && beamElapsed >= beamContactDelay) story.Discover(StoryTextController.Discovery.UFO);
        }
    }

    void CaptureShip()
    {
        departureElapsed = 0f;
        beamElapsed = 0f;
        heldShip = LanderController.Instance;
        if (heldShip) heldShip.SetTractorHold(true);
    }

    void ReleaseShip()
    {
        if (heldShip) heldShip.SetTractorHold(false);
        heldShip = null;
        if (tractorBeam) tractorBeam.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    public float DebugTriggerHeight => GravityManager2D.Instance.transform.position.y
        + GravityManager2D.Instance.moonEnterRadius + distanceBeyondMoon;

    public void DebugReset()
    {
        departureElapsed = 0f;
        approaching = false;
        ReleaseShip();
        if (model) model.SetActive(false);
    }
#endif

    void OnDisable()
    {
        approaching = false;
        ReleaseShip();
        if (Instance == this) Instance = null;
    }

    void OnEnable() => Instance = this;
}
