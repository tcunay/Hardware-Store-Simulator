using HardwareStore.Gameplay.Features.Customers.Systems;
using HardwareStore.Infrastructure.Systems;

namespace HardwareStore.Gameplay.Features.Customers
{
    public sealed class CustomerFeature : Feature
    {
        public CustomerFeature(ISystemFactory systems)
        {
            Add(systems.Create<TickCustomerCooldownSystem>());
            Add(systems.Create<FinalizeAcceptedCustomerPatienceSystem>());
            Add(systems.Create<TickCustomerPatienceSystem>());
            Add(systems.Create<BeginCustomerAbandonmentSystem>());
            Add(systems.Create<BeginCustomerVehicleDepartureDelaySystem>());
            Add(systems.Create<TickCustomerVehicleDepartureDelaySystem>());
            Add(systems.Create<ReserveCustomerLoadingBaySystem>());
            Add(systems.Create<BeginCustomerVehicleDepartureSystem>());
            Add(systems.Create<BeginCustomerAbandonDepartureSystem>());
            Add(systems.Create<BeginCustomerReturnSystem>());
            Add(systems.Create<AdvanceCustomerQueueSystem>());
            Add(systems.Create<MoveCustomerVehicleToLoadingBaySystem>());
            Add(systems.Create<SpawnCustomerVisitSystem>());
            Add(systems.Create<MoveRouteSystem>());
            Add(systems.Create<ReleaseDepartedOrderContentSystem>());
            Add(systems.Create<CompleteCustomerVehicleDepartureSystem>());
            Add(systems.Create<CompleteCustomerAbandonDepartureSystem>());
            Add(systems.Create<CompleteCustomerLoadingBayArrivalSystem>());
            Add(systems.Create<CompleteCustomerVehicleArrivalSystem>());
            Add(systems.Create<CompleteCustomerReturnSystem>());
            Add(systems.Create<CompleteCustomerAbandonReturnSystem>());
            Add(systems.Create<CompleteCustomerApproachSystem>());
            Add(systems.Create<PromoteCustomerAtCounterSystem>());
            Add(systems.Create<ValidateCustomerFlowStateSystem>());
        }
    }
}
