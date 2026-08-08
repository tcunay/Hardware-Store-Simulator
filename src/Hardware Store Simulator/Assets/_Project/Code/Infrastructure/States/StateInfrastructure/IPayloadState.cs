namespace HardwareStore.Infrastructure.States.StateInfrastructure
{
    public interface IPayloadState<in TPayload> : IExitableState
    {
        void Enter(TPayload payload);
    }
}
