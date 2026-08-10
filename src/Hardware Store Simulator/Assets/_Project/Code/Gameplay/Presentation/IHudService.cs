namespace HardwareStore.Gameplay.Presentation
{
    public interface IHudService
    {
        void Present(HudSnapshot snapshot);
        void PresentConsultation(ConsultationSnapshot? snapshot);
    }
}
