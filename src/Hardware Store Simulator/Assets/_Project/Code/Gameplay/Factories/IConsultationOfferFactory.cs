namespace HardwareStore.Gameplay.Factories
{
    public interface IConsultationOfferFactory
    {
        GameEntity CreateOffer(GameEntity customerVisit, int offerIndex);
    }
}
