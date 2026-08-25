using HardwareStore.Gameplay.Components;

namespace HardwareStore.Gameplay.Factories
{
    public interface IProcurementCartFactory
    {
        GameEntity Create(int procurementTerminalEntityId, int storeEntityId);
        GameEntity CreateLine(int cartEntityId, ProductTypeId productType, int packageCount);
    }
}
