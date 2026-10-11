using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpaceNavigationMarker : MonoBehaviour
{
    public RectTransform arrow;
    public Graphic graphic;
    public TMP_Text distanceText;
    public TMP_Text unknownText;
    public void Draw(Vector2 position, Vector2 direction, string distance, Color tint, bool unknown = false)
    {
        ((RectTransform)transform).anchoredPosition = position;
        arrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        graphic.color = tint;
        if (unknownText) { unknownText.gameObject.SetActive(unknown); unknownText.color = tint; }
        distanceText.gameObject.SetActive(!string.IsNullOrEmpty(distance));
        distanceText.SetLocalizedText(distance); distanceText.color = tint;
        gameObject.SetActive(true);
    }
}
