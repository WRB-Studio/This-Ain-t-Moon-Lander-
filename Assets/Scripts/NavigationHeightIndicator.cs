using UnityEngine;
using UnityEngine.UI;

public class NavigationHeightIndicator : MonoBehaviour
{
    [SerializeField] Graphic above;
    [SerializeField] Graphic below;

    public void Draw(float heightDifference, float tolerance, Color tint)
    {
        above.gameObject.SetActive(heightDifference > tolerance);
        below.gameObject.SetActive(heightDifference < -tolerance);
        above.color = below.color = tint;
    }
}
