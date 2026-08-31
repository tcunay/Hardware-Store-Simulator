namespace HardwareStore.Gameplay.Common.Time
{
    public sealed class UnityPhysicsTimeService : IPhysicsTimeService
    {
        public float FixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
    }
}
