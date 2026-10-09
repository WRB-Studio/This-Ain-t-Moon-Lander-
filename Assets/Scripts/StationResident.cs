using TMPro;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(NPCMotor2D))]
public class StationResident : MonoBehaviour
{
    public enum PatrolMode { MinMaxX, Waypoints }
    public enum WaypointTraversal { PingPong, Loop }
    [SerializeField] int residentId;
    [SerializeField] PatrolMode patrolMode;
    [SerializeField] Transform[] waypoints;
    [SerializeField] WaypointTraversal waypointTraversal;
    [SerializeField, Min(0.01f)] float arrivalDistance = 0.2f;
    [SerializeField] Vector2 pauseDuration = new(0.8f, 2.8f);
    [SerializeField] float minX = -3f;
    [SerializeField] float maxX = 3f;
    [SerializeField] float floorY = 0.45f;
    [SerializeField, Min(0f)] float walkSpeed = 1f;
    [SerializeField] bool entersStationOnApproach;
    [SerializeField, Min(0.1f)] float approachDistance = 3f;
    [SerializeField] TMP_Text bubbleText;
    [SerializeField, TextArea] string[] comments;
    [SerializeField, Min(0.1f)] float commentDistance = 1.5f;
    [SerializeField, Min(0.1f)] float commentDuration = 7f;
    [SerializeField, Min(0.1f)] float commentHideDistance = 5f;
    [SerializeField, Min(0f)] float commentCooldown = 10f;
    Animator animator;
    SpriteRenderer sprite;
    CharacterVisual characterVisual;
    NPCMotor2D motor;
    int waypointIndex;
    int waypointDirection = 1;
    bool placed;
    bool movingRight = true;
    bool walkingToEntrance;
    bool entered;
    float pause;
    float bubbleUntil;
    float nextComment;
    int lastComment = -1;
    static readonly int Walking = Animator.StringToHash("IsWalking");
    static readonly List<StationResident> activeResidents = new();
    Bounds VisualBounds => characterVisual ? characterVisual.Bounds : sprite.bounds;

