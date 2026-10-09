using UnityEngine;

[RequireComponent(typeof(NPCMotor2D))]
public class CargoPassenger : MonoBehaviour
{
    [SerializeField] CharacterVisual visual;
    [SerializeField, Min(0.1f)] float walkSpeed = 2f;
    NPCMotor2D motor;
    void Awake() => motor = GetComponent<NPCMotor2D>();
    public NPCMotor2D Motor { get { if (!motor) motor = GetComponent<NPCMotor2D>(); return motor; } }
    public float FootOffset
    {
        get
        {
            return Motor.FootOffset;
        }
    }
    public void WalkTowards(Vector3 destination)
    {
        float delta = destination.x - Motor.Body.position.x;
        Motor.Walk(Mathf.Abs(delta) > 0.025f ? Mathf.Sign(delta) * walkSpeed : 0f);
        bool walking = Motor.IsGrounded && Mathf.Abs(Motor.Body.linearVelocity.x) > 0.02f;
        visual.SetWalking(walking, walking ? Motor.Body.linearVelocity.magnitude : 0f);
        if (walking) visual.FaceLeft(Motor.Body.linearVelocity.x < 0f);
    }
    public void Stand() { Motor.Stop(); visual.SetWalking(false, 0f); }
    public void AdvanceHiddenTransit(Vector3 destination)
    {
        // Unloaded station rooms have no active floor colliders.
        Vector3 position = transform.position;
        position.x = Mathf.MoveTowards(position.x, destination.x, walkSpeed * Time.deltaTime);
        Motor.Teleport(position);
    }
}
