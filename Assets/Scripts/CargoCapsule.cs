using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(DistanceJoint2D))]
public class CargoCapsule : MonoBehaviour
{
    [SerializeField] Transform cableAnchor;
    [SerializeField] LineRenderer cable;
    [SerializeField] GameObject closedVisual;
    [SerializeField] GameObject openVisual;
    [SerializeField] GameObject wreckVisual;
    [SerializeField] AudioSource knocking;
    [SerializeField, Min(0.1f)] float destructiveImpactSpeed = 4f;
    [SerializeField, Min(0.1f)] float safeDeliverySpeed = 1.25f;
    [SerializeField, Min(0.1f)] float settlingTime = 0.8f;
    [SerializeField, Min(1f)] float towLength = 12f;
    public Rigidbody2D Body { get; private set; }
    public float TowLength => towLength;
    public float CableLength => joint && joint.enabled ? joint.distance : towLength;
    public Vector3 Anchor => cableAnchor.position;
    DistanceJoint2D joint;
    Collider2D shape;
    Transform hand;
    LanderController ship;
    Collider2D deliverySurface;
    float settled;
    float nextKnock;
    float impactGrace;

    void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        joint = GetComponent<DistanceJoint2D>();
        shape = GetComponent<Collider2D>();
        joint.enabled = false;
        Body.gravityScale = 0f;
    }
    void FixedUpdate()
    {
        if (Body.bodyType == RigidbodyType2D.Dynamic && GravityManager2D.Instance)
            Body.AddForce(GravityManager2D.Instance.GetGravity(Body.position) * Body.mass);
        var mission = CargoMission.Instance;
        if (joint.enabled && ship && ship.landerState == LanderController.eLanderState.Flying)
            joint.distance = Mathf.MoveTowards(joint.distance, towLength, Time.fixedDeltaTime * 1.2f);
        if (!mission || mission.Stage != CargoMission.Progress.Towing || !deliverySurface) { settled = 0f; return; }
        bool calm = Body.linearVelocity.magnitude <= safeDeliverySpeed && Mathf.Abs(Body.angularVelocity) < 15f;
        bool supported = shape.bounds.min.x >= deliverySurface.bounds.min.x + 0.1f
            && shape.bounds.max.x <= deliverySurface.bounds.max.x - 0.1f
            && shape.bounds.min.y >= deliverySurface.bounds.max.y - 0.2f
            && Mathf.Abs(Mathf.DeltaAngle(Body.rotation, 0f)) < 25f;
        settled = calm && supported ? settled + Time.fixedDeltaTime : 0f;
        if (settled >= settlingTime) mission.Deliver();
    }
    void LateUpdate()
    {
        cable.enabled = hand || ship;
        if (cable.enabled)
        {
            Vector3 end = hand ? hand.position + hand.up * 0.5f : ShipAnchor(ship);
            cable.SetPosition(0, Anchor);
            cable.SetPosition(1, end);
        }
        var mission = CargoMission.Instance;
        var game = GameController.Instance;
        bool near = mission && mission.Stage <= CargoMission.Progress.Briefed && game && game.IsPlaying
            && game.ControlledTarget && Vector2.Distance(game.ControlledTarget.position, transform.position) < 28f;
        if (knocking && near && Time.time >= nextKnock && Time.timeScale > 0f)
        {
            knocking.volume = Mathf.Clamp01(1f - Vector2.Distance(game.ControlledTarget.position, transform.position) / 28f)
                * SaveLoadManager.Instance.Data.volSfx * 0.65f;
            knocking.Play();
            nextKnock = Time.time + Random.Range(3f, 5f);
        }
    }
    public static Vector3 ShipAnchor(LanderController lander)
    {
        var sprite = lander.GetComponent<SpriteRenderer>().sprite;
        return lander.transform.TransformPoint(new Vector3(0f, sprite.bounds.min.y - 0.08f, 0f));
    }
    public void HoldCable(Transform astronaut) { Disconnect(); hand = astronaut; }
    public void Attach(LanderController lander, float savedLength = 0f)
    {
        Disconnect();
        ship = lander;
        Body.bodyType = RigidbodyType2D.Dynamic;
        joint.connectedBody = lander.rb;
        joint.autoConfigureConnectedAnchor = false;
        joint.autoConfigureDistance = false;
        joint.anchor = transform.InverseTransformPoint(Anchor);
        joint.connectedAnchor = lander.transform.InverseTransformPoint(ShipAnchor(lander));
        joint.distance = savedLength > 0f ? savedLength : Mathf.Max(towLength, Vector2.Distance(Anchor, ShipAnchor(lander)));
        joint.maxDistanceOnly = true;
        joint.enableCollision = false;
        joint.enabled = true;
        impactGrace = Time.time + 0.3f;
    }
    public void Disconnect()
    {
        hand = null;
        ship = null;
        if (joint) { joint.enabled = false; joint.connectedBody = null; }
        deliverySurface = null;
        settled = 0f;
        if (cable) cable.enabled = false;
    }
    public void SetAppearance(CargoMission.Progress stage)
    {
        bool failed = stage == CargoMission.Progress.Failed;
        bool opened = stage >= CargoMission.Progress.Released && !failed;
        closedVisual.SetActive(!failed && !opened);
        openVisual.SetActive(opened);
        wreckVisual.SetActive(failed);
        shape.enabled = !failed;
        Body.bodyType = stage == CargoMission.Progress.Towing ? RigidbodyType2D.Dynamic : RigidbodyType2D.Static;
        if (!knocking) return;
        if (stage > CargoMission.Progress.Briefed) knocking.Stop();
    }
    void OnCollisionEnter2D(Collision2D collision)
    {
        var mission = CargoMission.Instance;
        if (!mission || mission.Stage != CargoMission.Progress.Towing) return;
        if (Time.time >= impactGrace && collision.relativeVelocity.magnitude > destructiveImpactSpeed)
        {
            mission.Fail();
            return;
        }
        RecordDeliveryContact(collision);
    }
    void OnCollisionStay2D(Collision2D collision) => RecordDeliveryContact(collision);
    void RecordDeliveryContact(Collision2D collision)
    {
        if (!CargoMission.Instance || collision.collider != CargoMission.Instance.DeliverySurface) return;
        for (int i = 0; i < collision.contactCount; i++)
            if (collision.GetContact(i).normal.y > 0.5f) { deliverySurface = collision.collider; return; }
    }
    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider == deliverySurface) { deliverySurface = null; settled = 0f; }
    }
    void OnDisable() => Disconnect();
}
