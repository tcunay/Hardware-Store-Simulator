namespace HardwareStore.Gameplay.Factories
{
    public interface IOrderFactory
    {
        GameEntity CreateOrder(int storeEntityId, int storageZoneEntityId);
    }
}
