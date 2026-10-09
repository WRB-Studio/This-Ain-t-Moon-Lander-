using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class ShipDockingPad : MonoBehaviour
{
    public static readonly List<ShipDockingPad> ActivePads = new();
    public Transform dockPoint;
    public Transform exitPoint;
    [Tooltip("Entfernung, in der die Landehilfe aktiv wird.")]
    public float approachRange = 9f;
    [Tooltip("Lokale Schwerkraft über diesem Pad.")]
    public float gravity = 3.5f;
    public float refillPerSecond = 8f;
    public bool allowExit = true;
    public AssistedSpacecraft Occupant { get; private set; }
    public Vector2 Up => transform.up;
    public BoxCollider2D Surface => GetComponent<BoxCollider2D>();
    void OnEnable() { if (!ActivePads.Contains(this)) ActivePads.Add(this); }
    void OnDisable() => ActivePads.Remove(this);
    public bool AvailableFor(AssistedSpacecraft ship) => !Occupant || Occupant == ship;
    public bool Claim(AssistedSpacecraft ship)
    {
        if (!AvailableFor(ship)) return false;
        Occupant = ship; return true;
    }
    public void Release(AssistedSpacecraft ship) { if (Occupant == ship) Occupant = null; }
    public Vector2 GravityAt(Vector2 position)
    {
        Vector3 local = transform.InverseTransformPoint(position);
        return Mathf.Abs(local.x) < Surface.size.x * 0.5f + 2f && local.y > -1f && local.y < approachRange
            ? -Up * gravity : Vector2.zero;
    }
    public static ShipDockingPad NearestApproach(Vector2 position, AssistedSpacecraft ship)
    {
        ShipDockingPad result = null; float best = float.PositiveInfinity;
        foreach (var pad in ActivePads)
        {
            if (!pad || !pad.AvailableFor(ship)) continue;
            Vector3 local = pad.transform.InverseTransformPoint(position);
            if (local.y < -0.2f || local.y > pad.approachRange || Mathf.Abs(local.x) > pad.Surface.size.x * 0.5f) continue;
            float distance = (position - (Vector2)pad.dockPoint.position).sqrMagnitude;
            if (distance < best) { best = distance; result = pad; }
        }
        return result;
    }
    public static Vector2 GetGravity(Vector2 position)
    {
        Vector2 gravity = Vector2.zero;
        foreach (var pad in ActivePads)
            if (pad)
            {
                Vector2 candidate = pad.GravityAt(position);
                if (candidate.sqrMagnitude > gravity.sqrMagnitude) gravity = candidate;
            }
        return gravity;
    }
}
