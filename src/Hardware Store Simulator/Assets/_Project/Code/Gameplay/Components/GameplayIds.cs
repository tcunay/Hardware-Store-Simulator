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
        CustomerLoadingZone,
        ProcurementTerminal,
        StorageZone
    }

    public enum ProductTypeId
    {
        CementBag
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
