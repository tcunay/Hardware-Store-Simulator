namespace HardwareStore.Gameplay.Factories
{
    public interface IOrderFactory
    {
        GameEntity AddOrderComponents(GameEntity customerVisit, int storageZoneEntityId);
    }
}
