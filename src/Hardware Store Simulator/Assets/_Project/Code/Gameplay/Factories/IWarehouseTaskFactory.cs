namespace HardwareStore.Gameplay.Factories
{
    public interface IWarehouseTaskFactory
    {
        GameEntity CreateInboundToStorage(int storeEntityId, int productEntityId,
            int storageZoneEntityId, int reservedStorageSlotIndex);

        GameEntity CreateStockToCustomerLoading(int storeEntityId,
            int productEntityId, int customerVisitEntityId,
            int orderLineEntityId, int reservedLoadingSlotIndex);
    }
}
