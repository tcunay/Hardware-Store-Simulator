namespace HardwareStore.Gameplay.Factories
{
    public interface ICustomerVisitFactory
    {
        GameEntity Create(GameEntity store, GameEntity parkingSpot,
            GameEntity trafficLane);
    }
}
