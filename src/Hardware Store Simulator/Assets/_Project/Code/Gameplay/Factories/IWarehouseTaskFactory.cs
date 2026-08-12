namespace HardwareStore.Gameplay.Factories
{
    public interface IWarehouseTaskFactory
    {
        GameEntity CreateInboundToStorage(int storeEntityId, int productEntityId,
            int storageZoneEntityId, int reservedStorageSlotIndex);
    }
}
