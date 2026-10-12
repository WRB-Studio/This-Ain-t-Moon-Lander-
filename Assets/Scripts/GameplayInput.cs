public static class GameplayInput
{
    public static bool IsPausedOrRadioOpen => PauseMenu.IsPaused
        || (RadioController.Instance && RadioController.Instance.IsOpen);

    public static bool IsConversationOpen => (StationConversation.Instance && StationConversation.Instance.IsShowing)
        || (CargoMission.Instance && CargoMission.Instance.IsShowing);

    public static bool IsStationTransitioning => StationInterior.Instance && StationInterior.Instance.IsTransitioning;

    public static bool IsStoryInputBlocked => StoryTextController.Instance && StoryTextController.Instance.BlocksGameplayInput;

    public static bool IsInteractionBlocked => IsPausedOrRadioOpen || IsConversationOpen || IsStoryInputBlocked;

    public static bool IsBlocked => IsInteractionBlocked || IsStationTransitioning;

    // Door prompts do not add pause or radio checks to their visibility rules.
    public static bool IsStationInteractionBlocked => IsConversationOpen || IsStoryInputBlocked || IsStationTransitioning;
}
