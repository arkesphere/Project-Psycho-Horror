namespace SurvivalHorror
{
    public readonly struct InteractionMessageRequestedEvent
    {
        public readonly string Text;
        public readonly float Duration;

        public InteractionMessageRequestedEvent(string text, float duration)
        {
            Text = text;
            Duration = duration;
        }
    }

    public readonly struct PlayerControlLockChangedEvent
    {
        public readonly bool IsLocked;

        public PlayerControlLockChangedEvent(bool isLocked)
        {
            IsLocked = isLocked;
        }
    }
}