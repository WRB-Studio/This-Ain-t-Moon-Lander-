using System.Linq;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SpacecraftSandbox : MonoBehaviour
{
    public Camera view;
    public SpacecraftSandboxPilot pilot;
    public TMP_Text status;
    public TMP_Text interactionLabel;
    public Button interactionButton;
    public Button assistButton;
    public TMP_Text assistLabel;
    public TMP_Text controlsHelp;
    [Tooltip("Kameraabstand im Schiff. Größer = weiter entfernt.")]
    public float cameraSize = 22f;
    public float footCameraSize = 15f;
    public float cameraResponse = 5f;
    public RectTransform hudTop;
    public RectTransform hudButtons;
    AssistedSpacecraft[] ships;
    AssistedSpacecraft selected;
    SpriteRenderer selectedVisual;
    string message;
    float messageUntil;
    bool OnFoot => pilot.gameObject.activeSelf;
    readonly List<RaycastResult> uiHits = new();
    readonly HashSet<int> uiTouches = new();
    bool mouseCapturedByUI;
    public AssistedSpacecraft SelectedShip => selected;
    void Start()
    {
        ships = FindObjectsByType<AssistedSpacecraft>(FindObjectsSortMode.None).OrderBy(ship => ship.name).ToArray();
        foreach (var ship in ships) ship.ResetAtHome();
        pilot.gameObject.SetActive(false);
        if (!controlsHelp) controlsHelp = status.transform.parent.Find("Controls").GetComponent<TMP_Text>();
        if (ships.Length > 0) Board(ships[0]);
    }
    void Update()
    {
        if (!selected) return;
        if (Input.GetKeyDown(KeyCode.R)) ResetSelected();
        if (Input.GetKeyDown(KeyCode.Tab)) SelectShip((System.Array.IndexOf(ships, selected) + 1) % ships.Length);
        if (Input.GetKeyDown(KeyCode.F)) Interact();
        Vector2 direction = new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (!OnFoot && selected.IsClassic) direction = Vector2.zero;
        bool hasPointer = TryPointer(out var pointer);
        bool pointerSteering = direction.sqrMagnitude < .01f && hasPointer;
        Vector2 worldPointer = view.ScreenToWorldPoint(pointer);
        if (pointerSteering)
        {
            Vector2 origin = OnFoot ? pilot.transform.position : selected.transform.position;
            Vector2 delta = (Vector2)view.ScreenToWorldPoint(pointer) - origin;
            direction = delta / Mathf.Max(1f, delta.magnitude);
        }
        bool braking = !selected.IsClassic && Input.GetKey(KeyCode.Space);
        if (braking) direction = Vector2.zero;
        if (OnFoot) pilot.Move(direction.x);
        else selected.SetInput(direction, (Input.GetKey(KeyCode.Q) ? 1f : 0f) - (Input.GetKey(KeyCode.E) ? 1f : 0f), braking, pointerSteering && !braking ? pointer : (Vector2?)null);
        UpdateHUD();
    }
    bool TryPointer(out Vector2 position)
    {
        position = default;
        bool worldTouch = false;
        foreach (var touch in Input.touches)
        {
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            { uiTouches.Remove(touch.fingerId); continue; }
            if (IsPointerOverUI(touch.position)) uiTouches.Add(touch.fingerId);
            if (!worldTouch && !uiTouches.Contains(touch.fingerId)) { position = touch.position; worldTouch = true; }
        }
        if (Input.touchCount > 0) return worldTouch;
        uiTouches.Clear();
        if (!Input.GetMouseButton(0)) { mouseCapturedByUI = false; return false; }
        if (IsPointerOverUI(Input.mousePosition)) mouseCapturedByUI = true;
        if (mouseCapturedByUI) return false;
        position = Input.mousePosition; return true;
    }
    bool IsPointerOverUI(Vector2 position)
    {
        // Query this frame's position; cached EventSystem hover data can lag behind a button press.
        if (EventSystem.current)
        {
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
            foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return true;
        }
        return InHUD(hudTop, position) || InHUD(hudButtons, position);
    }
    static bool InHUD(RectTransform rect, Vector2 position)
    {
        if (!rect || !rect.gameObject.activeInHierarchy) return false;
        var canvas = rect.GetComponentInParent<Canvas>();
        Camera camera = canvas && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, position, camera);
    }
    void LateUpdate()
    {
        if (!selected) return;
        float scroll = Input.mouseScrollDelta.y;
        if (OnFoot) footCameraSize = Mathf.Clamp(footCameraSize - scroll * 2f, 7f, 100f);
        else cameraSize = Mathf.Clamp(cameraSize - scroll * 2f, 7f, 100f);
        UpdateCamera(false);
    }
    void UpdateCamera(bool snap)
    {
        float screenHeight = Mathf.Max(1f, view.pixelHeight);
        float top = hudTop ? hudTop.rect.height * hudTop.lossyScale.y : 0f;
        float bottom = hudButtons ? (hudButtons.rect.height + 12f) * hudButtons.lossyScale.y : 0f;
        float usableHeight = Mathf.Clamp(1f - (top + bottom) / screenHeight, .25f, 1f);
        float minimum = 7f;
        if (!OnFoot && selectedVisual)
        {
            Vector3 extents = selectedVisual.bounds.extents;
            float margin = 2f + selected.Speed / Mathf.Max(1f, cameraResponse);
            minimum = Mathf.Max((extents.x + margin) / Mathf.Max(.1f, view.aspect), (extents.y + margin) / usableHeight);
        }
        float requested = Mathf.Max(OnFoot ? footCameraSize : cameraSize, minimum);
        // Immediately expand when rotating or switching to a larger ship; ease only safe zoom changes.
        view.orthographicSize = snap ? requested : Mathf.Max(minimum, Mathf.Lerp(view.orthographicSize, requested, 1f - Mathf.Exp(-5f * Time.deltaTime)));
        Vector3 target = OnFoot ? pilot.transform.position : selected.transform.position;
        target.y += (top - bottom) / screenHeight * view.orthographicSize;
        target.z = -10f;
        view.transform.position = snap ? target : Vector3.Lerp(view.transform.position, target, 1f - Mathf.Exp(-cameraResponse * Time.deltaTime));
    }
    AssistedSpacecraft NearbyShip()
    {
        AssistedSpacecraft result = null; float best = 4f;
        foreach (var ship in ships)
        {
            if (!ship.DockedPad || ship.Crashed) continue;
            float distance = Vector2.Distance(pilot.transform.position, ship.GetComponent<Collider2D>().ClosestPoint(pilot.transform.position));
            if (distance < best) { best = distance; result = ship; }
        }
        return result;
    }
    void Board(AssistedSpacecraft ship)
    {
        if (selected) selected.SetControl(false);
        pilot.gameObject.SetActive(false); selected = ship; selected.SetControl(true);
        selectedVisual = ship.GetComponentInChildren<SpriteRenderer>();
        Canvas.ForceUpdateCanvases(); UpdateCamera(true);
    }
    public void Interact()
    {
        if (OnFoot) { var ship = NearbyShip(); if (ship) Board(ship); }
        else if (selected && selected.DockedPad && selected.DockedPad.allowExit)
        {
            selected.SetControl(false); pilot.PlaceFeet(selected.DockedPad.exitPoint.position);
        }
    }
    public void SelectShip(int index)
    {
        if (ships == null || index < 0 || index >= ships.Length) return;
        if (!OnFoot && selected && !selected.DockedPad && !selected.Crashed)
        { Notify("Vor dem Schiffwechsel landen oder R zum Rücksetzen drücken."); return; }
        Board(ships[index]);
    }
    public void ResetSelected()
    {
        if (!selected) return;
        if (!selected.homePad.AvailableFor(selected))
        { Notify("Das Startpad ist belegt. Zuerst das dortige Schiff versetzen."); return; }
        selected.ResetAtHome(); Board(selected);
    }
    public void ToggleAssist()
    {
        if (selected && !selected.IsClassic) selected.flightAssist = !selected.flightAssist;
    }
    void Notify(string text) { message = text; messageUntil = Time.time + 5f; }
    void UpdateHUD()
    {
        string mode = selected.Crashed ? "BESCHÄDIGT – R setzt zurück" : OnFoot ? "ZU FUSS" : selected.DockedPad ? "GEDOCKT / TANKEN" : selected.ApproachPad ? "LANDEHILFE" : "FLUG";
        status.text = $"{selected.displayName}   |   {mode}\nTempo {selected.Speed:0.0} / {selected.maxSpeed:0.0}   Tank {selected.Fuel:0} / {selected.fuelCapacity:0}\nSchub {selected.acceleration:0.0}   Bremse {selected.braking:0.0}   Drehen {selected.turnSpeed:0}°/s"
            + (Time.time < messageUntil ? "\n" + message : "");
        if (selected.IsClassic)
        {
            var flight=selected.ClassicFlight;
            status.text=$"{selected.displayName}   |   {mode}   |   KLASSISCH\nTempo {selected.Speed:0.0}   Tank {selected.Fuel:0} / {selected.fuelCapacity:0}\nTriebwerkskraft {flight.thrustForce:0.0}   Drehen {flight.rotationSpeed:0}°/s"
                +(Time.time<messageUntil?"\n"+message:"");
        }
        var nearby = OnFoot ? NearbyShip() : null;
        interactionLabel.text = OnFoot ? (nearby ? "Einsteigen: " + nearby.displayName : "Kein Schiff in Reichweite") : "Aussteigen (F)";
        interactionButton.interactable = OnFoot ? nearby : selected.DockedPad && selected.DockedPad.allowExit;
        assistButton.interactable = !OnFoot && !selected.IsClassic;
        assistLabel.text = selected.IsClassic ? "Klassische Lander-Steuerung" : "Flugassistenz: " + (selected.flightAssist ? "AN" : "AUS");
        if (controlsHelp) controlsHelp.text = selected.IsClassic && !OnFoot
            ? "Original-Lander: Maus halten = Schub | Maus seitlich = Kippen | Loslassen = Triebwerk aus\nF: Ein-/Aussteigen | Tab: Schiffwechsel | R: Rücksetzen | Mausrad: Zoom"
            : "WASD / Pfeile / Maus halten: Flug | Loslassen / Leertaste: Bremsen\nQ/E: Drehen | F: Ein-/Aussteigen | Tab: Schiffwechsel | R: Rücksetzen | Mausrad: Zoom";
    }
}
