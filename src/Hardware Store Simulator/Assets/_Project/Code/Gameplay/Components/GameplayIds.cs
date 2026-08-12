namespace HardwareStore.Gameplay.Components
{
    public enum SpawnPointId
    {
        Player,
        DeliveryVehicle,
        PlatformTrolley,
        WarehouseWorker,
        WarehouseWorkerDeliveryAccess,
        WarehouseWorkerStorageAccess
    }

    public enum SceneViewId
    {
        CustomerOrderCounter,
        ProcurementTerminal,
        StorageZone,
        TrolleyUpgradeTerminal,
        StoreControlTerminal
    }

    public enum SceneRouteId
    {
        CustomerVehicleArrival,
        CustomerVehicleDeparture,
        CustomerWalkToCounter,
        CustomerWalkToVehicle
    }

    public enum ProductTypeId
    {
        CementBag = 0,
        BoardBundle = 1
    }

    public enum CustomerProjectTypeId
    {
        CementFoundation = 0,
        LumberShelving = 1,
        WorkbenchFoundation = 2
    }

    public enum InteractionTypeId
    {
        ProcurementTerminal,
        StorageZone,
        OrderCounter,
        Product,
        LoadingZone,
        TrolleyUpgradeTerminal,
        PlatformTrolley,
        StoreControlTerminal
    }

    public enum AudioCueId
    {
        OrderAccepted,
        DeliveryPurchased,
        ProductStocked,
        DeliveryCompleted,
        PickUp,
        Drop,
        Load,
        Reward
    }
}
