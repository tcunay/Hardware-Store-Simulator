using HardwareStore.Gameplay.Scene;

namespace HardwareStore.Gameplay.Factories
{
    public interface ICustomerFlowFactory
    {
        void Create(GameEntity store, CustomerFlowSceneLayout layout);
    }
}
