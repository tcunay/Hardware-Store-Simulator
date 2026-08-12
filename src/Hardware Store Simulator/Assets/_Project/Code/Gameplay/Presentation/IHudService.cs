namespace HardwareStore.Gameplay.Presentation
{
    public interface IHudService
    {
        void Present(HudSnapshot snapshot);
        void PresentDayReport(DayReportSnapshot? snapshot);
        void PresentConsultation(ConsultationSnapshot? snapshot);
        void PresentProcurement(ProcurementSnapshot? snapshot);
    }
}
