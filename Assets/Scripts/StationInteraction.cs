using UnityEngine;

public class StationInteraction : MonoBehaviour
{
    public enum Action { OutsideEntrance, ExitStation, RegistrationEntrance, ExitRegistration, Talk, Locked }
    public Action action;
    public string buttonLabel = "Enter";
    [Min(0.1f)] public float distance = 2f;
    public bool CanUse => action != Action.Locked;

    public bool IsNear(Vector2 position) => gameObject.activeInHierarchy
        && Vector2.Distance(position, transform.position) <= distance;

    public void Use()
    {
        var game = GameController.Instance;
        if (!CanUse || !game || game.Phase != GameController.GamePhase.EVA || !game.ControlledTarget
            || !IsNear(game.ControlledTarget.position) || !StationInterior.Instance) return;
        switch (action)
        {
            case Action.OutsideEntrance: SpaceStation.Instance.RequestEntry(); break;
            case Action.ExitStation: StationInterior.Instance.ExitStation(); break;
            case Action.RegistrationEntrance: StationInterior.Instance.EnterRegistration(); break;
            case Action.ExitRegistration: StationInterior.Instance.ReturnToHall(); break;
            case Action.Talk: StationConversation.Instance.BeginConversation(); break;
        }
    }
}