    void Awake() => Cache();
    void Cache()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>(true);
        if (!characterVisual) characterVisual = GetComponentInChildren<CharacterVisual>(true);
        if (!motor) motor = GetComponent<NPCMotor2D>();
    }
    void OnEnable()
    {
        Cache();
        if (entersStationOnApproach && SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null
            && SaveLoadManager.Instance.Data.GetFlag("story.stationGuideEntered")) entered = true;
        if (entered) { gameObject.SetActive(false); return; }
        if (!activeResidents.Contains(this)) activeResidents.Add(this);
        if (!placed && patrolMode == PatrolMode.MinMaxX) PlaceOnInitialFloor();
        placed = true;
        if (bubbleText) bubbleText.gameObject.SetActive(false);
    }
    void Update()
    {
        var game = GameController.Instance;
        var station = SpaceStation.Instance;
        if (entered) { motor.Stop(); return; }
        bool nearPlayer = game && station && station.IsAvailable && game.IsPlaying && game.Phase == GameController.GamePhase.EVA && game.ControlledTarget
            && Vector2.Distance(game.ControlledTarget.position, transform.position) <= approachDistance;
        if (entersStationOnApproach && nearPlayer && (!StationInterior.Instance || !StationInterior.Instance.IsInside))
            walkingToEntrance = true;
        Vector3 target;
        bool validTarget = GetTarget(out target);
        pause = Mathf.Max(0f, pause - Time.deltaTime);
        float delta = target.x - motor.Body.position.x;
        float parentScale = transform.parent ? Mathf.Abs(transform.parent.lossyScale.x) : 1f;
        float speed = validTarget && pause <= 0f && Mathf.Abs(delta) > 0.025f ? Mathf.Sign(delta) * walkSpeed * parentScale : 0f;
        motor.Walk(speed);
        bool moving = motor.IsGrounded && Mathf.Abs(motor.Body.linearVelocity.x) > 0.02f;
        if (characterVisual)
        {
            float worldSpeed = moving ? motor.Body.linearVelocity.magnitude : 0f;
            characterVisual.SetWalking(moving, worldSpeed);
            if (moving) characterVisual.FaceLeft(motor.Body.linearVelocity.x < 0f);
        }
        else
        {
            if (animator) animator.SetBool(Walking, moving);
            if (moving && sprite) sprite.flipX = motor.Body.linearVelocity.x < 0f;
        }
        bool arrived = validTarget && Mathf.Abs(delta) <= arrivalDistance
            && (walkingToEntrance || patrolMode == PatrolMode.MinMaxX || Mathf.Abs(motor.FeetPosition.y - target.y) <= arrivalDistance);
        if (arrived && pause <= 0f)
        {
            if (walkingToEntrance)
            {
                entered = true;
                SaveLoadManager.Instance.Data.SetFlag("story.stationGuideEntered", true);
                gameObject.SetActive(false);
                SaveLoadManager.Instance.Save();
                return;
            }
            if (patrolMode == PatrolMode.Waypoints) AdvanceWaypoint();
            else movingRight = !movingRight;
            pause = Random.Range(Mathf.Max(0f, pauseDuration.x), Mathf.Max(pauseDuration.x, pauseDuration.y));
            motor.Stop();
        }
        UpdateComment(game);
    }
    bool GetTarget(out Vector3 target)
    {
        target = transform.position;
        if (walkingToEntrance && SpaceStation.Instance && SpaceStation.Instance.Entrance)
        {
            target = SpaceStation.Instance.Entrance.position; return true;
        }
        if (patrolMode == PatrolMode.MinMaxX)
        {
            float x = movingRight ? Mathf.Max(minX, maxX) : Mathf.Min(minX, maxX);
            target = transform.parent ? transform.parent.TransformPoint(new Vector3(x, floorY, 0f)) : new Vector3(x, floorY, 0f);
            return true;
        }
        if (waypoints == null || waypoints.Length == 0) return false;
        waypointIndex = Mathf.Clamp(waypointIndex, 0, waypoints.Length - 1);
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[waypointIndex]) { target = waypoints[waypointIndex].position; return true; }
            AdvanceWaypoint();
        }
        return false;
    }
    void AdvanceWaypoint()
    {
        if (waypoints == null || waypoints.Length <= 1) { waypointIndex = 0; return; }
        if (waypointTraversal == WaypointTraversal.Loop) waypointIndex = (waypointIndex + 1) % waypoints.Length;
        else
        {
            if (waypointIndex >= waypoints.Length - 1) waypointDirection = -1;
            else if (waypointIndex <= 0) waypointDirection = 1;
            waypointIndex += waypointDirection;
        }
    }
    void PlaceOnInitialFloor()
    {
        float bottomOffset = motor.FootOffset;
        Vector3 position = transform.localPosition;
        float parentScale = transform.parent ? Mathf.Max(0.0001f, Mathf.Abs(transform.parent.lossyScale.y)) : 1f;
        position.y = floorY - bottomOffset / parentScale + 0.02f;
        motor.Teleport(transform.parent ? transform.parent.TransformPoint(position) : position);
    }
    void UpdateComment(GameController game)
    {
        if (!bubbleText) return;
        Bounds bounds = VisualBounds;
        Vector3 bubblePosition = bubbleText.transform.parent.position;
        bubblePosition.x = bounds.center.x;
        bubblePosition.y = bounds.max.y + 0.7f;
        bubbleText.transform.parent.position = bubblePosition;
        bool canComment = game && game.IsPlaying && game.Phase == GameController.GamePhase.EVA && game.ControlledTarget
            && (!StationConversation.Instance || !StationConversation.Instance.IsShowing)
            && (!StationInterior.Instance || !StationInterior.Instance.IsTransitioning)
            && Vector2.Distance(game.ControlledTarget.position, transform.position) <= commentDistance;
        if (canComment)
            foreach (var other in activeResidents)
                if (other != this && other.comments != null && other.comments.Length > 0
                    && Vector2.Distance(game.ControlledTarget.position, other.transform.position)
                        < Vector2.Distance(game.ControlledTarget.position, transform.position)) { canComment = false; break; }
        if (canComment && comments != null && comments.Length > 0 && Time.time >= nextComment)
        {
            int index = Random.Range(0, comments.Length);
            if (comments.Length > 1 && index == lastComment) index = (index + 1) % comments.Length;
            lastComment = index;
            bubbleText.SetLocalizedText(comments[index]);
            bubbleUntil = Time.time + commentDuration;
            nextComment = bubbleUntil + commentCooldown;
        }
        bool keepVisible = game && game.IsPlaying && game.Phase == GameController.GamePhase.EVA && game.ControlledTarget
            && (!StationConversation.Instance || !StationConversation.Instance.IsShowing)
            && (!StationInterior.Instance || !StationInterior.Instance.IsTransitioning)
            && Vector2.Distance(game.ControlledTarget.position, transform.position) <= commentHideDistance;
        bubbleText.gameObject.SetActive(keepVisible && Time.time < bubbleUntil);
    }
    public ResidentSave CaptureState()
    {
        Cache();
        return new ResidentSave
        {
        id = residentId, x = transform.localPosition.x, pause = pause,
        movingRight = movingRight, walkingToEntrance = walkingToEntrance, entered = entered,
        hasPhysicsState = true, position = transform.parent ? transform.parent.InverseTransformPoint(motor.Body.position) : (Vector3)motor.Body.position,
        velocity = motor.Body.linearVelocity, waypointIndex = waypointIndex, waypointDirection = waypointDirection
        };
    }
    public void RestoreState(ResidentSave state)
    {
        if (state.id != residentId) return;
        movingRight = state.movingRight;
        walkingToEntrance = state.walkingToEntrance;
        entered = state.entered;
        pause = state.pause;
        Cache(); placed = true;
        waypointIndex = state.waypointIndex;
        waypointDirection = state.waypointDirection == -1 ? -1 : 1;
        Vector3 position = state.hasPhysicsState ? state.position : new Vector3(state.x, transform.localPosition.y, transform.localPosition.z);
        motor.Teleport(transform.parent ? transform.parent.TransformPoint(position) : position, state.hasPhysicsState ? state.velocity : Vector2.zero);
        if (!state.hasPhysicsState && patrolMode == PatrolMode.MinMaxX) PlaceOnInitialFloor();
        gameObject.SetActive(!entered);
    }
    public int Id => residentId;
    void OnDrawGizmosSelected()
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        // Patrol coordinates and floor height are relative to the resident's parent.
        Gizmos.matrix = transform.parent ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
        float z = transform.localPosition.z;
        Vector3 left = new(minX, floorY, z);
        Vector3 right = new(maxX, floorY, z);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawLine(left + Vector3.down * 0.3f, left + Vector3.up * 0.3f);
        Gizmos.DrawLine(right + Vector3.down * 0.3f, right + Vector3.up * 0.3f);
        Gizmos.color = Color.yellow;
        Vector3 floor = new(transform.localPosition.x, floorY, z);
        Gizmos.DrawWireSphere(floor, 0.12f);
        if (patrolMode == PatrolMode.Waypoints && waypoints != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.green;
            Transform previous = null;
            foreach (var point in waypoints)
            {
                if (!point) continue;
                Gizmos.DrawWireSphere(point.position, 0.18f);
                if (previous) Gizmos.DrawLine(previous.position, point.position);
                previous = point;
            }
            if (waypointTraversal == WaypointTraversal.Loop && previous && waypoints.Length > 1 && waypoints[0])
                Gizmos.DrawLine(previous.position, waypoints[0].position);
        }
#if UNITY_EDITOR
        Vector3 WorldPoint(Vector3 point) => transform.parent ? transform.parent.TransformPoint(point) : point;
        UnityEditor.Handles.Label(WorldPoint(left + Vector3.up * 0.4f), $"Min X: {minX:0.##}");
        UnityEditor.Handles.Label(WorldPoint(right + Vector3.up * 0.8f), $"Max X: {maxX:0.##}");
        UnityEditor.Handles.Label(WorldPoint(floor + Vector3.down * 0.6f), $"Floor Y: {floorY:0.##}");
#endif
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
    void OnDisable()
    {
        activeResidents.Remove(this);
        if (bubbleText) bubbleText.gameObject.SetActive(false);
        if (motor) motor.Stop();
    }
#if UNITY_EDITOR
    public void DebugReset()
    {
        entered = walkingToEntrance = false;
        movingRight = true;
        waypointIndex = 0; waypointDirection = 1;
        pause = nextComment = bubbleUntil = 0f;
        var position = transform.localPosition;
        position.x = minX;
        transform.localPosition = position;
        gameObject.SetActive(true);
        PlaceOnInitialFloor();
    }
#endif
}
