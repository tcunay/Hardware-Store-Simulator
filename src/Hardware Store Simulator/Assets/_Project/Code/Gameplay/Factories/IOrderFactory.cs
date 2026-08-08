namespace HardwareStore.Gameplay.Factories
{
    public interface IOrderFactory
    {
        GameEntity CreateOrder(int walletEntityId);
        GameEntity CreateWallet();
    }
}
