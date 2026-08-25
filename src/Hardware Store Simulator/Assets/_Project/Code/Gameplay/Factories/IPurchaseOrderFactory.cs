namespace HardwareStore.Gameplay.Factories
{
    public interface IPurchaseOrderFactory
    {
        GameEntity Create(int cartEntityId, int procurementTerminalEntityId,
            int storeEntityId);
    }
}
