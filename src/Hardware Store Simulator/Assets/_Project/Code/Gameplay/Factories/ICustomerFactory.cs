namespace HardwareStore.Gameplay.Factories
{
    public interface ICustomerFactory
    {
        GameEntity Create(GameEntity customerVisit, GameEntity parkingSpot,
            GameEntity queueSpot);
    }
}
