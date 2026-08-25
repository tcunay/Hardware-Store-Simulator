using HardwareStore.Gameplay.Features.Carrying;
using HardwareStore.Gameplay.Features.Cameras;
using HardwareStore.Gameplay.Features.Cleanup;
using HardwareStore.Gameplay.Features.Consultation;
using HardwareStore.Gameplay.Features.Customers;
using HardwareStore.Gameplay.Features.Delivery;
using HardwareStore.Gameplay.Features.Employees;
using HardwareStore.Gameplay.Features.Forklift;
using HardwareStore.Gameplay.Features.Input;
using HardwareStore.Gameplay.Features.Interaction;
using HardwareStore.Gameplay.Features.Movement;
using HardwareStore.Gameplay.Features.Orders;
using HardwareStore.Gameplay.Features.Player;
using HardwareStore.Gameplay.Features.Presentation;
using HardwareStore.Gameplay.Features.Procurement;
using HardwareStore.Gameplay.Features.Products;
using HardwareStore.Gameplay.Features.StorageState;
using HardwareStore.Gameplay.Features.StoreDay;
using HardwareStore.Gameplay.Features.StoreDay.Systems;
using HardwareStore.Gameplay.Features.StoreSceneBindings;
using HardwareStore.Gameplay.Features.Trolley;
using HardwareStore.Infrastructure.Systems;
using HardwareStore.Infrastructure.View;

namespace HardwareStore.Gameplay
{
    public sealed class StoreFeature : Feature
    {
        public StoreFeature(ISystemFactory systems)
        {
            Add(systems.Create<BindViewFeature>());
            Add(systems.Create<StoreSceneBindingsFeature>());
            Add(systems.Create<InputFeature>());
            Add(systems.Create<PlayerFeature>());
            Add(systems.Create<ProductRecoveryFeature>());
#if UNITY_EDITOR
            Add(systems.Create<ReconcileEditorMoneyOverrideSystem>());
#endif
            Add(systems.Create<StorageStateFeature>());
            Add(systems.Create<InteractionFeature>());
            Add(systems.Create<EmployeeFeature>());
            Add(systems.Create<StorageStateFeature>());
            Add(systems.Create<StoreDayFeature>());
            Add(systems.Create<ProcurementFeature>());
            Add(systems.Create<ConsultationFeature>());
            Add(systems.Create<DeliveryFeature>());
            Add(systems.Create<CarryingFeature>());
            Add(systems.Create<OrderProgressFeature>());
            Add(systems.Create<StorageStateFeature>());
            Add(systems.Create<TrolleyFeature>());
            Add(systems.Create<StorageStateFeature>());
            Add(systems.Create<ForkliftFeature>());
            Add(systems.Create<ProductPlacementFeature>());
            Add(systems.Create<CustomerFeature>());
            Add(systems.Create<MovementFeature>());
            Add(systems.Create<TrolleyMovementFeature>());
            Add(systems.Create<VehicleCameraFeature>());
            Add(systems.Create<InteractionPromptFeature>());
            Add(systems.Create<ValidateStoreDayStateSystem>());
            Add(systems.Create<PresentationFeature>());
            Add(systems.Create<CleanupFeature>());
        }
    }
}
