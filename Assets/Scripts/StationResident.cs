using TMPro;
using UnityEngine;
using System.Collections.Generic;

public class StationResident : MonoBehaviour
{
    [SerializeField] int residentId;
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
    }
    void OnEnable()
    {
        Cache();
        if (entersStationOnApproach && SaveLoadManager.Instance && SaveLoadManager.Instance.Data != null
            && SaveLoadManager.Instance.Data.GetFlag("story.stationGuideEntered")) entered = true;
        if (entered) { gameObject.SetActive(false); return; }
        if (!activeResidents.Contains(this)) activeResidents.Add(this);
        float bottomOffset = VisualBounds.min.y - transform.position.y;
        Vector3 position = transform.localPosition;
        float parentScale = transform.parent ? Mathf.Max(0.0001f, transform.parent.lossyScale.y) : 1f;
        position.y = floorY - bottomOffset / parentScale;
        transform.localPosition = position;
        if (bubbleText) bubbleText.gameObject.SetActive(false);
    }
    void Update()
    {
        var game = GameController.Instance;
        var station = SpaceStation.Instance;
        if (!game || !station || !station.IsAvailable || entered) return;
        bool nearPlayer = game.IsPlaying && game.Phase == GameController.GamePhase.EVA && game.ControlledTarget
            && Vector2.Distance(game.ControlledTarget.position, transform.position) <= approachDistance;
        if (entersStationOnApproach && nearPlayer && (!StationInterior.Instance || !StationInterior.Instance.IsInside))
            walkingToEntrance = true;
        float goal = walkingToEntrance ? transform.parent.InverseTransformPoint(station.Entrance.position).x : movingRight ? maxX : minX;
        pause = Mathf.Max(0f, pause - Time.deltaTime);
        Vector3 local = transform.localPosition;
        float previousX = local.x;
        Vector3 previousWorldPosition = transform.position;
        if (pause <= 0f) local.x = Mathf.MoveTowards(local.x, goal, walkSpeed * Time.deltaTime);
        transform.localPosition = local;
        bool moving = Mathf.Abs(local.x - previousX) > 0.00001f;
        if (characterVisual)
        {
            float worldSpeed = Vector3.Distance(transform.position, previousWorldPosition) / Mathf.Max(0.0001f, Time.deltaTime);
            characterVisual.SetWalking(moving, worldSpeed);
            if (moving) characterVisual.FaceLeft(local.x < previousX);
        }
        else
        {
            if (animator) animator.SetBool(Walking, moving);
            if (moving && sprite) sprite.flipX = local.x < previousX;
        }
        if (Mathf.Abs(local.x - goal) < 0.03f && pause <= 0f)
        {
            if (walkingToEntrance)
            {
                entered = true;
                SaveLoadManager.Instance.Data.SetFlag("story.stationGuideEntered", true);
                gameObject.SetActive(false);
                SaveLoadManager.Instance.Save();
                return;
            }
            movingRight = !movingRight;
            pause = Random.Range(0.8f, 2.8f);
        }
        UpdateComment(game);
    }
    void UpdateComment(GameController game)
    {
        if (!bubbleText) return;
        Bounds bounds = VisualBounds;
        Vector3 bubblePosition = bubbleText.transform.parent.position;
        bubblePosition.x = bounds.center.x;
        bubblePosition.y = bounds.max.y + 0.7f;
        bubbleText.transform.parent.position = bubblePosition;
        bool canComment = game.IsPlaying && game.Phase == GameController.GamePhase.EVA && game.ControlledTarget
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
        bool keepVisible = game.IsPlaying && game.Phase == GameController.GamePhase.EVA && game.ControlledTarget
            && (!StationConversation.Instance || !StationConversation.Instance.IsShowing)
            && (!StationInterior.Instance || !StationInterior.Instance.IsTransitioning)
            && Vector2.Distance(game.ControlledTarget.position, transform.position) <= commentHideDistance;
        bubbleText.gameObject.SetActive(keepVisible && Time.time < bubbleUntil);
    }
    public ResidentSave CaptureState() => new()
    {
        id = residentId, x = transform.localPosition.x, pause = pause,
        movingRight = movingRight, walkingToEntrance = walkingToEntrance, entered = entered
    };
    public void RestoreState(ResidentSave state)
    {
        if (state.id != residentId) return;
        movingRight = state.movingRight;
        walkingToEntrance = state.walkingToEntrance;
        entered = state.entered;
        pause = state.pause;
        Vector3 position = transform.localPosition;
        position.x = state.x;
        transform.localPosition = position;
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
    }
#if UNITY_EDITOR
    public void DebugReset()
    {
        entered = walkingToEntrance = false;
        movingRight = true;
        pause = nextComment = bubbleUntil = 0f;
        var position = transform.localPosition;
        position.x = minX;
        transform.localPosition = position;
        gameObject.SetActive(true);
    }
#endif
}
