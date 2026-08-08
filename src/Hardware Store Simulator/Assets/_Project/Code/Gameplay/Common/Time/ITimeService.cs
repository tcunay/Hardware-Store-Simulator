namespace HardwareStore.Gameplay.Common.Time
{
    public interface ITimeService
    {
        float DeltaTime { get; }
        float UnscaledTime { get; }
    }
}
