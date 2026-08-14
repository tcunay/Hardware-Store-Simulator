using HardwareStore.Common.Entity;
using HardwareStore.Common.Extensions;
using HardwareStore.Gameplay.Components;
using HardwareStore.Gameplay.StaticData;
using HardwareStore.Infrastructure.Identifiers;

namespace HardwareStore.Gameplay.Factories
{
    public sealed class WarehouseTaskFactory : IWarehouseTaskFactory
    {
        private readonly IIdentifierService _identifiers;
        private readonly IStaticDataService _staticData;

        public WarehouseTaskFactory(IIdentifierService identifiers,
            IStaticDataService staticData)
        {
            _identifiers = identifiers;
            _staticData = staticData;
        }

        public GameEntity CreateInboundToStorage(int storeEntityId,
            int productEntityId, int storageZoneEntityId,
            int reservedStorageSlotIndex)
        {
            return CreateEntity.Empty(_identifiers.Next())
                .AddWarehouseTaskStoreEntityId(storeEntityId)
                .AddWarehouseTaskStorageZoneEntityId(storageZoneEntityId)
                .AddWarehouseTaskProductEntityId(productEntityId)
                .AddWarehouseTaskReservedStorageSlotIndex(reservedStorageSlotIndex)
                .AddWarehouseTaskStep(WarehouseTaskStepId.Available)
                .AddWarehouseTaskBlockReason(WarehouseTaskBlockReasonId.None)
                .AddWarehouseTaskTimeoutRemaining(
                    _staticData.WarehouseWorker.TaskTimeout)
                .With(x => x.isWarehouseTask = true)
                .With(x => x.isInboundToStorageTask = true);
        }

        public GameEntity CreateStockToCustomerLoading(int storeEntityId,
            int productEntityId, int customerVisitEntityId,
            int orderLineEntityId, int reservedLoadingSlotIndex)
        {
            return CreateEntity.Empty(_identifiers.Next())
                .AddWarehouseTaskStoreEntityId(storeEntityId)
                .AddWarehouseTaskProductEntityId(productEntityId)
                .AddWarehouseTaskCustomerVisitEntityId(customerVisitEntityId)
                .AddWarehouseTaskOrderLineEntityId(orderLineEntityId)
                .AddWarehouseTaskReservedLoadingSlotIndex(
                    reservedLoadingSlotIndex)
                .AddWarehouseTaskStep(WarehouseTaskStepId.Available)
                .AddWarehouseTaskBlockReason(WarehouseTaskBlockReasonId.None)
                .AddWarehouseTaskTimeoutRemaining(
                    _staticData.WarehouseWorker.TaskTimeout)
                .With(x => x.isWarehouseTask = true)
                .With(x => x.isStockToCustomerLoadingTask = true);
        }
    }
}
