using UnityEngine;

public class MoonFlagDiscovery : MonoBehaviour
{
    [SerializeField, Min(.1f)] float distance = 3;
    [SerializeField] string text = "[[moon.flag.discovery]]";
    void Update()
    {
        var game = GameController.Instance; var save = SaveLoadManager.Instance;
        if (!game || !save || !game.IsPlaying || game.Phase != GameController.GamePhase.EVA
            || save.Data.GetFlag("story.moonFlagSeen") || !game.ControlledTarget) return;
        if (Vector2.Distance(game.ControlledTarget.position, transform.position) > distance) return;
        SpaceFlightNavigation.Instance?.ShowAside(text);
        save.Data.SetFlag("story.moonFlagSeen", true); save.Save();
    }
}
