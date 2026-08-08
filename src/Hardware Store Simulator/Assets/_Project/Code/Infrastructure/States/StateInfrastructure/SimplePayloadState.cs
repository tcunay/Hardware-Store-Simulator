namespace HardwareStore.Infrastructure.States.StateInfrastructure
{
    public class SimplePayloadState<TPayload> : IPayloadState<TPayload>
    {
        public virtual void Enter(TPayload payload)
        {
        }

        public virtual void Exit()
        {
        }
    }
}
