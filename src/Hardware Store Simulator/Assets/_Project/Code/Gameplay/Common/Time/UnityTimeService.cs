namespace HardwareStore.Gameplay.Common.Time
{
    public sealed class UnityTimeService : ITimeService
    {
        public float DeltaTime => UnityEngine.Time.deltaTime;
        public float UnscaledTime => UnityEngine.Time.unscaledTime;
    }
}
