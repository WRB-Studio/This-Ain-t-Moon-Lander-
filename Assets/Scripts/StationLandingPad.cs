using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class StationLandingPad : MonoBehaviour
{
    public int index;
    public bool isOutpost;
    public Collider2D Surface => GetComponent<Collider2D>();
    public bool HasParkedShip(LanderController incoming)
    {
        foreach (var ship in FindObjectsByType<LanderController>(FindObjectsSortMode.None))
            if (ship != incoming && ship.IsParkedOnPad && ship.StationPad == this) return true;
        return false;
    }
}
