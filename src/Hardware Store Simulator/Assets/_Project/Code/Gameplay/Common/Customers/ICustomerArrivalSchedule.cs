namespace HardwareStore.Gameplay.Common.Customers
{
    public interface ICustomerArrivalSchedule
    {
        float GetDelay(float currentDayMinute);
    }
}
