namespace HardwareStore.Gameplay.Components
{
    public enum SpawnPointId
    {
        Player,
        DeliveryVehicle,
        PlatformTrolley,
        WarehouseWorker,
        WarehouseWorkerDeliveryAccess,
        WarehouseWorkerStorageAccess,
        WarehouseWorkerCustomerLoadingAccess,
        WarehouseWorkerTrolley,
        WarehouseWorkerTrolleyCustomerLoadingAccess
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
        BoardBundle = 1,
        BrickPack = 2,
        DrywallSheet = 3,
        PaintBucket = 4,
        InsulationRoll = 5
    }

    public enum CustomerProjectTypeId
    {
        CementFoundation = 0,
        LumberShelving = 1,
        WorkbenchFoundation = 2,
        GardenWall = 3,
        DrywallPartition = 4,
        WorkshopRenovation = 5,
        GarageInsulation = 6
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
