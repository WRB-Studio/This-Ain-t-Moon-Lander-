using UnityEngine;

public sealed class ConversationPause
{
    CanvasGroup hud;
    float previousTimeScale, previousHudAlpha;
    bool previousInteractable, previousRaycasts;

    public void Begin(CanvasGroup gameplayHud)
    {
        hud = gameplayHud;
        previousTimeScale = Time.timeScale;
        previousHudAlpha = hud.alpha;
        previousInteractable = hud.interactable;
        previousRaycasts = hud.blocksRaycasts;
        Time.timeScale = 0f;
        hud.alpha = 0f;
        hud.interactable = hud.blocksRaycasts = false;
    }

    public void End(StoryDialog dialog)
    {
        if (dialog) dialog.Hide();
        Time.timeScale = previousTimeScale;
        if (hud)
        {
            hud.alpha = previousHudAlpha;
            hud.interactable = previousInteractable;
            hud.blocksRaycasts = previousRaycasts;
        }
        if (StoryTextController.Instance) StoryTextController.Instance.BlockInputUntilRelease();
    }
}
