namespace HardwareStore.Gameplay.Components
{
    public enum SpawnPointId
    {
        Player,
        DeliveryVehicle
    }

    public enum SceneViewId
    {
        CustomerOrderCounter,
        ProcurementTerminal,
        StorageZone
    }

    public enum SceneRouteId
    {
        CustomerVehicleArrival,
        CustomerVehicleDeparture
    }

    public enum ProductTypeId
    {
        CementBag = 0,
        BoardBundle = 1
    }

    public enum InteractionTypeId
    {
        ProcurementTerminal,
        StorageZone,
        OrderCounter,
        Product,
        LoadingZone
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
