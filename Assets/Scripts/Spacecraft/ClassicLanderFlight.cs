using UnityEngine;

// Supplies sandbox input/session data to the actual LanderController; contains no flight equations.
[RequireComponent(typeof(Rigidbody2D))]
public class ClassicLanderFlight : MonoBehaviour
{
    [Tooltip("Kraft des Haupttriebwerks. Schub wirkt nur entlang der Schiffsnase.")]
    public float thrustForce = 9.5f;
    [Tooltip("Drehgeschwindigkeit beim Kippen.")]
    public float rotationSpeed = 180f;
    [Tooltip("Glättung der Drehung. Höher = direkter.")]
    [Range(0f, 1f)] public float rotationSmooth = .12f;
    [Tooltip("Bereich um die Schiffsmitte ohne Kippen.")]
    public float steeringDeadzone = .15f;
    [Tooltip("Seitlicher Mausabstand für volle Drehstärke, in lokalen Einheiten.")]
    public float steeringRange = 2f;
    [Range(0f, 1f)] public float maxSteer = 1f;
    [Tooltip("Reaktionsgeschwindigkeit auf die Mausposition.")]
    public float steerResponse = 8f;
    public float maxFallSpeed = 6f;
    public float gravityScale = 1f;
    public float safeVerticalSpeed = 1.5f;
    [Tooltip("Die bisherigen Weltraum-Faktoren: doppeltes Drehtempo und 70 % Schub.")]
    public bool spaceTuning = true;
    public LanderController Controller => GetComponent<LanderController>();
    public bool ThrustRequested(Vector2 movement, bool pointer) => pointer;
    public void ResetRotation(float rotation) => Controller.ResetSandboxFlight(rotation);
    public bool Step(Vector2 gravity, bool pointer, Vector2 screenPointer, bool controlled, float fuel)
    {
        var controller = Controller;
        controller.thrustForce = thrustForce; controller.rotationSpeed = rotationSpeed; controller.rotationSmooth = rotationSmooth;
        controller.steeringDeadzone = steeringDeadzone; controller.steeringRange = steeringRange; controller.maxSteer = maxSteer;
        controller.steerResponse = steerResponse; controller.maxFallSpeed = -Mathf.Abs(maxFallSpeed);
        controller.fuelBurnPerSec = GetComponent<AssistedSpacecraft>().fuelBurnPerSecond;
        return controller.StepSandboxFlight(screenPointer, controlled && pointer, gravity, gravityScale, fuel, spaceTuning);
    }
}
