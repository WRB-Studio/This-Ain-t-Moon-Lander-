using UnityEngine;
using UnityEngine.UI;

public class StationSignalIndicator : MonoBehaviour
{
    [SerializeField] RectTransform icon;
    [SerializeField] Graphic graphic;
    [SerializeField, Min(1f)] float orbitRadius = 150f;
    [SerializeField, Min(0.05f)] float visibilityFadeDuration = 0.3f;
    bool stationInView;
    float visibility;
    [SerializeField, Min(1f)] float weakSignalDistance = 500f;
    [SerializeField, Range(0f, 1f)] float minimumAlpha = 0.06f;
    [SerializeField, Range(0f, 1f)] float maximumAlpha = 0.9f;
    void LateUpdate()
    {
        var station = SpaceStation.Instance;
        var game = GameController.Instance;
        var camera = Camera.main;
        bool visible = station && station.IsAvailable && game && game.ControlledTarget && camera
            && game.Phase == GameController.GamePhase.Flight
            && !(RadioController.Instance && RadioController.Instance.IsOwned);
        graphic.enabled = visible;
        if (!visible) return;
        var canvas = GetComponentInParent<Canvas>();
        var parent = (RectTransform)icon.parent;
        Vector2 shipScreen = camera.WorldToScreenPoint(game.ControlledTarget.position);
        var data = SaveLoadManager.Instance.Data;
        stationInView = station.IsInView(camera, stationInView ? 0.08f : 0f);
        visibility = Mathf.MoveTowards(visibility, stationInView ? 0f : 1f, Time.unscaledDeltaTime / visibilityFadeDuration);
        Vector3 destination = station.transform.position;
        Vector2 destinationScreen = camera.WorldToScreenPoint(destination);
        Vector2 direction = (destinationScreen - shipScreen).normalized;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, shipScreen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 localPosition);
        icon.anchoredPosition = localPosition + direction * orbitRadius;
        icon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        float distance = Vector2.Distance(game.ControlledTarget.position, destination);
        if (data.stationSignalStartDistance <= 0f) data.stationSignalStartDistance = Mathf.Max(station.GreetingDistance + 1f, distance);
        float faintDistance = Mathf.Min(weakSignalDistance, data.stationSignalStartDistance);
        float strength = 1f - Mathf.InverseLerp(station.GreetingDistance, faintDistance, distance);
        Color tint = graphic.color;
        tint.a = Mathf.Lerp(minimumAlpha, maximumAlpha, Mathf.SmoothStep(0f, 1f, strength)) * visibility;
        graphic.color = tint;
        graphic.enabled = visibility > 0f;
    }
}
